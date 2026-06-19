using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using BranchEntity = WebHomestay.Models.Entities.Core.Branch;
using RoomEntity = WebHomestay.Models.Entities.Core.Room;

namespace WebHomestay.Services.AI;

public class AdminAIReseedService : IAdminAIReseedService
{
    private readonly ApplicationDbContext _context;

    public AdminAIReseedService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminAIReseedResult> ReseedAsync()
    {
        _context.AIGraphEdges.RemoveRange(_context.AIGraphEdges);
        _context.AIGraphNodes.RemoveRange(_context.AIGraphNodes);
        _context.AIKnowledgeUnits.RemoveRange(_context.AIKnowledgeUnits);
        _context.AIBrainScopes.RemoveRange(_context.AIBrainScopes);
        await _context.SaveChangesAsync();

        var result = new AdminAIReseedResult();
        var scope = new AIBrainScope
        {
            Name = "Tri thức hệ thống được seed",
            Description = "Tri thức và graph được tái tạo trực tiếp từ dữ liệu chi nhánh và phòng hiện có.",
            Order = 1
        };
        _context.AIBrainScopes.Add(scope);
        result.CreatedScopes++;

        var branches = await _context.Branches
            .Include(b => b.Rooms)
            .AsNoTracking()
            .ToListAsync();

        var nodes = new List<AIGraphNode>();
        var edges = new List<AIGraphEdge>();
        var units = new List<AIKnowledgeUnit>();

        foreach (var branch in branches)
        {
            var leadTimeText = $"theo giờ {branch.BookingLeadTimeHours} giờ, theo ngày {branch.BookingLeadTimeDays} ngày";

            var branchNode = CreateNode(
                "branch",
                branch.Name,
                $"Chi nhánh tại {branch.Address}. Hotline: {branch.Hotline ?? "chưa cập nhật"}. Lead time đặt phòng: {leadTimeText}.",
                new { source = "system-seed", branchId = branch.Id });
            nodes.Add(branchNode);

            units.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = $"Chi nhánh {branch.Name}",
                Content = $"Chi nhánh {branch.Name} ở {branch.Address}. Hotline: {branch.Hotline ?? "chưa cập nhật"}. Thời gian đặt trước tối thiểu: {leadTimeText}.",
                Tags = $"system-seed,branch,branch-{branch.Id}",
                Priority = 50
            });

            foreach (var room in branch.Rooms.OrderBy(r => r.Name))
            {
                var roomNode = CreateNode(
                    "room",
                    room.Name,
                    BuildRoomSummary(branch, room),
                    new { source = "system-seed", branchId = branch.Id, roomId = room.Id });
                nodes.Add(roomNode);

                edges.Add(new AIGraphEdge
                {
                    FromNodeId = branchNode.Id,
                    ToNodeId = roomNode.Id,
                    RelationshipType = "branch_contains_room",
                    Weight = 1,
                    Evidence = "Derived from Branch.Rooms during reseed."
                });

                units.Add(new AIKnowledgeUnit
                {
                    Scope = scope,
                    Title = $"Phòng {room.Name} - {branch.Name}",
                    Content = BuildRoomSummary(branch, room),
                    Tags = $"system-seed,room,branch-{branch.Id},room-{room.Id},status-{NormalizeKey(room.Status)}",
                    Priority = 60
                });
            }

            units.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = $"Tổng hợp phòng tại {branch.Name}",
                Content = BuildBranchRoomSummary(branch),
                Tags = $"system-seed,branch-summary,branch-{branch.Id}",
                Priority = 40
            });
        }

        var allRooms = branches.SelectMany(b => b.Rooms).ToList();
        var capacityGroups = allRooms.GroupBy(room => $"{room.Capacity}-{room.MaxGuests}").OrderBy(group => group.Key);
        foreach (var capacityGroup in capacityGroups)
        {
            var sample = capacityGroup.First();
            var capacityNode = CreateNode(
                "capacity_band",
                $"Sức chứa {sample.Capacity}/{sample.MaxGuests}",
                $"Nhóm phòng sức chứa chuẩn {sample.Capacity} và tối đa {sample.MaxGuests} khách.",
                new { source = "system-seed", capacity = sample.Capacity, maxGuests = sample.MaxGuests });
            nodes.Add(capacityNode);

            units.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = $"Nhóm sức chứa {sample.Capacity}/{sample.MaxGuests}",
                Content = $"Có {capacityGroup.Count()} phòng trong hệ thống thuộc nhóm sức chứa {sample.Capacity}, tối đa {sample.MaxGuests} khách.",
                Tags = $"system-seed,capacity-band,capacity-{sample.Capacity},max-{sample.MaxGuests}",
                Priority = 30
            });

            foreach (var room in capacityGroup)
            {
                var roomNode = nodes.First(node => node.NodeType == "room" && node.Label == room.Name);
                edges.Add(new AIGraphEdge
                {
                    FromNodeId = roomNode.Id,
                    ToNodeId = capacityNode.Id,
                    RelationshipType = "room_has_capacity_band",
                    Weight = 1,
                    Evidence = "Derived from room capacity and max guest fields."
                });
            }
        }

        var priceGroups = allRooms.GroupBy(GetPriceBandKey).OrderBy(group => group.Key);
        foreach (var priceGroup in priceGroups)
        {
            var priceNode = CreateNode(
                "price_band",
                priceGroup.Key,
                $"Nhóm giá theo dữ liệu phòng hiện có: {priceGroup.Key}.",
                new { source = "system-seed", key = priceGroup.Key });
            nodes.Add(priceNode);

            units.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = $"Nhóm giá {priceGroup.Key}",
                Content = $"Có {priceGroup.Count()} phòng trong hệ thống thuộc nhóm giá {priceGroup.Key}.",
                Tags = $"system-seed,price-band,{NormalizeKey(priceGroup.Key)}",
                Priority = 25
            });

            foreach (var room in priceGroup)
            {
                var roomNode = nodes.First(node => node.NodeType == "room" && node.Label == room.Name);
                edges.Add(new AIGraphEdge
                {
                    FromNodeId = roomNode.Id,
                    ToNodeId = priceNode.Id,
                    RelationshipType = "room_has_price_band",
                    Weight = 1,
                    Evidence = "Derived from hourly and daily room prices."
                });
            }
        }

        var statusGroups = allRooms
            .GroupBy(room => string.IsNullOrWhiteSpace(room.Status) ? "unknown" : room.Status.Trim())
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var statusGroup in statusGroups)
        {
            var statusNode = CreateNode(
                "status",
                statusGroup.Key,
                $"Nhóm phòng có trạng thái {statusGroup.Key}.",
                new { source = "system-seed", status = statusGroup.Key });
            nodes.Add(statusNode);

            units.Add(new AIKnowledgeUnit
            {
                Scope = scope,
                Title = $"Trạng thái phòng {statusGroup.Key}",
                Content = $"Có {statusGroup.Count()} phòng đang mang trạng thái {statusGroup.Key}.",
                Tags = $"system-seed,status,{NormalizeKey(statusGroup.Key)}",
                Priority = 20
            });

            foreach (var room in statusGroup)
            {
                var roomNode = nodes.First(node => node.NodeType == "room" && node.Label == room.Name);
                edges.Add(new AIGraphEdge
                {
                    FromNodeId = roomNode.Id,
                    ToNodeId = statusNode.Id,
                    RelationshipType = "room_has_status",
                    Weight = 1,
                    Evidence = "Derived from room status."
                });
            }
        }

        _context.AIGraphNodes.AddRange(nodes);
        _context.AIGraphEdges.AddRange(edges);
        _context.AIKnowledgeUnits.AddRange(units);
        await _context.SaveChangesAsync();

        result.CreatedKnowledgeUnits = units.Count;
        result.CreatedNodes = nodes.Count;
        result.CreatedEdges = edges.Count;
        result.TotalScopes = await _context.AIBrainScopes.CountAsync();
        result.TotalKnowledgeUnits = await _context.AIKnowledgeUnits.CountAsync();
        result.TotalNodes = await _context.AIGraphNodes.CountAsync();
        result.TotalEdges = await _context.AIGraphEdges.CountAsync();
        return result;
    }

    private static AIGraphNode CreateNode(string nodeType, string label, string summary, object metadata)
    {
        return new AIGraphNode
        {
            NodeType = nodeType,
            Label = label,
            Summary = summary,
            MetadataJson = JsonSerializer.Serialize(metadata),
            IsActive = true
        };
    }

    private static string BuildRoomSummary(BranchEntity branch, RoomEntity room)
    {
        return $"Phòng {room.Name} thuộc chi nhánh {branch.Name}, sức chứa {room.Capacity}, tối đa {room.MaxGuests} khách, giá giờ {room.PricePerHour:N0}, giá ngày {room.PricePerDay:N0}, phụ thu khách thêm {room.ExtraGuestFee:N0}, trạng thái {room.Status}.";
    }

    private static string BuildBranchRoomSummary(BranchEntity branch)
    {
        if (branch.Rooms.Count == 0)
        {
            return $"Chi nhánh {branch.Name} hiện chưa có phòng nào được cấu hình trong hệ thống.";
        }

        var roomNames = string.Join(", ", branch.Rooms.OrderBy(room => room.Name).Select(room => room.Name));
        var minHourly = branch.Rooms.Min(room => room.PricePerHour);
        var maxHourly = branch.Rooms.Max(room => room.PricePerHour);
        var minDaily = branch.Rooms.Min(room => room.PricePerDay);
        var maxDaily = branch.Rooms.Max(room => room.PricePerDay);

        return $"Chi nhánh {branch.Name} có {branch.Rooms.Count} phòng: {roomNames}. Giá giờ dao động {minHourly:N0} - {maxHourly:N0}, giá ngày dao động {minDaily:N0} - {maxDaily:N0}.";
    }

    private static string GetPriceBandKey(RoomEntity room)
    {
        var hourlyBand = room.PricePerHour switch
        {
            < 300000m => "Giá giờ thấp",
            < 600000m => "Giá giờ trung bình",
            _ => "Giá giờ cao"
        };

        var dailyBand = room.PricePerDay switch
        {
            < 800000m => "giá ngày thấp",
            < 1500000m => "giá ngày trung bình",
            _ => "giá ngày cao"
        };

        return $"{hourlyBand}, {dailyBand}";
    }

    private static string NormalizeKey(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '-');
    }
}

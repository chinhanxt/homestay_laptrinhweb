using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using WebHomestay.Data;
using WebHomestay.Models;
using WebHomestay.Services.AI;
using System.Text.Json;

namespace WebHomestay.Services
{
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public interface IBulkImportService
    {
        Task<ImportResult> ImportBranchesAsync(IFormFile file);
        Task<ImportResult> ImportRoomsAsync(IFormFile file);
        Task<ImportResult> ImportStaffAsync(IFormFile file);
        Task<ImportResult> ImportAIKnowledgeAsync(IFormFile file);
        Task<ImportResult> ImportRoomSlotTemplatesAsync(IFormFile file);
        Task<ImportResult> ImportHolidaysAsync(IFormFile file);
        Task ValidateZipSizeAsync(IFormFile file);
    }

    public class BulkImportService : IBulkImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmbeddingService _embeddingService;

        public BulkImportService(ApplicationDbContext context, IWebHostEnvironment env, IEmbeddingService embeddingService)
        {
            _context = context;
            _env = env;
            _embeddingService = embeddingService;
        }

        public async Task ValidateZipSizeAsync(IFormFile file)
        {
            // Default 30MB if not configured
            long maxSizeMB = 30;
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "MaxZipUploadSizeMB");
            if (setting != null && long.TryParse(setting.SettingValue, out var configuredSize))
            {
                maxSizeMB = configuredSize;
            }

            long maxSizeBytes = maxSizeMB * 1024 * 1024;
            if (file.Length > maxSizeBytes)
            {
                throw new Exception($"Kích thước file vượt quá giới hạn tối đa cho phép là {maxSizeMB}MB.");
            }
        }

        private async Task<(IXLWorkbook workbook, Dictionary<string, string> imageMappings)> ProcessUploadFileAsync(IFormFile file, string uploadSubfolder)
        {
            await ValidateZipSizeAsync(file);

            var imageMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IXLWorkbook workbook;

            string fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (fileExtension == ".xlsx")
            {
                var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                workbook = new XLWorkbook(memoryStream);
                return (workbook, imageMappings);
            }
            else if (fileExtension == ".zip")
            {
                var zipStream = new MemoryStream();
                await file.CopyToAsync(zipStream);
                zipStream.Position = 0;

                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
                {
                    // Find the Excel file first
                    var excelEntry = archive.Entries.FirstOrDefault(e => Path.GetExtension(e.Name).Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
                    if (excelEntry == null)
                    {
                        throw new Exception("Không tìm thấy file Excel (.xlsx) trong file ZIP.");
                    }

                    var excelStream = new MemoryStream();
                    using (var originalStream = excelEntry.Open())
                    {
                        await originalStream.CopyToAsync(excelStream);
                    }
                    excelStream.Position = 0;
                    workbook = new XLWorkbook(excelStream);

                    // Extract and map images
                    string targetFolder = Path.Combine(_env.WebRootPath, "uploads", uploadSubfolder);
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

                        string ext = Path.GetExtension(entry.Name).ToLower();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif")
                        {
                            // Save with unique name to avoid collisions
                            string uniqueName = Guid.NewGuid().ToString() + ext;
                            string targetPath = Path.Combine(targetFolder, uniqueName);

                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(targetPath, FileMode.Create))
                            {
                                await entryStream.CopyToAsync(fileStream);
                            }

                            // Keep map relative URL: "/uploads/{subfolder}/{uniqueName}"
                            string relativeUrl = $"/uploads/{uploadSubfolder}/{uniqueName}";
                            
                            // Map both full path inside ZIP and just the filename
                            imageMappings[entry.Name] = relativeUrl;
                            imageMappings[Path.GetFileName(entry.Name)] = relativeUrl;
                        }
                    }
                }

                return (workbook, imageMappings);
            }
            else
            {
                throw new Exception("Định dạng file không được hỗ trợ. Vui lòng upload file .xlsx hoặc .zip.");
            }
        }

        public async Task<ImportResult> ImportBranchesAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "branches");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1); // Skip header row
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        string name = row.Cell(1).GetValue<string>().Trim();
                        string address = row.Cell(2).GetValue<string>().Trim();
                        string desc = row.Cell(3).GetValue<string>().Trim();
                        string hotline = row.Cell(4).GetValue<string>().Trim();
                        string email = row.Cell(5).GetValue<string>().Trim();
                        string mapUrl = row.Cell(6).GetValue<string>().Trim();
                        int bookingLeadTime = 2;
                        
                        var cell7 = row.Cell(7).Value;
                        if (!cell7.IsBlank)
                        {
                            int.TryParse(cell7.ToString(), out bookingLeadTime);
                        }

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(address))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Tên chi nhánh và Địa chỉ là bắt buộc.");
                            continue;
                        }

                        // Check duplicate
                        var exists = await _context.Branches.AnyAsync(b => b.Name.ToLower() == name.ToLower() && !b.IsDeleted);
                        if (exists)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Chi nhánh '{name}' đã tồn tại trong hệ thống.");
                            continue;
                        }

                        var branch = new Branch
                        {
                            Name = name,
                            Address = address,
                            Description = string.IsNullOrEmpty(desc) ? null : desc,
                            Hotline = string.IsNullOrEmpty(hotline) ? null : hotline,
                            Email = string.IsNullOrEmpty(email) ? null : email,
                            MapUrl = string.IsNullOrEmpty(mapUrl) ? null : mapUrl,
                            BookingLeadTimeHours = bookingLeadTime,
                            IsDeleted = false
                        };

                        _context.Branches.Add(branch);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ImportRoomsAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, imageMappings) = await ProcessUploadFileAsync(file, "rooms");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                // Pre-fetch branches for performance
                var branches = await _context.Branches.Where(b => !b.IsDeleted).ToListAsync();

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        string name = row.Cell(1).GetValue<string>().Trim();
                        string desc = row.Cell(2).GetValue<string>().Trim();
                        decimal priceHour = row.Cell(3).GetValue<decimal>();
                        decimal priceDay = row.Cell(4).GetValue<decimal>();
                        decimal extraGuestFee = row.Cell(5).GetValue<decimal>();
                        decimal priceWkHour = row.Cell(6).GetValue<decimal>();
                        decimal priceWkDay = row.Cell(7).GetValue<decimal>();
                        decimal priceHolHour = row.Cell(8).GetValue<decimal>();
                        decimal priceHolDay = row.Cell(9).GetValue<decimal>();
                        int capacity = row.Cell(10).GetValue<int>();
                        int maxGuests = row.Cell(11).GetValue<int>();
                        string status = row.Cell(12).GetValue<string>().Trim();
                        string branchName = row.Cell(13).GetValue<string>().Trim();
                        string avatarFile = row.Cell(14).GetValue<string>().Trim();
                        string additionalFilesStr = row.Cell(15).GetValue<string>().Trim();

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(branchName))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Tên phòng và Tên chi nhánh là bắt buộc.");
                            continue;
                        }

                        var branch = branches.FirstOrDefault(b => b.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
                        if (branch == null)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Chi nhánh '{branchName}' không tồn tại.");
                            continue;
                        }

                        // Check duplicate in same branch
                        var exists = await _context.Rooms.AnyAsync(r => r.Name.ToLower() == name.ToLower() && r.BranchId == branch.Id && !r.IsDeleted);
                        if (exists)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Phòng '{name}' đã tồn tại trong chi nhánh '{branchName}'.");
                            continue;
                        }

                        // Map images
                        string? imageUrl = null;
                        if (!string.IsNullOrEmpty(avatarFile) && imageMappings.TryGetValue(avatarFile, out var mappedUrl))
                        {
                            imageUrl = mappedUrl;
                        }

                        var additionalImagesList = new List<string>();
                        if (!string.IsNullOrEmpty(additionalFilesStr))
                        {
                            var files = additionalFilesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var f in files)
                            {
                                var cleanFile = f.Trim();
                                if (imageMappings.TryGetValue(cleanFile, out var mappedAddUrl))
                                {
                                    additionalImagesList.Add(mappedAddUrl);
                                }
                            }
                        }

                        var room = new Room
                        {
                            Name = name,
                            Description = string.IsNullOrEmpty(desc) ? null : desc,
                            PricePerHour = priceHour,
                            PricePerDay = priceDay,
                            ExtraGuestFee = extraGuestFee,
                            PriceWeekendPerHour = priceWkHour > 0 ? priceWkHour : priceHour,
                            PriceWeekendPerDay = priceWkDay > 0 ? priceWkDay : priceDay,
                            PriceHolidayPerHour = priceHolHour > 0 ? priceHolHour : priceHour,
                            PriceHolidayPerDay = priceHolDay > 0 ? priceHolDay : priceDay,
                            Capacity = capacity > 0 ? capacity : 2,
                            MaxGuests = maxGuests > 0 ? maxGuests : 4,
                            Status = string.IsNullOrEmpty(status) ? "Available" : status,
                            BranchId = branch.Id,
                            ImageUrl = imageUrl,
                            AdditionalImages = additionalImagesList.Any() ? JsonSerializer.Serialize(additionalImagesList) : null,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        // Generate embedding for room (AI search ground truth)
                        try
                        {
                            var embedText = $"Phòng {room.Name} thuộc chi nhánh {branch.Name}, giá giờ {room.PricePerHour:N0}đ, giá ngày {room.PricePerDay:N0}đ. Sức chứa {room.Capacity} người, tối đa {room.MaxGuests} khách. Tiện nghi và mô tả: {room.Description ?? "Chưa cập nhật."}";
                            var rawEmbedding = await _embeddingService.GetEmbeddingAsync(embedText);
                            room.Embedding = new Pgvector.Vector(rawEmbedding);
                        }
                        catch (Exception embedEx)
                        {
                            result.Errors.Add($"Cảnh báo dòng {rowIndex}: Không thể tạo AI Embedding cho phòng: {embedEx.Message}");
                        }

                        _context.Rooms.Add(room);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ImportStaffAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "staff");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                var branches = await _context.Branches.Where(b => !b.IsDeleted).ToListAsync();

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        string username = row.Cell(1).GetValue<string>().Trim();
                        string fullName = row.Cell(2).GetValue<string>().Trim();
                        string roleStr = row.Cell(3).GetValue<string>().Trim();
                        string branchName = row.Cell(4).GetValue<string>().Trim();
                        string permsStr = row.Cell(5).GetValue<string>().Trim();

                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(roleStr))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Tên đăng nhập, Họ tên và Vai trò là bắt buộc.");
                            continue;
                        }

                        // Validate Username duplicate
                        var exists = await _context.AdminUsers.AnyAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsDeleted);
                        if (exists)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Tên đăng nhập '{username}' đã tồn tại.");
                            continue;
                        }

                        // Parse Role
                        if (!Enum.TryParse<AdminRole>(roleStr, true, out var role))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Vai trò '{roleStr}' không hợp lệ (Chấp nhận: SuperAdmin, Manager, Staff).");
                            continue;
                        }

                        // Find Branch
                        int? branchId = null;
                        if (!string.IsNullOrEmpty(branchName))
                        {
                            var branch = branches.FirstOrDefault(b => b.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
                            if (branch == null)
                            {
                                result.FailureCount++;
                                result.Errors.Add($"Dòng {rowIndex}: Chi nhánh '{branchName}' không tồn tại.");
                                continue;
                            }
                            branchId = branch.Id;
                        }

                        // Build permissions dict
                        var permissionsDict = new Dictionary<string, bool>();
                        if (!string.IsNullOrEmpty(permsStr))
                        {
                            var perms = permsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var p in perms)
                            {
                                permissionsDict[p.Trim()] = true;
                            }
                        }

                        // Default pass: 123456
                        string passwordHash = BCrypt.Net.BCrypt.HashPassword("123456");

                        var staff = new AdminUser
                        {
                            Username = username,
                            FullName = fullName,
                            PasswordHash = passwordHash,
                            Role = role,
                            BranchId = branchId,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        };

                        staff.Permissions = permissionsDict;

                        _context.AdminUsers.Add(staff);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ImportAIKnowledgeAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "ai");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                var scopes = await _context.AIBrainScopes.ToListAsync();

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        string scopeName = row.Cell(1).GetValue<string>().Trim();
                        string title = row.Cell(2).GetValue<string>().Trim();
                        string content = row.Cell(3).GetValue<string>().Trim();
                        string tags = row.Cell(4).GetValue<string>().Trim();
                        int priority = 1;
                        var priorityCell = row.Cell(5).Value;
                        if (!priorityCell.IsBlank)
                        {
                            int.TryParse(priorityCell.ToString(), out priority);
                        }
                        
                        string activeStr = row.Cell(6).GetValue<string>().Trim();
                        bool isActive = !activeStr.Equals("No", StringComparison.OrdinalIgnoreCase);

                        if (string.IsNullOrEmpty(scopeName) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Phạm vi (Scope), Tiêu đề và Nội dung là bắt buộc.");
                            continue;
                        }

                        // Look up or create Scope dynamically
                        var scope = scopes.FirstOrDefault(s => s.Name.Equals(scopeName, StringComparison.OrdinalIgnoreCase));
                        if (scope == null)
                        {
                            scope = new AIBrainScope
                            {
                                Id = Guid.NewGuid(),
                                Name = scopeName,
                                Description = "Được tạo tự động từ quy trình Import",
                                IsActive = true,
                                Order = scopes.Count + 1,
                                CreatedAt = DateTime.Now
                            };
                            _context.AIBrainScopes.Add(scope);
                            scopes.Add(scope);
                            await _context.SaveChangesAsync(); // Commit immediately to get scope ID if needed
                        }

                        var unit = new AIKnowledgeUnit
                        {
                            Id = Guid.NewGuid(),
                            ScopeId = scope.Id,
                            Title = title,
                            Content = content,
                            Tags = tags,
                            Priority = priority,
                            IsActive = isActive,
                            LastUpdated = DateTime.Now,
                            IsDeleted = false
                        };

                        // Generate embedding for AIKnowledgeUnit
                        try
                        {
                            var embedText = $"{unit.Title}. {unit.Content}. Thẻ: {unit.Tags}";
                            var rawEmbedding = await _embeddingService.GetEmbeddingAsync(embedText);
                            unit.Embedding = new Pgvector.Vector(rawEmbedding);
                        }
                        catch (Exception embedEx)
                        {
                            result.Errors.Add($"Cảnh báo dòng {rowIndex}: Không thể tạo AI Embedding cho tri thức: {embedEx.Message}");
                        }

                        _context.AIKnowledgeUnits.Add(unit);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ImportRoomSlotTemplatesAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "slots");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        string name = row.Cell(1).GetValue<string>().Trim();
                        string code = row.Cell(2).GetValue<string>().Trim();
                        int duration = row.Cell(3).GetValue<int>();
                        int cleanup = row.Cell(4).GetValue<int>();

                        TimeOnly? seedStart = null;
                        var cell5 = row.Cell(5).GetValue<string>().Trim();
                        if (TimeOnly.TryParse(cell5, out var parseSeedStart)) seedStart = parseSeedStart;

                        TimeOnly? fixedStart = null;
                        var cell6 = row.Cell(6).GetValue<string>().Trim();
                        if (TimeOnly.TryParse(cell6, out var parseFixedStart)) fixedStart = parseFixedStart;

                        TimeOnly? fixedEnd = null;
                        var cell7 = row.Cell(7).GetValue<string>().Trim();
                        if (TimeOnly.TryParse(cell7, out var parseFixedEnd)) fixedEnd = parseFixedEnd;

                        string crossesMidnightStr = row.Cell(8).GetValue<string>().Trim();
                        bool crossesMidnight = crossesMidnightStr.Equals("Yes", StringComparison.OrdinalIgnoreCase);

                        string isActiveStr = row.Cell(9).GetValue<string>().Trim();
                        bool isActive = !isActiveStr.Equals("No", StringComparison.OrdinalIgnoreCase);

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code) || duration <= 0)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Tên mẫu, Mã mẫu và Thời lượng (> 0) là bắt buộc.");
                            continue;
                        }

                        // Check duplicate Code
                        var exists = await _context.RoomSlotTemplates.AnyAsync(t => t.Code.ToLower() == code.ToLower() && !t.IsDeleted);
                        if (exists)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Mã khung giờ '{code}' đã tồn tại.");
                            continue;
                        }

                        var template = new RoomSlotTemplate
                        {
                            Name = name,
                            Code = code,
                            DurationMinutes = duration,
                            CleanupMinutes = cleanup,
                            SeedStartTime = seedStart,
                            FixedStartTime = fixedStart,
                            FixedEndTime = fixedEnd,
                            CrossesMidnight = crossesMidnight,
                            IsActive = isActive,
                            IsDeleted = false
                        };

                        _context.RoomSlotTemplates.Add(template);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ImportHolidaysAsync(IFormFile file)
        {
            var result = new ImportResult();
            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "holidays");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    result.Errors.Add("File Excel không có worksheet nào.");
                    return result;
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    try
                    {
                        var cell1 = row.Cell(1).Value;
                        if (cell1.IsBlank)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Ngày lễ là bắt buộc.");
                            continue;
                        }

                        DateTime holidayDate;
                        if (cell1.IsDateTime)
                        {
                            holidayDate = cell1.GetDateTime();
                        }
                        else if (!DateTime.TryParse(cell1.ToString(), out holidayDate))
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Ngày lễ '{cell1}' không đúng định dạng (Chấp nhận: yyyy-MM-dd).");
                            continue;
                        }

                        // Standardize to Date
                        holidayDate = holidayDate.Date;

                        string desc = row.Cell(2).GetValue<string>().Trim();

                        // Check duplicate Date
                        var exists = await _context.Holidays.AnyAsync(h => h.Date == holidayDate && !h.IsDeleted);
                        if (exists)
                        {
                            result.FailureCount++;
                            result.Errors.Add($"Dòng {rowIndex}: Ngày lễ '{holidayDate:yyyy-MM-dd}' đã tồn tại.");
                            continue;
                        }

                        var holiday = new Holiday
                        {
                            Date = holidayDate,
                            Description = string.IsNullOrEmpty(desc) ? null : desc,
                            IsDeleted = false
                        };

                        _context.Holidays.Add(holiday);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Dòng {rowIndex}: Lỗi xử lý: {ex.Message}");
                    }
                }

                if (result.SuccessCount > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Lỗi Import: {ex.Message}");
            }

            return result;
        }
    }
}

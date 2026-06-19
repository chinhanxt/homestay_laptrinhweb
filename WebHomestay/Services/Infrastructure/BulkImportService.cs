using WebHomestay.Services;
using BranchEntity = WebHomestay.Models.Entities.Core.Branch;
using RoomEntity = WebHomestay.Models.Entities.Core.Room;
using BookingEntity = WebHomestay.Models.Entities.Core.Booking;
using WebHomestay.Services.Slots;
using WebHomestay.Services.Room;
using WebHomestay.Services.Chat;
using WebHomestay.Services.Settings;
using WebHomestay.Services.Infrastructure;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
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
using WebHomestay.Services.AI;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WebHomestay.Services.Infrastructure
{
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class ImportPreviewRow
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
        public List<string> Values { get; set; } = new List<string>();
    }

    public class ImportPreviewResult
    {
        public string CacheKey { get; set; } = "";
        public List<string> Headers { get; set; } = new List<string>();
        public List<ImportPreviewRow> Rows { get; set; } = new List<ImportPreviewRow>();
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }

    public class ConfirmImportRequest
    {
        public string CacheKey { get; set; } = "";
    }

    public class AIKnowledgeImportCache
    {
        public List<AIBrainScope> ScopesToCreate { get; set; } = new List<AIBrainScope>();
        public List<AIKnowledgeUnit> UnitsToCreate { get; set; } = new List<AIKnowledgeUnit>();
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

        // Preview & Confirm support
        Task<ImportPreviewResult> PreviewBranchesAsync(IFormFile file);
        Task<ImportResult> ConfirmBranchesAsync(string cacheKey);

        Task<ImportPreviewResult> PreviewRoomsAsync(IFormFile file);
        Task<ImportResult> ConfirmRoomsAsync(string cacheKey);

        Task<ImportPreviewResult> PreviewStaffAsync(IFormFile file);
        Task<ImportResult> ConfirmStaffAsync(string cacheKey);

        Task<ImportPreviewResult> PreviewAIKnowledgeAsync(IFormFile file);
        Task<ImportResult> ConfirmAIKnowledgeAsync(string cacheKey);

        Task<ImportPreviewResult> PreviewRoomSlotTemplatesAsync(IFormFile file);
        Task<ImportResult> ConfirmRoomSlotTemplatesAsync(string cacheKey);

        Task<ImportPreviewResult> PreviewHolidaysAsync(IFormFile file);
        Task<ImportResult> ConfirmHolidaysAsync(string cacheKey);
    }

    public class BulkImportService : IBulkImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemoryCache _cache;

        public BulkImportService(ApplicationDbContext context, IWebHostEnvironment env, IEmbeddingService embeddingService, IMemoryCache cache)
        {
            _context = context;
            _env = env;
            _embeddingService = embeddingService;
            _cache = cache;
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

        private static string NormalizeImportText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            var previousWasSpace = false;

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                var safeChar = ch switch
                {
                    '\u0111' => 'd',
                    '\u0110' => 'D',
                    '\u00A0' => ' ',
                    _ => ch
                };

                if (char.IsWhiteSpace(safeChar))
                {
                    if (!previousWasSpace)
                    {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }
                }
                else
                {
                    builder.Append(char.ToLowerInvariant(safeChar));
                    previousWasSpace = false;
                }
            }

            return builder.ToString().Trim();
        }

        private static BranchEntity? ResolveBranchByName(IEnumerable<BranchEntity> branches, string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return null;
            }

            var rawName = branchName.Trim();
            var exact = branches.FirstOrDefault(b => string.Equals(b.Name?.Trim(), rawName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            var normalizedTarget = NormalizeImportText(rawName);
            var normalizedMatches = branches
                .Where(b => NormalizeImportText(b.Name) == normalizedTarget)
                .ToList();

            if (normalizedMatches.Count == 1)
            {
                return normalizedMatches[0];
            }

            var containsMatches = branches
                .Where(b =>
                {
                    var normalizedBranch = NormalizeImportText(b.Name);
                    return normalizedBranch.Contains(normalizedTarget) || normalizedTarget.Contains(normalizedBranch);
                })
                .ToList();

            return containsMatches.Count == 1 ? containsMatches[0] : null;
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
            else if (fileExtension == ".docx")
            {
                var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                workbook = ConvertDocxToWorkbook(memoryStream);
                return (workbook, imageMappings);
            }
            else if (fileExtension == ".zip")
            {
                var zipStream = new MemoryStream();
                await file.CopyToAsync(zipStream);
                zipStream.Position = 0;

                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
                {
                    // Find the Excel or Word file first
                    var docEntry = archive.Entries.FirstOrDefault(e => 
                        Path.GetExtension(e.Name).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(e.Name).Equals(".docx", StringComparison.OrdinalIgnoreCase));

                    if (docEntry == null)
                    {
                        throw new Exception("Không tìm thấy file Excel (.xlsx) hoặc Word (.docx) trong file ZIP.");
                    }

                    var docStream = new MemoryStream();
                    using (var originalStream = docEntry.Open())
                    {
                        await originalStream.CopyToAsync(docStream);
                    }
                    docStream.Position = 0;

                    if (Path.GetExtension(docEntry.Name).Equals(".docx", StringComparison.OrdinalIgnoreCase))
                    {
                        workbook = ConvertDocxToWorkbook(docStream);
                    }
                    else
                    {
                        workbook = new XLWorkbook(docStream);
                    }

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
                throw new Exception("Định dạng file không được hỗ trợ. Vui lòng upload file .xlsx, .docx hoặc .zip.");
            }
        }

        private IXLWorkbook ConvertDocxToWorkbook(Stream docxStream)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Import Template");

            using (var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, true))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null)
                {
                    throw new Exception("Không phải file Word (.docx) hợp lệ (thiếu word/document.xml).");
                }

                using (var entryStream = entry.Open())
                {
                    var doc = XDocument.Load(entryStream);
                    XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                    var tables = doc.Descendants(w + "tbl").ToList();
                    if (!tables.Any())
                    {
                        throw new Exception("Không tìm thấy bảng dữ liệu nào trong file Word.");
                    }

                    XElement selectedTable = null;
                    foreach (var tbl in tables)
                    {
                        var firstRow = tbl.Descendants(w + "tr").FirstOrDefault();
                        if (firstRow == null) continue;

                        var cellTexts = firstRow.Descendants(w + "tc")
                            .Select(tc => string.Concat(tc.Descendants(w + "t").Select(t => t.Value)).Trim().ToLower())
                            .ToList();

                        bool isMetadataTable = cellTexts.Any(txt => 
                            txt.Contains("stt") || 
                            txt.Contains("tên cột") || 
                            txt.Contains("tên cot") || 
                            txt.Contains("bắt buộc") || 
                            txt.Contains("bat buoc")
                        );

                        if (!isMetadataTable)
                        {
                            selectedTable = tbl;
                            break;
                        }
                    }

                    if (selectedTable == null)
                    {
                        selectedTable = tables.Last();
                    }

                    int r = 1;
                    foreach (var rowEl in selectedTable.Descendants(w + "tr"))
                    {
                        var cells = rowEl.Elements(w + "tc").ToList();
                        for (int c = 1; c <= cells.Count; c++)
                        {
                            var cellEl = cells[c - 1];
                            var pTexts = cellEl.Descendants(w + "p")
                                .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)))
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList();

                            string cellText = string.Join(Environment.NewLine, pTexts).Trim();
                            worksheet.Cell(r, c).Value = cellText;
                        }
                        r++;
                    }
                }
            }

            return workbook;
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
                            BookingLeadTimeValue = bookingLeadTime,
                            BookingLeadTimeUnit = BranchLeadTimeUnit.Hours,
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

                        var branch = ResolveBranchByName(branches, branchName);
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

                        var room = new RoomEntity
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

        public async Task<ImportPreviewResult> PreviewBranchesAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Tên chi nhánh", "Địa chỉ", "Mô tả", "Hotline", "Email", "Map URL", "Lead Time (giờ)" };
            var validBranches = new List<BranchEntity>();
            string cacheKey = "import_branches_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "branches");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
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

                        previewRow.Values = new List<string> { name, address, desc, hotline, email, mapUrl, bookingLeadTime.ToString() };

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(address))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Tên chi nhánh và Địa chỉ là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var exists = await _context.Branches.AnyAsync(b => b.Name.ToLower() == name.ToLower() && !b.IsDeleted);
                        if (exists)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Chi nhánh '{name}' đã tồn tại trong hệ thống.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
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
                            BookingLeadTimeValue = bookingLeadTime,
                            BookingLeadTimeUnit = BranchLeadTimeUnit.Hours,
                            IsDeleted = false
                        };

                        validBranches.Add(branch);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ";
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validBranches.Any())
                {
                    _cache.Set(cacheKey, validBranches, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmBranchesAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out List<BranchEntity> branches))
            {
                try
                {
                    _context.Branches.AddRange(branches);
                    await _context.SaveChangesAsync();
                    result.SuccessCount = branches.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = branches.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }

        public async Task<ImportPreviewResult> PreviewRoomsAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Tên phòng", "Chi nhánh", "Giá giờ", "Giá ngày", "Sức chứa", "Khách tối đa", "Ảnh đại diện", "Trạng thái" };
            var validRooms = new List<RoomEntity>();
            string cacheKey = "import_rooms_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, imageMappings) = await ProcessUploadFileAsync(file, "rooms");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                var branches = await _context.Branches.Where(b => !b.IsDeleted).ToListAsync();

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
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

                        previewRow.Values = new List<string> { 
                            name, 
                            branchName, 
                            priceHour.ToString("N0") + "đ", 
                            priceDay.ToString("N0") + "đ", 
                            capacity.ToString(), 
                            maxGuests.ToString(),
                            avatarFile,
                            string.IsNullOrEmpty(status) ? "Available" : status
                        };

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(branchName))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Tên phòng và Tên chi nhánh là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var branch = ResolveBranchByName(branches, branchName);
                        if (branch == null)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Chi nhánh '{branchName}' không tồn tại.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var existsInDb = await _context.Rooms.AnyAsync(r => r.Name.ToLower() == name.ToLower() && r.BranchId == branch.Id && !r.IsDeleted);
                        if (existsInDb)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Phòng '{name}' đã tồn tại trong chi nhánh '{branchName}'.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        string imageUrl = null;
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

                        var room = new RoomEntity
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

                        validRooms.Add(room);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ";
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validRooms.Any())
                {
                    _cache.Set(cacheKey, validRooms, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmRoomsAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out List<RoomEntity> rooms))
            {
                try
                {
                    foreach (var room in rooms)
                    {
                        var branch = await _context.Branches.FindAsync(room.BranchId);
                        string branchName = branch?.Name ?? "Chưa rõ";
                        try
                        {
                            var embedText = $"Phòng {room.Name} thuộc chi nhánh {branchName}, giá giờ {room.PricePerHour:N0}đ, giá ngày {room.PricePerDay:N0}đ. Sức chứa {room.Capacity} người, tối đa {room.MaxGuests} khách. Tiện nghi và mô tả: {room.Description ?? "Chưa cập nhật."}";
                            var rawEmbedding = await _embeddingService.GetEmbeddingAsync(embedText);
                            room.Embedding = new Pgvector.Vector(rawEmbedding);
                        }
                        catch (Exception embedEx)
                        {
                            result.Errors.Add($"Cảnh báo: Không thể tạo AI Embedding cho phòng '{room.Name}': {embedEx.Message}");
                        }
                    }
                    _context.Rooms.AddRange(rooms);
                    await _context.SaveChangesAsync();
                    result.SuccessCount = rooms.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = rooms.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }

        public async Task<ImportPreviewResult> PreviewStaffAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Username", "Họ tên", "Vai trò", "Chi nhánh", "Quyền" };
            var validStaff = new List<AdminUser>();
            string cacheKey = "import_staff_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "staff");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                var branches = await _context.Branches.Where(b => !b.IsDeleted).ToListAsync();

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
                    try
                    {
                        string username = row.Cell(1).GetValue<string>().Trim();
                        string fullName = row.Cell(2).GetValue<string>().Trim();
                        string roleStr = row.Cell(3).GetValue<string>().Trim();
                        string branchName = row.Cell(4).GetValue<string>().Trim();
                        string permsStr = row.Cell(5).GetValue<string>().Trim();

                        previewRow.Values = new List<string> { username, fullName, roleStr, branchName, permsStr };

                        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(roleStr))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Tên đăng nhập, Họ tên và Vai trò là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var exists = await _context.AdminUsers.AnyAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsDeleted);
                        if (exists)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Tên đăng nhập '{username}' đã tồn tại.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        if (!Enum.TryParse<AdminRole>(roleStr, true, out var role))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Vai trò '{roleStr}' không hợp lệ (SuperAdmin, Manager, Staff).";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        int? branchId = null;
                        if (!string.IsNullOrEmpty(branchName))
                        {
                            var branch = branches.FirstOrDefault(b => b.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase));
                            if (branch == null)
                            {
                                previewRow.IsValid = false;
                                previewRow.Message = $"Chi nhánh '{branchName}' không tồn tại.";
                                result.FailureCount++;
                                result.Rows.Add(previewRow);
                                continue;
                            }
                            branchId = branch.Id;
                        }

                        var permissionsDict = new Dictionary<string, bool>();
                        if (!string.IsNullOrEmpty(permsStr))
                        {
                            var perms = permsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var p in perms)
                            {
                                permissionsDict[p.Trim()] = true;
                            }
                        }

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

                        validStaff.Add(staff);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ";
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validStaff.Any())
                {
                    _cache.Set(cacheKey, validStaff, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmStaffAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out List<AdminUser> staffList))
            {
                try
                {
                    _context.AdminUsers.AddRange(staffList);
                    await _context.SaveChangesAsync();
                    result.SuccessCount = staffList.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = staffList.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }

        public async Task<ImportPreviewResult> PreviewAIKnowledgeAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Phạm vi (Scope)", "Tiêu đề", "Nội dung", "Thẻ (Tags)", "Độ ưu tiên", "Bật" };
            var validUnits = new List<AIKnowledgeUnit>();
            var newScopesToCreate = new List<AIBrainScope>();
            string cacheKey = "import_ai_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "ai");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                var existingScopes = await _context.AIBrainScopes.ToListAsync();
                var transientScopes = new List<AIBrainScope>(existingScopes);

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
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

                        previewRow.Values = new List<string> { scopeName, title, content, tags, priority.ToString(), isActive ? "Yes" : "No" };

                        if (string.IsNullOrEmpty(scopeName) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Phạm vi (Scope), Tiêu đề và Nội dung là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var scope = transientScopes.FirstOrDefault(s => s.Name.Equals(scopeName, StringComparison.OrdinalIgnoreCase));
                        if (scope == null)
                        {
                            scope = new AIBrainScope
                            {
                                Id = Guid.NewGuid(),
                                Name = scopeName,
                                Description = "Được tạo tự động từ quy trình Import",
                                IsActive = true,
                                Order = transientScopes.Count + 1,
                                CreatedAt = DateTime.Now
                            };
                            newScopesToCreate.Add(scope);
                            transientScopes.Add(scope);
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

                        validUnits.Add(unit);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ" + (newScopesToCreate.Contains(scope) ? " (Tạo scope mới)" : "");
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validUnits.Any())
                {
                    var cacheData = new AIKnowledgeImportCache
                    {
                        ScopesToCreate = newScopesToCreate,
                        UnitsToCreate = validUnits
                    };
                    _cache.Set(cacheKey, cacheData, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmAIKnowledgeAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out AIKnowledgeImportCache cacheData))
            {
                try
                {
                    if (cacheData.ScopesToCreate.Any())
                    {
                        _context.AIBrainScopes.AddRange(cacheData.ScopesToCreate);
                        await _context.SaveChangesAsync();
                    }

                    foreach (var unit in cacheData.UnitsToCreate)
                    {
                        try
                        {
                            var embedText = $"{unit.Title}. {unit.Content}. Thẻ: {unit.Tags}";
                            var rawEmbedding = await _embeddingService.GetEmbeddingAsync(embedText);
                            unit.Embedding = new Pgvector.Vector(rawEmbedding);
                        }
                        catch (Exception embedEx)
                        {
                            result.Errors.Add($"Cảnh báo: Không thể tạo AI Embedding cho tri thức '{unit.Title}': {embedEx.Message}");
                        }
                    }

                    _context.AIKnowledgeUnits.AddRange(cacheData.UnitsToCreate);
                    await _context.SaveChangesAsync();

                    result.SuccessCount = cacheData.UnitsToCreate.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = cacheData.UnitsToCreate.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }

        public async Task<ImportPreviewResult> PreviewRoomSlotTemplatesAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Tên mẫu", "Mã mẫu", "Thời lượng (phút)", "Dọn phòng", "Giờ bắt đầu", "Giờ cố định bắt đầu", "Giờ cố định kết thúc", "Qua đêm", "Bật" };
            var validTemplates = new List<RoomSlotTemplate>();
            string cacheKey = "import_slots_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "slots");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
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

                        previewRow.Values = new List<string> { 
                            name, 
                            code, 
                            duration.ToString(), 
                            cleanup.ToString(), 
                            seedStart?.ToString("HH:mm") ?? "", 
                            fixedStart?.ToString("HH:mm") ?? "", 
                            fixedEnd?.ToString("HH:mm") ?? "", 
                            crossesMidnight ? "Yes" : "No", 
                            isActive ? "Yes" : "No" 
                        };

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code) || duration <= 0)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Tên mẫu, Mã mẫu và Thời lượng (> 0) là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var exists = await _context.RoomSlotTemplates.AnyAsync(t => t.Code.ToLower() == code.ToLower() && !t.IsDeleted);
                        if (exists)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Mã khung giờ '{code}' đã tồn tại.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
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

                        validTemplates.Add(template);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ";
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validTemplates.Any())
                {
                    _cache.Set(cacheKey, validTemplates, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmRoomSlotTemplatesAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out List<RoomSlotTemplate> templates))
            {
                try
                {
                    _context.RoomSlotTemplates.AddRange(templates);
                    await _context.SaveChangesAsync();
                    result.SuccessCount = templates.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = templates.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }

        public async Task<ImportPreviewResult> PreviewHolidaysAsync(IFormFile file)
        {
            var result = new ImportPreviewResult();
            result.Headers = new List<string> { "Ngày lễ", "Mô tả" };
            var validHolidays = new List<Holiday>();
            string cacheKey = "import_holidays_" + Guid.NewGuid().ToString();

            try
            {
                var (workbook, _) = await ProcessUploadFileAsync(file, "holidays");
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    throw new Exception("File Excel/Word không có worksheet nào.");
                }

                var rows = worksheet.RowsUsed().Skip(1);
                int rowIndex = 1;

                foreach (var row in rows)
                {
                    rowIndex++;
                    var previewRow = new ImportPreviewRow();
                    try
                    {
                        var cell1 = row.Cell(1).Value;
                        if (cell1.IsBlank)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = "Ngày lễ là bắt buộc.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        DateTime holidayDate;
                        if (cell1.IsDateTime)
                        {
                            holidayDate = cell1.GetDateTime();
                        }
                        else if (!DateTime.TryParse(cell1.ToString(), out holidayDate))
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Ngày lễ '{cell1}' không đúng định dạng (yyyy-MM-dd).";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        holidayDate = holidayDate.Date;
                        string desc = row.Cell(2).GetValue<string>().Trim();

                        previewRow.Values = new List<string> { holidayDate.ToString("yyyy-MM-dd"), desc };

                        var exists = await _context.Holidays.AnyAsync(h => h.Date == holidayDate && !h.IsDeleted);
                        if (exists)
                        {
                            previewRow.IsValid = false;
                            previewRow.Message = $"Ngày lễ '{holidayDate:yyyy-MM-dd}' đã tồn tại.";
                            result.FailureCount++;
                            result.Rows.Add(previewRow);
                            continue;
                        }

                        var holiday = new Holiday
                        {
                            Date = holidayDate,
                            Description = string.IsNullOrEmpty(desc) ? null : desc,
                            IsDeleted = false
                        };

                        validHolidays.Add(holiday);
                        previewRow.IsValid = true;
                        previewRow.Message = "Hợp lệ";
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        previewRow.IsValid = false;
                        previewRow.Message = $"Lỗi xử lý: {ex.Message}";
                        result.FailureCount++;
                    }
                    result.Rows.Add(previewRow);
                }

                if (validHolidays.Any())
                {
                    _cache.Set(cacheKey, validHolidays, TimeSpan.FromMinutes(15));
                    result.CacheKey = cacheKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi đọc file: {ex.Message}");
            }

            return result;
        }

        public async Task<ImportResult> ConfirmHolidaysAsync(string cacheKey)
        {
            var result = new ImportResult();
            if (_cache.TryGetValue(cacheKey, out List<Holiday> holidays))
            {
                try
                {
                    _context.Holidays.AddRange(holidays);
                    await _context.SaveChangesAsync();
                    result.SuccessCount = holidays.Count;
                    _cache.Remove(cacheKey);
                }
                catch (Exception ex)
                {
                    result.FailureCount = holidays.Count;
                    result.Errors.Add($"Lỗi khi lưu cơ sở dữ liệu: {ex.Message}");
                }
            }
            else
            {
                result.Errors.Add("Phiên import đã hết hạn hoặc không tồn tại. Vui lòng tải lại file.");
            }
            return result;
        }
    }
}

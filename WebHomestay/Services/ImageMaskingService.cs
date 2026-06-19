using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Entities.Slots;
using WebHomestay.Models.Entities.Chat;
using WebHomestay.Models.Entities.AI;
using WebHomestay.Models.Enums;
using WebHomestay.Models.DTOs.Booking;
using WebHomestay.Models.DTOs.AI;
using WebHomestay.Models.Configuration;
using WebHomestay.Models.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace WebHomestay.Services.Infrastructure
{
    public class ImageMaskingService : IImageMaskingService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImageMaskingService> _logger;

        public ImageMaskingService(IWebHostEnvironment env, ILogger<ImageMaskingService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string> MaskIdCardAsync(string fileName, bool isFront)
        {
            try
            {
                // Source: App_Data/SecureUploads/IDCards/
                string sourcePath = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "IDCards", fileName);
                
                // Destination: wwwroot/uploads/masked/idcards/
                string outputDir = Path.Combine(_env.WebRootPath, "uploads", "masked", "idcards");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                string outFileName = "masked_" + fileName;
                string outputPath = Path.Combine(outputDir, outFileName);

                if (!File.Exists(sourcePath))
                {
                    _logger.LogWarning($"Source ID Card image not found: {sourcePath}");
                    return string.Empty;
                }

                using (var image = await Image.LoadAsync(sourcePath))
                {
                    int w = image.Width;
                    int h = image.Height;

                    image.Mutate(ctx => {
                        if (isFront)
                        {
                            // 1. Che số CCCD (giữ 3 số cuối là khó nếu không có OCR, nên che toàn bộ vùng số để an toàn)
                            // Tọa độ ước lượng cho CCCD Chip Việt Nam
                            ctx.Fill(Color.Black, new RectangleF(w * 0.42f, h * 0.18f, w * 0.45f, h * 0.08f));

                            // 2. Che Quê quán & Nơi thường trú
                            ctx.Fill(Color.Black, new RectangleF(w * 0.38f, h * 0.55f, w * 0.60f, h * 0.32f));
                        }
                        else
                        {
                            // Mặt sau: Che QR Code (Góc trên bên trái)
                            ctx.Fill(Color.Black, new RectangleF(w * 0.02f, h * 0.02f, w * 0.28f, h * 0.28f));
                            
                            // Che vùng mã vạch/ký tự lạ phía dưới (MRZ)
                            ctx.Fill(Color.Black, new RectangleF(0, h * 0.75f, w, h * 0.25f));
                        }

                        // 3. Đóng dấu Watermark bảo mật (Dùng Brush mờ)
                        // Vì SixLabors.Fonts yêu cầu cài đặt thêm font, tạm thời dùng vẽ đè mờ
                        // ctx.DrawText(...) - Bỏ qua nếu chưa cài Font
                    });

                    await image.SaveAsync(outputPath);
                }

                return "/uploads/masked/idcards/" + outFileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error masking ID card {fileName}");
                return string.Empty;
            }
        }
    }
}

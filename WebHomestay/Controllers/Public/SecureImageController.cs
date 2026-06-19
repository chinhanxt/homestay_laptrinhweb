using Microsoft.AspNetCore.Mvc;
using WebHomestay.Filters;

namespace WebHomestay.Controllers.Public
{
    [AdminAuthorize]
    public class SecureImageController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SecureImageController> _logger;

        public SecureImageController(IWebHostEnvironment env, ILogger<SecureImageController> logger)
        {
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetOriginal(string fileName)
        {
            bool hasImageView = Helpers.PermissionHelper.HasPermission(HttpContext, "images.original");
            
            if (!hasImageView)
            {
                return Forbid("Bạn không có quyền xem ảnh gốc.");
            }

            if (string.IsNullOrEmpty(fileName)) return NotFound();

            string secureDir = Path.Combine(_env.ContentRootPath, "App_Data", "SecureUploads", "IDCards");
            string filePath = Path.Combine(secureDir, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning($"Secure file not found: {filePath}");
                return NotFound();
            }

            // Detect content type
            string extension = Path.GetExtension(fileName).ToLower();
            string contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(filePath, contentType);
        }
    }
}

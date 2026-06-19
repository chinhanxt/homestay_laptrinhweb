using WebHomestay.Models.Entities.Core;
using WebHomestay.Models.Enums;
namespace WebHomestay.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

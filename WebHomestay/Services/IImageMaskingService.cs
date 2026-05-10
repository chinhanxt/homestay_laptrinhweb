namespace WebHomestay.Services
{
    public interface IImageMaskingService
    {
        Task<string> MaskIdCardAsync(string fileName, bool isFront);
    }
}

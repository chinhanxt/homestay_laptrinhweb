namespace WebHomestay.Services
{
    public interface ISettingService
    {
        Task<string> GetStringAsync(string key, string defaultValue = "");
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task UpdateSettingAsync(string key, string value);
    }
}

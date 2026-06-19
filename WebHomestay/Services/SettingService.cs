using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;
using WebHomestay.Models.Entities.Core;

namespace WebHomestay.Services
{
    public class SettingService : ISettingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CachePrefix = "Setting_";

        public SettingService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<string> GetStringAsync(string key, string defaultValue = "")
        {
            if (_cache.TryGetValue(CachePrefix + key, out string? value)) return value ?? defaultValue;

            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            value = setting?.SettingValue ?? defaultValue;

            // Cache for 10 minutes
            _cache.Set(CachePrefix + key, value, TimeSpan.FromMinutes(10));
            return value;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            string val = await GetStringAsync(key, defaultValue.ToString());
            return int.TryParse(val, out int result) ? result : defaultValue;
        }

        public async Task UpdateSettingAsync(string key, string value)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting 
                { 
                    SettingKey = key, 
                    SettingValue = value,
                    GroupName = "General"
                });
            }
            else
            {
                setting.SettingValue = value;
                setting.LastUpdated = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            _cache.Remove(CachePrefix + key);
        }
    }
}

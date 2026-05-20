using Microsoft.EntityFrameworkCore;
using WebHomestay.Data;

namespace WebHomestay.Services
{
    public class PricingService
    {
        private readonly ApplicationDbContext _context;
        public PricingService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tính toán giá phòng cho một ngày cụ thể dựa trên thứ tự ưu tiên:
        /// 1. Ngày Lễ (Priority 1)
        /// 2. Thứ 7 & Chủ Nhật (Priority 2)
        /// 3. Ngày thường (Priority 3)
        /// </summary>
        public async Task<decimal> GetRoomPriceForDate(int roomId, DateTime date, bool isHourly = false)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room == null) return 0;

            // 1. Kiểm tra ngày Lễ (Priority 1)
            var isHoliday = await _context.Holidays.AnyAsync(h => h.Date.Date == date.Date);
            if (isHoliday)
            {
                if (isHourly)
                    return room.PriceHolidayPerHour > 0 ? room.PriceHolidayPerHour : room.PricePerHour;
                else
                    return room.PriceHolidayPerDay > 0 ? room.PriceHolidayPerDay : room.PricePerDay;
            }

            // 2. Kiểm tra Cuối tuần (Priority 2)
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
            if (isWeekend)
            {
                if (isHourly)
                    return room.PriceWeekendPerHour > 0 ? room.PriceWeekendPerHour : room.PricePerHour;
                else
                    return room.PriceWeekendPerDay > 0 ? room.PriceWeekendPerDay : room.PricePerDay;
            }

            // 3. Ngày thường (Priority 3)
            return isHourly ? room.PricePerHour : room.PricePerDay;
        }
        public async Task<decimal> CalculateStayPriceAsync(int roomId, DateTime start, DateTime end, bool isHourly)
        {
            if (isHourly)
            {
                var hourlyPrice = await GetRoomPriceForDate(roomId, start, true);
                var hours = (decimal)Math.Max(0, (end - start).TotalHours);
                return hourlyPrice * hours;
            }

            decimal total = 0;
            for (var d = start.Date; d < end.Date; d = d.AddDays(1))
            {
                total += await GetRoomPriceForDate(roomId, d, false);
            }
            return total;
        }
    }
}

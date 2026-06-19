# Refactor Status - 2026-06-19

## Đã hoàn thành (276 lỗi → 31 lỗi)

### ✅ Tổ chức lại cấu trúc thư mục
- **Controllers**: Chia thành Admin/, Public/, AI/
- **Models**: Chia thành Entities/(Core/Slots/Chat/AI), DTOs/(Booking/AI), Enums/, Configuration/, ViewModels/
- **Services**: Chia thành Booking/, Slots/, Room/, Branch/, Chat/, Settings/, Infrastructure/, AI/

### ✅ Cập nhật namespace
- Tất cả files đã được update namespace declaration
- Thêm using statements cho hầu hết files
- Cập nhật DI registration trong Program.cs

### ✅ Fix namespace conflicts
- Thêm aliases: `BranchEntity`, `RoomEntity`, `BookingEntity` cho các file có conflict
- Fix hardcoded `Models.*` references thành fully qualified names

## Còn lại 31 lỗi cần fix

### 1. Duplicate class definitions (CS0101)
- `Booking.cs` và `Branch.cs` trong `WebHomestay.Models.Entities.Core`
- Nguyên nhân: Có thể do file còn code cũ hoặc build cache
- **Giải pháp**: Kiểm tra file, xóa code trùng nếu có

### 2. Missing interfaces in subfolder services (CS0246)
Các service trong subfolder không tìm thấy interface (vì interface vẫn ở root):
- `IAvailabilityService` (AvailabilityService.cs)
- `IBookingCancellationService` (BookingCancellationService.cs)
- `IBookingCreationService` (BookingCreationService.cs)
- `IBranchLeadTimeService` (BranchLeadTimeService.cs)

**Giải pháp**: Thêm `using WebHomestay.Services;` vào các service implementation files

### 3. PricingService references
- RoomBookingViewService không tìm thấy PricingService
- **Giải pháp**: Thêm `using WebHomestay.Services.Booking;` hoặc dùng fully qualified name

### 4. Missing AI DTOs
- `AIBookingSessionState`, `AIUiBlock` trong BookingDecision.cs
- **Giải pháp**: Thêm `using WebHomestay.Models.DTOs.AI;`

### 5. Missing DTOs
- `CreateCancellationRequestDto` trong BookingCancellationService.cs
- **Giải pháp**: Kiểm tra xem DTO này có tồn tại không, nếu có thì thêm using

## Script hỗ trợ đã tạo

Các file script .sh đã được gom vào `scripts/refactor/legacy-shell/`:
- `scripts/refactor/legacy-shell/fix_remaining_errors.sh`
- `scripts/refactor/legacy-shell/fix_di_registrations.sh`
- `scripts/refactor/legacy-shell/comprehensive_namespace_fix.sh`
- `scripts/refactor/legacy-shell/fix_entity_references.sh`

## Bước tiếp theo

1. **Fix missing using statements trong service implementations:**
```bash
for file in WebHomestay/Services/Booking/*.cs \
            WebHomestay/Services/Branch/*.cs \
            WebHomestay/Services/Room/*.cs \
            WebHomestay/Services/Slots/*.cs; do
    if ! grep -q "using WebHomestay.Services;" "$file" 2>/dev/null; then
        sed -i '1i using WebHomestay.Services;' "$file"
    fi
done
```

2. **Kiểm tra và fix duplicate class definitions:**
```bash
# Xem nội dung Booking.cs và Branch.cs
cat WebHomestay/Models/Entities/Core/Booking.cs | head -20
cat WebHomestay/Models/Entities/Core/Branch.cs | head -20
```

3. **Build lại và xem lỗi cụ thể:**
```bash
dotnet build WebHomestay/WebHomestay.csproj 2>&1 | grep "error CS" | head -20
```

4. **Fix từng lỗi còn lại dựa trên error messages**

## Notes

- Interfaces vẫn ở `WebHomestay/Services/` (root)
- Implementations đã move vào subfolders
- Cần thêm `using WebHomestay.Services;` cho các implementation files để tìm thấy interface

# CHƯƠNG V: DEMO XÂY DỰNG CHƯƠNG TRÌNH (TIẾP THEO)

## 5.2 Quy trình khởi động hệ thống (Ngắn gọn & Đơn giản)

Để chạy thử nghiệm cục bộ ứng dụng **WebHomestay**, quy trình khởi động được thực hiện qua 4 bước sau:

### Bước 1: Chuẩn bị cơ sở dữ liệu và API Key
- Khởi động dịch vụ cơ sở dữ liệu **PostgreSQL** cục bộ trên máy tính.
- Tạo file `key.md` ở thư mục cha (thư mục nằm ngoài thư mục dự án) để lưu trữ mã khóa API Groq/Gemini hợp lệ phục vụ lõi AI.

### Bước 2: Restore và Biên dịch ứng dụng (Build)
- Mở PowerShell tại thư mục gốc của dự án.
- Chạy lệnh biên dịch và khôi phục thư viện:
  ```powershell
  dotnet build WebHomestay/WebHomestay.csproj
  ```

### Bước 3: Khởi chạy máy chủ Web (Run)
- Khởi động máy chủ Web cục bộ bằng lệnh:
  ```powershell
  dotnet run --project WebHomestay/WebHomestay.csproj
  ```
  *(Hoặc chạy lệnh tắt nhanh bằng file script có sẵn: `.\r.ps1 up`)*

### Bước 4: Di trú CSDL tự động và Truy cập
- Khi ứng dụng chạy lên, `Program.cs` sẽ tự động gọi lệnh di trú cấu trúc bảng (`Database.Migrate()`) xuống PostgreSQL.
- Mở trình duyệt web và truy cập địa chỉ: `http://localhost:5000` để bắt đầu trải nghiệm hệ thống.

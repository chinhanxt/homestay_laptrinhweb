# Thiết kế Trang Thống kê Homestay (Analytics Bento Pro)

**Ngày tạo**: 2026-05-11
**Trạng thái**: Chờ phê duyệt

## 1. Mục tiêu dự án
Tái cấu trúc trang thống kê quản trị thành một dashboard hiện đại, chuyên nghiệp với khả năng hiển thị dữ liệu đa chiều, giúp chủ homestay nắm bắt tình hình kinh doanh (doanh thu, hiệu suất, xu hướng) một cách nhanh chóng và chính xác.

## 2. Yêu cầu chức năng

### 2.1. Hệ thống chỉ số (Key Metrics)
*   **Doanh thu tổng**: Tổng số tiền từ các đơn đặt phòng thành công.
*   **Doanh thu theo chi nhánh/phòng**: Tách bạch rõ ràng thông qua bộ lọc.
*   **Tỉ lệ phụ thu**: % số lượng đơn đặt phòng có phát sinh phụ thu trên tổng số đơn.
*   **Tỉ lệ lấp đầy (Occupancy Rate)**: Tỉ lệ số giờ/ngày đã được đặt trên tổng quỹ thời gian khả dụng.

### 2.2. Biểu đồ & Phân tích (Analytics)
*   **Xu hướng doanh thu (Line Chart)**: Biểu đồ đường hiển thị biến động doanh thu theo thời gian đã chọn.
*   **Cơ cấu doanh thu (Doughnut Chart)**: Hiển thị tỉ trọng đóng góp doanh thu của từng chi nhánh khi xem ở chế độ "Toàn hệ thống".
*   **Bản đồ nhiệt (Time Heatmap)**: Lưới 7x24 (Thứ x Giờ) hiển thị mật độ đặt phòng. Màu sắc đậm nhạt tương ứng với số lượng đơn.
*   **Bảng xếp hạng (Top Ranking)**: Top 5-10 phòng có hiệu suất cao nhất (hỗ trợ chuyển đổi giữa tiêu chí Doanh thu và Số lượt đặt).

### 2.3. Bộ lọc & Điều hướng (Filters)
*   **Bộ lọc thời gian**:
    *   Nhanh: Hôm nay, 7 ngày, 30 ngày, Tháng này, Năm này.
    *   Tùy chỉnh: Chọn khoảng ngày bất kỳ.
*   **Bộ lọc đối tượng**: Chọn theo Chi nhánh hoặc theo từng Phòng cụ thể.

### 2.4. Xuất báo cáo (Export)
*   **Excel**: Xuất bảng dữ liệu chi tiết các chỉ số và danh sách phòng.
*   **PDF**: Xuất bản in dashboard (dạng snapshot chuyên nghiệp).

## 3. Thiết kế UI/UX (Aesthetics)
*   **Phong cách**: Modern Bento Dashboard (Glassmorphism).
*   **Màu sắc**: 
    *   Chủ đạo: Dark Mode (Nền tối sâu).
    *   Accent: Emerald Green (Doanh thu), Electric Blue (Đơn hàng), Amber (Cảnh báo/Phụ thu).
*   **Trải nghiệm**: Tương tác mượt mà, tải dữ liệu AJAX không load lại trang, hiệu ứng hover tinh tế cho các ô Heatmap.

## 4. Kiến trúc kỹ thuật

### 4.1. Frontend
*   **Library**: Chart.js (v4+) cho các biểu đồ chính.
*   **Heatmap**: Custom CSS Grid hoặc SVG để tối ưu hiệu suất hiển thị.
*   **Communication**: Fetch API/AJAX để lấy dữ liệu JSON từ Server.

### 4.2. Backend (C# / ASP.NET Core)
*   **Service**: `StatisticsService` mở rộng thêm các hàm tính toán:
    *   `GetSurchargeRatioAsync`
    *   `GetTimeHeatmapDataAsync`
    *   `GetBranchRevenueSplitAsync`
*   **Repository**: Sử dụng LINQ to Entities để tối ưu các câu truy vấn GROUP BY và SUM.

### 4.3. Export Logic
*   **Excel**: Thư viện `ClosedXML`.
*   **PDF**: Thư viện `iTextSharp` hoặc `QuestPDF` để tạo layout báo cáo từ Code.

## 5. Kế hoạch triển khai (Tóm tắt)
1.  Nâng cấp `StatisticsService` để cung cấp đủ dữ liệu mới.
2.  Xây dựng giao diện Bento mới trong `Index.cshtml` và `admin-stats.css`.
3.  Tích hợp Chart.js và Heatmap logic.
4.  Cài đặt các bộ lọc và xử lý AJAX.
5.  Xây dựng chức năng Export Excel/PDF.

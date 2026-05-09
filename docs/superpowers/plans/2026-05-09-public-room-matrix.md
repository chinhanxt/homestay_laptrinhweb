# Ma trận phòng tương tác (Public Matrix) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng bảng ma trận hiển thị lịch trống của tất cả các phòng cho khách hàng, hỗ trợ đặt phòng nhanh khi click vào ô trống.

**Architecture:** Sử dụng ViewModel để tổng hợp dữ liệu từ nhiều chi nhánh. Xây dựng Grid UI linh hoạt bằng CSS và xử lý tương tác bằng JavaScript.

**Tech Stack:** ASP.NET Core MVC, CSS Grid, JavaScript (Vanilla).

---

### Task 1: Xây dựng ViewModel & Logic Backend

**Files:**
- Create: `WebHomestay/Models/ViewModels/PublicMatrixViewModel.cs`
- Modify: `WebHomestay/Controllers/HomeController.cs`

- [ ] **Step 1: Tạo PublicMatrixViewModel**
Lưu thông tin danh sách ngày (30 ngày), danh sách chi nhánh và trạng thái từng phòng.

- [ ] **Step 2: Viết logic lấy dữ liệu trong HomeController**
Tính toán trạng thái `Available`, `Booked`, `AwaitingApproval` cho từng phòng trong 30 ngày tới.

- [ ] **Step 3: Commit**

### Task 2: Xây dựng Giao diện Grid (Frontend)

**Files:**
- Create: `WebHomestay/Views/Home/Availability.cshtml`
- Modify: `WebHomestay/wwwroot/css/site.css`

- [ ] **Step 1: Thiết kế CSS cho Bảng Ma trận**
Sử dụng `display: grid` với `sticky` cho cột tên phòng và hàng ngày tháng. Đảm bảo hiển thị đẹp trên cả mobile (scroll ngang).

- [ ] **Step 2: Viết mã HTML cho bảng**
Hiển thị tên chi nhánh làm header, sau đó là danh sách phòng và các ô trạng thái.

- [ ] **Step 3: Commit**

### Task 3: Tương tác Click-to-Book (JavaScript)

**Files:**
- Modify: `WebHomestay/Views/Home/Availability.cshtml`

- [ ] **Step 1: Viết Script xử lý sự kiện Click**
Khi khách bấm vào ô xanh (Trống), hiển thị một Modal nhỏ tại chỗ hoặc Modal Bootstrap để xác nhận ngày đặt.

- [ ] **Step 2: Chuyển hướng tới Checkout**
Truyền các tham số `roomId`, `start`, `end` vào URL của trang Checkout.

- [ ] **Step 3: Commit**

### Task 4: Tích hợp & Hoàn thiện

**Files:**
- Modify: `WebHomestay/Views/Shared/_Layout.cshtml`
- Modify: `WebHomestay/Views/Home/Index.cshtml`

- [ ] **Step 1: Thêm Menu "Lịch trống" vào Thanh điều hướng**
- [ ] **Step 2: Thêm một Banner/Section giới thiệu Ma trận ở Trang chủ**
- [ ] **Step 3: Kiểm thử tổng thể**

- [ ] **Step 4: Commit**

# Public AI Redesign and Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triển khai luồng tạm dừng AI (Handoff) đồng bộ với Admin AI Studio Config, cập nhật Dynamic Gating và làm lại giao diện chat khách hàng theo phong cách Luxury Editorial.

**Architecture:** Mở rộng `ContextAwareBookingConductor` để đọc `Handoff.Keywords` từ `AdminAIStudioConfig` và kích hoạt SignalR. Cập nhật `user-premium.css` cho giao diện kính mờ và `site.js` để hiển thị khối UI Handoff. Bổ sung test case.

**Tech Stack:** ASP.NET Core, SignalR, Vanilla JS, CSS.

---

### Task 1: Bổ sung logic Handoff và Dynamic Gating vào ContextAwareBookingConductor

**Files:**
- Modify: `WebHomestay/Services/ContextAwareBookingConductor.cs`
- Modify: `WebHomestay/Models/AI/MessageIntent.cs` (nếu cần định nghĩa thêm `HandoffRequest`)

- [ ] **Step 1: Bổ sung MessageIntent.HandoffRequest**
Kiểm tra `MessageIntent` enum và thêm `HandoffRequest` nếu chưa có. (Mặc định thêm vào file nếu tách riêng, hoặc định nghĩa bên trong nếu nằm cùng file).

- [ ] **Step 2: Cập nhật phương thức ClassifyIntent**
Sửa `ClassifyIntent` để nhận thêm tham số `AdminAIStudioConfig studioConfig`. Kiểm tra xem `message` có chứa từ khóa trong `studioConfig.Handoff.Keywords` không. Nếu có, trả về `MessageIntent.HandoffRequest`.

- [ ] **Step 3: Xử lý HandoffRequest trong DecideAsync**
Trong `DecideAsync`, khi intent là `HandoffRequest`, tiến hành:
1. Gọi `_adminChatService.UpdateSessionStatusAsync(sessionId, "paused", "handoff_request")`.
2. Tạo system message lưu vào lịch sử: `"Khách hàng yêu cầu gặp nhân viên."`.
3. Gửi thông báo SignalR (nếu có interface tích hợp trong hệ thống, ví dụ qua `IHubContext` hoặc phương thức của `_adminChatService`).
4. Trả về `ConductorResult` với hành động `ConductorAction.Reply`, kèm `UiBlocks` chứa khối `handoffContact` (thông tin lấy từ `studioConfig.Handoff.ContactInstruction` hoặc branch).

- [ ] **Step 4: Cập nhật Dynamic Gating**
Trong `GetMissingRequiredFields`, thay vì hardcode các trường tĩnh, đọc từ `studioConfig.ConversationFlows` theo `BookingMode` hiện tại để lấy `RequiredFieldKeys`.

- [ ] **Step 5: Ánh xạ Notes**
Trong `HandleActionAsync` (phần "submit-booking-form"), đảm bảo `customerNote` được ánh xạ đúng.

### Task 2: Cập nhật Giao diện Khách hàng - Luxury Editorial CSS

**Files:**
- Modify: `WebHomestay/wwwroot/css/user-premium.css`

- [ ] **Step 1: Nâng cấp thiết kế khung chat (Glassmorphism)**
Thêm hoặc cập nhật CSS cho `.ai-chat-widget`, `.ai-chat-messages`, `.ai-chat-header` với hiệu ứng kính mờ (backdrop-filter: blur(20px)) và nền trong suốt `rgba(255,255,255,0.88)`.
Đổi màu khung viền sang vàng đồng `var(--luxury-accent)`.

- [ ] **Step 2: Hoạt ảnh Pulse cho Avatar**
Thêm CSS `.online-pulse` cho avatar trên header chat.

- [ ] **Step 3: Kiểu dáng cho Handoff Contact Block**
Thêm CSS cho `.ai-handoff-contact`: dạng thẻ bo góc, viền vàng đồng, bên trong có các nút liên hệ Hotline, Zalo dạng `lux-btn-ghost` hoặc `btn-auth-gold`.

### Task 3: Cập nhật JS xử lý UI mới

**Files:**
- Modify: `WebHomestay/wwwroot/js/site.js`

- [ ] **Step 1: Cập nhật renderUiBlocks để hỗ trợ handoffContact**
Thêm điều kiện `if (block.type === 'handoffContact') renderHandoffContact(block.data);` vào `renderUiBlocks`.

- [ ] **Step 2: Viết hàm renderHandoffContact**
Tạo hàm DOM xây dựng thẻ Handoff chứa thông tin tên chi nhánh, địa chỉ, các nút `tel:`, link zalo và link Google Maps.

- [ ] **Step 3: Cập nhật thẻ phòng có ảnh**
Trong `renderRoomCards`, nếu `room.imageUrl` có dữ liệu, tạo thẻ `<img>` với `object-fit: cover` và chèn vào đầu thẻ phòng.

### Task 4: Viết Tests cho Logic Handoff

**Files:**
- Modify: `WebHomestay.Tests/Services/ContextAwareBookingConductorTests.cs`

- [ ] **Step 1: Viết test case kích hoạt Handoff**
Tạo test mô phỏng người dùng nhập từ khóa "gặp nhân viên", verify `DecideAsync` trả về block `handoffContact` và gọi dịch vụ update session thành `paused`.

- [ ] **Step 2: Viết test case cho Dynamic Gating**
Tạo test mô phỏng thiếu trường `hourlyDate`, đảm bảo Conductor lấy từ danh sách cấu hình và yêu cầu bổ sung trường này.

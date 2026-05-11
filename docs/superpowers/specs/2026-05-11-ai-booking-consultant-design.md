# Đặc tả Thiết kế: Hệ thống Quản trị Tri thức AI (AI Knowledge Hub)

**Ngày:** 2026-05-11  
**Trạng thái:** Dự thảo (Đã duyệt ý tưởng)  
**Tác giả:** Antigravity AI

---

## 1. Mục tiêu (Objective)
Xây dựng một phân hệ quản trị toàn diện ("Ngôi trường của AI") nằm trong tab **AI Tư vấn đặt phòng**. Đây là nơi Admin cung cấp kiến thức, định hình phong cách và kết nối dữ liệu thực tế để AI trở thành một nhân viên Sale chuyên nghiệp, hỗ trợ khách hàng đặt phòng trực tiếp.

## 2. Các Tính năng Chính (Key Features)

### 2.1. Quản lý Kho Tri thức (Knowledge Management)
- **Cấu trúc:** Chia thành các **Chủ đề Kỹ năng** (Collections):
    - *Kỹ năng Sales & Thuyết phục:* Các kịch bản chốt đơn, xử lý từ chối.
    - *Kiến thức Hệ thống:* Thông tin chi tiết chi nhánh, tiện ích, đặc điểm phòng.
    - *Quy định & Chính sách:* Quy định đặt/hủy, phí dịch vụ, nội quy.
- **Biên tập:** Hỗ trợ trình soạn thảo **Markdown/HTML** để trình bày nội dung có cấu trúc (bảng, danh sách, in đậm...).

### 2.2. Kết nối Dữ liệu Hệ thống (Live Data Integration)
- **Tự động hóa:** AI được "cắm" trực tiếp vào luồng dữ liệu thời gian thực:
    - Danh sách chi nhánh (Vị trí, liên hệ).
    - Trạng thái phòng trống (Real-time availability).
    - Biểu giá gốc và quy tắc ngày lễ.
- **Tư duy Tính toán:** AI có khả năng tự tính toán giá cuối cùng (bao gồm phụ phí, giảm giá) dựa trên các quy tắc đã được dạy trong Kho tri thức.

### 2.3. Định hình Nhân sự AI (Staff Persona)
- **Cấu hình định danh:** Tên nhân viên, chức vụ, phong cách ngôn ngữ (Chuyên nghiệp/Thân thiện).
- **Nguyên tắc hành động:** Danh sách các điều được làm và không được làm (luôn upsell, không tiết lộ thông tin bảo mật khách hàng...).
- **Khả năng tạo đơn:** AI có thể tạo link đặt phòng nhanh dựa trên lựa chọn của khách.

### 2.4. Khu vực Thực hành (Testing Playground)
- **Mô phỏng:** Một cửa sổ chat tích hợp ngay tại trang quản trị.
- **Mục đích:** Admin có thể chat thử với AI để kiểm tra xem nó đã "thuộc bài" và áp dụng đúng các kiến thức vừa được cập nhật hay chưa.

## 3. Kiến trúc Giao diện (UI/UX Design)
- **Phong cách:** Bento Grid Premium (đồng bộ với hệ thống hiện tại).
- **Bố cục:**
    - **Sidebar trái:** Danh mục kỹ năng.
    - **Main Area:** Danh sách bài học & Trình soạn thảo Markdown.
    - **Sidebar phải:** Trạng thái kết nối dữ liệu & Cửa sổ Testing Playground.

## 4. Mô hình Dữ liệu (Data Models)

### `AIKnowledgeCollection`
- `Id` (Guid)
- `Name` (String): Tên nhóm kỹ năng.
- `Icon` (String): FontAwesome icon.
- `Order` (Int): Thứ tự hiển thị.

### `AIKnowledgeArticle`
- `Id` (Guid)
- `CollectionId` (Guid)
- `Title` (String): Tiêu đề bài học.
- `Content` (String/Text): Nội dung Markdown/HTML.
- `LastUpdated` (DateTime)

### `AISetting` (Key-Value)
- `StaffName`, `StaffPersona`, `SecurityRules`, `PriceCalculationLogic`.

## 5. Luồng Xử lý AI (Conceptual Flow)
1. Khách hàng gửi tin nhắn.
2. Hệ thống tìm kiếm các đoạn tri thức liên quan trong **Knowledge Hub** (Sử dụng RAG).
3. Hệ thống lấy dữ liệu phòng trống thời gian thực.
4. Tổng hợp thành một Prompt gửi cho LLM.
5. AI trả lời với vai trò nhân viên Sale và kèm theo các "Action" (tạo đơn, gợi ý phòng).

---
*Lưu ý: Tài liệu này tập trung vào phần xây dựng môi trường quản trị (Tab AI) trước khi tích hợp Model AI cụ thể.*

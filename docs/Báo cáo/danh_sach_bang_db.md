# ĐẶC TẢ CHI TIẾT CÁC THUỘC TÍNH CỦA BẢNG TRONG CƠ SỞ DỮ LIỆU (DATA DICTIONARY)

Dưới đây là đặc tả chi tiết toàn bộ các cột/thuộc tính của 27 bảng thực tế trong cơ sở dữ liệu PostgreSQL của hệ thống **WebHomestay**, tương ứng với nội dung trong file Word [danh_sach_bang_db.docx](file:///c:/Users/admin/Documents/VS%20T%C3%ADm/web_homestay/web_homestay/B%C3%A1o%20c%C3%A1o/danh_sach_bang_db.docx).

---

## 1. Bảng `branches` (Chi nhánh)
*Ý nghĩa:* Lưu trữ danh sách các chi nhánh homestay trên hệ thống.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã số định danh duy nhất của chi nhánh. |
| 2 | `name` | Không | Tên hiển thị của chi nhánh. |
| 3 | `address` | Không | Địa chỉ cụ thể của chi nhánh. |
| 4 | `description` | Không | Mô tả chi tiết hoặc giới thiệu về chi nhánh. |
| 5 | `hotline` | Không | Số điện thoại đường dây nóng liên hệ chi nhánh. |
| 6 | `map_url` | Không | Đường dẫn liên kết bản đồ Google Map. |
| 7 | `email` | Không | Hòm thư điện tử hỗ trợ của chi nhánh. |
| 8 | `booking_lead_time_hours` | Không | Số giờ tối thiểu khách phải đặt trước giờ nhận phòng. |
| 9 | `deleted_at` | Không | Thời gian thực hiện xóa tạm thời (nếu có). |
| 10 | `is_deleted` | Không | Đánh dấu trạng thái đã xóa mềm của chi nhánh. |

---

## 2. Bảng `rooms` (Phòng)
*Ý nghĩa:* Lưu trữ danh sách phòng vật lý thuộc các chi nhánh.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã số định danh duy nhất của phòng. |
| 2 | `name` | Không | Tên phòng hoặc số phòng vật lý. |
| 3 | `description` | Không | Mô tả chi tiết phòng (tiện nghi, hướng nhìn). |
| 4 | `price_per_hour` | Không | Đơn giá thuê cơ bản theo giờ. |
| 5 | `price_per_day` | Không | Đơn giá thuê cơ bản theo ngày. |
| 6 | `extra_guest_fee` | Không | Phụ thu cho mỗi khách phát sinh vượt định biên. |
| 7 | `capacity` | Không | Sức chứa tiêu chuẩn của phòng. |
| 8 | `max_guests` | Không | Số khách tối đa được phép lưu trú trong phòng. |
| 9 | `status` | Không | Trạng thái vật lý hiện tại của phòng. |
| 10 | `image_url` | Không | Đường dẫn ảnh đại diện chính của phòng. |
| 11 | `branch_id` | FK | Mã chi nhánh sở hữu phòng (liên kết tới `branches(id)`). |
| 12 | `created_at` | Không | Ngày giờ thêm phòng vào hệ thống. |
| 13 | `additional_images` | Không | Danh sách đường dẫn ảnh bổ sung dạng văn bản. |
| 14 | `price_weekend_per_day` | Không | Giá thuê theo ngày áp dụng cho dịp cuối tuần. |
| 15 | `price_weekend_per_hour` | Không | Giá thuê theo giờ áp dụng cho dịp cuối tuần. |
| 16 | `price_holiday_per_day` | Không | Giá thuê theo ngày áp dụng cho dịp nghỉ lễ. |
| 17 | `price_holiday_per_hour` | Không | Giá thuê theo giờ áp dụng cho dịp nghỉ lễ. |
| 18 | `embedding` | Không | Vector đặc trưng dùng cho tìm kiếm ngữ nghĩa của AI. |
| 19 | `deleted_at` | Không | Thời gian xóa tạm thời. |
| 20 | `is_deleted` | Không | Trạng thái đánh dấu đã xóa mềm. |

---

## 3. Bảng `amenities` (Tiện ích)
*Ý nghĩa:* Lưu trữ danh mục tiện ích dùng chung.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh duy nhất của tiện ích. |
| 2 | `name` | Không | Tên của tiện ích (Wifi, Tủ lạnh, Bồn tắm...). |
| 3 | `icon_class` | Không | Lớp CSS của icon FontAwesome đại diện. |

---

## 4. Bảng `room_amenities` (Tiện ích phòng)
*Ý nghĩa:* Bảng trung gian ánh xạ quan hệ nhiều-nhiều giữa phòng và tiện ích.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh liên kết tiện ích phòng. |
| 2 | `amenity_id` | FK | Liên kết tới mã tiện ích (`amenities(id)`). |
| 3 | `room_id` | FK | Liên kết tới mã phòng (`rooms(id)`). |

---

## 5. Bảng `room_slot_templates` (Mẫu slot giờ)
*Ý nghĩa:* Định nghĩa mẫu các khung giờ thuê (slots) tiêu chuẩn.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh mẫu khung giờ. |
| 2 | `name` | Không | Tên hiển thị của khung giờ mẫu. |
| 3 | `code` | Không | Mã ký hiệu viết tắt của khung giờ. |
| 4 | `duration_minutes` | Không | Thời lượng thuê tính bằng phút. |
| 5 | `cleanup_minutes` | Không | Thời gian dành cho dọn dẹp phòng sau check-out. |
| 6 | `seed_start_time` | Không | Thời gian khởi điểm phát sinh giờ. |
| 7 | `fixed_start_time` | Không | Giờ bắt đầu cố định của slot thuê. |
| 8 | `fixed_end_time` | Không | Giờ kết thúc cố định của slot thuê. |
| 9 | `crosses_midnight` | Không | Đánh dấu khung giờ có kéo dài qua nửa đêm hay không. |
| 10 | `is_active` | Không | Trạng thái kích hoạt hoạt động. |

---

## 6. Bảng `room_slot_template_assignments` (Phân bổ mẫu slot)
*Ý nghĩa:* Phân bổ các khung giờ mẫu áp dụng cho từng phòng.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh phân bổ. |
| 2 | `room_id` | FK | Mã phòng được áp dụng mẫu slot (`rooms(id)`). |
| 3 | `template_id` | FK | Mã khung giờ mẫu áp dụng (`room_slot_templates(id)`). |
| 4 | `effective_from` | Không | Ngày bắt đầu áp dụng khung giờ. |
| 5 | `effective_to` | Không | Ngày kết thúc áp dụng khung giờ. |
| 6 | `is_active` | Không | Trạng thái hiệu lực của gán mẫu. |

---

## 7. Bảng `room_slot_inventories` (Kho slot tồn kho)
*Ý nghĩa:* Lưu trữ chi tiết tình trạng kho slot giờ/ngày thực tế để phục vụ đặt lịch.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh kho slot thực tế. |
| 2 | `room_id` | FK | Mã phòng chứa slot (`rooms(id)`). |
| 3 | `template_id` | FK | Mã khung giờ mẫu (`room_slot_templates(id)`). |
| 4 | `slot_date` | Không | Ngày cụ thể của slot phòng. |
| 5 | `slot_label` | Không | Nhãn đại diện của slot (ví dụ: Slot 09:00 - 12:00). |
| 6 | `start_time` | Không | Thời gian bắt đầu chính xác. |
| 7 | `end_time` | Không | Thời gian kết thúc chính xác. |
| 8 | `status` | Không | Trạng thái slot: Trống, Giữ chỗ tạm thời, Đã đặt. |
| 9 | `booking_id` | FK | Mã đơn đặt liên kết nếu slot đã được đặt (`bookings(id)`). |

---

## 8. Bảng `room_slot_overrides` (Ghi đè slot)
*Ý nghĩa:* Lưu cấu hình ghi đè khung giờ của phòng trong một ngày cụ thể.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh ghi đè. |
| 2 | `room_id` | FK | Mã phòng được ghi đè (`rooms(id)`). |
| 3 | `target_date` | Không | Ngày áp dụng ghi đè. |
| 4 | `template_id` | FK | Mã mẫu giờ thay thế (`room_slot_templates(id)`). |
| 5 | `inventory_id` | FK | Mã slot tồn kho tương ứng bị ghi đè. |
| 6 | `override_type` | Không | Loại ghi đè (thêm mới, khóa slot...). |
| 7 | `reason` | Không | Lý do ghi đè phòng. |

---

## 9. Bảng `bookings` (Đơn đặt phòng)
*Ý nghĩa:* Bảng trung tâm quản lý các đơn đặt phòng của khách hàng.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã đơn đặt phòng (UUID duy nhất). |
| 2 | `user_id` | FK | Mã tài khoản khách hàng nếu có đăng nhập (`users(id)`). |
| 3 | `room_id` | FK | Mã phòng được đặt thuê (`rooms(id)`). |
| 4 | `customer_name` | Không | Họ và tên của khách hàng đặt phòng. |
| 5 | `customer_phone` | Không | Số điện thoại liên hệ của khách hàng. |
| 6 | `customer_email` | Không | Hòm thư điện tử liên hệ nhận hóa đơn. |
| 7 | `customer_zalo` | Không | Số tài khoản Zalo liên hệ khi cần. |
| 8 | `guest_count` | Không | Số lượng khách lưu trú thực tế. |
| 9 | `id_card_front_path` | Không | Đường dẫn tệp ảnh mặt trước CCCD của khách check-in. |
| 10 | `id_card_back_path` | Không | Đường dẫn tệp ảnh mặt sau CCCD của khách check-in. |
| 11 | `customer_note` | Không | Ghi chú bổ sung từ khách hàng lúc đặt. |
| 12 | `admin_note` | Không | Ghi chú vận hành của admin hoặc nhân viên dọn dẹp. |
| 13 | `start_time` | Không | Thời gian nhận phòng thực tế. |
| 14 | `end_time` | Không | Thời gian trả phòng thực tế. |
| 15 | `total_price` | Không | Tổng tiền đơn hàng. |
| 16 | `status` | Không | Trạng thái đơn: PendingPayment, Confirmed, CheckedIn, CheckedOut, Cancelled. |
| 17 | `payment_status` | Không | Trạng thái thanh toán (Unpaid, Paid, Refunded). |
| 18 | `created_at` | Không | Thời gian phát sinh đặt phòng. |
| 19 | `check_in_instructions` | Không | Nội dung hướng dẫn nhận phòng tự động. |
| 20 | `payment_proof_url` | Không | Đường dẫn ảnh chụp màn hình bill thanh toán. |
| 21 | `smart_lock_code` | Không | Mã số mở khóa phòng thông minh. |
| 22 | `wifi_password` | Không | Mật khẩu mạng wifi của phòng đó. |
| 23 | `booking_mode` | Không | Chế độ đặt phòng (Daily hay Hourly). |
| 24 | `room_slot_inventory_id` | FK | Mã liên kết slot kho thực tế nếu đặt theo giờ (liên kết tới `room_slot_inventories(id)`). |
| 25 | `slot_label` | Không | Nhãn slot thời gian phục vụ hiển thị. |

---

## 10. Bảng `booking_cancellation_requests` (Yêu cầu hủy đơn)
*Ý nghĩa:* Bảng quản lý yêu cầu hủy phòng và tiến trình hoàn tiền cho khách.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã yêu cầu hủy đơn. |
| 2 | `chat_session_id` | FK | Mã phiên chat admin liên kết (`admin_chat_sessions(id)`). |
| 3 | `booking_id` | FK | Mã đơn đặt phòng yêu cầu hủy (`bookings(id)`). |
| 4 | `customer_name` | Không | Tên khách hàng yêu cầu hủy đơn. |
| 5 | `customer_phone` | Không | Số điện thoại khách. |
| 6 | `customer_email` | Không | Email nhận thông báo hủy phòng. |
| 7 | `refund_bank_name` | Không | Tên ngân hàng nhận hoàn tiền. |
| 8 | `refund_bank_account_number` | Không | Số tài khoản nhận hoàn tiền. |
| 9 | `refund_bank_account_holder` | Không | Tên chủ tài khoản nhận hoàn tiền. |
| 10 | `status` | Không | Trạng thái yêu cầu (Chờ duyệt, Đồng ý, Từ chối). |
| 11 | `refund_bill_proof_path` | Không | Đường dẫn ảnh minh chứng chuyển khoản hoàn tiền. |
| 12 | `created_at` | Không | Ngày gửi yêu cầu hủy đơn. |

---

## 11. Bảng `users` (Khách hàng)
*Ý nghĩa:* Bảng quản lý thông tin tài khoản người dùng đăng ký ngoài trang chủ.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh người dùng. |
| 2 | `full_name` | Không | Họ tên đầy đủ. |
| 3 | `email` | Không | Hòm thư điện tử đăng nhập. |
| 4 | `password_hash` | Không | Mật khẩu đã mã hóa băm. |
| 5 | `phone_number` | Không | Số điện thoại. |
| 6 | `role` | Không | Vai trò tài khoản (mặc định: Customer). |
| 7 | `created_at` | Không | Ngày đăng ký tài khoản. |

---

## 12. Bảng `admin_users` (Nhân viên quản trị)
*Ý nghĩa:* Bảng quản lý thông tin tài khoản nhân sự nội bộ (Admin/Staff/Cleaner).

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh nhân viên. |
| 2 | `Username` | Không | Tên tài khoản đăng nhập admin. |
| 3 | `PasswordHash` | Không | Mật khẩu băm bảo mật. |
| 4 | `FullName` | Không | Họ tên đầy đủ nhân viên. |
| 5 | `Role` | Không | Vai trò cụ thể (SuperAdmin, Manager, Staff, Cleaner). |
| 6 | `BranchId` | Không | Mã chi nhánh làm việc được gán. |
| 7 | `PermissionsJson` | Không | Chuỗi JSON cấu hình các quyền ghi đè cá nhân. |
| 8 | `CreatedAt` | Không | Ngày khởi tạo tài khoản nhân viên. |

---

## 13. Bảng `role_permission_templates` (Mẫu quyền vai trò)
*Ý nghĩa:* Bảng cấu hình mẫu quyền hạn mặc định ứng với từng vai trò nhân sự.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `Id` | PK | Mã định danh mẫu quyền. |
| 2 | `Role` | Không | Vai trò áp dụng quyền mẫu. |
| 3 | `PermissionsJson` | Không | Chuỗi JSON cấu hình mặc định các key quyền hạn. |

---

## 14. Bảng `activity_logs` (Nhật ký hành động)
*Ý nghĩa:* Bảng ghi nhận lịch sử hành động thao tác của nhân viên admin.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `Id` | PK | Mã định danh log. |
| 2 | `AdminUserId` | FK | Mã nhân viên thực hiện (`admin_users(id)`). |
| 3 | `Action` | Không | Tên hành động (tạo phòng, duyệt thanh toán...). |
| 4 | `Target` | Không | Thực thể chịu tác động (ví dụ: Room 202, Booking X). |
| 5 | `Details` | Không | Mô tả chi tiết nội dung thay đổi. |
| 6 | `Timestamp` | Không | Thời điểm ghi nhận sự kiện. |

---

## 15. Bảng `system_settings` (Cài đặt hệ thống)
*Ý nghĩa:* Bảng lưu trữ cấu hình hệ thống và các tham số vận hành AI.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh cấu hình. |
| 2 | `setting_key` | Không | Khóa cấu hình duy nhất. |
| 3 | `setting_value` | Không | Giá trị cấu hình hiện hành. |
| 4 | `description` | Không | Mô tả tác dụng của cấu hình. |
| 5 | `group_name` | Không | Nhóm cấu hình (General, AI...). |

---

## 16. Bảng `holidays` (Ngày lễ tết)
*Ý nghĩa:* Bảng định nghĩa danh sách các ngày nghỉ lễ để áp dụng đơn giá lễ.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh ngày lễ. |
| 2 | `date` | Không | Ngày tháng nghỉ lễ. |
| 3 | `description` | Không | Tên hoặc mô tả dịp lễ tết. |

---

## 17. Bảng `ai_knowledge_collections` (Bộ sưu tập tri thức AI)
*Ý nghĩa:* Quản lý danh mục/nhóm các tri thức chung của AI.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `Id` | PK | Mã nhóm tri thức. |
| 2 | `Name` | Không | Tên nhóm tri thức (Quy chế check-in, Tiện ích xung quanh...). |
| 3 | `Icon` | Không | Ký hiệu icon hiển thị. |
| 4 | `Description` | Không | Mô tả sơ bộ về nhóm tri thức. |

---

## 18. Bảng `ai_knowledge_articles` (Bài viết tri thức AI)
*Ý nghĩa:* Bảng lưu trữ các bài viết tri thức lớn chi tiết của AI.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `Id` | PK | Mã bài viết tri thức. |
| 2 | `CollectionId` | FK | Mã nhóm tri thức thuộc về (`ai_knowledge_collections(Id)`). |
| 3 | `Title` | Không | Tiêu đề bài viết tri thức. |
| 4 | `Content` | Không | Nội dung văn bản chi tiết. |

---

## 19. Bảng `ai_knowledge_units` (Đơn vị tri thức RAG)
*Ý nghĩa:* Bảng lưu các câu tri thức bóc tách ngắn kèm vector nhúng phục vụ AI RAG.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã đơn vị tri thức. |
| 2 | `scope_id` | FK | Mã giới hạn scope (`ai_brain_scopes(id)`). |
| 3 | `title` | Không | Tiêu đề tri thức bóc tách. |
| 4 | `content` | Không | Nội dung chi tiết để AI RAG đối sánh. |
| 5 | `tags` | Không | Các tag phân loại để lọc nhanh. |
| 6 | `priority` | Không | Độ ưu tiên đối sánh từ khóa tri thức. |
| 7 | `is_active` | Không | Đánh dấu tri thức có đang kích hoạt sử dụng. |

---

## 20. Bảng `ai_brain_scopes` (Phạm vi tri thức AI)
*Ý nghĩa:* Bảng định biên các phạm vi tri thức hoạt động của AI.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã scope tri thức. |
| 2 | `name` | Không | Tên phạm vi tri thức. |
| 3 | `description` | Không | Mô tả phạm vi áp dụng. |
| 4 | `is_active` | Không | Trạng thái hoạt động. |

---

## 21. Bảng `ai_graph_nodes` (Đỉnh đồ thị AI)
*Ý nghĩa:* Bảng lưu trữ các đỉnh thực thể tri thức trong sơ đồ đồ thị AI Graph.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã đỉnh tri thức. |
| 2 | `node_type` | Không | Loại đỉnh (ví dụ: Chi nhánh, Loại phòng, Dịch vụ). |
| 3 | `label` | Không | Nhãn định danh của đỉnh. |
| 4 | `summary` | Không | Tóm tắt thông tin ngữ nghĩa tại đỉnh. |

---

## 22. Bảng `ai_graph_edges` (Cạnh đồ thị AI)
*Ý nghĩa:* Bảng lưu trữ các liên kết quan hệ (cạnh) giữa các đỉnh tri thức AI Graph.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã cạnh liên kết. |
| 2 | `from_node_id` | FK | Mã đỉnh xuất phát (`ai_graph_nodes(id)`). |
| 3 | `to_node_id` | FK | Mã đỉnh đích đến (`ai_graph_nodes(id)`). |
| 4 | `relationship_type` | Không | Loại quan hệ ngữ nghĩa (ví dụ: có_tiện_ích, thuộc_chi_nhánh). |
| 5 | `weight` | Không | Trọng số thể hiện mức độ chặt chẽ của quan hệ. |

---

## 23. Bảng `ai_agent_definitions` (Định nghĩa Agent AI)
*Ý nghĩa:* Bảng quản lý prompt hệ thống và định nghĩa vai trò các Agents trong pipeline.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định nghĩa agent. |
| 2 | `name` | Không | Tên tác nhân (Persona, Safety Guard...). |
| 3 | `role` | Không | Mô tả vai trò của tác nhân. |
| 4 | `system_prompt` | Không | Chuỗi prompt hệ thống chỉ định cách hành xử. |

---

## 24. Bảng `ai_conversation_traces` (Nhật ký trace AI)
*Ý nghĩa:* Bảng lưu vết chi tiết toàn bộ luồng xử lý Agent AI trong phiên chat của khách.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh log trace. |
| 2 | `session_id` | Không | Mã phiên làm việc chat. |
| 3 | `customer_message` | Không | Tin nhắn thô của khách hàng gửi lên. |
| 4 | `persona_summary` | Không | Tóm tắt trạng thái tâm lý do Persona Agent xử lý. |
| 5 | `live_system_snapshot` | Không | Dữ liệu phòng trống thực tế trích xuất lúc đó. |
| 6 | `retrieved_knowledge_json` | Không | Danh sách tri thức RAG đối sánh được dạng JSON. |
| 7 | `graph_reasoning_json` | Không | Kết quả suy luận ngữ nghĩa AI Graph dạng JSON. |
| 8 | `guard_result` | Không | Kết quả kiểm duyệt từ Safety Guard Agent. |
| 9 | `final_answer` | Không | Câu trả lời cuối cùng phản hồi cho người dùng. |

---

## 25. Bảng `admin_chat_sessions` (Phiên chat Admin tiếp quản)
*Ý nghĩa:* Bảng lưu trữ phiên chat admin tiếp quản cuộc trò chuyện với khách hàng.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã phiên chat tiếp quản. |
| 2 | `session_id` | Không | Mã phiên chat đại diện. |
| 3 | `customer_name` | Không | Tên khách hàng cần hỗ trợ. |
| 4 | `status` | Không | Trạng thái phiên tiếp quản (Active, Paused, Closed). |

---

## 26. Bảng `admin_chat_messages` (Tin nhắn phiên chat Admin)
*Ý nghĩa:* Bảng chi tiết các tin nhắn trong phiên chat tiếp quản.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `id` | PK | Mã định danh tin nhắn. |
| 2 | `session_id` | FK | Mã phiên chat tiếp quản liên kết (`admin_chat_sessions(id)`). |
| 3 | `role` | Không | Vai trò gửi tin nhắn (Admin hoặc Customer). |
| 4 | `content` | Không | Nội dung văn bản tin nhắn trao đổi. |
| 5 | `created_at` | Không | Thời điểm gửi tin nhắn. |

---

## 27. Bảng `__EFMigrationsHistory` (Lịch sử Entity Framework Migration)
*Ý nghĩa:* Bảng lưu trữ lịch sử cập nhật di trú cấu trúc CSDL của Entity Framework Core.

| STT | Tên thuộc tính / Cột | Khóa | Mô tả ý nghĩa trường dữ liệu |
|:---:|:---|:---:|:---|
| 1 | `MigrationId` | PK | Mã định danh duy nhất của bản migration. |
| 2 | `ProductVersion` | Không | Phiên bản thư viện EF Core đang hoạt động. |

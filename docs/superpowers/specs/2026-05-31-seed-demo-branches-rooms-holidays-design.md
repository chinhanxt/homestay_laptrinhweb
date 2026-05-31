# Seed dữ liệu demo chi nhánh, phòng và ngày lễ

## Mục tiêu

Tạo dữ liệu demo trực tiếp vào PostgreSQL hiện tại để thay thế thao tác tạo thủ công trên web admin. Dữ liệu cần đủ thật để các luồng hiện có như danh sách phòng, tìm kiếm, tính giá ngày thường/cuối tuần/ngày lễ và AI availability có dữ liệu hoạt động.

## Phạm vi

Seed một lần vào database local hiện tại:

- 5 chi nhánh.
- 25 phòng, mỗi chi nhánh 5 phòng.
- Giá ngày thường, Thứ 7/CN và ngày lễ cho từng phòng.
- Ma trận ngày lễ Việt Nam cho năm 2026 và 2027.

Không seed booking, user, slot inventory, ảnh upload thật, thanh toán hoặc dữ liệu AI knowledge.

## Chi nhánh

Seed 5 chi nhánh mẫu, dùng tên chi nhánh làm khóa nhận diện để tránh tạo trùng:

1. LumiStay Sài Gòn Quận 1 — khu trung tâm, phù hợp du lịch, công tác, cặp đôi.
2. LumiStay Riverside Quận 7 — không gian yên tĩnh, ven sông, phù hợp nghỉ dưỡng ngắn ngày.
3. LumiStay Bình Dương City — phù hợp khách công tác và khách gần khu công nghiệp.
4. LumiStay Biên Hòa Đồng Nai — phù hợp gia đình nhỏ, công tác và lưu trú ngắn ngày.
5. LumiStay Sen Hồng Đồng Tháp — phong cách nghỉ dưỡng nhẹ, du lịch miền Tây.

Mỗi chi nhánh có địa chỉ, mô tả, hotline, email, map URL mẫu và `BookingLeadTimeHours = 2`.

## Phòng và hạng phòng

Mỗi chi nhánh có 5 phòng, tương ứng 5 hạng tiếng Việt sang trọng:

| Hạng phòng | Định vị | Sức chứa đề xuất |
| --- | --- | --- |
| Tiêu Chuẩn Thanh Lịch | Phòng gọn, đẹp, giá dễ tiếp cận | 2 tiêu chuẩn / 3 tối đa |
| Cao Cấp An Nhiên | Tiện nghi hơn, hợp cặp đôi hoặc công tác | 2 tiêu chuẩn / 4 tối đa |
| Studio Ánh Sáng | Không gian mở, phù hợp lưu trú dài hơn | 2 tiêu chuẩn / 4 tối đa |
| Gia Đình Sum Vầy | Rộng hơn cho gia đình hoặc nhóm nhỏ | 4 tiêu chuẩn / 5 tối đa |
| Thượng Hạng Hoàng Gia | Cao cấp nhất, trải nghiệm sang trọng | 4 tiêu chuẩn / 6 tối đa |

Tên phòng sẽ dùng phong cách sang trọng kèm mã chi nhánh/tầng, ví dụ `An Nhiên Q1-101`, `Lụa Trắng Q7-201`, `Bình Minh BD-301`, `Sông Xanh DN-401`, `Hoàng Gia DT-501`.

Tất cả phòng seed có `Status = Available`. `ImageUrl` và `AdditionalImages` để trống để tránh tạo đường dẫn ảnh hỏng vì ảnh thật thường được upload qua web.

## Khung giá

Mỗi phòng được cấu hình đủ 6 giá:

| Hạng phòng | Ngày thường/giờ | Ngày thường/ngày | T7-CN/giờ | T7-CN/ngày | Lễ/giờ | Lễ/ngày |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Tiêu Chuẩn Thanh Lịch | 110000 | 750000 | 130000 | 860000 | 150000 | 980000 |
| Cao Cấp An Nhiên | 150000 | 1050000 | 175000 | 1200000 | 200000 | 1380000 |
| Studio Ánh Sáng | 190000 | 1350000 | 220000 | 1550000 | 250000 | 1780000 |
| Gia Đình Sum Vầy | 230000 | 1650000 | 265000 | 1900000 | 300000 | 2150000 |
| Thượng Hạng Hoàng Gia | 300000 | 2250000 | 345000 | 2600000 | 390000 | 2950000 |

`ExtraGuestFee` tăng theo hạng phòng, từ 80000 đến 150000. Giữ cùng khung giá giữa các chi nhánh để dễ demo và dễ kiểm tra.

## Ma trận ngày lễ 2026-2027

Seed các ngày lễ phổ biến ở Việt Nam vào bảng `holidays`:

- Tết Dương lịch.
- Tết Nguyên Đán.
- Giỗ Tổ Hùng Vương.
- Ngày Giải phóng miền Nam 30/4.
- Quốc tế Lao động 1/5.
- Quốc khánh 2/9.

Các dịp kéo dài được insert từng ngày với cùng mô tả. Lịch nghỉ bù chính thức có thể thay đổi theo công bố nhà nước; bộ seed này dùng lịch lễ phổ biến để phục vụ demo và có thể chỉnh lại trong web admin sau.

## Cách thực hiện

Dùng script SQL/PSQL chạy một lần vào PostgreSQL local theo connection string của `WebHomestay/appsettings.json`.

Script phải idempotent:

- Nếu chi nhánh seed đã tồn tại, cập nhật thông tin chi nhánh thay vì tạo trùng.
- Nếu phòng seed đã tồn tại trong chi nhánh, cập nhật mô tả, giá, sức chứa, trạng thái thay vì tạo trùng.
- Nếu ngày lễ đã tồn tại cùng ngày, không tạo trùng; có thể cập nhật mô tả nếu cần.
- Không xóa dữ liệu hiện có.
- Không sửa phòng hoặc chi nhánh ngoài bộ seed nhận diện được.

## Kiểm tra sau khi chạy

Sau khi chạy script, kiểm tra bằng truy vấn DB:

- Có đủ 5 chi nhánh LumiStay.
- Mỗi chi nhánh có đúng 5 phòng seed.
- Tổng số phòng seed là 25.
- Tất cả phòng seed có đủ giá ngày thường, T7/CN và lễ lớn hơn 0.
- Bảng `holidays` có dữ liệu ngày lễ cho 2026 và 2027.
- Không có duplicate theo tên chi nhánh, tên phòng trong chi nhánh, hoặc ngày lễ.

## Rủi ro và giới hạn

- Dữ liệu chỉ được seed vào database hiện tại, không tự xuất hiện ở môi trường khác nếu chưa chạy script.
- Không seed ảnh thật nên một số giao diện có thể hiển thị placeholder hoặc thiếu ảnh nếu view yêu cầu ảnh.
- Lịch ngày lễ/nghỉ bù có thể cần chỉnh lại khi có công bố chính thức.

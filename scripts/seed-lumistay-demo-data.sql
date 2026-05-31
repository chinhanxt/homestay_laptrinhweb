BEGIN;

-- 1) Branches: insert missing demo branches, then update their display data.
WITH seed_branches(name, address, description, hotline, email, map_url, booking_lead_time_hours) AS (
    VALUES
    ('LumiStay Sài Gòn Quận 1', '12 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', 'Chi nhánh trung tâm dành cho khách công tác, du lịch và cặp đôi muốn trải nghiệm nhịp sống Sài Gòn sang trọng.', '0901000001', 'q1@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Hue+Quan+1+Ho+Chi+Minh', 2),
    ('LumiStay Riverside Quận 7', '88 Nguyễn Văn Linh, Phường Tân Phong, Quận 7, TP. Hồ Chí Minh', 'Không gian ven sông yên tĩnh, phù hợp nghỉ dưỡng ngắn ngày, làm việc từ xa và gia đình nhỏ.', '0901000007', 'q7@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Van+Linh+Quan+7+Ho+Chi+Minh', 2),
    ('LumiStay Bình Dương City', '25 Đại lộ Bình Dương, Phường Phú Cường, TP. Thủ Dầu Một, Bình Dương', 'Điểm lưu trú tiện nghi cho khách công tác, chuyên gia và khách ghé khu công nghiệp Bình Dương.', '0901000061', 'binhduong@lumistay.vn', 'https://maps.google.com/?q=Dai+lo+Binh+Duong+Thu+Dau+Mot', 2),
    ('LumiStay Biên Hòa Đồng Nai', '39 Võ Thị Sáu, Phường Thống Nhất, TP. Biên Hòa, Đồng Nai', 'Chi nhánh cân bằng giữa công tác và nghỉ ngơi, phù hợp gia đình nhỏ hoặc khách lưu trú ngắn ngày.', '0901000039', 'dongnai@lumistay.vn', 'https://maps.google.com/?q=Vo+Thi+Sau+Bien+Hoa+Dong+Nai', 2),
    ('LumiStay Sen Hồng Đồng Tháp', '66 Nguyễn Huệ, Phường 1, TP. Cao Lãnh, Đồng Tháp', 'Phong cách nghỉ dưỡng nhẹ nhàng miền Tây, gần gũi, tinh tế và riêng tư.', '0901000066', 'dongthap@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Hue+Cao+Lanh+Dong+Thap', 2)
), inserted AS (
    INSERT INTO branches (name, address, description, hotline, email, map_url, booking_lead_time_hours)
    SELECT name, address, description, hotline, email, map_url, booking_lead_time_hours
    FROM seed_branches sb
    WHERE NOT EXISTS (
        SELECT 1 FROM branches b WHERE b.name = sb.name
    )
    RETURNING id
)
UPDATE branches b
SET address = sb.address,
    description = sb.description,
    hotline = sb.hotline,
    email = sb.email,
    map_url = sb.map_url,
    booking_lead_time_hours = sb.booking_lead_time_hours
FROM seed_branches sb
WHERE b.name = sb.name;

-- 2) Rooms: insert/update 5 Vietnamese luxury room tiers per branch.
WITH seed_rooms(branch_name, room_name, description, price_per_hour, price_per_day, extra_guest_fee, price_weekend_per_hour, price_weekend_per_day, price_holiday_per_hour, price_holiday_per_day, capacity, max_guests, status) AS (
    VALUES
    -- Quận 1
    ('LumiStay Sài Gòn Quận 1', 'An Nhiên Q1-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng gọn đẹp, ánh sáng ấm, phù hợp khách công tác hoặc cặp đôi cần không gian riêng tư tại trung tâm.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3, 'Available'),
    ('LumiStay Sài Gòn Quận 1', 'Lụa Trắng Q1-201', 'Hạng Cao Cấp An Nhiên: nội thất tinh tế, giường lớn, góc làm việc nhỏ và tiện nghi đầy đủ cho kỳ nghỉ ngắn ngày.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4, 'Available'),
    ('LumiStay Sài Gòn Quận 1', 'Bình Minh Q1-301', 'Hạng Studio Ánh Sáng: không gian mở, nhiều ánh sáng, phù hợp khách thích ở dài hơn hoặc làm việc từ xa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4, 'Available'),
    ('LumiStay Sài Gòn Quận 1', 'Sông Xanh Q1-401', 'Hạng Gia Đình Sum Vầy: diện tích rộng hơn, bố trí thoải mái cho gia đình nhỏ hoặc nhóm bạn.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5, 'Available'),
    ('LumiStay Sài Gòn Quận 1', 'Hoàng Gia Q1-501', 'Hạng Thượng Hạng Hoàng Gia: trải nghiệm cao cấp nhất với không gian sang trọng, riêng tư và chỉn chu.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6, 'Available'),

    -- Quận 7
    ('LumiStay Riverside Quận 7', 'An Nhiên Q7-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng yên tĩnh, tinh gọn, phù hợp nghỉ ngắn ngày gần khu đô thị Quận 7.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3, 'Available'),
    ('LumiStay Riverside Quận 7', 'Lụa Trắng Q7-201', 'Hạng Cao Cấp An Nhiên: tiện nghi nhẹ nhàng, gam màu thanh lịch, phù hợp cặp đôi và khách công tác.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4, 'Available'),
    ('LumiStay Riverside Quận 7', 'Bình Minh Q7-301', 'Hạng Studio Ánh Sáng: không gian mở sáng sủa, lý tưởng để nghỉ dưỡng ven sông hoặc làm việc từ xa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4, 'Available'),
    ('LumiStay Riverside Quận 7', 'Sông Xanh Q7-401', 'Hạng Gia Đình Sum Vầy: phòng rộng, tiện nghi cho nhóm nhỏ và gia đình cần sự thoải mái.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5, 'Available'),
    ('LumiStay Riverside Quận 7', 'Hoàng Gia Q7-501', 'Hạng Thượng Hạng Hoàng Gia: không gian cao cấp, riêng tư, phù hợp nghỉ dưỡng sang trọng tại Quận 7.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6, 'Available'),

    -- Bình Dương
    ('LumiStay Bình Dương City', 'An Nhiên BD-101', 'Hạng Tiêu Chuẩn Thanh Lịch: lựa chọn gọn gàng, tiện nghi cho khách công tác tại Bình Dương.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3, 'Available'),
    ('LumiStay Bình Dương City', 'Lụa Trắng BD-201', 'Hạng Cao Cấp An Nhiên: không gian thư giãn sau ngày làm việc, phù hợp chuyên gia và cặp đôi.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4, 'Available'),
    ('LumiStay Bình Dương City', 'Bình Minh BD-301', 'Hạng Studio Ánh Sáng: bố trí mở, sáng và thoáng, phù hợp lưu trú dài hơn.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4, 'Available'),
    ('LumiStay Bình Dương City', 'Sông Xanh BD-401', 'Hạng Gia Đình Sum Vầy: phòng rộng cho gia đình nhỏ hoặc nhóm khách công tác.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5, 'Available'),
    ('LumiStay Bình Dương City', 'Hoàng Gia BD-501', 'Hạng Thượng Hạng Hoàng Gia: lựa chọn cao cấp cho khách cần sự riêng tư và chỉn chu.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6, 'Available'),

    -- Đồng Nai
    ('LumiStay Biên Hòa Đồng Nai', 'An Nhiên DN-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng tinh gọn, sạch đẹp, phù hợp nghỉ nhanh hoặc công tác ngắn ngày.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3, 'Available'),
    ('LumiStay Biên Hòa Đồng Nai', 'Lụa Trắng DN-201', 'Hạng Cao Cấp An Nhiên: tiện nghi cân bằng, không gian êm dịu cho khách cần thư giãn.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4, 'Available'),
    ('LumiStay Biên Hòa Đồng Nai', 'Bình Minh DN-301', 'Hạng Studio Ánh Sáng: không gian mở, phù hợp khách ở dài hơn tại Biên Hòa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4, 'Available'),
    ('LumiStay Biên Hòa Đồng Nai', 'Sông Xanh DN-401', 'Hạng Gia Đình Sum Vầy: phòng rộng, thoải mái cho gia đình hoặc nhóm bạn.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5, 'Available'),
    ('LumiStay Biên Hòa Đồng Nai', 'Hoàng Gia DN-501', 'Hạng Thượng Hạng Hoàng Gia: không gian cao cấp, chỉnh chu cho kỳ nghỉ riêng tư.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6, 'Available'),

    -- Đồng Tháp
    ('LumiStay Sen Hồng Đồng Tháp', 'An Nhiên DT-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng nhẹ nhàng, gần gũi, phù hợp du lịch miền Tây ngắn ngày.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3, 'Available'),
    ('LumiStay Sen Hồng Đồng Tháp', 'Lụa Trắng DT-201', 'Hạng Cao Cấp An Nhiên: thiết kế thanh lịch, tiện nghi cho cặp đôi hoặc khách công tác.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4, 'Available'),
    ('LumiStay Sen Hồng Đồng Tháp', 'Bình Minh DT-301', 'Hạng Studio Ánh Sáng: không gian sáng, thoáng, phù hợp nghỉ dưỡng nhẹ nhàng.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4, 'Available'),
    ('LumiStay Sen Hồng Đồng Tháp', 'Sông Xanh DT-401', 'Hạng Gia Đình Sum Vầy: rộng rãi, ấm cúng, hợp gia đình và nhóm nhỏ.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5, 'Available'),
    ('LumiStay Sen Hồng Đồng Tháp', 'Hoàng Gia DT-501', 'Hạng Thượng Hạng Hoàng Gia: trải nghiệm cao cấp nhất tại chi nhánh Đồng Tháp, riêng tư và sang trọng.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6, 'Available')
), resolved_rooms AS (
    SELECT b.id AS branch_id,
           sr.room_name,
           sr.description,
           sr.price_per_hour,
           sr.price_per_day,
           sr.extra_guest_fee,
           sr.price_weekend_per_hour,
           sr.price_weekend_per_day,
           sr.price_holiday_per_hour,
           sr.price_holiday_per_day,
           sr.capacity,
           sr.max_guests,
           sr.status
    FROM seed_rooms sr
    JOIN branches b ON b.name = sr.branch_name
), inserted_rooms AS (
    INSERT INTO rooms (
        branch_id,
        name,
        description,
        price_per_hour,
        price_per_day,
        extra_guest_fee,
        price_weekend_per_hour,
        price_weekend_per_day,
        price_holiday_per_hour,
        price_holiday_per_day,
        capacity,
        max_guests,
        status,
        image_url,
        additional_images,
        created_at
    )
    SELECT branch_id,
           room_name,
           description,
           price_per_hour,
           price_per_day,
           extra_guest_fee,
           price_weekend_per_hour,
           price_weekend_per_day,
           price_holiday_per_hour,
           price_holiday_per_day,
           capacity,
           max_guests,
           status,
           NULL,
           NULL,
           NOW()
    FROM resolved_rooms rr
    WHERE NOT EXISTS (
        SELECT 1 FROM rooms r WHERE r.branch_id = rr.branch_id AND r.name = rr.room_name
    )
    RETURNING id
)
UPDATE rooms r
SET description = rr.description,
    price_per_hour = rr.price_per_hour,
    price_per_day = rr.price_per_day,
    extra_guest_fee = rr.extra_guest_fee,
    price_weekend_per_hour = rr.price_weekend_per_hour,
    price_weekend_per_day = rr.price_weekend_per_day,
    price_holiday_per_hour = rr.price_holiday_per_hour,
    price_holiday_per_day = rr.price_holiday_per_day,
    capacity = rr.capacity,
    max_guests = rr.max_guests,
    status = rr.status
FROM resolved_rooms rr
WHERE r.branch_id = rr.branch_id
  AND r.name = rr.room_name;

-- 3) Holidays 2026-2027. These are demo holiday-matrix dates; adjust in Admin UI if official substitute days change.
WITH seed_holidays(date_value, description) AS (
    VALUES
    ('2026-01-01'::date, 'Tết Dương lịch 2026'),
    ('2026-02-16'::date, 'Tết Nguyên Đán 2026'),
    ('2026-02-17'::date, 'Tết Nguyên Đán 2026'),
    ('2026-02-18'::date, 'Tết Nguyên Đán 2026'),
    ('2026-02-19'::date, 'Tết Nguyên Đán 2026'),
    ('2026-02-20'::date, 'Tết Nguyên Đán 2026'),
    ('2026-04-26'::date, 'Giỗ Tổ Hùng Vương 2026'),
    ('2026-04-30'::date, 'Ngày Giải phóng miền Nam 2026'),
    ('2026-05-01'::date, 'Quốc tế Lao động 2026'),
    ('2026-09-02'::date, 'Quốc khánh 2026'),
    ('2026-09-03'::date, 'Kỳ nghỉ Quốc khánh 2026'),
    ('2027-01-01'::date, 'Tết Dương lịch 2027'),
    ('2027-02-05'::date, 'Tết Nguyên Đán 2027'),
    ('2027-02-06'::date, 'Tết Nguyên Đán 2027'),
    ('2027-02-07'::date, 'Tết Nguyên Đán 2027'),
    ('2027-02-08'::date, 'Tết Nguyên Đán 2027'),
    ('2027-02-09'::date, 'Tết Nguyên Đán 2027'),
    ('2027-04-16'::date, 'Giỗ Tổ Hùng Vương 2027'),
    ('2027-04-30'::date, 'Ngày Giải phóng miền Nam 2027'),
    ('2027-05-01'::date, 'Quốc tế Lao động 2027'),
    ('2027-09-02'::date, 'Quốc khánh 2027'),
    ('2027-09-03'::date, 'Kỳ nghỉ Quốc khánh 2027')
), inserted_holidays AS (
    INSERT INTO holidays (date, description)
    SELECT date_value, description
    FROM seed_holidays sh
    WHERE NOT EXISTS (
        SELECT 1 FROM holidays h WHERE h.date::date = sh.date_value
    )
    RETURNING id
)
UPDATE holidays h
SET description = sh.description
FROM seed_holidays sh
WHERE h.date::date = sh.date_value
  AND (h.description IS NULL OR h.description = '' OR h.description LIKE 'Tết%' OR h.description LIKE 'Ngày%' OR h.description LIKE 'Quốc%' OR h.description LIKE 'Giỗ%' OR h.description LIKE 'Kỳ%');

COMMIT;

BEGIN;

-- Core LumiStay normalization seed.
-- Safe to run multiple times: it upserts demo branches/rooms, normalizes room status,
-- ensures combo templates + active assignments, and guarantees branch QR settings.

WITH seed_branches(name, address, description, hotline, email, map_url, booking_lead_time_hours) AS (
    VALUES
    ('LumiStay Sài Gòn Quận 1', '12 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', 'Chi nhánh trung tâm dành cho khách công tác, du lịch và cặp đôi muốn trải nghiệm nhịp sống Sài Gòn sang trọng.', '0901000001', 'q1@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Hue+Quan+1+Ho+Chi+Minh', 2),
    ('LumiStay Riverside Quận 7', '88 Nguyễn Văn Linh, Phường Tân Phong, Quận 7, TP. Hồ Chí Minh', 'Không gian ven sông yên tĩnh, phù hợp nghỉ dưỡng ngắn ngày, làm việc từ xa và gia đình nhỏ.', '0901000007', 'q7@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Van+Linh+Quan+7+Ho+Chi+Minh', 2),
    ('LumiStay Bình Dương City', '25 Đại lộ Bình Dương, Phường Phú Cường, TP. Thủ Dầu Một, Bình Dương', 'Điểm lưu trú tiện nghi cho khách công tác, chuyên gia và khách ghé khu công nghiệp Bình Dương.', '0901000061', 'binhduong@lumistay.vn', 'https://maps.google.com/?q=Dai+lo+Binh+Duong+Thu+Dau+Mot', 2),
    ('LumiStay Biên Hòa Đồng Nai', '39 Võ Thị Sáu, Phường Thống Nhất, TP. Biên Hòa, Đồng Nai', 'Chi nhánh cân bằng giữa công tác và nghỉ ngơi, phù hợp gia đình nhỏ hoặc khách lưu trú ngắn ngày.', '0901000039', 'dongnai@lumistay.vn', 'https://maps.google.com/?q=Vo+Thi+Sau+Bien+Hoa+Dong+Nai', 2),
    ('LumiStay Sen Hồng Đồng Tháp', '66 Nguyễn Huệ, Phường 1, TP. Cao Lãnh, Đồng Tháp', 'Phong cách nghỉ dưỡng nhẹ nhàng miền Tây, gần gũi, tinh tế và riêng tư.', '0901000066', 'dongthap@lumistay.vn', 'https://maps.google.com/?q=Nguyen+Hue+Cao+Lanh+Dong+Thap', 2)
), inserted_branches AS (
    INSERT INTO branches (name, address, description, hotline, email, map_url, booking_lead_time_hours, is_deleted, deleted_at)
    SELECT name, address, description, hotline, email, map_url, booking_lead_time_hours, false, NULL
    FROM seed_branches sb
    WHERE NOT EXISTS (
        SELECT 1
        FROM branches b
        WHERE b.name = sb.name
    )
    RETURNING id
)
UPDATE branches b
SET address = sb.address,
    description = sb.description,
    hotline = sb.hotline,
    email = sb.email,
    map_url = sb.map_url,
    booking_lead_time_hours = sb.booking_lead_time_hours,
    is_deleted = false,
    deleted_at = NULL
FROM seed_branches sb
WHERE b.name = sb.name;

WITH seed_rooms(branch_name, room_name, description, price_per_hour, price_per_day, extra_guest_fee, price_weekend_per_hour, price_weekend_per_day, price_holiday_per_hour, price_holiday_per_day, capacity, max_guests) AS (
    VALUES
    ('LumiStay Sài Gòn Quận 1', 'An Nhiên Q1-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng gọn đẹp, ánh sáng ấm, phù hợp khách công tác hoặc cặp đôi cần không gian riêng tư tại trung tâm.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3),
    ('LumiStay Sài Gòn Quận 1', 'Lụa Trắng Q1-201', 'Hạng Cao Cấp An Nhiên: nội thất tinh tế, giường lớn, góc làm việc nhỏ và tiện nghi đầy đủ cho kỳ nghỉ ngắn ngày.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4),
    ('LumiStay Sài Gòn Quận 1', 'Bình Minh Q1-301', 'Hạng Studio Ánh Sáng: không gian mở, nhiều ánh sáng, phù hợp khách thích ở dài hơn hoặc làm việc từ xa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4),
    ('LumiStay Sài Gòn Quận 1', 'Sông Xanh Q1-401', 'Hạng Gia Đình Sum Vầy: diện tích rộng hơn, bố trí thoải mái cho gia đình nhỏ hoặc nhóm bạn.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5),
    ('LumiStay Sài Gòn Quận 1', 'Hoàng Gia Q1-501', 'Hạng Thượng Hạng Hoàng Gia: trải nghiệm cao cấp nhất với không gian sang trọng, riêng tư và chỉn chu.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6),
    ('LumiStay Riverside Quận 7', 'An Nhiên Q7-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng yên tĩnh, tinh gọn, phù hợp nghỉ ngắn ngày gần khu đô thị Quận 7.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3),
    ('LumiStay Riverside Quận 7', 'Lụa Trắng Q7-201', 'Hạng Cao Cấp An Nhiên: tiện nghi nhẹ nhàng, gam màu thanh lịch, phù hợp cặp đôi và khách công tác.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4),
    ('LumiStay Riverside Quận 7', 'Bình Minh Q7-301', 'Hạng Studio Ánh Sáng: không gian mở sáng sủa, lý tưởng để nghỉ dưỡng ven sông hoặc làm việc từ xa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4),
    ('LumiStay Riverside Quận 7', 'Sông Xanh Q7-401', 'Hạng Gia Đình Sum Vầy: phòng rộng, tiện nghi cho nhóm nhỏ và gia đình cần sự thoải mái.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5),
    ('LumiStay Riverside Quận 7', 'Hoàng Gia Q7-501', 'Hạng Thượng Hạng Hoàng Gia: không gian cao cấp, riêng tư, phù hợp nghỉ dưỡng sang trọng tại Quận 7.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6),
    ('LumiStay Bình Dương City', 'An Nhiên BD-101', 'Hạng Tiêu Chuẩn Thanh Lịch: lựa chọn gọn gàng, tiện nghi cho khách công tác tại Bình Dương.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3),
    ('LumiStay Bình Dương City', 'Lụa Trắng BD-201', 'Hạng Cao Cấp An Nhiên: không gian thư giãn sau ngày làm việc, phù hợp chuyên gia và cặp đôi.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4),
    ('LumiStay Bình Dương City', 'Bình Minh BD-301', 'Hạng Studio Ánh Sáng: bố trí mở, sáng và thoáng, phù hợp lưu trú dài hơn.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4),
    ('LumiStay Bình Dương City', 'Sông Xanh BD-401', 'Hạng Gia Đình Sum Vầy: phòng rộng cho gia đình nhỏ hoặc nhóm khách công tác.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5),
    ('LumiStay Bình Dương City', 'Hoàng Gia BD-501', 'Hạng Thượng Hạng Hoàng Gia: lựa chọn cao cấp cho khách cần sự riêng tư và chỉn chu.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6),
    ('LumiStay Biên Hòa Đồng Nai', 'An Nhiên DN-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng tinh gọn, sạch đẹp, phù hợp nghỉ nhanh hoặc công tác ngắn ngày.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3),
    ('LumiStay Biên Hòa Đồng Nai', 'Lụa Trắng DN-201', 'Hạng Cao Cấp An Nhiên: tiện nghi cân bằng, không gian êm dịu cho khách cần thư giãn.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4),
    ('LumiStay Biên Hòa Đồng Nai', 'Bình Minh DN-301', 'Hạng Studio Ánh Sáng: không gian mở, phù hợp khách ở dài hơn tại Biên Hòa.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4),
    ('LumiStay Biên Hòa Đồng Nai', 'Sông Xanh DN-401', 'Hạng Gia Đình Sum Vầy: phòng rộng, thoải mái cho gia đình hoặc nhóm bạn.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5),
    ('LumiStay Biên Hòa Đồng Nai', 'Hoàng Gia DN-501', 'Hạng Thượng Hạng Hoàng Gia: không gian cao cấp, chỉnh chu cho kỳ nghỉ riêng tư.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6),
    ('LumiStay Sen Hồng Đồng Tháp', 'An Nhiên DT-101', 'Hạng Tiêu Chuẩn Thanh Lịch: phòng nhẹ nhàng, gần gũi, phù hợp du lịch miền Tây ngắn ngày.', 110000, 750000, 80000, 130000, 860000, 150000, 980000, 2, 3),
    ('LumiStay Sen Hồng Đồng Tháp', 'Lụa Trắng DT-201', 'Hạng Cao Cấp An Nhiên: thiết kế thanh lịch, tiện nghi cho cặp đôi hoặc khách công tác.', 150000, 1050000, 100000, 175000, 1200000, 200000, 1380000, 2, 4),
    ('LumiStay Sen Hồng Đồng Tháp', 'Bình Minh DT-301', 'Hạng Studio Ánh Sáng: không gian sáng, thoáng, phù hợp nghỉ dưỡng nhẹ nhàng.', 190000, 1350000, 120000, 220000, 1550000, 250000, 1780000, 2, 4),
    ('LumiStay Sen Hồng Đồng Tháp', 'Sông Xanh DT-401', 'Hạng Gia Đình Sum Vầy: rộng rãi, ấm cúng, hợp gia đình và nhóm nhỏ.', 230000, 1650000, 130000, 265000, 1900000, 300000, 2150000, 4, 5),
    ('LumiStay Sen Hồng Đồng Tháp', 'Hoàng Gia DT-501', 'Hạng Thượng Hạng Hoàng Gia: trải nghiệm cao cấp nhất tại chi nhánh Đồng Tháp, riêng tư và sang trọng.', 300000, 2250000, 150000, 345000, 2600000, 390000, 2950000, 4, 6)
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
           sr.max_guests
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
        created_at,
        is_deleted,
        deleted_at
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
           'Available',
           NULL,
           NULL,
           NOW(),
           false,
           NULL
    FROM resolved_rooms rr
    WHERE NOT EXISTS (
        SELECT 1
        FROM rooms r
        WHERE r.branch_id = rr.branch_id
          AND r.name = rr.room_name
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
    status = 'Available',
    is_deleted = false,
    deleted_at = NULL
FROM resolved_rooms rr
WHERE r.branch_id = rr.branch_id
  AND r.name = rr.room_name;

WITH seed_templates(name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active) AS (
    VALUES
    ('Combo 6 tiếng', 'combo_6h', 360, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 8 tiếng', 'combo_8h', 480, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 12 tiếng', 'combo_12h', 720, 45, '08:00'::time, NULL::time, NULL::time, false, true)
), inserted_templates AS (
    INSERT INTO room_slot_templates (name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active, is_deleted, deleted_at)
    SELECT name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active, false, NULL
    FROM seed_templates st
    WHERE NOT EXISTS (
        SELECT 1
        FROM room_slot_templates rst
        WHERE rst.code = st.code
    )
    RETURNING id
)
UPDATE room_slot_templates rst
SET name = st.name,
    duration_minutes = st.duration_minutes,
    cleanup_minutes = st.cleanup_minutes,
    seed_start_time = st.seed_start_time,
    fixed_start_time = st.fixed_start_time,
    fixed_end_time = st.fixed_end_time,
    crosses_midnight = st.crosses_midnight,
    is_active = st.is_active,
    is_deleted = false,
    deleted_at = NULL
FROM seed_templates st
WHERE rst.code = st.code;

WITH lumi_room_templates AS (
    SELECT r.id AS room_id, rst.id AS template_id
    FROM rooms r
    JOIN branches b ON b.id = r.branch_id
    JOIN room_slot_templates rst ON rst.code IN ('combo_6h', 'combo_8h', 'combo_12h')
    WHERE b.name LIKE 'LumiStay %'
      AND COALESCE(b.is_deleted, false) = false
      AND COALESCE(r.is_deleted, false) = false
), reactivated_assignments AS (
    UPDATE room_slot_template_assignments a
    SET effective_to = NULL,
        is_active = true
    FROM lumi_room_templates lrt
    WHERE a.room_id = lrt.room_id
      AND a.template_id = lrt.template_id
      AND a.effective_to IS NULL
      AND a.is_active = false
    RETURNING a.id
)
INSERT INTO room_slot_template_assignments (room_id, template_id, effective_from, effective_to, is_active)
SELECT lrt.room_id, lrt.template_id, CURRENT_DATE, NULL, true
FROM lumi_room_templates lrt
WHERE NOT EXISTS (
    SELECT 1
    FROM room_slot_template_assignments a
    WHERE a.room_id = lrt.room_id
      AND a.template_id = lrt.template_id
      AND a.effective_to IS NULL
      AND a.is_active = true
);

WITH seed_qr(branch_name, qr_image_path, qr_image_url, bank_code, bank_account_number, bank_account_name, transfer_content_template, countdown_minutes, before_bill_message, after_bill_message, expired_message) AS (
    VALUES
    ('LumiStay Sài Gòn Quận 1', '/uploads/payment-qr/demo-q1.png', 'https://img.vietqr.io/image/MB-0901000001-compact.png?amount=0&addInfo=LumiStay%20Q1', 'MB', '0901000001', 'LumiStay Sài Gòn Quận 1', 'Q1 {BookingId}', '15', 'Vui lòng chuyển khoản đúng nội dung và tải bill để nhân viên đối soát nhanh.', 'Chúng tôi đã nhận bill thanh toán của bạn và sẽ xác nhận sớm nhất.', 'Phiên giữ chỗ đã hết hạn. Vui lòng tạo lại đơn để tiếp tục.'),
    ('LumiStay Riverside Quận 7', '/uploads/payment-qr/demo-q7.png', 'https://img.vietqr.io/image/MB-0901000007-compact.png?amount=0&addInfo=LumiStay%20Q7', 'MB', '0901000007', 'LumiStay Riverside Quận 7', 'Q7 {BookingId}', '15', 'Vui lòng chuyển khoản đúng nội dung và tải bill để nhân viên đối soát nhanh.', 'Chúng tôi đã nhận bill thanh toán của bạn và sẽ xác nhận sớm nhất.', 'Phiên giữ chỗ đã hết hạn. Vui lòng tạo lại đơn để tiếp tục.'),
    ('LumiStay Bình Dương City', '/uploads/payment-qr/demo-bd.png', 'https://img.vietqr.io/image/MB-0901000061-compact.png?amount=0&addInfo=LumiStay%20BD', 'MB', '0901000061', 'LumiStay Bình Dương City', 'BD {BookingId}', '15', 'Vui lòng chuyển khoản đúng nội dung và tải bill để nhân viên đối soát nhanh.', 'Chúng tôi đã nhận bill thanh toán của bạn và sẽ xác nhận sớm nhất.', 'Phiên giữ chỗ đã hết hạn. Vui lòng tạo lại đơn để tiếp tục.'),
    ('LumiStay Biên Hòa Đồng Nai', '/uploads/payment-qr/demo-dn.png', 'https://img.vietqr.io/image/MB-0901000039-compact.png?amount=0&addInfo=LumiStay%20DN', 'MB', '0901000039', 'LumiStay Biên Hòa Đồng Nai', 'DN {BookingId}', '15', 'Vui lòng chuyển khoản đúng nội dung và tải bill để nhân viên đối soát nhanh.', 'Chúng tôi đã nhận bill thanh toán của bạn và sẽ xác nhận sớm nhất.', 'Phiên giữ chỗ đã hết hạn. Vui lòng tạo lại đơn để tiếp tục.'),
    ('LumiStay Sen Hồng Đồng Tháp', '/uploads/payment-qr/demo-dt.png', 'https://img.vietqr.io/image/MB-0901000066-compact.png?amount=0&addInfo=LumiStay%20DT', 'MB', '0901000066', 'LumiStay Sen Hồng Đồng Tháp', 'DT {BookingId}', '15', 'Vui lòng chuyển khoản đúng nội dung và tải bill để nhân viên đối soát nhanh.', 'Chúng tôi đã nhận bill thanh toán của bạn và sẽ xác nhận sớm nhất.', 'Phiên giữ chỗ đã hết hạn. Vui lòng tạo lại đơn để tiếp tục.')
), resolved_qr(branch_id, setting_name, setting_value, description) AS (
    SELECT b.id,
           v.setting_name,
           v.setting_value,
           v.description
    FROM seed_qr sq
    JOIN branches b ON b.name = sq.branch_name
    CROSS JOIN LATERAL (
        VALUES
        ('QrImagePath', sq.qr_image_path, 'Payment QR setting QrImagePath for branch ' || b.id),
        ('QrImageUrl', sq.qr_image_url, 'Payment QR setting QrImageUrl for branch ' || b.id),
        ('BankCode', sq.bank_code, 'Payment QR setting BankCode for branch ' || b.id),
        ('BankAccountNumber', sq.bank_account_number, 'Payment QR setting BankAccountNumber for branch ' || b.id),
        ('BankAccountName', sq.bank_account_name, 'Payment QR setting BankAccountName for branch ' || b.id),
        ('TransferContentTemplate', sq.transfer_content_template, 'Payment QR setting TransferContentTemplate for branch ' || b.id),
        ('CountdownMinutes', sq.countdown_minutes, 'Payment QR setting CountdownMinutes for branch ' || b.id),
        ('BeforeBillMessage', sq.before_bill_message, 'Payment QR setting BeforeBillMessage for branch ' || b.id),
        ('AfterBillMessage', sq.after_bill_message, 'Payment QR setting AfterBillMessage for branch ' || b.id),
        ('ExpiredMessage', sq.expired_message, 'Payment QR setting ExpiredMessage for branch ' || b.id)
    ) AS v(setting_name, setting_value, description)
), inserted_settings AS (
    INSERT INTO system_settings (setting_key, setting_value, description, group_name, last_updated)
    SELECT 'PaymentQr:' || branch_id || ':' || setting_name,
           setting_value,
           description,
           'PaymentQr',
           NOW()
    FROM resolved_qr rq
    WHERE NOT EXISTS (
        SELECT 1
        FROM system_settings s
        WHERE s.setting_key = 'PaymentQr:' || rq.branch_id || ':' || rq.setting_name
    )
    RETURNING id
)
UPDATE system_settings s
SET setting_value = rq.setting_value,
    description = rq.description,
    group_name = 'PaymentQr',
    last_updated = NOW()
FROM resolved_qr rq
WHERE s.setting_key = 'PaymentQr:' || rq.branch_id || ':' || rq.setting_name
  AND (
      COALESCE(s.setting_value, '') IS DISTINCT FROM rq.setting_value
      OR COALESCE(s.description, '') IS DISTINCT FROM rq.description
      OR COALESCE(s.group_name, '') IS DISTINCT FROM 'PaymentQr'
  );

COMMIT;

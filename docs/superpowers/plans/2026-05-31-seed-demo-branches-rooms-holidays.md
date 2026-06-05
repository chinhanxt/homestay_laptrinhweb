# Seed Demo Branches Rooms Holidays Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Seed the current PostgreSQL database once with 5 LumiStay branches, 25 Vietnamese luxury rooms, weekday/weekend/holiday pricing, and Vietnam holiday dates for 2026-2027.

**Architecture:** Use one idempotent SQL script executed against the existing `web_homestay` PostgreSQL database. The script uses natural keys (`branches.name`, `rooms.name + branch_id`, `holidays.date`) to update existing demo rows and insert missing rows without deleting user data.

**Tech Stack:** PostgreSQL SQL, `psql`, ASP.NET Core MVC data model (`branches`, `rooms`, `holidays`).

---

## File Structure

- Create: `scripts/seed-lumistay-demo-data.sql`
  - Self-contained idempotent SQL seed script.
  - Inserts/updates 5 branches, 25 rooms, and 2026-2027 holidays.
  - Does not delete existing records.
- No application code changes.
- No EF migration changes.
- No automated test project changes; validation is done by SQL verification queries against the target local DB.

## Database Connection

Use the existing local connection from `WebHomestay/appsettings.json`:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay
```

---

### Task 1: Create the idempotent seed SQL script

**Files:**
- Create: `scripts/seed-lumistay-demo-data.sql`

- [ ] **Step 1: Create the scripts folder if it does not exist**

Run:

```bash
mkdir -p scripts
```

Expected: command exits with status 0.

- [ ] **Step 2: Create `scripts/seed-lumistay-demo-data.sql` with this exact content**

```sql
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
```

- [ ] **Step 3: Commit the seed script**

Run:

```bash
git add scripts/seed-lumistay-demo-data.sql
git commit -m "chore: add lumistay demo seed script"
```

Expected: commit succeeds and includes only `scripts/seed-lumistay-demo-data.sql`.

---

### Task 2: Execute the seed script against the local PostgreSQL database

**Files:**
- Use: `scripts/seed-lumistay-demo-data.sql`
- Read config: `WebHomestay/appsettings.json`

- [ ] **Step 1: Confirm PostgreSQL is reachable**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT current_database() AS database_name;"
```

Expected output includes:

```text
 database_name
---------------
 web_homestay
```

If this fails with `connection refused`, start PostgreSQL locally before continuing. If this fails with authentication error, verify the password in `WebHomestay/appsettings.json` before continuing.

- [ ] **Step 2: Execute the seed script**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -f scripts/seed-lumistay-demo-data.sql
```

Expected output includes:

```text
BEGIN
UPDATE 5
UPDATE 25
UPDATE 22
COMMIT
```

The exact insert CTE row messages may vary, but the script must end with `COMMIT` and no `ERROR` lines.

---

### Task 3: Verify seeded branch and room data

**Files:**
- Use: database tables `branches`, `rooms`

- [ ] **Step 1: Verify all 5 LumiStay branches exist**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT name, booking_lead_time_hours FROM branches WHERE name LIKE 'LumiStay %' ORDER BY name;"
```

Expected output contains exactly these 5 branch names:

```text
LumiStay Biên Hòa Đồng Nai
LumiStay Bình Dương City
LumiStay Riverside Quận 7
LumiStay Sen Hồng Đồng Tháp
LumiStay Sài Gòn Quận 1
```

Each row should have `booking_lead_time_hours = 2`.

- [ ] **Step 2: Verify each branch has 5 seeded rooms**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT b.name AS branch_name, COUNT(r.id) AS room_count FROM branches b LEFT JOIN rooms r ON r.branch_id = b.id WHERE b.name LIKE 'LumiStay %' GROUP BY b.name ORDER BY b.name;"
```

Expected output has 5 rows and each `room_count` is `5`.

- [ ] **Step 3: Verify total seeded rooms and pricing completeness**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT COUNT(*) AS seeded_rooms, COUNT(*) FILTER (WHERE price_per_hour > 0 AND price_per_day > 0 AND price_weekend_per_hour > 0 AND price_weekend_per_day > 0 AND price_holiday_per_hour > 0 AND price_holiday_per_day > 0) AS rooms_with_complete_pricing FROM rooms r JOIN branches b ON b.id = r.branch_id WHERE b.name LIKE 'LumiStay %';"
```

Expected output:

```text
 seeded_rooms | rooms_with_complete_pricing
--------------+-----------------------------
           25 |                          25
```

- [ ] **Step 4: Verify duplicate room names do not exist inside the same branch**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT b.name AS branch_name, r.name AS room_name, COUNT(*) FROM rooms r JOIN branches b ON b.id = r.branch_id WHERE b.name LIKE 'LumiStay %' GROUP BY b.name, r.name HAVING COUNT(*) > 1;"
```

Expected output has zero data rows.

---

### Task 4: Verify holiday matrix data

**Files:**
- Use: database table `holidays`

- [ ] **Step 1: Verify holiday counts by year**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT EXTRACT(YEAR FROM date)::int AS holiday_year, COUNT(*) AS holiday_count FROM holidays WHERE date >= '2026-01-01' AND date < '2028-01-01' GROUP BY holiday_year ORDER BY holiday_year;"
```

Expected output:

```text
 holiday_year | holiday_count
--------------+---------------
         2026 |            11
         2027 |            11
```

If the table already had extra admin-created dates for 2026-2027, `holiday_count` can be higher. In that case, run Task 4 Step 2 to confirm the seed dates themselves exist.

- [ ] **Step 2: Verify the exact seed holiday dates exist**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "WITH expected(date_value) AS (VALUES ('2026-01-01'::date), ('2026-02-16'::date), ('2026-02-17'::date), ('2026-02-18'::date), ('2026-02-19'::date), ('2026-02-20'::date), ('2026-04-26'::date), ('2026-04-30'::date), ('2026-05-01'::date), ('2026-09-02'::date), ('2026-09-03'::date), ('2027-01-01'::date), ('2027-02-05'::date), ('2027-02-06'::date), ('2027-02-07'::date), ('2027-02-08'::date), ('2027-02-09'::date), ('2027-04-16'::date), ('2027-04-30'::date), ('2027-05-01'::date), ('2027-09-02'::date), ('2027-09-03'::date)) SELECT e.date_value FROM expected e LEFT JOIN holidays h ON h.date::date = e.date_value WHERE h.id IS NULL ORDER BY e.date_value;"
```

Expected output has zero data rows.

- [ ] **Step 3: Verify duplicate holidays do not exist by date**

Run:

```bash
PGPASSWORD=1510 psql -h localhost -p 5432 -U postgres -d web_homestay -c "SELECT date::date, COUNT(*) FROM holidays WHERE date >= '2026-01-01' AND date < '2028-01-01' GROUP BY date::date HAVING COUNT(*) > 1 ORDER BY date::date;"
```

Expected output has zero data rows.

---

### Task 5: Verify application-level behavior with tests

**Files:**
- Use: `WebHomestay.Tests/WebHomestay.Tests.csproj`
- Use: `WebHomestay/Services/PricingService.cs`

- [ ] **Step 1: Run tests related to pricing or domain services**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~Pricing
```

Expected: tests pass if pricing tests exist. If output says no tests matched the filter, continue to Step 2.

- [ ] **Step 2: Run the full test suite**

Run:

```bash
dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj
```

Expected: all tests pass. If failures are unrelated to seed data, report them exactly with the failing test names and error output.

---

### Task 6: Final status report

**Files:**
- Use: `scripts/seed-lumistay-demo-data.sql`
- Use: `docs/superpowers/specs/2026-05-31-seed-demo-branches-rooms-holidays-design.md`
- Use: `docs/superpowers/plans/2026-05-31-seed-demo-branches-rooms-holidays.md`

- [ ] **Step 1: Check git status**

Run:

```bash
git status --short
```

Expected: only pre-existing unrelated changes may remain. The seed script and plan should be committed or intentionally left staged/unstaged according to the implementation worker's commit policy.

- [ ] **Step 2: Report verification summary**

Report these exact facts to the user:

```text
Seed completed:
- 5 LumiStay branches present.
- 25 LumiStay rooms present, 5 per branch.
- All 25 rooms have weekday/weekend/holiday pricing.
- Vietnam holiday matrix dates for 2026-2027 are present.
- Duplicate checks passed for seeded rooms and holiday dates.
- Test result: <paste dotnet test result>.
```

If any command failed, report the failed command, the exact error, and the next recommended action.

---

## Self-Review

- Spec coverage: The plan covers direct DB seeding, 5 branches, 25 rooms, Vietnamese luxury tiers, weekday/weekend/holiday prices, 2026-2027 holiday matrix, no deletes, idempotence, and post-run verification.
- Placeholder scan: No TBD/TODO/fill-later placeholders remain.
- Type consistency: SQL column names match `ApplicationDbContext` mappings for `branches`, `rooms`, and `holidays`.

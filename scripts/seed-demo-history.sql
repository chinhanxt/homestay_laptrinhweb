BEGIN;

DELETE FROM bookings
WHERE customer_note = '[DEMO-SEED:HISTORY-2026-Q2]';

WITH history_days AS (
    SELECT generate_series(DATE '2026-03-15', DATE '2026-06-14', INTERVAL '1 day')::date AS day_value
), lumi_rooms AS (
    SELECT
        r.id AS room_id,
        r.name AS room_name,
        b.id AS branch_id,
        b.name AS branch_name,
        r.capacity,
        r.max_guests,
        r.price_per_hour,
        r.price_per_day,
        r.price_weekend_per_hour,
        r.price_weekend_per_day
    FROM rooms r
    JOIN branches b ON b.id = r.branch_id
    WHERE b.name LIKE 'LumiStay %'
      AND COALESCE(b.is_deleted, false) = false
      AND COALESCE(r.is_deleted, false) = false
), daily_rows AS (
    SELECT
        lr.*,
        hd.day_value,
        ROW_NUMBER() OVER (PARTITION BY lr.room_id ORDER BY hd.day_value) AS seq_in_room
    FROM lumi_rooms lr
    CROSS JOIN history_days hd
    WHERE ((lr.room_id + EXTRACT(DOY FROM hd.day_value)::int) % 4) IN (0, 1)
), hourly_rows AS (
    SELECT
        lr.*,
        hd.day_value,
        ROW_NUMBER() OVER (PARTITION BY lr.room_id ORDER BY hd.day_value) AS seq_in_room
    FROM lumi_rooms lr
    CROSS JOIN history_days hd
    WHERE ((lr.room_id + EXTRACT(DOY FROM hd.day_value)::int) % 5) = 2
)
INSERT INTO bookings (
    room_id,
    customer_name,
    customer_phone,
    customer_email,
    customer_zalo,
    guest_count,
    customer_note,
    admin_note,
    start_time,
    end_time,
    total_price,
    status,
    payment_status,
    payment_proof_url,
    created_at,
    is_deleted,
    deleted_at,
    booking_mode,
    slot_label
)
SELECT
    dr.room_id,
    'Demo lịch sử ngày ' || dr.branch_id || '-' || dr.room_id || '-' || TO_CHAR(dr.day_value, 'DDMM'),
    '08' || LPAD((20000000 + dr.room_id * 50 + EXTRACT(DOY FROM dr.day_value)::int)::text, 8, '0'),
    'history-daily+' || dr.room_id || '-' || TO_CHAR(dr.day_value, 'YYYYMMDD') || '@lumistay.vn',
    '08' || LPAD((20000000 + dr.room_id * 50 + EXTRACT(DOY FROM dr.day_value)::int)::text, 8, '0'),
    LEAST(dr.max_guests, dr.capacity + (dr.seq_in_room % 2)),
    '[DEMO-SEED:HISTORY-2026-Q2]',
    '[DEMO-SEED:HISTORY-2026-Q2] daily ' || dr.branch_name,
    dr.day_value::timestamp + TIME '14:00',
    (dr.day_value + 1)::timestamp + TIME '11:00',
    CASE
        WHEN EXTRACT(ISODOW FROM dr.day_value) IN (6, 7) THEN dr.price_weekend_per_day
        ELSE dr.price_per_day
    END,
    'CheckedOut',
    'Paid',
    NULL,
    dr.day_value::timestamp + TIME '08:00',
    false,
    NULL::timestamp,
    1,
    NULL::varchar(100)
FROM daily_rows dr
;

WITH history_days AS (
    SELECT generate_series(DATE '2026-03-15', DATE '2026-06-14', INTERVAL '1 day')::date AS day_value
), lumi_rooms AS (
    SELECT
        r.id AS room_id,
        r.name AS room_name,
        b.id AS branch_id,
        b.name AS branch_name,
        r.capacity,
        r.max_guests,
        r.price_per_hour,
        r.price_per_day,
        r.price_weekend_per_hour,
        r.price_weekend_per_day
    FROM rooms r
    JOIN branches b ON b.id = r.branch_id
    WHERE b.name LIKE 'LumiStay %'
      AND COALESCE(b.is_deleted, false) = false
      AND COALESCE(r.is_deleted, false) = false
), hourly_rows AS (
    SELECT
        lr.*,
        hd.day_value,
        ROW_NUMBER() OVER (PARTITION BY lr.room_id ORDER BY hd.day_value) AS seq_in_room
    FROM lumi_rooms lr
    CROSS JOIN history_days hd
    WHERE ((lr.room_id + EXTRACT(DOY FROM hd.day_value)::int) % 5) = 2
)
INSERT INTO bookings (
    room_id,
    customer_name,
    customer_phone,
    customer_email,
    customer_zalo,
    guest_count,
    customer_note,
    admin_note,
    start_time,
    end_time,
    total_price,
    status,
    payment_status,
    payment_proof_url,
    created_at,
    is_deleted,
    deleted_at,
    booking_mode,
    slot_label
)
SELECT
    hr.room_id,
    'Demo lịch sử giờ ' || hr.branch_id || '-' || hr.room_id || '-' || TO_CHAR(hr.day_value, 'DDMM'),
    '07' || LPAD((30000000 + hr.room_id * 50 + EXTRACT(DOY FROM hr.day_value)::int)::text, 8, '0'),
    'history-hourly+' || hr.room_id || '-' || TO_CHAR(hr.day_value, 'YYYYMMDD') || '@lumistay.vn',
    '07' || LPAD((30000000 + hr.room_id * 50 + EXTRACT(DOY FROM hr.day_value)::int)::text, 8, '0'),
    LEAST(hr.max_guests, hr.capacity + ((hr.seq_in_room + 1) % 2)),
    '[DEMO-SEED:HISTORY-2026-Q2]',
    '[DEMO-SEED:HISTORY-2026-Q2] hourly ' || hr.branch_name,
    hr.day_value::timestamp + TIME '10:00',
    hr.day_value::timestamp + TIME '18:00',
    ROUND(
        CASE
            WHEN EXTRACT(ISODOW FROM hr.day_value) IN (6, 7) THEN hr.price_weekend_per_hour * 6.8
            ELSE hr.price_per_hour * 6.8
        END,
        2
    ),
    'CheckedOut',
    'Paid',
    NULL,
    hr.day_value::timestamp + TIME '09:00',
    false,
    NULL::timestamp,
    2,
    '10:00-18:00'
FROM hourly_rows hr;

COMMIT;

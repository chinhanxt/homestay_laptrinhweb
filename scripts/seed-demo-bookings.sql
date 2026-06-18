BEGIN;

DELETE FROM booking_cancellation_requests
WHERE policy_message_snapshot LIKE '[DEMO-SEED:CANCEL-2026-06]%'
   OR booking_id IN (
       SELECT id
       FROM bookings
       WHERE customer_note = '[DEMO-SEED:MATRIX-2026-06]'
   );

UPDATE room_slot_inventories i
SET booking_id = NULL,
    status = 'Available'
FROM bookings b
WHERE i.booking_id = b.id
  AND b.customer_note = '[DEMO-SEED:MATRIX-2026-06]';

DELETE FROM bookings
WHERE customer_note = '[DEMO-SEED:MATRIX-2026-06]';

WITH RECURSIVE target_templates AS (
    SELECT
        r.id AS room_id,
        t.id AS template_id,
        t.code,
        t.duration_minutes,
        t.cleanup_minutes,
        t.seed_start_time,
        g.slot_date::date AS slot_date
    FROM rooms r
    JOIN branches b ON b.id = r.branch_id
    JOIN room_slot_templates t
      ON t.code IN ('combo_6h', 'combo_8h', 'combo_12h')
    CROSS JOIN generate_series(DATE '2026-06-15', DATE '2026-06-22', INTERVAL '1 day') AS g(slot_date)
    WHERE b.name LIKE 'LumiStay %'
      AND COALESCE(b.is_deleted, false) = false
      AND COALESCE(r.is_deleted, false) = false
      AND COALESCE(t.is_deleted, false) = false
      AND COALESCE(t.is_active, false) = true
      AND t.seed_start_time IS NOT NULL
), generated_slots AS (
    SELECT
        room_id,
        template_id,
        code,
        slot_date,
        (slot_date::timestamp + seed_start_time) AS start_time,
        (slot_date::timestamp + seed_start_time) + make_interval(mins => duration_minutes) AS end_time,
        duration_minutes,
        cleanup_minutes
    FROM target_templates

    UNION ALL

    SELECT
        gs.room_id,
        gs.template_id,
        gs.code,
        gs.slot_date,
        gs.start_time + make_interval(mins => gs.duration_minutes + gs.cleanup_minutes) AS start_time,
        gs.start_time + make_interval(mins => (gs.duration_minutes + gs.cleanup_minutes) + gs.duration_minutes) AS end_time,
        gs.duration_minutes,
        gs.cleanup_minutes
    FROM generated_slots gs
    WHERE gs.start_time + make_interval(mins => (gs.duration_minutes + gs.cleanup_minutes) + gs.duration_minutes)
        <= gs.slot_date::timestamp + TIME '23:59'
), inserted_slots AS (
    INSERT INTO room_slot_inventories (
        room_id,
        template_id,
        slot_date,
        slot_label,
        start_time,
        end_time,
        status,
        booking_id
    )
    SELECT
        gs.room_id,
        gs.template_id,
        gs.slot_date,
        TO_CHAR(gs.start_time, 'HH24:MI') || '-' || TO_CHAR(gs.end_time, 'HH24:MI'),
        gs.start_time,
        gs.end_time,
        'Available',
        NULL
    FROM generated_slots gs
    WHERE NOT EXISTS (
        SELECT 1
        FROM room_slot_inventories existing
        WHERE existing.room_id = gs.room_id
          AND existing.template_id = gs.template_id
          AND existing.slot_date = gs.slot_date
          AND existing.start_time = gs.start_time
          AND existing.end_time = gs.end_time
    )
    RETURNING id
), candidate_slots AS (
    SELECT
        i.id,
        i.room_id,
        i.template_id,
        t.code,
        i.slot_date,
        i.slot_label,
        i.start_time,
        i.end_time,
        r.name AS room_name,
        b.id AS branch_id,
        b.name AS branch_name,
        r.price_per_hour,
        r.price_per_day,
        r.price_weekend_per_hour,
        r.price_weekend_per_day,
        r.capacity,
        r.max_guests,
        ROW_NUMBER() OVER (PARTITION BY i.room_id, i.slot_date ORDER BY i.start_time) AS slot_order
    FROM room_slot_inventories i
    JOIN rooms r ON r.id = i.room_id
    JOIN branches b ON b.id = r.branch_id
    JOIN room_slot_templates t ON t.id = i.template_id
    WHERE b.name LIKE 'LumiStay %'
      AND i.slot_date BETWEEN DATE '2026-06-15' AND DATE '2026-06-22'
      AND i.status = 'Available'
      AND i.booking_id IS NULL
      AND COALESCE(r.is_deleted, false) = false
      AND COALESCE(b.is_deleted, false) = false
), selected_slots AS (
    SELECT
        cs.*,
        CASE
            WHEN cs.slot_order <= 2 THEN true
            WHEN cs.slot_order = 3 AND ((cs.room_id + EXTRACT(DOY FROM cs.slot_date)::int) % 2 = 0) THEN true
            ELSE false
        END AS should_seed
    FROM candidate_slots cs
), inserted_bookings AS (
    INSERT INTO bookings (
        room_id,
        customer_name,
        customer_phone,
        customer_email,
        customer_zalo,
        guest_count,
        id_card_front_path,
        id_card_back_path,
        "IdCardFrontMaskedPath",
        "IdCardBackMaskedPath",
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
        room_slot_inventory_id,
        slot_label
    )
    SELECT
        ss.room_id,
        'Demo khách ' || ss.branch_id || '-' || ss.room_id || '-' || TO_CHAR(ss.slot_date, 'DDMM') || '-' || ss.slot_order,
        '09' || LPAD((10000000 + ss.room_id * 100 + EXTRACT(DOY FROM ss.slot_date)::int + ss.slot_order)::text, 8, '0'),
        'demo+' || ss.room_id || '-' || TO_CHAR(ss.slot_date, 'YYYYMMDD') || '-' || ss.slot_order || '@lumistay.vn',
        '09' || LPAD((10000000 + ss.room_id * 100 + EXTRACT(DOY FROM ss.slot_date)::int + ss.slot_order)::text, 8, '0'),
        LEAST(ss.max_guests, ss.capacity + ((ss.slot_order + EXTRACT(DAY FROM ss.slot_date)::int) % 2)),
        'demo-cccd-front.png',
        'demo-cccd-back.png',
        '/uploads/masked/idcards/demo-masked-front.png',
        '/uploads/masked/idcards/demo-masked-back.png',
        '[DEMO-SEED:MATRIX-2026-06]',
        '[DEMO-SEED:MATRIX-2026-06] ' || ss.branch_name || ' ' || ss.room_name || ' ' || ss.slot_label,
        ss.start_time,
        ss.end_time,
        CASE
            WHEN ss.code = 'combo_12h' THEN
                CASE
                    WHEN EXTRACT(ISODOW FROM ss.slot_date) IN (6, 7) THEN ss.price_weekend_per_day
                    ELSE ss.price_per_day
                END
            WHEN ss.code = 'combo_8h' THEN
                ROUND(
                    CASE
                        WHEN EXTRACT(ISODOW FROM ss.slot_date) IN (6, 7) THEN ss.price_weekend_per_hour * 6.8
                        ELSE ss.price_per_hour * 6.8
                    END,
                    2
                )
            ELSE
                ROUND(
                    CASE
                        WHEN EXTRACT(ISODOW FROM ss.slot_date) IN (6, 7) THEN ss.price_weekend_per_hour * 5.2
                        ELSE ss.price_per_hour * 5.2
                    END,
                    2
                )
        END,
        CASE
            WHEN ss.slot_date < DATE '2026-06-18' THEN 'CheckedOut'
            WHEN ss.slot_date = DATE '2026-06-18' AND ss.slot_order = 1 THEN 'CheckedIn'
            WHEN ss.slot_date = DATE '2026-06-18' AND ss.slot_order = 2 THEN 'Confirmed'
            WHEN ss.slot_date > DATE '2026-06-18' AND ss.slot_order = 1 THEN 'Confirmed'
            WHEN ss.slot_date > DATE '2026-06-18' AND ss.slot_order = 2 THEN 'AwaitingApproval'
            ELSE 'PendingPayment'
        END,
        CASE
            WHEN ss.slot_date < DATE '2026-06-18' THEN 'Paid'
            WHEN ss.slot_date = DATE '2026-06-18' AND ss.slot_order IN (1, 2) THEN 'Paid'
            WHEN ss.slot_date > DATE '2026-06-18' AND ss.slot_order IN (1, 2) THEN 'Paid'
            ELSE 'Unpaid'
        END,
        CASE
            WHEN ss.slot_date < DATE '2026-06-18' THEN '/uploads/payments/demo-payment-bill.jpg'
            WHEN ss.slot_date = DATE '2026-06-18' AND ss.slot_order IN (1, 2) THEN '/uploads/payments/demo-payment-bill.jpg'
            WHEN ss.slot_date > DATE '2026-06-18' AND ss.slot_order IN (1, 2) THEN '/uploads/payments/demo-payment-bill.jpg'
            ELSE NULL
        END,
        CASE
            WHEN ss.slot_date >= DATE '2026-06-18' THEN NOW()
            ELSE ss.start_time - INTERVAL '2 day'
        END,
        false,
        NULL,
        2,
        ss.id,
        ss.slot_label
    FROM selected_slots ss
    WHERE ss.should_seed = true
    RETURNING id, room_slot_inventory_id, status
)
UPDATE room_slot_inventories i
SET booking_id = b.id,
    status = CASE
        WHEN b.status = 'PendingPayment' THEN 'Reserved'
        ELSE 'Booked'
    END
FROM inserted_bookings b
WHERE i.id = b.room_slot_inventory_id;

COMMIT;

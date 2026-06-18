BEGIN;

DELETE FROM booking_cancellation_requests
WHERE policy_message_snapshot LIKE '[DEMO-SEED:CANCEL-2026-06]%';

WITH branch_bookings AS (
    SELECT DISTINCT ON (r.branch_id)
        b.id AS booking_id,
        r.branch_id,
        br.name AS branch_name,
        b.customer_name,
        b.customer_phone,
        COALESCE(b.customer_email, 'demo-cancel+' || b.id || '@lumistay.vn') AS customer_email
    FROM bookings b
    JOIN rooms r ON r.id = b.room_id
    JOIN branches br ON br.id = r.branch_id
    WHERE b.customer_note = '[DEMO-SEED:MATRIX-2026-06]'
      AND COALESCE(b.is_deleted, false) = false
      AND br.name LIKE 'LumiStay %'
    ORDER BY r.branch_id, b.start_time DESC
)
INSERT INTO booking_cancellation_requests (
    chat_session_id,
    booking_id,
    submitted_booking_code,
    customer_name,
    customer_phone,
    customer_email,
    confirmation_email_proof_path,
    refund_qr_image_path,
    refund_bank_name,
    refund_bank_account_number,
    refund_bank_account_holder,
    status,
    suggested_booking_ids_json,
    policy_notice_hours_snapshot,
    refund_percent_before_notice_snapshot,
    refund_percent_after_notice_snapshot,
    policy_message_snapshot,
    is_manual,
    applied_refund_percent,
    refund_status,
    refund_bill_proof_path,
    staff_reason,
    created_at,
    updated_at
)
SELECT
    'demo-cancel-' || bb.booking_id,
    bb.booking_id,
    'DEMO-BK-' || bb.booking_id,
    'Demo hủy ' || bb.branch_name,
    bb.customer_phone,
    bb.customer_email,
    'demo-confirmation.png',
    'demo-refund-qr.png',
    'MB Bank',
    '09' || LPAD((10000000 + bb.branch_id)::text, 8, '0'),
    bb.branch_name,
    'Pending',
    NULL,
    24,
    80,
    30,
    '[DEMO-SEED:CANCEL-2026-06] Yêu cầu hủy chờ duyệt cho ' || bb.branch_name,
    false,
    NULL,
    'NotRefunded',
    NULL,
    'Demo queue cho admin xử lý.',
    NOW() - (bb.branch_id || ' hours')::interval,
    NOW() - (bb.branch_id || ' hours')::interval
FROM branch_bookings bb;

COMMIT;

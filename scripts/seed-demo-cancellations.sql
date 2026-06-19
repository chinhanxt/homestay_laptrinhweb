BEGIN;

DELETE FROM booking_cancellation_requests
WHERE policy_message_snapshot LIKE '[DEMO-SEED:CANCEL-2026-06]%';

WITH branch_specs(branch_id, branch_name, branch_code) AS (
    VALUES
        (5, 'LumiStay Sài Gòn Quận 1', 'q1'),
        (1, 'LumiStay Riverside Quận 7', 'q7'),
        (2, 'LumiStay Bình Dương City', 'bd'),
        (4, 'LumiStay Biên Hòa Đồng Nai', 'dn'),
        (3, 'LumiStay Sen Hồng Đồng Tháp', 'dt')
),
ranked_branch_bookings AS (
    SELECT
        b.id AS booking_id,
        r.branch_id,
        ROW_NUMBER() OVER (PARTITION BY r.branch_id ORDER BY b.start_time DESC, b.id DESC) AS branch_rank,
        b.customer_name,
        b.customer_phone,
        COALESCE(b.customer_email, 'demo-cancel+' || b.id || '@lumistay.vn') AS customer_email
    FROM bookings b
    JOIN rooms r ON r.id = b.room_id
    JOIN branches br ON br.id = r.branch_id
    WHERE b.customer_note = '[DEMO-SEED:MATRIX-2026-06]'
      AND COALESCE(b.is_deleted, false) = false
      AND br.name LIKE 'LumiStay %'
),
selected_cancellations AS (
    SELECT
        bs.branch_id,
        bs.branch_name,
        bs.branch_code,
        rbb.booking_id,
        rbb.branch_rank,
        rbb.customer_name,
        rbb.customer_phone,
        rbb.customer_email,
        ROW_NUMBER() OVER (ORDER BY bs.branch_id, rbb.branch_rank) AS seq_no
    FROM branch_specs bs
    JOIN ranked_branch_bookings rbb
      ON rbb.branch_id = bs.branch_id
     AND rbb.branch_rank <= CASE WHEN bs.branch_code = 'dt' THEN 1 ELSE 2 END
),
normalized_seed AS (
    SELECT
        sc.*,
        CASE
            WHEN sc.seq_no <= 5 THEN 'Pending'
            WHEN sc.seq_no <= 7 THEN 'Rejected'
            ELSE 'Approved'
        END AS status,
        CASE
            WHEN sc.seq_no <= 5 THEN 'demo-chat-' || sc.branch_code || '-01'
            WHEN sc.seq_no <= 7 THEN 'demo-chat-' || sc.branch_code || '-04'
            ELSE 'demo-chat-' || sc.branch_code || '-03'
        END AS chat_session_id,
        CASE
            WHEN sc.seq_no <= 5 THEN 'Yêu cầu hủy chờ nhân viên kiểm tra lại điều kiện và thời gian báo trước.'
            WHEN sc.seq_no <= 7 THEN 'Yêu cầu bị từ chối vì booking đã sát giờ nhận phòng theo chính sách demo.'
            ELSE 'Yêu cầu đã được chấp nhận, đang chờ cập nhật hoàn tiền/huỷ booking theo quy trình demo.'
        END AS policy_note,
        CASE
            WHEN sc.seq_no <= 5 THEN 'Demo queue cho admin xử lý.'
            WHEN sc.seq_no <= 7 THEN 'Demo từ chối vì khách báo huỷ quá sát giờ nhận phòng.'
            ELSE 'Demo đã chấp nhận yêu cầu hủy và chờ nhân viên xử lý tiếp.'
        END AS staff_reason,
        CASE
            WHEN sc.seq_no <= 5 THEN NULL
            WHEN sc.seq_no <= 7 THEN 'demo.manager'
            ELSE 'demo.staff'
        END AS processed_by,
        CASE
            WHEN sc.seq_no <= 7 THEN 0
            ELSE CASE WHEN sc.branch_code = 'q1' THEN 80 ELSE 50 END
        END AS applied_refund_percent,
        CASE
            WHEN sc.seq_no <= 7 THEN NULL
            ELSE NOW() - (sc.seq_no || ' hours')::interval
        END AS processed_at,
        CASE
            WHEN sc.seq_no <= 7 THEN 'NotRefunded'
            ELSE 'RefundPending'
        END AS refund_status
    FROM selected_cancellations sc
    WHERE sc.seq_no <= 9
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
    processed_by,
    processed_at,
    created_at,
    updated_at
)
SELECT
    ns.chat_session_id,
    ns.booking_id,
    'DEMO-BK-' || ns.booking_id,
    'Demo hủy ' || ns.branch_name || ' #' || ns.branch_rank,
    ns.customer_phone,
    ns.customer_email,
    'demo-confirmation.png',
    'demo-refund-qr.png',
    'MB Bank',
    '09' || LPAD((10000000 + ns.branch_id)::text, 8, '0'),
    ns.branch_name,
    ns.status,
    NULL,
    24,
    80,
    30,
    '[DEMO-SEED:CANCEL-2026-06] ' || ns.policy_note || ' [' || ns.branch_code || ']',
    false,
    CASE WHEN ns.status = 'Pending' THEN NULL ELSE ns.applied_refund_percent END,
    ns.refund_status,
    NULL,
    ns.staff_reason,
    ns.processed_by,
    ns.processed_at,
    NOW() - ((10 - ns.seq_no) || ' hours')::interval,
    NOW() - ((10 - ns.seq_no) || ' hours')::interval
FROM normalized_seed ns;

COMMIT;

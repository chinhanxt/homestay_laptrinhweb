BEGIN;

DELETE FROM admin_chat_messages
WHERE session_id LIKE 'demo-chat-%';

DELETE FROM ai_conversation_traces
WHERE session_id LIKE 'demo-chat-%';

DELETE FROM admin_chat_sessions
WHERE session_id LIKE 'demo-chat-%';

WITH branch_specs(branch_name, branch_code, branch_order) AS (
    VALUES
        ('LumiStay Sài Gòn Quận 1', 'q1', 1),
        ('LumiStay Riverside Quận 7', 'q7', 2),
        ('LumiStay Bình Dương City', 'bd', 3),
        ('LumiStay Biên Hòa Đồng Nai', 'dn', 4),
        ('LumiStay Sen Hồng Đồng Tháp', 'dt', 5)
),
chat_templates(chat_no, customer_name, status, paused_by, pause_reason, taken_over_by, booking_mode, guest_count, topic, opening_line, ai_line_1, follow_up_line, closing_line) AS (
    VALUES
        (1, 'Khách demo hỏi phòng theo giờ', 'auto', NULL, NULL, NULL, 'hourly', 2, 'hourly-search',
            'Chào bạn, tối nay bên mình còn phòng theo giờ không ạ? Mình cần khoảng 2 tiếng.',
            'Dạ còn bạn nhé. Mình sẽ lọc phòng trống theo giờ để gửi đúng lựa chọn phù hợp cho bạn.',
            'Mình ưu tiên phòng yên tĩnh, check-in khoảng 20h và đi 2 người.',
            'Mình đã ghi nhận nhu cầu đặt theo giờ lúc 20h cho 2 khách rồi nhé, bạn chỉ cần chọn phòng phù hợp.'),
        (2, 'Khách demo hỏi theo ngày', 'auto', NULL, NULL, NULL, 'daily', 3, 'daily-search',
            'Mình cần đặt phòng theo ngày, ở 2 đêm cuối tuần cho 3 người, bạn tư vấn giúp mình nhé.',
            'Dạ được bạn nhé. Mình sẽ lọc các phòng theo ngày phù hợp cho 3 khách và thời gian cuối tuần.',
            'Nếu có phòng rộng, sạch và gần thang máy thì ưu tiên giúp mình luôn nha.',
            'Mình đã note rõ nhu cầu ở 2 đêm, 3 khách và ưu tiên phòng rộng thuận tiện di chuyển cho bạn rồi nhé.'),
        (3, 'Khách demo đã chuyển khoản chờ xác nhận', 'paused', 'demo.staff', 'manual_handoff', 'demo.staff', 'hourly', 2, 'payment-followup',
            'Mình đã chuyển khoản rồi, nhờ kiểm tra bill giúp mình để xác nhận booking.',
            'Dạ mình đã nhận thông tin thanh toán của bạn và sẽ chuyển nhân viên đối soát bill ngay.',
            'Nếu được thì báo mình khi booking qua bước duyệt để mình yên tâm nhé.',
            'Nhân viên đang kiểm tra bill chuyển khoản và sẽ phản hồi xác nhận sớm nhất cho bạn.'),
        (4, 'Khách demo yêu cầu gặp nhân viên', 'paused', 'demo.manager', 'manual_handoff', 'demo.manager', 'hourly', 2, 'handoff',
            'Mình muốn gặp nhân viên để hỏi thêm về giờ linh hoạt và thủ tục nhận phòng.',
            'Dạ mình sẽ chuyển nhân viên hỗ trợ trực tiếp để trao đổi kỹ hơn cho bạn ngay bây giờ.',
            'Bạn giúp mình giữ lịch tạm khoảng 2 tiếng trước khi chốt nhé.',
            'Nhân viên đã tiếp quản cuộc trò chuyện này để hỗ trợ bạn trực tiếp tiếp theo rồi nhé.'),
        (5, 'Khách demo hỏi giá và phụ thu cuối tuần', 'auto', NULL, NULL, NULL, 'daily', 4, 'pricing',
            'Cho mình hỏi phòng 4 người thì tổng giá khoảng bao nhiêu, cuối tuần có phụ thu không bạn?',
            'Dạ mình sẽ tính theo số khách và báo rõ phần giá cuối tuần cũng như phụ thu thêm người nếu có.',
            'Mình muốn biết luôn nếu đi 4 người thì phụ thu thêm cụ thể thế nào nhé.',
            'Mình đã giải thích cả giá phòng cơ bản, phụ thu thêm người và phần tăng giá cuối tuần cho bạn rồi nhé.')
),
resolved_sessions AS (
    SELECT
        'demo-chat-' || bs.branch_code || '-' || LPAD(ct.chat_no::text, 2, '0') AS session_id,
        bs.branch_name,
        bs.branch_code,
        bs.branch_order,
        ct.chat_no,
        ct.customer_name || ' - ' || bs.branch_code AS customer_name,
        ct.status,
        ct.paused_by,
        ct.pause_reason,
        ct.taken_over_by,
        ct.booking_mode,
        ct.guest_count,
        ct.topic,
        ct.opening_line,
        ct.ai_line_1,
        ct.follow_up_line,
        ct.closing_line,
        b.id AS branch_id,
        NOW() - (((bs.branch_order - 1) * 5 + ct.chat_no) * INTERVAL '3 hours') AS started_at
    FROM branch_specs bs
    JOIN branches b ON b.name = bs.branch_name
    CROSS JOIN chat_templates ct
),
inserted_sessions AS (
    INSERT INTO admin_chat_sessions (
        id,
        session_id,
        customer_name,
        branch_id,
        has_prompted_for_branch,
        status,
        paused_by,
        paused_at,
        "PauseReason",
        "TakenOverBy",
        "TakenOverAt",
        auto_reply_message,
        created_at,
        last_activity_at,
        is_deleted,
        deleted_at,
        "DeletedBy"
    )
    SELECT
        gen_random_uuid(),
        rs.session_id,
        rs.customer_name,
        rs.branch_id,
        true,
        rs.status,
        rs.paused_by,
        CASE WHEN rs.status = 'paused' THEN rs.started_at + INTERVAL '18 minutes' ELSE NULL END,
        rs.pause_reason,
        rs.taken_over_by,
        CASE WHEN rs.status = 'paused' THEN rs.started_at + INTERVAL '18 minutes' ELSE NULL END,
        'Nhân viên đang xem và sẽ phản hồi sớm nhất cho bạn.',
        rs.started_at,
        rs.started_at + INTERVAL '24 minutes',
        false,
        NULL,
        NULL
    FROM resolved_sessions rs
    RETURNING session_id
),
seed_messages AS (
    SELECT
        rs.session_id,
        'user'::text AS role,
        rs.opening_line AS content,
        rs.customer_name AS created_by,
        rs.started_at AS created_at,
        true AS is_read,
        1 AS sort_order
    FROM resolved_sessions rs
    UNION ALL
    SELECT
        rs.session_id,
        'ai',
        rs.ai_line_1,
        'AI',
        rs.started_at + INTERVAL '4 minutes',
        true,
        2
    FROM resolved_sessions rs
    UNION ALL
    SELECT
        rs.session_id,
        'user',
        rs.follow_up_line,
        rs.customer_name,
        rs.started_at + INTERVAL '11 minutes',
        CASE WHEN rs.status = 'paused' THEN false ELSE true END,
        3
    FROM resolved_sessions rs
    UNION ALL
    SELECT
        rs.session_id,
        CASE WHEN rs.status = 'paused' THEN 'admin' ELSE 'ai' END,
        rs.closing_line,
        CASE WHEN rs.status = 'paused' THEN COALESCE(rs.taken_over_by, rs.paused_by, 'demo.staff') ELSE 'AI' END,
        rs.started_at + INTERVAL '24 minutes',
        true,
        4
    FROM resolved_sessions rs
),
inserted_messages AS (
    INSERT INTO admin_chat_messages (
        id,
        session_id,
        role,
        content,
        created_by,
        created_at,
        is_read
    )
    SELECT
        gen_random_uuid(),
        sm.session_id,
        sm.role,
        sm.content,
        sm.created_by,
        sm.created_at,
        sm.is_read
    FROM seed_messages sm
    ORDER BY sm.session_id, sm.sort_order
    RETURNING session_id
)
INSERT INTO ai_conversation_traces (
    id,
    session_id,
    customer_message,
    persona_summary,
    live_system_snapshot,
    retrieved_knowledge_json,
    graph_reasoning_json,
    guard_result,
    final_answer,
    model_provider,
    created_at
)
SELECT
    gen_random_uuid(),
    rs.session_id,
    rs.opening_line || E'\n' || rs.follow_up_line,
    'Demo persona for ' || rs.branch_code,
    'Demo branch snapshot for ' || rs.branch_name,
    '[]',
    '[]',
    'safe-demo',
    rs.ai_line_1 || E'\n' || rs.closing_line,
    'demo-seed',
    rs.started_at + INTERVAL '24 minutes'
FROM resolved_sessions rs;

COMMIT;

BEGIN;

-- 1) Role permission templates for Manager and Staff.
WITH role_templates(role, permissions_json) AS (
    VALUES
    ('Manager', '{"matrix.view":true,"bookings.view":true,"bookings.detail":true,"bookings.create":true,"bookings.edit":true,"branches.view":true,"branches.detail":true,"rooms.view":true,"rooms.detail":true,"rooms.create":true,"rooms.edit":true,"images.view":true,"images.detail":true,"statistics.view":true,"statistics.export":true,"staff.view":true,"staff.create":true,"staff.edit":true,"staff.permissions":true,"staff.logs":true,"settings.view":true,"holidays.manage":true,"slots.manage":true,"branch.settings":true,"payment.settings":true,"ai.view":true,"ai.detail":true,"ai.knowledge":true,"ai.graph":true,"ai.trace":true,"chat.view":true,"chat.reply":true}'),
    ('Staff', '{"matrix.view":true,"bookings.view":true,"bookings.detail":true,"bookings.edit":true,"branches.view":true,"branches.detail":true,"rooms.view":true,"rooms.detail":true,"images.view":true,"images.detail":true,"staff.view":true,"chat.view":true,"chat.reply":true}')
), inserted AS (
    INSERT INTO role_permission_templates (role, permissions_json, updated_at)
    SELECT role, permissions_json, NOW()
    FROM role_templates rt
    WHERE NOT EXISTS (SELECT 1 FROM role_permission_templates rpt WHERE rpt.role = rt.role)
)
UPDATE role_permission_templates rpt
SET permissions_json = rt.permissions_json,
    updated_at = NOW()
FROM role_templates rt
WHERE rpt.role = rt.role;

-- 2) Staff accounts. Password is plain '1' by request; login code accepts plain password match.
WITH manager_perms AS (
    SELECT permissions_json FROM role_permission_templates WHERE role = 'Manager'
), staff_perms AS (
    SELECT permissions_json FROM role_permission_templates WHERE role = 'Staff'
), seed_users(branch_name, username, full_name, role_value, permissions_json) AS (
    VALUES
    ('LumiStay Sài Gòn Quận 1', 'q1.manager', 'Quản lý LumiStay Sài Gòn Quận 1', 1, (SELECT permissions_json FROM manager_perms)),
    ('LumiStay Sài Gòn Quận 1', 'q1.staff01', 'Nhân viên Q1 01', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Sài Gòn Quận 1', 'q1.staff02', 'Nhân viên Q1 02', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Riverside Quận 7', 'q7.manager', 'Quản lý LumiStay Riverside Quận 7', 1, (SELECT permissions_json FROM manager_perms)),
    ('LumiStay Riverside Quận 7', 'q7.staff01', 'Nhân viên Q7 01', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Riverside Quận 7', 'q7.staff02', 'Nhân viên Q7 02', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Bình Dương City', 'bd.manager', 'Quản lý LumiStay Bình Dương City', 1, (SELECT permissions_json FROM manager_perms)),
    ('LumiStay Bình Dương City', 'bd.staff01', 'Nhân viên Bình Dương 01', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Bình Dương City', 'bd.staff02', 'Nhân viên Bình Dương 02', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Biên Hòa Đồng Nai', 'dn.manager', 'Quản lý LumiStay Biên Hòa Đồng Nai', 1, (SELECT permissions_json FROM manager_perms)),
    ('LumiStay Biên Hòa Đồng Nai', 'dn.staff01', 'Nhân viên Đồng Nai 01', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Biên Hòa Đồng Nai', 'dn.staff02', 'Nhân viên Đồng Nai 02', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Sen Hồng Đồng Tháp', 'dt.manager', 'Quản lý LumiStay Sen Hồng Đồng Tháp', 1, (SELECT permissions_json FROM manager_perms)),
    ('LumiStay Sen Hồng Đồng Tháp', 'dt.staff01', 'Nhân viên Đồng Tháp 01', 2, (SELECT permissions_json FROM staff_perms)),
    ('LumiStay Sen Hồng Đồng Tháp', 'dt.staff02', 'Nhân viên Đồng Tháp 02', 2, (SELECT permissions_json FROM staff_perms))
), resolved_users AS (
    SELECT b.id AS branch_id, su.username, su.full_name, su.role_value, su.permissions_json
    FROM seed_users su
    JOIN branches b ON b.name = su.branch_name
), inserted_users AS (
    INSERT INTO admin_users ("Username", "PasswordHash", "FullName", "Role", "BranchId", "PermissionsJson", "CreatedAt")
    SELECT username, '1', full_name, role_value, branch_id, permissions_json, NOW()
    FROM resolved_users ru
    WHERE NOT EXISTS (SELECT 1 FROM admin_users au WHERE au."Username" = ru.username)
)
UPDATE admin_users au
SET "PasswordHash" = '1',
    "FullName" = ru.full_name,
    "Role" = ru.role_value,
    "BranchId" = ru.branch_id,
    "PermissionsJson" = ru.permissions_json
FROM resolved_users ru
WHERE au."Username" = ru.username;

-- 3) Slot templates for 6h, 8h, 12h combos.
WITH seed_templates(name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active) AS (
    VALUES
    ('Combo 6 tiếng', 'combo_6h', 360, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 8 tiếng', 'combo_8h', 480, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 12 tiếng', 'combo_12h', 720, 45, '08:00'::time, NULL::time, NULL::time, false, true)
), inserted_templates AS (
    INSERT INTO room_slot_templates (name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active)
    SELECT name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active
    FROM seed_templates st
    WHERE NOT EXISTS (SELECT 1 FROM room_slot_templates rst WHERE rst.code = st.code)
)
UPDATE room_slot_templates rst
SET name = st.name,
    duration_minutes = st.duration_minutes,
    cleanup_minutes = st.cleanup_minutes,
    seed_start_time = st.seed_start_time,
    fixed_start_time = st.fixed_start_time,
    fixed_end_time = st.fixed_end_time,
    crosses_midnight = st.crosses_midnight,
    is_active = st.is_active
FROM seed_templates st
WHERE rst.code = st.code;

-- Assign the three combo templates to all LumiStay rooms.
INSERT INTO room_slot_template_assignments (room_id, template_id, effective_from, effective_to, is_active)
SELECT r.id, rst.id, CURRENT_DATE, NULL, true
FROM rooms r
JOIN branches b ON b.id = r.branch_id
JOIN room_slot_templates rst ON rst.code IN ('combo_6h', 'combo_8h', 'combo_12h')
WHERE b.name LIKE 'LumiStay %'
  AND NOT EXISTS (
      SELECT 1 FROM room_slot_template_assignments a
      WHERE a.room_id = r.id AND a.template_id = rst.id AND a.effective_to IS NULL
  );

-- 4) AI Brain scopes and knowledge units.
WITH seed_scopes(name, description, order_value) AS (
    VALUES
    ('Tổng quan LumiStay', 'Mô hình self check-in/self check-out và nguyên tắc AI tư vấn của LumiStay.', 10),
    ('Chi nhánh & khu vực', 'Thông tin 5 chi nhánh LumiStay và định vị từng khu vực.', 20),
    ('Hạng phòng & trải nghiệm', 'Các hạng phòng tiếng Việt sang trọng và nhu cầu phù hợp.', 30),
    ('Giá & ngày đặc biệt', 'Nguyên tắc giá ngày thường, T7/CN, ngày lễ và live snapshot.', 40),
    ('Quy trình đặt phòng', 'Các bước đặt phòng chính thức và vai trò hỗ trợ của AI.', 50),
    ('Chính sách vận hành', 'Check-in, check-out, hủy đơn, hoàn tiền và các giới hạn AI.', 60)
), inserted_scopes AS (
    INSERT INTO ai_brain_scopes (id, name, description, is_active, "order", created_at)
    SELECT gen_random_uuid(), name, description, true, order_value, NOW()
    FROM seed_scopes ss
    WHERE NOT EXISTS (SELECT 1 FROM ai_brain_scopes s WHERE s.name = ss.name)
)
UPDATE ai_brain_scopes s
SET description = ss.description,
    is_active = true,
    "order" = ss.order_value
FROM seed_scopes ss
WHERE s.name = ss.name;

WITH seed_units(scope_name, title, content, tags, priority) AS (
    VALUES
    ('Tổng quan LumiStay', 'Vai trò AI Assistant LumiStay', 'AI Assistant dùng để tư vấn phòng theo nhu cầu, giải thích chính sách và hướng khách vào luồng đặt phòng chính thức. AI không tự xác nhận booking, không tạo mã khóa, không bịa giá hoặc tình trạng phòng.', 'ai,guard,overview,self-check-in', 100),
    ('Tổng quan LumiStay', 'Mô hình self check-in/self check-out', 'LumiStay vận hành theo mô hình khách tự đặt phòng, thanh toán, nhận hướng dẫn và tự check-in/check-out trên web. Nhân sự hỗ trợ khi khách gặp vấn đề hoặc cần xác minh.', 'self-check-in,operations', 90),
    ('Chi nhánh & khu vực', 'Định vị 5 chi nhánh LumiStay', 'Hệ thống hiện có 5 chi nhánh: Sài Gòn Quận 1 cho khách trung tâm, Riverside Quận 7 cho nghỉ dưỡng yên tĩnh, Bình Dương City cho khách công tác, Biên Hòa Đồng Nai cho gia đình/công tác ngắn ngày, Sen Hồng Đồng Tháp cho du lịch miền Tây.', 'branches,location,lumistay', 100),
    ('Hạng phòng & trải nghiệm', 'Năm hạng phòng LumiStay', 'Các hạng phòng gồm Tiêu Chuẩn Thanh Lịch, Cao Cấp An Nhiên, Studio Ánh Sáng, Gia Đình Sum Vầy và Thượng Hạng Hoàng Gia. AI nên gợi ý theo số khách, ngân sách, nhu cầu riêng tư, công tác, gia đình hoặc nghỉ dưỡng.', 'room-tier,recommendation', 100),
    ('Giá & ngày đặc biệt', 'Nguyên tắc giá động', 'Giá phòng có ba nhóm: ngày thường, Thứ 7/CN và ngày lễ. Ngày lễ ưu tiên cao hơn cuối tuần. AI phải dựa vào live snapshot hoặc dữ liệu hệ thống khi nói giá, không tự suy đoán.', 'pricing,holiday,weekend,live-snapshot', 100),
    ('Quy trình đặt phòng', 'Luồng đặt phòng chính thức', 'Khách cần chọn chi nhánh, phòng, ngày giờ, số khách và hoàn tất thông tin theo form web. AI chỉ hướng dẫn và tư vấn; mọi xác nhận booking phải đi qua hệ thống booking chính thức.', 'booking-flow,official-flow', 100),
    ('Chính sách vận hành', 'Giới hạn khi tư vấn chính sách', 'AI có thể giải thích check-in/check-out, hủy đơn và hoàn tiền theo cấu hình hiện có. AI không được hứa hoàn tiền, không xác nhận hủy đơn và không cấp mã khóa thay nhân sự.', 'policy,cancellation,guard', 100)
), resolved_units AS (
    SELECT s.id AS scope_id, su.title, su.content, su.tags, su.priority
    FROM seed_units su
    JOIN ai_brain_scopes s ON s.name = su.scope_name
), inserted_units AS (
    INSERT INTO ai_knowledge_units (id, scope_id, title, content, tags, priority, is_active, last_updated)
    SELECT gen_random_uuid(), scope_id, title, content, tags, priority, true, NOW()
    FROM resolved_units ru
    WHERE NOT EXISTS (SELECT 1 FROM ai_knowledge_units u WHERE u.title = ru.title)
)
UPDATE ai_knowledge_units u
SET scope_id = ru.scope_id,
    content = ru.content,
    tags = ru.tags,
    priority = ru.priority,
    is_active = true,
    last_updated = NOW()
FROM resolved_units ru
WHERE u.title = ru.title;

-- 5) AI graph nodes and edges.
WITH seed_nodes(node_type, label, summary, metadata_json) AS (
    VALUES
    ('Brand', 'LumiStay', 'Thương hiệu homestay self check-in/self check-out với AI Assistant hỗ trợ tư vấn.', '{"seed":"lumistay"}'),
    ('Branch', 'LumiStay Sài Gòn Quận 1', 'Chi nhánh trung tâm phù hợp du lịch, công tác và cặp đôi.', '{"area":"HCM Q1"}'),
    ('Branch', 'LumiStay Riverside Quận 7', 'Chi nhánh yên tĩnh ven sông phù hợp nghỉ dưỡng ngắn ngày.', '{"area":"HCM Q7"}'),
    ('Branch', 'LumiStay Bình Dương City', 'Chi nhánh phù hợp khách công tác và chuyên gia khu công nghiệp.', '{"area":"Binh Duong"}'),
    ('Branch', 'LumiStay Biên Hòa Đồng Nai', 'Chi nhánh phù hợp gia đình nhỏ và công tác ngắn ngày.', '{"area":"Dong Nai"}'),
    ('Branch', 'LumiStay Sen Hồng Đồng Tháp', 'Chi nhánh phong cách miền Tây, nghỉ dưỡng nhẹ nhàng.', '{"area":"Dong Thap"}'),
    ('RoomTier', 'Tiêu Chuẩn Thanh Lịch', 'Phòng gọn đẹp, giá dễ tiếp cận cho 2-3 khách.', '{"capacity":"2-3"}'),
    ('RoomTier', 'Cao Cấp An Nhiên', 'Phòng tiện nghi hơn cho cặp đôi hoặc khách công tác.', '{"capacity":"2-4"}'),
    ('RoomTier', 'Studio Ánh Sáng', 'Không gian mở, nhiều ánh sáng, phù hợp lưu trú dài hơn.', '{"capacity":"2-4"}'),
    ('RoomTier', 'Gia Đình Sum Vầy', 'Phòng rộng cho gia đình hoặc nhóm nhỏ.', '{"capacity":"4-5"}'),
    ('RoomTier', 'Thượng Hạng Hoàng Gia', 'Trải nghiệm cao cấp, riêng tư và sang trọng.', '{"capacity":"4-6"}'),
    ('Audience', 'Khách công tác', 'Khách cần vị trí thuận tiện, wifi, yên tĩnh và thao tác nhanh.', '{}'),
    ('Audience', 'Cặp đôi', 'Khách ưu tiên riêng tư, thẩm mỹ và trải nghiệm nhẹ nhàng.', '{}'),
    ('Audience', 'Gia đình nhỏ', 'Khách cần sức chứa rộng hơn, tiện nghi và dễ check-in.', '{}'),
    ('Policy', 'Giá theo ngày đặc biệt', 'Giá ưu tiên theo ngày lễ, sau đó cuối tuần, sau cùng ngày thường.', '{}'),
    ('Policy', 'Guard không tự xác nhận booking', 'AI không xác nhận booking, không tạo mã khóa và không bịa tình trạng phòng.', '{}')
), inserted_nodes AS (
    INSERT INTO ai_graph_nodes (id, node_type, label, summary, metadata_json, is_active)
    SELECT gen_random_uuid(), node_type, label, summary, metadata_json, true
    FROM seed_nodes sn
    WHERE NOT EXISTS (SELECT 1 FROM ai_graph_nodes n WHERE n.label = sn.label)
)
UPDATE ai_graph_nodes n
SET node_type = sn.node_type,
    summary = sn.summary,
    metadata_json = sn.metadata_json,
    is_active = true
FROM seed_nodes sn
WHERE n.label = sn.label;

WITH seed_edges(from_label, to_label, relationship_type, weight, evidence) AS (
    VALUES
    ('LumiStay', 'LumiStay Sài Gòn Quận 1', 'HAS_BRANCH', 1.00, 'LumiStay có chi nhánh trung tâm tại Quận 1.'),
    ('LumiStay', 'LumiStay Riverside Quận 7', 'HAS_BRANCH', 1.00, 'LumiStay có chi nhánh Riverside tại Quận 7.'),
    ('LumiStay', 'LumiStay Bình Dương City', 'HAS_BRANCH', 1.00, 'LumiStay có chi nhánh tại Bình Dương.'),
    ('LumiStay', 'LumiStay Biên Hòa Đồng Nai', 'HAS_BRANCH', 1.00, 'LumiStay có chi nhánh tại Biên Hòa Đồng Nai.'),
    ('LumiStay', 'LumiStay Sen Hồng Đồng Tháp', 'HAS_BRANCH', 1.00, 'LumiStay có chi nhánh tại Đồng Tháp.'),
    ('LumiStay Sài Gòn Quận 1', 'Khách công tác', 'GOOD_FOR', 0.90, 'Vị trí trung tâm phù hợp lịch trình công tác.'),
    ('LumiStay Sài Gòn Quận 1', 'Cặp đôi', 'GOOD_FOR', 0.80, 'Trung tâm thuận tiện vui chơi và nghỉ ngắn ngày.'),
    ('LumiStay Riverside Quận 7', 'Cặp đôi', 'GOOD_FOR', 0.85, 'Không gian yên tĩnh và riêng tư.'),
    ('LumiStay Bình Dương City', 'Khách công tác', 'GOOD_FOR', 0.95, 'Gần khu vực công tác và chuyên gia.'),
    ('LumiStay Biên Hòa Đồng Nai', 'Gia đình nhỏ', 'GOOD_FOR', 0.85, 'Phù hợp gia đình hoặc nhóm nhỏ.'),
    ('LumiStay Sen Hồng Đồng Tháp', 'Gia đình nhỏ', 'GOOD_FOR', 0.80, 'Phong cách nghỉ dưỡng miền Tây nhẹ nhàng.'),
    ('Studio Ánh Sáng', 'Khách công tác', 'GOOD_FOR', 0.75, 'Không gian mở phù hợp làm việc từ xa.'),
    ('Gia Đình Sum Vầy', 'Gia đình nhỏ', 'GOOD_FOR', 0.95, 'Sức chứa và bố trí phù hợp gia đình.'),
    ('Thượng Hạng Hoàng Gia', 'Cặp đôi', 'GOOD_FOR', 0.80, 'Trải nghiệm cao cấp và riêng tư.'),
    ('Giá theo ngày đặc biệt', 'Guard không tự xác nhận booking', 'REQUIRES_LIVE_DATA', 1.00, 'AI phải dùng live snapshot và không tự bịa giá.'),
    ('Guard không tự xác nhận booking', 'LumiStay', 'PROTECTS', 1.00, 'Guard bảo vệ quy trình booking chính thức của LumiStay.')
), resolved_edges AS (
    SELECT f.id AS from_id, t.id AS to_id, se.relationship_type, se.weight, se.evidence
    FROM seed_edges se
    JOIN ai_graph_nodes f ON f.label = se.from_label
    JOIN ai_graph_nodes t ON t.label = se.to_label
)
INSERT INTO ai_graph_edges (id, from_node_id, to_node_id, relationship_type, weight, evidence)
SELECT gen_random_uuid(), from_id, to_id, relationship_type, weight, evidence
FROM resolved_edges re
WHERE NOT EXISTS (
    SELECT 1 FROM ai_graph_edges e
    WHERE e.from_node_id = re.from_id
      AND e.to_node_id = re.to_id
      AND e.relationship_type = re.relationship_type
);

COMMIT;

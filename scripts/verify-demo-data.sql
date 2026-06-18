\echo 'Demo verification start'

DROP TABLE IF EXISTS demo_verification_summary;

CREATE TEMP TABLE demo_verification_summary (
    demo_booking_count bigint NOT NULL,
    pending_demo_cancellation_count bigint NOT NULL,
    branch_qr_count bigint NOT NULL,
    lumistay_branch_qr_count bigint NOT NULL,
    lumistay_branch_count bigint NOT NULL,
    lumistay_room_count bigint NOT NULL,
    lumistay_available_room_count bigint NOT NULL,
    combo_template_count bigint NOT NULL,
    lumistay_active_combo_assignment_count bigint NOT NULL
);

DO $$
DECLARE
    demo_booking_count bigint := 0;
    pending_demo_cancellation_count bigint := 0;
    branch_qr_count bigint := 0;
    lumistay_branch_qr_count bigint := 0;
    lumistay_branch_count bigint := 0;
    lumistay_room_count bigint := 0;
    lumistay_available_room_count bigint := 0;
    combo_template_count bigint := 0;
    lumistay_active_combo_assignment_count bigint := 0;
    has_bookings_table boolean := to_regclass('public.bookings') IS NOT NULL;
    has_cancellations_table boolean := to_regclass('public.booking_cancellation_requests') IS NOT NULL;
    has_system_settings_table boolean := to_regclass('public.system_settings') IS NOT NULL;
    has_branches_table boolean := to_regclass('public.branches') IS NOT NULL;
    has_rooms_table boolean := to_regclass('public.rooms') IS NOT NULL;
    has_templates_table boolean := to_regclass('public.room_slot_templates') IS NOT NULL;
    has_assignments_table boolean := to_regclass('public.room_slot_template_assignments') IS NOT NULL;
    has_bookings_id boolean := false;
    has_bookings_customer_note boolean := false;
    has_bookings_admin_note boolean := false;
    has_cancellations_status boolean := false;
    has_cancellations_customer_name boolean := false;
    has_cancellations_policy_message boolean := false;
    has_cancellations_submitted_code boolean := false;
    has_cancellations_booking_id boolean := false;
    has_system_settings_key boolean := false;
    has_system_settings_value boolean := false;
    has_branches_name boolean := false;
    has_branches_is_deleted boolean := false;
    has_rooms_branch_id boolean := false;
    has_rooms_status boolean := false;
    has_rooms_is_deleted boolean := false;
    has_templates_code boolean := false;
    has_templates_is_active boolean := false;
    has_templates_is_deleted boolean := false;
    has_assignments_room_id boolean := false;
    has_assignments_template_id boolean := false;
    has_assignments_effective_to boolean := false;
    has_assignments_is_active boolean := false;
    can_query_demo_bookings boolean := false;
    can_query_linked_demo_bookings boolean := false;
    can_query_demo_cancellations boolean := false;
    can_query_branch_qr boolean := false;
    can_query_lumistay_branches boolean := false;
    can_query_lumistay_rooms boolean := false;
    can_query_combo_templates boolean := false;
    can_query_active_combo_assignments boolean := false;
    demo_bookings_where text := '';
    linked_demo_bookings_where text := '';
    cancellation_marker_where text := '';
    cancellation_sql text := '';
    has_any_cancellation_marker boolean := false;
BEGIN
    IF has_bookings_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'bookings'
                     AND column_name = 'id'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'bookings'
                     AND column_name = 'customer_note'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'bookings'
                     AND column_name = 'admin_note'
               )
        INTO has_bookings_id,
             has_bookings_customer_note,
             has_bookings_admin_note;
    END IF;

    IF has_cancellations_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'booking_cancellation_requests'
                     AND column_name = 'status'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'booking_cancellation_requests'
                     AND column_name = 'customer_name'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'booking_cancellation_requests'
                     AND column_name = 'policy_message_snapshot'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'booking_cancellation_requests'
                     AND column_name = 'submitted_booking_code'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'booking_cancellation_requests'
                     AND column_name = 'booking_id'
               )
        INTO has_cancellations_status,
             has_cancellations_customer_name,
             has_cancellations_policy_message,
             has_cancellations_submitted_code,
             has_cancellations_booking_id;
    END IF;

    IF has_system_settings_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'system_settings'
                     AND column_name = 'setting_key'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'system_settings'
                     AND column_name = 'setting_value'
               )
        INTO has_system_settings_key,
             has_system_settings_value;
    END IF;

    IF has_branches_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'branches'
                     AND column_name = 'name'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'branches'
                     AND column_name = 'is_deleted'
               )
        INTO has_branches_name,
             has_branches_is_deleted;
    END IF;

    IF has_rooms_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'rooms'
                     AND column_name = 'branch_id'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'rooms'
                     AND column_name = 'status'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'rooms'
                     AND column_name = 'is_deleted'
               )
        INTO has_rooms_branch_id,
             has_rooms_status,
             has_rooms_is_deleted;
    END IF;

    IF has_templates_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_templates'
                     AND column_name = 'code'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_templates'
                     AND column_name = 'is_active'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_templates'
                     AND column_name = 'is_deleted'
               )
        INTO has_templates_code,
             has_templates_is_active,
             has_templates_is_deleted;
    END IF;

    IF has_assignments_table THEN
        SELECT EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_template_assignments'
                     AND column_name = 'room_id'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_template_assignments'
                     AND column_name = 'template_id'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_template_assignments'
                     AND column_name = 'effective_to'
               ),
               EXISTS (
                   SELECT 1
                   FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND table_name = 'room_slot_template_assignments'
                     AND column_name = 'is_active'
               )
        INTO has_assignments_room_id,
             has_assignments_template_id,
             has_assignments_effective_to,
             has_assignments_is_active;
    END IF;

    IF has_bookings_customer_note THEN
        demo_bookings_where := demo_bookings_where
            || CASE WHEN demo_bookings_where <> '' THEN ' OR ' ELSE '' END
            || 'COALESCE(b.customer_note, '''') LIKE ''[DEMO-SEED:%''';
    END IF;

    IF has_bookings_admin_note THEN
        demo_bookings_where := demo_bookings_where
            || CASE WHEN demo_bookings_where <> '' THEN ' OR ' ELSE '' END
            || 'COALESCE(b.admin_note, '''') LIKE ''[DEMO-SEED:%''';
    END IF;

    can_query_demo_bookings := has_bookings_table
        AND demo_bookings_where <> '';

    can_query_linked_demo_bookings := has_bookings_table
        AND has_bookings_id
        AND has_cancellations_booking_id
        AND demo_bookings_where <> '';

    can_query_branch_qr := has_system_settings_table
        AND has_system_settings_key
        AND has_system_settings_value;

    can_query_lumistay_branches := has_branches_table
        AND has_branches_name;

    can_query_lumistay_rooms := can_query_lumistay_branches
        AND has_rooms_table
        AND has_rooms_branch_id
        AND has_rooms_status;

    can_query_combo_templates := has_templates_table
        AND has_templates_code
        AND has_templates_is_active;

    can_query_active_combo_assignments := can_query_lumistay_rooms
        AND can_query_combo_templates
        AND has_assignments_table
        AND has_assignments_room_id
        AND has_assignments_template_id
        AND has_assignments_effective_to
        AND has_assignments_is_active;

    IF can_query_demo_bookings THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.bookings b ' ||
            'WHERE ' || demo_bookings_where
        INTO demo_booking_count;
    END IF;

    IF has_cancellations_table THEN
        IF has_cancellations_customer_name THEN
            cancellation_marker_where := cancellation_marker_where
                || CASE WHEN cancellation_marker_where <> '' THEN ' OR ' ELSE '' END
                || 'COALESCE(c.customer_name, '''') LIKE ''Demo %''';
        END IF;

        IF has_cancellations_policy_message THEN
            cancellation_marker_where := cancellation_marker_where
                || CASE WHEN cancellation_marker_where <> '' THEN ' OR ' ELSE '' END
                || 'COALESCE(c.policy_message_snapshot, '''') LIKE ''[DEMO-SEED:%''';
        END IF;

        IF has_cancellations_submitted_code THEN
            cancellation_marker_where := cancellation_marker_where
                || CASE WHEN cancellation_marker_where <> '' THEN ' OR ' ELSE '' END
                || 'COALESCE(c.submitted_booking_code, '''') LIKE ''BK-DEMO-%''';
        END IF;

        IF can_query_linked_demo_bookings THEN
            linked_demo_bookings_where :=
                'EXISTS (' ||
                'SELECT 1 FROM public.bookings b ' ||
                'WHERE b.id = c.booking_id AND (' || demo_bookings_where || ')' ||
                ')';

            cancellation_marker_where := cancellation_marker_where
                || CASE WHEN cancellation_marker_where <> '' THEN ' OR ' ELSE '' END
                || linked_demo_bookings_where;
        END IF;

        has_any_cancellation_marker := cancellation_marker_where <> '';
        can_query_demo_cancellations := has_cancellations_table
            AND has_cancellations_status
            AND has_any_cancellation_marker;

        IF can_query_demo_cancellations THEN
            cancellation_sql :=
                'SELECT COUNT(*) ' ||
                'FROM public.booking_cancellation_requests c ' ||
                'WHERE COALESCE(c.status, '''') = ''Pending'' AND ' ||
                '(' || cancellation_marker_where || ')';

            EXECUTE cancellation_sql
            INTO pending_demo_cancellation_count;
        END IF;
    END IF;

    IF can_query_branch_qr THEN
        EXECUTE $sql$
            SELECT COUNT(*)
            FROM public.system_settings s
            WHERE COALESCE(s.setting_key, '') LIKE 'PaymentQr:%:QrImagePath'
              AND BTRIM(COALESCE(s.setting_value, '')) <> ''
        $sql$
        INTO branch_qr_count;
    END IF;

    IF can_query_branch_qr AND can_query_lumistay_branches THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.system_settings s ' ||
            'JOIN public.branches b ' ||
            '  ON s.setting_key = ''PaymentQr:'' || b.id || '':QrImagePath'' ' ||
            'WHERE COALESCE(b.name, '''') LIKE ''LumiStay %'' ' ||
            'AND BTRIM(COALESCE(s.setting_value, '''')) <> '''' ' ||
            CASE
                WHEN has_branches_is_deleted THEN 'AND COALESCE(b.is_deleted, false) = false'
                ELSE ''
            END
        INTO lumistay_branch_qr_count;
    END IF;

    IF can_query_lumistay_branches THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.branches b ' ||
            'WHERE COALESCE(b.name, '''') LIKE ''LumiStay %'' ' ||
            CASE
                WHEN has_branches_is_deleted THEN 'AND COALESCE(b.is_deleted, false) = false'
                ELSE ''
            END
        INTO lumistay_branch_count;
    END IF;

    IF can_query_lumistay_rooms THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.rooms r ' ||
            'JOIN public.branches b ON b.id = r.branch_id ' ||
            'WHERE COALESCE(b.name, '''') LIKE ''LumiStay %'' ' ||
            CASE
                WHEN has_branches_is_deleted THEN 'AND COALESCE(b.is_deleted, false) = false '
                ELSE ''
            END ||
            CASE
                WHEN has_rooms_is_deleted THEN 'AND COALESCE(r.is_deleted, false) = false'
                ELSE ''
            END
        INTO lumistay_room_count;

        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.rooms r ' ||
            'JOIN public.branches b ON b.id = r.branch_id ' ||
            'WHERE COALESCE(b.name, '''') LIKE ''LumiStay %'' ' ||
            'AND COALESCE(r.status, '''') = ''Available'' ' ||
            CASE
                WHEN has_branches_is_deleted THEN 'AND COALESCE(b.is_deleted, false) = false '
                ELSE ''
            END ||
            CASE
                WHEN has_rooms_is_deleted THEN 'AND COALESCE(r.is_deleted, false) = false'
                ELSE ''
            END
        INTO lumistay_available_room_count;
    END IF;

    IF can_query_combo_templates THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.room_slot_templates t ' ||
            'WHERE COALESCE(t.code, '''') IN (''combo_6h'', ''combo_8h'', ''combo_12h'') ' ||
            'AND COALESCE(t.is_active, false) = true ' ||
            CASE
                WHEN has_templates_is_deleted THEN 'AND COALESCE(t.is_deleted, false) = false'
                ELSE ''
            END
        INTO combo_template_count;
    END IF;

    IF can_query_active_combo_assignments THEN
        EXECUTE
            'SELECT COUNT(*) ' ||
            'FROM public.room_slot_template_assignments a ' ||
            'JOIN public.rooms r ON r.id = a.room_id ' ||
            'JOIN public.branches b ON b.id = r.branch_id ' ||
            'JOIN public.room_slot_templates t ON t.id = a.template_id ' ||
            'WHERE COALESCE(b.name, '''') LIKE ''LumiStay %'' ' ||
            'AND COALESCE(t.code, '''') IN (''combo_6h'', ''combo_8h'', ''combo_12h'') ' ||
            'AND COALESCE(a.is_active, false) = true ' ||
            'AND a.effective_to IS NULL ' ||
            CASE
                WHEN has_branches_is_deleted THEN 'AND COALESCE(b.is_deleted, false) = false '
                ELSE ''
            END ||
            CASE
                WHEN has_rooms_is_deleted THEN 'AND COALESCE(r.is_deleted, false) = false '
                ELSE ''
            END ||
            CASE
                WHEN has_templates_is_deleted THEN 'AND COALESCE(t.is_deleted, false) = false'
                ELSE ''
            END
        INTO lumistay_active_combo_assignment_count;
    END IF;

    INSERT INTO pg_temp.demo_verification_summary (
        demo_booking_count,
        pending_demo_cancellation_count,
        branch_qr_count,
        lumistay_branch_qr_count,
        lumistay_branch_count,
        lumistay_room_count,
        lumistay_available_room_count,
        combo_template_count,
        lumistay_active_combo_assignment_count
    )
    VALUES (
        demo_booking_count,
        pending_demo_cancellation_count,
        branch_qr_count,
        lumistay_branch_qr_count,
        lumistay_branch_count,
        lumistay_room_count,
        lumistay_available_room_count,
        combo_template_count,
        lumistay_active_combo_assignment_count
    );
END $$;

SELECT
    demo_booking_count,
    pending_demo_cancellation_count,
    branch_qr_count,
    lumistay_branch_qr_count,
    lumistay_branch_count,
    lumistay_room_count,
    lumistay_available_room_count,
    combo_template_count,
    lumistay_active_combo_assignment_count
FROM pg_temp.demo_verification_summary;
\echo 'Demo verification complete'

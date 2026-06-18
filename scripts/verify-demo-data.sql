\echo 'Demo verification start'

DROP TABLE IF EXISTS pg_temp.demo_verification_summary;

CREATE TEMP TABLE demo_verification_summary (
    demo_booking_count bigint NOT NULL,
    pending_demo_cancellation_count bigint NOT NULL,
    branch_qr_count bigint NOT NULL
) ON COMMIT DROP;

DO $$
DECLARE
    demo_booking_count bigint := 0;
    pending_demo_cancellation_count bigint := 0;
    branch_qr_count bigint := 0;
    has_bookings_table boolean := to_regclass('public.bookings') IS NOT NULL;
    has_cancellations_table boolean := to_regclass('public.booking_cancellation_requests') IS NOT NULL;
    has_system_settings_table boolean := to_regclass('public.system_settings') IS NOT NULL;
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
    can_query_demo_bookings boolean := false;
    can_query_linked_demo_bookings boolean := false;
    can_query_demo_cancellations boolean := false;
    can_query_branch_qr boolean := false;
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

    can_query_demo_bookings := has_bookings_table
        AND has_bookings_customer_note
        AND has_bookings_admin_note;

    can_query_linked_demo_bookings := has_bookings_table
        AND has_bookings_id
        AND has_bookings_customer_note
        AND has_bookings_admin_note;

    can_query_demo_cancellations := has_cancellations_table
        AND has_cancellations_status
        AND has_cancellations_customer_name
        AND has_cancellations_policy_message
        AND has_cancellations_submitted_code
        AND has_cancellations_booking_id;

    can_query_branch_qr := has_system_settings_table
        AND has_system_settings_key
        AND has_system_settings_value;

    IF can_query_demo_bookings THEN
        EXECUTE $sql$
            SELECT COUNT(*)
            FROM public.bookings b
            WHERE COALESCE(b.customer_note, '') LIKE '[DEMO-SEED:%'
               OR COALESCE(b.admin_note, '') LIKE '[DEMO-SEED:%'
        $sql$
        INTO demo_booking_count;
    END IF;

    IF can_query_demo_cancellations THEN
        IF can_query_linked_demo_bookings THEN
            EXECUTE $sql$
                SELECT COUNT(*)
                FROM public.booking_cancellation_requests c
                WHERE COALESCE(c.status, '') = 'Pending'
                  AND (
                      COALESCE(c.customer_name, '') LIKE 'Demo %'
                      OR COALESCE(c.policy_message_snapshot, '') LIKE '[DEMO-SEED:%'
                      OR COALESCE(c.submitted_booking_code, '') LIKE 'BK-DEMO-%'
                      OR EXISTS (
                          SELECT 1
                          FROM public.bookings b
                          WHERE b.id = c.booking_id
                            AND (
                                COALESCE(b.customer_note, '') LIKE '[DEMO-SEED:%'
                                OR COALESCE(b.admin_note, '') LIKE '[DEMO-SEED:%'
                            )
                      )
                  )
            $sql$
            INTO pending_demo_cancellation_count;
        ELSE
            EXECUTE $sql$
                SELECT COUNT(*)
                FROM public.booking_cancellation_requests c
                WHERE COALESCE(c.status, '') = 'Pending'
                  AND (
                      COALESCE(c.customer_name, '') LIKE 'Demo %'
                      OR COALESCE(c.policy_message_snapshot, '') LIKE '[DEMO-SEED:%'
                      OR COALESCE(c.submitted_booking_code, '') LIKE 'BK-DEMO-%'
                  )
            $sql$
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

    INSERT INTO demo_verification_summary (
        demo_booking_count,
        pending_demo_cancellation_count,
        branch_qr_count
    )
    VALUES (
        demo_booking_count,
        pending_demo_cancellation_count,
        branch_qr_count
    );
END $$;

SELECT
    demo_booking_count,
    pending_demo_cancellation_count,
    branch_qr_count
FROM demo_verification_summary;

\echo 'Demo verification complete'

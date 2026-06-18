\echo 'Demo verification start'

DROP TABLE IF EXISTS pg_temp.demo_verification_results;

CREATE TEMP TABLE demo_verification_results (
    metric text PRIMARY KEY,
    count_value bigint NOT NULL,
    status text NOT NULL
) ON COMMIT DROP;

DO $$
DECLARE
    demo_booking_count bigint := 0;
    pending_demo_cancellation_count bigint := 0;
    branch_qr_count bigint := 0;
    bookings_status text := 'missing table or columns';
    cancellations_status text := 'missing table or columns';
    qr_status text := 'missing table or columns';
BEGIN
    IF to_regclass('public.bookings') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'bookings'
             AND column_name = 'customer_note'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'bookings'
             AND column_name = 'admin_note'
       ) THEN
        EXECUTE $sql$
            SELECT COUNT(*)
            FROM public.bookings b
            WHERE COALESCE(b.customer_note, '') LIKE '[DEMO-SEED:%'
               OR COALESCE(b.admin_note, '') LIKE '[DEMO-SEED:%'
        $sql$
        INTO demo_booking_count;

        bookings_status := 'ok';
    END IF;

    IF to_regclass('public.booking_cancellation_requests') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'booking_cancellation_requests'
             AND column_name = 'status'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'booking_cancellation_requests'
             AND column_name = 'customer_name'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'booking_cancellation_requests'
             AND column_name = 'policy_message_snapshot'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'booking_cancellation_requests'
             AND column_name = 'submitted_booking_code'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'booking_cancellation_requests'
             AND column_name = 'booking_id'
       ) THEN
        IF to_regclass('public.bookings') IS NOT NULL
           AND EXISTS (
               SELECT 1
               FROM information_schema.columns
               WHERE table_schema = 'public'
                 AND table_name = 'bookings'
                 AND column_name = 'customer_note'
           )
           AND EXISTS (
               SELECT 1
               FROM information_schema.columns
               WHERE table_schema = 'public'
                 AND table_name = 'bookings'
                 AND column_name = 'admin_note'
           ) THEN
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

        cancellations_status := 'ok';
    END IF;

    IF to_regclass('public.system_settings') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'system_settings'
             AND column_name = 'setting_key'
       )
       AND EXISTS (
           SELECT 1
           FROM information_schema.columns
           WHERE table_schema = 'public'
             AND table_name = 'system_settings'
             AND column_name = 'setting_value'
       ) THEN
        EXECUTE $sql$
            SELECT COUNT(*)
            FROM public.system_settings s
            WHERE COALESCE(s.setting_key, '') LIKE 'PaymentQr:%:QrImagePath'
              AND BTRIM(COALESCE(s.setting_value, '')) <> ''
        $sql$
        INTO branch_qr_count;

        qr_status := 'ok';
    END IF;

    INSERT INTO demo_verification_results (metric, count_value, status)
    VALUES
        ('demo_booking_count', demo_booking_count, bookings_status),
        ('pending_demo_cancellation_count', pending_demo_cancellation_count, cancellations_status),
        ('branch_qr_count', branch_qr_count, qr_status);
END $$;

TABLE demo_verification_results;

SELECT
    COALESCE(MAX(count_value) FILTER (WHERE metric = 'demo_booking_count'), 0) AS demo_booking_count,
    COALESCE(MAX(count_value) FILTER (WHERE metric = 'pending_demo_cancellation_count'), 0) AS pending_demo_cancellation_count,
    COALESCE(MAX(count_value) FILTER (WHERE metric = 'branch_qr_count'), 0) AS branch_qr_count
FROM demo_verification_results;

\echo 'Demo verification complete'

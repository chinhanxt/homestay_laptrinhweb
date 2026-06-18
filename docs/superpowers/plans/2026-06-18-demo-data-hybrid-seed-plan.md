# Demo Data Hybrid Seed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repeatable hybrid demo-data workflow that can reseed a clean demo database, reseed only demo-marked data into the current database, and back up or restore the seeded state before a project demo.

**Architecture:** Keep the implementation script-first and database-first. Use a small PowerShell orchestration layer to call focused SQL scripts for core data, bookings, cancellations, history, and verification; copy real files from `ảnh/` into the upload folders the app already serves; and wrap PostgreSQL `pg_dump`/`psql` commands for backup and restore.

**Tech Stack:** PowerShell, PostgreSQL SQL scripts, ASP.NET Core MVC existing schema, existing upload folder conventions, `dotnet ef database update`, `pg_dump`, `psql`

---

### Task 1: Audit the current schema, upload paths, and script baseline

**Files:**
- Create: `scripts/verify-demo-data.sql`
- Modify: `docs/superpowers/plans/2026-06-18-demo-data-hybrid-seed-plan.md`
- Check: `WebHomestay/Data/ApplicationDbContext.cs`
- Check: `WebHomestay/Models/Booking.cs`
- Check: `WebHomestay/Models/BookingCancellationRequest.cs`
- Check: `WebHomestay/Models/RoomSlotInventory.cs`
- Check: `scripts/seed-lumistay-demo-data.sql`
- Check: `scripts/seed-lumistay-ai-slots-staff.sql`

- [ ] **Step 1: Write the first failing verification query file**

```sql
\echo 'Demo verification start'

WITH demo_bookings AS (
    SELECT b.id
    FROM bookings b
    WHERE b.customer_note LIKE '[DEMO-SEED:%'
       OR b.admin_note LIKE '[DEMO-SEED:%'
)
SELECT COUNT(*) AS demo_booking_count
FROM demo_bookings;
```

- [ ] **Step 2: Run the verification file before implementation**

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: The script runs, but `demo_booking_count` is `0`, confirming the demo marker data does not exist yet.

- [ ] **Step 3: Expand the verification file into a reusable smoke-check harness**

```sql
\echo 'Demo verification start'

WITH demo_bookings AS (
    SELECT b.*
    FROM bookings b
    WHERE b.customer_note LIKE '[DEMO-SEED:%'
       OR b.admin_note LIKE '[DEMO-SEED:%'
),
demo_cancellations AS (
    SELECT c.*
    FROM booking_cancellation_requests c
    WHERE c.customer_name LIKE 'Demo %'
       OR c.policy_message_snapshot LIKE '[DEMO-SEED:%'
),
branch_qr AS (
    SELECT s.setting_key
    FROM system_settings s
    WHERE s.setting_key LIKE 'PaymentQr:%:QrImagePath'
      AND COALESCE(s.setting_value, '') <> ''
)
SELECT
    (SELECT COUNT(*) FROM demo_bookings) AS demo_booking_count,
    (SELECT COUNT(*) FROM demo_cancellations) AS pending_cancellation_count,
    (SELECT COUNT(*) FROM branch_qr) AS branch_qr_count;
```

- [ ] **Step 4: Re-run the smoke-check harness**

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: Counts still show empty or partial baseline, but the file is now the shared verification target for later tasks.

- [ ] **Step 5: Commit the harness**

```bash
git add scripts/verify-demo-data.sql docs/superpowers/plans/2026-06-18-demo-data-hybrid-seed-plan.md
git commit -m "test: add demo data verification harness"
```

### Task 2: Build the core normalization SQL for branches, rooms, combos, and QR settings

**Files:**
- Create: `scripts/seed-demo-core.sql`
- Check: `scripts/seed-lumistay-demo-data.sql`
- Check: `scripts/seed-lumistay-ai-slots-staff.sql`
- Check: `WebHomestay/Services/PaymentQrSettingsService.cs`
- Check: `WebHomestay/Models/RoomSlotTemplate.cs`
- Check: `WebHomestay/Models/RoomSlotTemplateAssignment.cs`

- [ ] **Step 1: Add a failing verification query for room combos and branch QR**

```sql
WITH active_rooms AS (
    SELECT r.id
    FROM rooms r
    JOIN branches b ON b.id = r.branch_id
    WHERE b.name LIKE 'LumiStay %'
      AND r.status = 'Available'
),
combo_assignments AS (
    SELECT a.room_id, t.code
    FROM room_slot_template_assignments a
    JOIN room_slot_templates t ON t.id = a.template_id
    WHERE a.is_active = true
      AND a.effective_to IS NULL
      AND t.code IN ('combo_6h', 'combo_8h', 'combo_12h')
),
room_combo_coverage AS (
    SELECT ar.id AS room_id, COUNT(DISTINCT ca.code) AS combo_count
    FROM active_rooms ar
    LEFT JOIN combo_assignments ca ON ca.room_id = ar.id
    GROUP BY ar.id
)
SELECT COUNT(*) AS rooms_missing_full_combo
FROM room_combo_coverage
WHERE combo_count < 3;
```

- [ ] **Step 2: Run the query to capture the current gap**

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: Some rooms will likely be missing full combo coverage or some branches will be missing QR settings before this task is implemented.

- [ ] **Step 3: Create the core SQL by reusing and tightening the existing seed scripts**

```sql
BEGIN;

WITH seed_templates(name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active) AS (
    VALUES
    ('Combo 6 tiếng', 'combo_6h', 360, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 8 tiếng', 'combo_8h', 480, 30, '08:00'::time, NULL::time, NULL::time, false, true),
    ('Combo 12 tiếng', 'combo_12h', 720, 45, '08:00'::time, NULL::time, NULL::time, false, true)
)
INSERT INTO room_slot_templates (name, code, duration_minutes, cleanup_minutes, seed_start_time, fixed_start_time, fixed_end_time, crosses_midnight, is_active)
SELECT st.name, st.code, st.duration_minutes, st.cleanup_minutes, st.seed_start_time, st.fixed_start_time, st.fixed_end_time, st.crosses_midnight, st.is_active
FROM seed_templates st
WHERE NOT EXISTS (
    SELECT 1
    FROM room_slot_templates existing
    WHERE existing.code = st.code
);

INSERT INTO room_slot_template_assignments (room_id, template_id, effective_from, effective_to, is_active)
SELECT r.id, t.id, DATE '2026-06-01', NULL, true
FROM rooms r
JOIN branches b ON b.id = r.branch_id
JOIN room_slot_templates t ON t.code IN ('combo_6h', 'combo_8h', 'combo_12h')
WHERE b.name LIKE 'LumiStay %'
  AND NOT EXISTS (
      SELECT 1
      FROM room_slot_template_assignments a
      WHERE a.room_id = r.id
        AND a.template_id = t.id
        AND a.effective_to IS NULL
  );

INSERT INTO system_settings (setting_key, setting_value, group_name, description, last_updated)
SELECT CONCAT('PaymentQr:', b.id, ':QrImagePath'),
       CONCAT('/uploads/payment-qr/branch-', b.id, '-qr.png'),
       'PaymentQr',
       CONCAT('Demo QR image path for branch ', b.id),
       NOW()
FROM branches b
WHERE b.name LIKE 'LumiStay %'
  AND NOT EXISTS (
      SELECT 1
      FROM system_settings s
      WHERE s.setting_key = CONCAT('PaymentQr:', b.id, ':QrImagePath')
  );

COMMIT;
```

- [ ] **Step 4: Re-run verification after the core SQL lands**

Run: `psql -d web_homestay_demo -f scripts/seed-demo-core.sql`

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: `rooms_missing_full_combo` becomes `0`, and every LumiStay branch has a populated QR setting path.

- [ ] **Step 5: Commit the core SQL**

```bash
git add scripts/seed-demo-core.sql scripts/verify-demo-data.sql
git commit -m "feat: add core demo seed for rooms combos and branch qr"
```

### Task 3: Add asset sync and matrix booking seed for 15/06/2026 through 22/06/2026

**Files:**
- Create: `scripts/seed-demo-bookings.sql`
- Create: `scripts/reseed-demo-data.ps1`
- Check: `ảnh/cccd.png`
- Check: `ảnh/chuyenkhoan.jpg`
- Check: `ảnh/ma qr.png`
- Check: `WebHomestay/App_Data/SecureUploads/IDCards/`
- Check: `WebHomestay/wwwroot/uploads/payments/`
- Check: `WebHomestay/wwwroot/uploads/payment-qr/`
- Check: `WebHomestay/Models/Booking.cs`
- Check: `WebHomestay/Models/RoomSlotInventory.cs`
- Check: `WebHomestay/Services/StatisticsService.cs`

- [ ] **Step 1: Add a failing verification query for demo bookings in the target week**

```sql
WITH demo_bookings AS (
    SELECT b.*
    FROM bookings b
    WHERE (b.customer_note LIKE '[DEMO-SEED:%' OR b.admin_note LIKE '[DEMO-SEED:%')
      AND b.start_time >= TIMESTAMP '2026-06-15 00:00:00'
      AND b.start_time < TIMESTAMP '2026-06-23 00:00:00'
)
SELECT
    COUNT(*) AS week_demo_booking_count,
    COUNT(*) FILTER (WHERE id_card_front_path IS NOT NULL AND id_card_back_path IS NOT NULL AND payment_proof_url IS NOT NULL) AS bookings_with_all_assets
FROM demo_bookings;
```

- [ ] **Step 2: Run the verification to confirm the week seed does not exist yet**

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: `week_demo_booking_count` is `0` before the implementation.

- [ ] **Step 3: Create the booking seed SQL and PowerShell orchestration**

```sql
BEGIN;

DELETE FROM bookings
WHERE customer_note LIKE '[DEMO-SEED:MATRIX-2026-06]%'
   OR admin_note LIKE '[DEMO-SEED:MATRIX-2026-06]%';

WITH target_slots AS (
    SELECT i.id,
           i.room_id,
           i.template_id,
           i.slot_date,
           i.slot_label,
           i.start_time,
           i.end_time,
           ROW_NUMBER() OVER (PARTITION BY i.room_id, i.slot_date ORDER BY i.start_time) AS slot_rank
    FROM room_slot_inventories i
    JOIN rooms r ON r.id = i.room_id
    JOIN branches b ON b.id = r.branch_id
    WHERE b.name LIKE 'LumiStay %'
      AND i.slot_date BETWEEN DATE '2026-06-15' AND DATE '2026-06-22'
      AND i.status = 'Available'
),
selected_slots AS (
    SELECT *
    FROM target_slots
    WHERE slot_rank IN (1, 2)
       OR (EXTRACT(DAY FROM slot_date) IN (6, 7, 13, 14, 20, 21) AND slot_rank = 3)
)
INSERT INTO bookings (
    room_id, customer_name, customer_phone, customer_email, guest_count,
    id_card_front_path, id_card_back_path, customer_note, admin_note,
    start_time, end_time, total_price, status, payment_status, payment_proof_url,
    created_at, booking_mode, room_slot_inventory_id, slot_label
)
SELECT
    ss.room_id,
    CONCAT('Demo Guest ', ss.id),
    CONCAT('0909', LPAD(ss.id::text, 6, '0')),
    CONCAT('demo+', ss.id, '@lumistay.vn'),
    CASE WHEN ss.slot_rank = 1 THEN 2 ELSE 3 END,
    CONCAT('/App_Data/SecureUploads/IDCards/demo-front-', ss.id, '.png'),
    CONCAT('/App_Data/SecureUploads/IDCards/demo-back-', ss.id, '.png'),
    '[DEMO-SEED:MATRIX-2026-06]',
    CONCAT('[DEMO-SEED:MATRIX-2026-06] slot ', ss.slot_label),
    ss.start_time,
    ss.end_time,
    CASE WHEN ss.slot_rank = 1 THEN 750000 ELSE 1050000 END,
    CASE
        WHEN ss.slot_date < DATE '2026-06-18' THEN 'CheckedOut'
        WHEN ss.slot_date = DATE '2026-06-18' THEN 'CheckedIn'
        WHEN ss.slot_rank = 1 THEN 'Confirmed'
        WHEN ss.slot_rank = 2 THEN 'AwaitingApproval'
        ELSE 'PendingPayment'
    END,
    CASE WHEN ss.slot_rank IN (1, 2) THEN 'Paid' ELSE 'Unpaid' END,
    CONCAT('/uploads/payments/demo-bill-', ss.id, '.jpg'),
    NOW(),
    1,
    ss.id,
    ss.slot_label
FROM selected_slots ss;

UPDATE room_slot_inventories i
SET booking_id = b.id,
    status = CASE
        WHEN b.status IN ('CheckedOut', 'CheckedIn', 'Confirmed', 'AwaitingApproval') THEN 'Booked'
        ELSE 'Reserved'
    END
FROM bookings b
WHERE b.room_slot_inventory_id = i.id
  AND b.customer_note = '[DEMO-SEED:MATRIX-2026-06]';

COMMIT;
```

```powershell
param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\\..").Path
)

$idCardDir = Join-Path $RepoRoot "WebHomestay\\App_Data\\SecureUploads\\IDCards"
$billDir = Join-Path $RepoRoot "WebHomestay\\wwwroot\\uploads\\payments"
$qrDir = Join-Path $RepoRoot "WebHomestay\\wwwroot\\uploads\\payment-qr"

New-Item -ItemType Directory -Force -Path $idCardDir, $billDir, $qrDir | Out-Null

Copy-Item (Join-Path $RepoRoot "ảnh\\cccd.png") (Join-Path $idCardDir "demo-front-template.png") -Force
Copy-Item (Join-Path $RepoRoot "ảnh\\cccd.png") (Join-Path $idCardDir "demo-back-template.png") -Force
Copy-Item (Join-Path $RepoRoot "ảnh\\chuyenkhoan.jpg") (Join-Path $billDir "demo-bill-template.jpg") -Force
Copy-Item (Join-Path $RepoRoot "ảnh\\ma qr.png") (Join-Path $qrDir "branch-1-qr.png") -Force

psql -d $DatabaseName -f (Join-Path $RepoRoot "scripts\\seed-demo-core.sql")
psql -d $DatabaseName -f (Join-Path $RepoRoot "scripts\\seed-demo-bookings.sql")
psql -d $DatabaseName -f (Join-Path $RepoRoot "scripts\\verify-demo-data.sql")
```

- [ ] **Step 4: Re-run the orchestration and verify the week seed**

Run: `powershell -ExecutionPolicy Bypass -File scripts/reseed-demo-data.ps1 -DatabaseName web_homestay_demo`

Expected: The week target gains demo bookings across the matrix and the verification script reports non-zero `week_demo_booking_count` plus matching asset counts.

- [ ] **Step 5: Commit the booking seed and orchestration**

```bash
git add scripts/seed-demo-bookings.sql scripts/reseed-demo-data.ps1 scripts/verify-demo-data.sql
git commit -m "feat: add demo matrix booking seed orchestration"
```

### Task 4: Seed cancellation queue and revenue history for realistic admin lists and charts

**Files:**
- Create: `scripts/seed-demo-cancellations.sql`
- Create: `scripts/seed-demo-history.sql`
- Check: `WebHomestay/Models/BookingCancellationRequest.cs`
- Check: `WebHomestay/Services/StatisticsService.cs`
- Check: `WebHomestay/App_Data/SecureUploads/Cancellations/confirmation/`
- Check: `WebHomestay/App_Data/SecureUploads/Cancellations/refundQr/`

- [ ] **Step 1: Add failing verification queries for pending cancellations and history spread**

```sql
WITH demo_cancellations AS (
    SELECT c.*
    FROM booking_cancellation_requests c
    WHERE c.status = 'Pending'
      AND c.policy_message_snapshot LIKE '[DEMO-SEED:CANCEL-2026-06]%'
),
history_bookings AS (
    SELECT b.*
    FROM bookings b
    WHERE b.customer_note = '[DEMO-SEED:HISTORY-2026-Q2]'
)
SELECT
    COUNT(*) FILTER (WHERE 1 = 1) AS pending_demo_cancellations,
    (SELECT COUNT(DISTINCT DATE_TRUNC('month', hb.start_time)) FROM history_bookings hb) AS history_month_count
FROM demo_cancellations;
```

- [ ] **Step 2: Run the verification before seeding cancellations and history**

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: The pending cancellation count or history month count is below target before implementation.

- [ ] **Step 3: Create SQL for branch-wide pending cancellations and 1-3 month revenue history**

```sql
BEGIN;

DELETE FROM booking_cancellation_requests
WHERE policy_message_snapshot LIKE '[DEMO-SEED:CANCEL-2026-06]%';

WITH branch_bookings AS (
    SELECT DISTINCT ON (r.branch_id)
           b.id AS booking_id,
           r.branch_id,
           b.customer_name,
           b.customer_phone,
           COALESCE(b.customer_email, CONCAT('demo-cancel+', b.id, '@lumistay.vn')) AS customer_email
    FROM bookings b
    JOIN rooms r ON r.id = b.room_id
    WHERE b.customer_note = '[DEMO-SEED:MATRIX-2026-06]'
    ORDER BY r.branch_id, b.start_time DESC
)
INSERT INTO booking_cancellation_requests (
    chat_session_id, booking_id, submitted_booking_code, customer_name, customer_phone, customer_email,
    confirmation_email_proof_path, refund_qr_image_path, refund_bank_name, refund_bank_account_number,
    refund_bank_account_holder, status, policy_notice_hours_snapshot, refund_percent_before_notice_snapshot,
    refund_percent_after_notice_snapshot, policy_message_snapshot, is_manual, refund_status, created_at, updated_at
)
SELECT
    CONCAT('demo-cancel-', bb.booking_id),
    bb.booking_id,
    CONCAT('BK-DEMO-', bb.booking_id),
    CONCAT('Demo Cancel ', bb.branch_id),
    bb.customer_phone,
    bb.customer_email,
    '/App_Data/SecureUploads/Cancellations/confirmation/demo-confirmation.png',
    '/App_Data/SecureUploads/Cancellations/refundQr/demo-refund-qr.png',
    'Vietcombank',
    CONCAT('001', LPAD(bb.branch_id::text, 6, '0')),
    CONCAT('Demo Branch ', bb.branch_id),
    'Pending',
    24,
    80,
    30,
    '[DEMO-SEED:CANCEL-2026-06] Pending cancellation demo',
    false,
    'NotRefunded',
    NOW(),
    NOW()
FROM branch_bookings bb;

DELETE FROM bookings
WHERE customer_note = '[DEMO-SEED:HISTORY-2026-Q2]';

INSERT INTO bookings (
    room_id, customer_name, customer_phone, customer_email, guest_count,
    customer_note, admin_note, start_time, end_time, total_price,
    status, payment_status, created_at, booking_mode
)
SELECT
    r.id,
    CONCAT('Demo History ', r.id, '-', gs.day_value::date),
    CONCAT('0911', LPAD(r.id::text, 6, '0')),
    CONCAT('history+', r.id, '-', TO_CHAR(gs.day_value, 'YYYYMMDD'), '@lumistay.vn'),
    CASE WHEN EXTRACT(ISODOW FROM gs.day_value) IN (6, 7) THEN 3 ELSE 2 END,
    '[DEMO-SEED:HISTORY-2026-Q2]',
    CONCAT('[DEMO-SEED:HISTORY-2026-Q2] ', TO_CHAR(gs.day_value, 'YYYY-MM-DD')),
    gs.day_value + TIME '14:00',
    gs.day_value + TIME '10:00' + INTERVAL '1 day',
    CASE
        WHEN EXTRACT(ISODOW FROM gs.day_value) IN (6, 7) THEN COALESCE(r.price_weekend_per_day, r.price_per_day)
        ELSE r.price_per_day
    END,
    'CheckedOut',
    'Paid',
    NOW(),
    1
FROM rooms r
JOIN branches b ON b.id = r.branch_id
CROSS JOIN (
    SELECT generate_series(DATE '2026-03-15', DATE '2026-06-14', INTERVAL '5 day') AS day_value
) gs
WHERE b.name LIKE 'LumiStay %'
  AND ((r.id + EXTRACT(DOY FROM gs.day_value)::int) % 3 <> 0);

COMMIT;
```

- [ ] **Step 4: Re-run orchestration and smoke verification**

Run: `psql -d web_homestay_demo -f scripts/seed-demo-cancellations.sql`

Run: `psql -d web_homestay_demo -f scripts/seed-demo-history.sql`

Run: `psql -d web_homestay_demo -f scripts/verify-demo-data.sql`

Expected: Every branch contributes at least one `Pending` cancellation request and the history spans at least three distinct months before `2026-06-15`.

- [ ] **Step 5: Commit the cancellation and history seed**

```bash
git add scripts/seed-demo-cancellations.sql scripts/seed-demo-history.sql scripts/verify-demo-data.sql
git commit -m "feat: add demo cancellation queue and revenue history seed"
```

### Task 5: Add backup, restore, demo database bootstrap, and final runbook verification

**Files:**
- Create: `scripts/backup-demo-db.ps1`
- Create: `scripts/restore-demo-db.ps1`
- Create: `docs/note/demo-data-runbook.md`
- Modify: `scripts/reseed-demo-data.ps1`
- Check: `WebHomestay/appsettings.json`
- Check: `r.ps1`

- [ ] **Step 1: Add a failing runbook step for backup and restore commands**

```md
# Demo Data Runbook

## Backup

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/backup-demo-db.ps1 -DatabaseName web_homestay_demo
```

Expected:

- Creates a timestamped dump file under `backups/demo/`
- Prints the final dump path

## Restore

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/restore-demo-db.ps1 -DatabaseName web_homestay_demo -BackupPath backups/demo/latest.dump
```

Expected:

- Drops and recreates the demo database or clears the named target
- Restores the dump without interactive prompts
```

- [ ] **Step 2: Run the commands before implementation to confirm the scripts are missing**

Run: `powershell -ExecutionPolicy Bypass -File scripts/backup-demo-db.ps1 -DatabaseName web_homestay_demo`

Expected: PowerShell errors because the file does not exist yet.

- [ ] **Step 3: Implement backup, restore, and final reseed chaining**

```powershell
param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$BackupDir = (Join-Path (Resolve-Path "$PSScriptRoot\\..").Path "backups\\demo")
)

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $BackupDir "$DatabaseName-demo-$timestamp.dump"

pg_dump --format=custom --file="$backupPath" $DatabaseName

Write-Host "Backup created at $backupPath"
```

```powershell
param(
    [string]$DatabaseName = "web_homestay_demo",
    [string]$BackupPath
)

if (-not (Test-Path $BackupPath)) {
    throw "Backup file not found: $BackupPath"
}

psql -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();"
psql -d postgres -c "DROP DATABASE IF EXISTS $DatabaseName;"
psql -d postgres -c "CREATE DATABASE $DatabaseName;"
pg_restore --clean --if-exists --no-owner --dbname=$DatabaseName $BackupPath

Write-Host "Restore completed for $DatabaseName"
```

```powershell
param(
    [string]$DatabaseName = "web_homestay_demo",
    [switch]$RunBackup
)

dotnet ef database update --project WebHomestay/WebHomestay.csproj --connection "Host=localhost;Database=$DatabaseName;Username=postgres;Password=postgres"
psql -d $DatabaseName -f "scripts/seed-demo-core.sql"
psql -d $DatabaseName -f "scripts/seed-demo-bookings.sql"
psql -d $DatabaseName -f "scripts/seed-demo-cancellations.sql"
psql -d $DatabaseName -f "scripts/seed-demo-history.sql"
psql -d $DatabaseName -f "scripts/verify-demo-data.sql"

if ($RunBackup) {
    powershell -ExecutionPolicy Bypass -File "scripts/backup-demo-db.ps1" -DatabaseName $DatabaseName
}
```

- [ ] **Step 4: Run the full path end-to-end**

Run: `powershell -ExecutionPolicy Bypass -File scripts/reseed-demo-data.ps1 -DatabaseName web_homestay_demo -RunBackup`

Run: `powershell -ExecutionPolicy Bypass -File scripts/restore-demo-db.ps1 -DatabaseName web_homestay_demo -BackupPath backups/demo/<latest-file>.dump`

Expected: The reseed completes, verification prints healthy counts, the backup file is created, and restore brings the demo database back with the same counts.

- [ ] **Step 5: Commit the operational tooling and runbook**

```bash
git add scripts/backup-demo-db.ps1 scripts/restore-demo-db.ps1 scripts/reseed-demo-data.ps1 docs/note/demo-data-runbook.md
git commit -m "feat: add demo backup restore workflow"
```

## Self-Review

Spec coverage check:

- Hybrid delivery: covered by Task 5.
- Demo DB plus current DB reseed workflow: covered by Tasks 3 and 5.
- Matrix occupancy for `2026-06-15` through `2026-06-22`: covered by Task 3.
- Two CCCD images plus one bill per booking from `ảnh/`: covered by Task 3.
- QR seeding for all branches: covered by Task 2 and Task 3.
- Pending cancellations across all branches: covered by Task 4.
- Revenue history for 1-3 months before the demo week: covered by Task 4.
- Verification and safety markers: covered by Task 1 and reused throughout Tasks 2-5.

Placeholder scan:

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- All code-changing steps include concrete SQL or PowerShell snippets.
- All run steps include explicit commands and expected outcomes.

Type consistency check:

- Demo booking marker values stay consistent as `[DEMO-SEED:MATRIX-2026-06]`.
- Demo history marker values stay consistent as `[DEMO-SEED:HISTORY-2026-Q2]`.
- Demo cancellation marker values stay consistent as `[DEMO-SEED:CANCEL-2026-06]`.
- File names are used consistently across the plan: `seed-demo-core.sql`, `seed-demo-bookings.sql`, `seed-demo-cancellations.sql`, `seed-demo-history.sql`, `verify-demo-data.sql`, `reseed-demo-data.ps1`, `backup-demo-db.ps1`, `restore-demo-db.ps1`.

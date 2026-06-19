# Demo DB Quickstart

## DB demo đang dùng

- Tên DB: `web_homestay_demo`
- App mặc định chạy ở: `http://localhost:5000`

## 1. Reseed lại dữ liệu demo

```powershell
powershell -ExecutionPolicy Bypass -File scripts/reseed-demo-data.ps1 -DatabaseName web_homestay_demo
```

## 2. Backup snapshot demo

```powershell
powershell -ExecutionPolicy Bypass -File scripts/backup-demo-db.ps1 -DatabaseName web_homestay_demo
```

## 3. Restore lại snapshot demo

```powershell
powershell -ExecutionPolicy Bypass -File scripts/restore-demo-db.ps1 -DatabaseName web_homestay_demo -BackupPath backups/demo/<file>.dump
```

## 4. Chạy web với DB demo

```powershell
.\wdemo
```

## 5. Chạy web với DB gốc

```powershell
.\wmain
```

## 6. Kiểm tra nhanh dữ liệu demo

```powershell
$env:PGPASSWORD = "1510"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -p 5432 -U postgres -d web_homestay_demo -f scripts/verify-demo-data.sql
```

## Số liệu verify hiện tại

- `demo_booking_count = 2110`
- `week_demo_booking_count = 500`
- `demo_booking_with_assets_count = 437`
- `demo_history_booking_count = 1610`
- `demo_history_month_count = 4`
- `pending_demo_cancellation_count = 5`
- `lumistay_branch_count = 5`
- `lumistay_room_count = 25`
- `lumistay_available_room_count = 25`
- `combo_template_count = 3`
- `lumistay_active_combo_assignment_count = 75`

## Ghi chú

- Ảnh demo được copy local khi chạy `reseed-demo-data.ps1`.
- Nếu cần bơm thẳng vào DB hiện tại, chỉ đổi `-DatabaseName`.

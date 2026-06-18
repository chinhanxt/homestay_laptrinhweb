# Demo Data Runbook

## Reseed demo DB

```powershell
powershell -ExecutionPolicy Bypass -File scripts/reseed-demo-data.ps1 -DatabaseName web_homestay_demo
```

## Reseed demo DB rồi backup ngay

```powershell
powershell -ExecutionPolicy Bypass -File scripts/reseed-demo-data.ps1 -DatabaseName web_homestay_demo -RunBackup
```

## Backup DB demo

```powershell
powershell -ExecutionPolicy Bypass -File scripts/backup-demo-db.ps1 -DatabaseName web_homestay_demo
```

## Restore DB demo từ file dump

```powershell
powershell -ExecutionPolicy Bypass -File scripts/restore-demo-db.ps1 -DatabaseName web_homestay_demo -BackupPath backups/demo/<file>.dump
```

## Gói dữ liệu demo hiện tại

- `scripts/seed-demo-core.sql`: chi nhánh, phòng, combo giờ, QR settings
- `scripts/seed-demo-bookings.sql`: inventory và booking tuần demo `15/06/2026 - 22/06/2026`
- `scripts/seed-demo-cancellations.sql`: đơn hủy chờ duyệt ở tất cả chi nhánh
- `scripts/seed-demo-history.sql`: lịch sử booking 3 tháng để làm đẹp thống kê
- `scripts/verify-demo-data.sql`: kiểm tra nhanh số lượng seed quan trọng

## Lưu ý

- Ảnh demo lấy từ thư mục `ảnh/`.
- CCCD và bill đang dùng asset shared để giảm số file trùng.
- QR demo cho các chi nhánh đều được copy từ `ảnh/ma qr.png`.

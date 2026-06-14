namespace WebHomestay.Models.ViewModels
{
    public class PaymentQrSettingsViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string QrImagePath { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;
        public string TransferContentTemplate { get; set; } = "chinhan {BookingId}";
        public int CountdownMinutes { get; set; } = 5;
        public string BeforeBillMessage { get; set; } = "Vui lòng tải lên ảnh chụp màn hình bill thanh toán thành công để chúng tôi xác nhận nhanh nhất.";
        public string AfterBillMessage { get; set; } = "Chúng tôi đã nhận được Bill của bạn. Nhân viên sẽ đối soát và gửi mã phòng qua Email sớm nhất.";
        public string ExpiredMessage { get; set; } = "Rất tiếc, thời gian giữ chỗ của bạn đã kết thúc. Vui lòng quay lại trang chủ để chọn lại.";
    }

    public class PaymentQrDisplayViewModel
    {
        public string QrImageSrc { get; set; } = string.Empty;
        public bool HasQrImage { get; set; }
        public string MissingQrMessage { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;
        public string TransferContent { get; set; } = string.Empty;
        public int CountdownMinutes { get; set; } = 5;
        public string BeforeBillMessage { get; set; } = string.Empty;
        public string AfterBillMessage { get; set; } = string.Empty;
        public string ExpiredMessage { get; set; } = string.Empty;
    }
}

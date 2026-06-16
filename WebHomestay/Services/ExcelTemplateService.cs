using System;
using System.IO;
using ClosedXML.Excel;

namespace WebHomestay.Services
{
    public interface IExcelTemplateService
    {
        byte[] GenerateTemplate(string type);
    }

    public class ExcelTemplateService : IExcelTemplateService
    {
        public byte[] GenerateTemplate(string type)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Template");
                worksheet.Row(1).Height = 25;

                // Setup header style
                var headerStyle = workbook.Style;
                headerStyle.Font.Bold = true;
                headerStyle.Fill.BackgroundColor = XLColor.FromHtml("#4F81BD");
                headerStyle.Font.FontColor = XLColor.White;
                headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                switch (type.ToLower())
                {
                    case "branch":
                        SetupBranchTemplate(worksheet, headerStyle);
                        break;
                    case "room":
                        SetupRoomTemplate(worksheet, headerStyle);
                        break;
                    case "staff":
                        SetupStaffTemplate(worksheet, headerStyle);
                        break;
                    case "ai":
                        SetupAITemplate(worksheet, headerStyle);
                        break;
                    case "slottemplate":
                        SetupSlotTemplateTemplate(worksheet, headerStyle);
                        break;
                    case "holiday":
                        SetupHolidayTemplate(worksheet, headerStyle);
                        break;
                    default:
                        throw new ArgumentException("Invalid template type", nameof(type));
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private void SetupBranchTemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { "Tên chi nhánh *", "Địa chỉ *", "Mô tả", "Hotline", "Email", "Map URL (Link bản đồ)", "Giờ đặt trước tối thiểu" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "Chi nhánh Quận 1";
            ws.Cell(2, 2).Value = "123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. HCM";
            ws.Cell(2, 3).Value = "Chi nhánh trung tâm thành phố, tiện ích cao cấp";
            ws.Cell(2, 4).Value = "0901234567";
            ws.Cell(2, 5).Value = "quan1@homestay.com";
            ws.Cell(2, 6).Value = "https://maps.app.goo.gl/example";
            ws.Cell(2, 7).Value = 2;
        }

        private void SetupRoomTemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { 
                "Tên phòng *", "Mô tả", "Giá thường theo giờ *", "Giá thường theo ngày *", "Phụ phí khách thêm", 
                "Giá cuối tuần theo giờ", "Giá cuối tuần theo ngày", "Giá lễ theo giờ", "Giá lễ theo ngày", 
                "Sức chứa chuẩn *", "Sức chứa tối đa *", "Trạng thái", "Tên chi nhánh *", 
                "Ảnh đại diện (Tên file trong ZIP)", "Ảnh bổ sung (Tên các file cách nhau bằng dấu phẩy)" 
            };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "Phòng Deluxe 101";
            ws.Cell(2, 2).Value = "Phòng Deluxe hướng phố, đầy đủ tiện nghi, giường King size.";
            ws.Cell(2, 3).Value = 100000;
            ws.Cell(2, 4).Value = 800000;
            ws.Cell(2, 5).Value = 50000;
            ws.Cell(2, 6).Value = 120000;
            ws.Cell(2, 7).Value = 950000;
            ws.Cell(2, 8).Value = 150000;
            ws.Cell(2, 9).Value = 1200000;
            ws.Cell(2, 10).Value = 2;
            ws.Cell(2, 11).Value = 4;
            ws.Cell(2, 12).Value = "Available"; // Available / Maintenance
            ws.Cell(2, 13).Value = "Chi nhánh Quận 1";
            ws.Cell(2, 14).Value = "deluxe_main.jpg";
            ws.Cell(2, 15).Value = "deluxe_sub1.jpg, deluxe_sub2.jpg";
        }

        private void SetupStaffTemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { "Tên đăng nhập *", "Họ và tên *", "Vai trò (SuperAdmin/Manager/Staff) *", "Tên chi nhánh *", "Danh sách quyền chi tiết (Cách nhau bằng dấu phẩy)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "nv_nguyenvana";
            ws.Cell(2, 2).Value = "Nguyễn Văn A";
            ws.Cell(2, 3).Value = "Staff";
            ws.Cell(2, 4).Value = "Chi nhánh Quận 1";
            ws.Cell(2, 5).Value = "bookings.view,bookings.create,rooms.view";
        }

        private void SetupAITemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { "Phạm vi tri thức (Scope) *", "Tiêu đề *", "Nội dung *", "Thẻ (Tags, cách nhau bằng dấu phẩy)", "Độ ưu tiên (Số)", "Hoạt động (Yes/No)" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "Quy định check-in";
            ws.Cell(2, 2).Value = "Thời gian Check-in và Check-out";
            ws.Cell(2, 3).Value = "Thời gian nhận phòng (Check-in) là từ 14:00. Thời gian trả phòng (Check-out) là trước 12:00 trưa ngày hôm sau.";
            ws.Cell(2, 4).Value = "checkin, checkout, gio_giac";
            ws.Cell(2, 5).Value = 10;
            ws.Cell(2, 6).Value = "Yes";
        }

        private void SetupSlotTemplateTemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { 
                "Tên mẫu *", "Mã mẫu (Code) *", "Thời lượng (Phút) *", "Dọn dẹp (Phút) *", 
                "Giờ bắt đầu seed (HH:mm)", "Giờ bắt đầu cố định (HH:mm)", "Giờ kết thúc cố định (HH:mm)", 
                "Qua đêm (Yes/No)", "Hoạt động (Yes/No)" 
            };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "Khung giờ Sáng";
            ws.Cell(2, 2).Value = "MORNING_SLOT";
            ws.Cell(2, 3).Value = 240;
            ws.Cell(2, 4).Value = 30;
            ws.Cell(2, 5).Value = "08:00";
            ws.Cell(2, 6).Value = "08:00";
            ws.Cell(2, 7).Value = "12:00";
            ws.Cell(2, 8).Value = "No";
            ws.Cell(2, 9).Value = "Yes";
        }

        private void SetupHolidayTemplate(IXLWorksheet ws, IXLStyle headerStyle)
        {
            string[] headers = { "Ngày (yyyy-MM-dd) *", "Mô tả lễ" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style = headerStyle;
            }

            // Sample data row
            ws.Cell(2, 1).Value = "2026-09-02";
            ws.Cell(2, 2).Value = "Quốc khánh nước CHXHCN Việt Nam";
        }
    }
}

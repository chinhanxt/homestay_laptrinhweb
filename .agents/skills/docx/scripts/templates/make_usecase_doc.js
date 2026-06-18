const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, ShadingType, WidthType, BorderStyle, HeadingLevel, AlignmentType } = require('docx');
const fs = require('fs');
const path = require('path');

const doc = new Document({
    styles: {
        default: {
            document: {
                run: {
                    font: "Times New Roman",
                    size: 26 // 13pt default
                }
            }
        },
        paragraphStyles: [
            {
                id: "Heading1",
                name: "Heading 1",
                basedOn: "Normal",
                next: "Normal",
                quickFormat: true,
                run: {
                    size: 32, // 16pt
                    bold: true,
                    font: "Times New Roman",
                    color: "000000"
                },
                paragraph: {
                    spacing: { before: 240, after: 240 }
                }
            },
            {
                id: "Heading2",
                name: "Heading 2",
                basedOn: "Normal",
                next: "Normal",
                quickFormat: true,
                run: {
                    size: 28, // 14pt
                    bold: true,
                    font: "Times New Roman",
                    color: "000000"
                },
                paragraph: {
                    spacing: { before: 180, after: 180 }
                }
            }
        ]
    },
    sections: [] // Will add via addSection below
});

const border = { style: BorderStyle.SINGLE, size: 4, color: "CCCCCC" };
const borders = { top: border, bottom: border, left: border, right: border };

// Helper to create cells
function makeCell(text, isHeader = false, width = 5000, align = "both") {
    return new TableCell({
        borders: borders,
        width: { size: width, type: WidthType.DXA },
        shading: isHeader ? { fill: "D5E8F0", type: ShadingType.CLEAR } : undefined,
        margins: { top: 100, bottom: 100, left: 120, right: 120 },
        children: [
            new Paragraph({
                alignment: align === "center" ? AlignmentType.CENTER : (align === "right" ? AlignmentType.RIGHT : AlignmentType.BOTH),
                spacing: { before: 40, after: 40 },
                children: [
                    new TextRun({
                        text: text,
                        bold: isHeader,
                        size: 24 // 12pt inside table
                    })
                ]
            })
        ]
    });
}

function createTable(headers, rows, colWidths) {
    const tableRows = [
        // Header Row
        new TableRow({
            children: headers.map((h, i) => makeCell(h, true, colWidths[i], "center"))
        }),
        // Data Rows
        ...rows.map((row, rIdx) => new TableRow({
            children: row.map((cellText, cIdx) => {
                // STT or ID columns centered
                const alignment = (cIdx === 0 || cIdx === 2) ? "center" : "both";
                const cellObj = makeCell(cellText, false, colWidths[cIdx], alignment);
                
                // Alternate zebra shading
                if (rIdx % 2 === 1) {
                    cellObj.shading = { fill: "F9F9F9", type: ShadingType.CLEAR };
                }
                return cellObj;
            })
        }))
    ];

    return new Table({
        width: { size: 9026, type: WidthType.DXA },
        columnWidths: colWidths,
        rows: tableRows
    });
}

// Table 1 Data: Tổng quát (STT, Tên Use Case, Tác nhân, Mô tả ngắn gọn)
const headers1 = ["ID", "Tên Use Case", "Tác nhân", "Mô tả ngắn gọn"];
const widths1 = [1100, 2500, 1800, 3626];
const rows1 = [
    ["UC-TQ01", "Đăng nhập", "Khách hàng, Admin", "Đăng nhập vào tài khoản trên hệ thống."],
    ["UC-TQ02", "Tra cứu & Lọc phòng trống", "Khách hàng", "Tìm phòng theo chi nhánh, thời gian và tiện ích."],
    ["UC-TQ03", "Chat với trợ lý ảo AI", "Khách hàng", "Tư vấn và đặt phòng tự động qua chatbot AI."],
    ["UC-TQ04", "Đặt phòng & Thanh toán online", "Khách hàng", "Đặt phòng và thanh toán qua VietQR động tự sinh."],
    ["UC-TQ05", "Tự Check-in / Check-out online", "Khách hàng", "Tự nhận phòng (tải CCCD) và trả phòng online."],
    ["UC-TQ06", "Quản lý danh mục & Tham số", "Quản trị viên / Nhân viên", "Quản lý chi nhánh, phòng, loại phòng và giá lễ."],
    ["UC-TQ07", "Vận hành Ma trận phòng", "Quản trị viên / Nhân viên", "Theo dõi và điều phối trạng thái phòng thời gian thực."],
    ["UC-TQ08", "Cấu hình & Vận hành AI", "Quản trị viên / Nhân viên", "Quản lý tri thức RAG và giám sát trace chat AI."],
    ["UC-TQ09", "Phân quyền nhân sự", "Quản trị viên / Nhân viên", "Cấp quyền nhân viên theo vai trò hoặc ghi đè."]
];

// Table 2 Data: Admin (ID, Tên Use Case, Mô tả ngắn gọn)
const headers2 = ["ID", "Tên Use Case", "Mô tả ngắn gọn"];
const widths2 = [1200, 2800, 5026];
const rows2 = [
    ["UC-AD01", "Đăng nhập hệ thống Admin", "Xác thực tài khoản quản trị qua cơ chế bảo mật Session-based Admin Authentication."],
    ["UC-AD02", "CRUD Chi nhánh, phòng, loại phòng", "Quản lý thông tin địa chỉ chi nhánh, phòng vật lý và đơn giá/sức chứa của từng loại phòng."],
    ["UC-AD03", "Duyệt đơn đặt phòng & Xem ảnh CCCD", "Xem chi tiết thông tin đơn hàng, kiểm tra ảnh bill chuyển khoản và ảnh CCCD tự check-in của khách."],
    ["UC-AD04", "Vận hành phòng trên Ma trận", "Thao tác trực tiếp trên lưới phòng: duyệt thanh toán, check-in, check-out nhanh, dọn phòng, đổi phòng."],
    ["UC-AD05", "Phân quyền nhân sự", "Cấu hình ma trận quyền nhân viên, tự động validate logic quyền cha-con ở cả Frontend và Backend."],
    ["UC-AD06", "Quản lý tri thức AI RAG & Đồ thị", "CRUD kho bài viết tri thức FAQ, thiết lập các đỉnh (Node) và cạnh (Edge) quan hệ tri thức đồ thị."],
    ["UC-AD07", "Cấu hình AI Agent Prompts", "Tùy chỉnh prompt hệ thống cho 5 Agent, xem dấu vết log traces từng cuộc gọi LLM để tinh chỉnh AI."],
    ["UC-AD08", "Xem doanh thu thống kê & Xuất file", "Theo dõi biểu đồ doanh thu chi nhánh, tỷ lệ lấp đầy phòng và xuất báo cáo dữ liệu đặt phòng."]
];

// Table 3 Data: User (ID, Tên Use Case, Mô tả ngắn gọn)
const headers3 = ["ID", "Tên Use Case", "Mô tả ngắn gọn"];
const widths3 = [1200, 2800, 5026];
const rows3 = [
    ["UC-US01", "Tìm kiếm & Lọc phòng trống", "Tra cứu phòng trống thời gian thực theo chi nhánh, sức chứa, tiện nghi và khoảng giá lọc."],
    ["UC-US02", "Đặt phòng theo ngày (Daily)", "Chọn khoảng thời gian ngày nhận phòng (Check-in) và ngày trả phòng (Check-out) để đặt phòng."],
    ["UC-US03", "Đặt phòng theo giờ (Hourly)", "Chọn ngày cụ thể và chọn các block slots khung giờ còn trống trong ngày để đặt phòng ngắn hạn."],
    ["UC-US04", "Chat với trợ lý AI", "Khung chat AI đặt phòng tự động phân tích ý định, hiển thị roomCards và bookingForm điền sẵn giúp đặt nhanh."],
    ["UC-US05", "Quét mã VietQR đóng thanh toán", "Hệ thống tự động tính tiền và sinh mã QR động chứa số tài khoản, số tiền và nội dung đơn hàng."],
    ["UC-US06", "Tải lên minh chứng chuyển khoản", "Tải ảnh bill chụp giao dịch thành công để chuyển đơn hàng sang trạng thái chờ duyệt, ngắt tự động hủy."]
];

const children = [
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 200, after: 100 },
        children: [
            new TextRun({
                text: "BẢNG MÔ TẢ TÓM TẮT HỆ THỐNG USE CASE",
                bold: true,
                size: 32 // 16pt
            })
        ]
    }),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 100, after: 300 },
        children: [
            new TextRun({
                text: "DỰ ÁN WEBSITE ĐẶT LỊCH & QUẢN LÝ HOMESTAY SELF CHECK-IN TÍCH HỢP AI",
                bold: true,
                size: 26 // 13pt
            })
        ]
    }),
    new Paragraph({
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Tài liệu này cung cấp 3 bảng mô tả ngắn gọn toàn bộ các Use Case được thể hiện trong các sơ đồ: Use Case tổng quát, Use Case phân hệ Admin và Use Case phân hệ Khách hàng.",
                italic: true
            })
        ]
    }),

    // Usecase 1
    new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun("1. Bảng mô tả danh sách Use Case Tổng quát")]
    }),
    new Paragraph({
        spacing: { before: 80, after: 120 },
        children: [new TextRun("Thể hiện sự tương tác của hai Actor chính (Khách hàng và Quản trị viên / Nhân viên) đối với toàn bộ các tính năng lớn của hệ thống:")]
    }),
    createTable(headers1, rows1, widths1),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 360 },
        children: [
            new TextRun({
                text: "Bảng 1: Danh sách mô tả Use Case tổng quát hệ thống",
                bold: true,
                italic: true,
                size: 22
            })
        ]
    }),

    // Usecase 2
    new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun("2. Bảng mô tả danh sách Use Case Phân hệ Quản trị (Admin)")]
    }),
    new Paragraph({
        spacing: { before: 80, after: 120 },
        children: [new TextRun("Mô tả chi tiết các Use Case chức năng phục vụ cho công tác điều hành, quản trị danh mục, phân quyền và giám sát cấu hình trợ lý AI:")]
    }),
    createTable(headers2, rows2, widths2),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 360 },
        children: [
            new TextRun({
                text: "Bảng 2: Danh sách mô tả Use Case phân hệ quản trị Admin",
                bold: true,
                italic: true,
                size: 22
            })
        ]
    }),

    // Usecase 3
    new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun("3. Bảng mô tả danh sách Use Case Phân hệ Khách hàng (User Web)")]
    }),
    new Paragraph({
        spacing: { before: 80, after: 120 },
        children: [new TextRun("Mô tả chi tiết các Use Case chức năng phục vụ cho khách hàng thực hiện tìm kiếm phòng trống, đặt phòng, thanh toán online và tương tác với chatbot AI:")]
    }),
    createTable(headers3, rows3, widths3),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Bảng 3: Danh sách mô tả Use Case phân hệ khách hàng User",
                bold: true,
                italic: true,
                size: 22
            })
        ]
    })
];

doc.addSection({
    properties: {
        page: {
            size: {
                width: 11906, // A4
                height: 16838
            },
            margin: {
                top: 1440,
                right: 1440,
                bottom: 1440,
                left: 1440
            }
        }
    },
    children: children
});

Packer.toBuffer(doc).then(buffer => {
    const outputPath = path.join("c:", "Users", "admin", "Documents", "VS Tím", "web_homestay", "web_homestay", "Báo cáo", "dac_ta_usecase_updated.docx");
    fs.writeFileSync(outputPath, buffer);
    console.log("Document generated successfully at:", outputPath);
}).catch(err => {
    console.error("Error generating document:", err);
});

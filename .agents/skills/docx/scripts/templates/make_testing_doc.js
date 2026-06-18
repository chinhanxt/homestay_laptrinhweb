const { Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell, ShadingType, WidthType, BorderStyle, HeadingLevel, AlignmentType } = require('docx');
const fs = require('fs');
const path = require('path');

const doc = new Document({
    styles: {
        default: {
            document: {
                run: {
                    font: "Times New Roman",
                    size: 26 // 13pt
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
    sections: []
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
        new TableRow({
            children: headers.map((h, i) => makeCell(h, true, colWidths[i], "center"))
        }),
        ...rows.map((row, rIdx) => new TableRow({
            children: row.map((cellText, cIdx) => {
                const alignment = (cIdx === 0 || cIdx === row.length - 1) ? "center" : "both";
                const cellObj = makeCell(cellText, false, colWidths[cIdx], alignment);
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

// 6.3 Table Data (Exactly 5 test cases)
const headers63 = ["Mã test", "Tính năng kiểm thử", "Dữ liệu đầu vào tiêu biểu", "Kết quả mong đợi"];
const widths63 = [1100, 2400, 2500, 3026];
const rows63 = [
    ["TC01", "Đăng nhập hệ thống", "Email: admin@homestay.com, mật khẩu đúng", "Đăng nhập thành công, truy cập trang quản trị."],
    ["TC02", "Tra cứu phòng trống", "Chọn chi nhánh A, ngày 18/06 đến 19/06", "Hiển thị chính xác danh sách các phòng còn trống."],
    ["TC03", "Đặt phòng & Thanh toán", "Chọn phòng 101, nhấn tiến hành thanh toán", "Tạo đơn hàng PendingPayment và sinh VietQR động."],
    ["TC04", "Tương tác với Trợ lý AI", "Chat: \"Tôi muốn đặt phòng ở Quận 1\"", "AI tự đề xuất thẻ phòng trống và điền biểu mẫu đặt."],
    ["TC05", "Duyệt đơn trên Ma trận phòng", "Admin chọn phòng màu cam, click 'Duyệt'", "Cập nhật trạng thái Confirmed và đổi màu ô phòng."]
];

// 6.4 Consolidated Results Table Data (5 Rows)
const headers64 = ["Mã test", "Mô tả kịch bản kiểm thử", "Kết quả mong đợi", "Kết quả thực tế"];
const widths64 = [1200, 3300, 3126, 1400];
const rows64 = [
    ["TC01", "Kiểm tra đăng nhập tài khoản quản trị.", "Đăng nhập thành công, vào đúng Dashboard.", "Đạt (Pass)"],
    ["TC02", "Kiểm tra tìm kiếm phòng trống theo ngày/giờ.", "Hiển thị đúng danh sách phòng trống thời gian thực.", "Đạt (Pass)"],
    ["TC03", "Kiểm tra luồng đặt phòng và tạo VietQR động.", "Đơn hàng được lưu, hiển thị đúng mã QR thanh toán.", "Đạt (Pass)"],
    ["TC04", "Kiểm tra chatbot AI nhận diện ý định và đề xuất.", "AI tư vấn đúng phòng khả dụng, điền sẵn form đặt.", "Đạt (Pass)"],
    ["TC05", "Kiểm tra duyệt đơn thanh toán trên ma trận Matrix.", "Database cập nhật Confirmed, ô phòng Matrix đổi màu.", "Đạt (Pass)"]
];

const children = [
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 200, after: 100 },
        children: [
            new TextRun({
                text: "KẾT QUẢ KIỂM THỬ HỆ THỐNG",
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
                text: "WEBSITE ĐẶT LỊCH & QUẢN LÝ HOMESTAY SELF CHECK-IN TÍCH HỢP AI",
                bold: true,
                size: 26 // 13pt
            })
        ]
    }),
    new Paragraph({
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Tài liệu này tổng hợp bộ kịch bản kiểm thử tinh gọn cho 5 chức năng cốt lõi (6.3) và kết quả thực tế tương ứng (6.4) của hệ thống WebHomestay.",
                italic: true
            })
        ]
    }),

    // Section 6.3
    new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun("6.3 Bộ test case cho 5 chức năng")]
    }),
    new Paragraph({
        spacing: { before: 80, after: 120 },
        children: [new TextRun("Bảng kịch bản kiểm thử (Test Cases) cho 5 tính năng chính của hệ thống WebHomestay:")]
    }),
    createTable(headers63, rows63, widths63),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 360 },
        children: [
            new TextRun({
                text: "Bảng 6.1: Bộ kịch bản kiểm thử cho 5 chức năng chính",
                bold: true,
                italic: true,
                size: 22
            })
        ]
    }),

    // Section 6.4
    new Paragraph({
        heading: HeadingLevel.HEADING_1,
        children: [new TextRun("6.4 Kết quả kiểm thử")]
    }),
    new Paragraph({
        spacing: { before: 80, after: 120 },
        children: [new TextRun("Bảng tổng hợp kết quả thực hiện kiểm thử thực tế đối với các kịch bản trên:")]
    }),
    createTable(headers64, rows64, widths64),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Bảng 6.2: Kết quả thực hiện kiểm thử thực tế",
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
    const outputPath = path.join("c:", "Users", "admin", "Documents", "VS Tím", "web_homestay", "web_homestay", "Báo cáo", "kiem_thu_he_thong_rut_gon.docx");
    fs.writeFileSync(outputPath, buffer);
    console.log("Testing document generated successfully at:", outputPath);
}).catch(err => {
    console.error("Error generating document:", err);
});

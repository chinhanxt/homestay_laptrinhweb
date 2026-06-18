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
            }
        ]
    },
    sections: []
});

const border = { style: BorderStyle.SINGLE, size: 4, color: "CCCCCC" };
const borders = { top: border, bottom: border, left: border, right: border };

// Helper to create cells
function makeCell(text, isHeader = false, width = 5000, align = "both", isBoldText = false) {
    return new TableCell({
        borders: borders,
        width: { size: width, type: WidthType.DXA },
        shading: isHeader ? { fill: "D5E8F0", type: ShadingType.CLEAR } : undefined,
        margins: { top: 120, bottom: 120, left: 150, right: 150 },
        children: [
            new Paragraph({
                alignment: align === "center" ? AlignmentType.CENTER : AlignmentType.BOTH,
                spacing: { before: 40, after: 40 },
                children: [
                    new TextRun({
                        text: text,
                        bold: isHeader || isBoldText,
                        size: 24 // 12pt inside table
                    })
                ]
            })
        ]
    });
}

function createFeaturesTable(headers, rows, colWidths) {
    const tableRows = [
        // Header Row
        new TableRow({
            children: headers.map((h, i) => makeCell(h, true, colWidths[i], "center"))
        }),
        // Data Rows
        ...rows.map((row, rIdx) => {
            const isSubHeader = row[0] !== "";
            return new TableRow({
                children: row.map((cellText, cIdx) => {
                    const alignment = (cIdx === 0 && cellText !== "") ? "center" : "both";
                    const isBoldText = (cIdx === 0 || cIdx === 1);
                    const cellObj = makeCell(cellText, false, colWidths[cIdx], alignment, isBoldText);
                    
                    // Style subheaders or alternating zebra shading
                    if (isSubHeader && cIdx === 0) {
                        cellObj.shading = { fill: "EAEAEA", type: ShadingType.CLEAR };
                    } else if (rIdx % 2 === 1) {
                        cellObj.shading = { fill: "F9F9F9", type: ShadingType.CLEAR };
                    }
                    return cellObj;
                })
            });
        })
    ];

    return new Table({
        width: { size: 9026, type: WidthType.DXA },
        columnWidths: colWidths,
        rows: tableRows
    });
}

const headers = ["Phân hệ", "Chức năng hệ thống", "Mô tả chi tiết tính năng"];
const colWidths = [2000, 2500, 4526];

const rows = [
    // Phân hệ Khách hàng
    ["User Web\n(Khách hàng)", "Tra cứu phòng trống (Ngày/Giờ)", "Tìm phòng trống theo chi nhánh, ngày hoặc giờ và tiện ích."],
    ["", "Đặt phòng & Thanh toán QR", "Giữ chỗ tạm thời và thanh toán nhanh qua VietQR động."],
    ["", "Self Check-in / Check-out", "Tự nhận phòng (tải CCCD) và trả phòng trực tuyến."],
    ["", "Chatbot AI tư vấn tự động", "Tư vấn phòng trống và chốt đặt phòng tự động qua chat."],
    
    // Phân hệ Quản trị
    ["Admin Panel\n(Quản trị)", "Ma trận vận hành phòng (Matrix)", "Giám sát trạng thái phòng thời gian thực và điều phối phòng."],
    ["", "Quản lý đặt phòng (Duyệt nhanh)", "Quản lý lịch đặt, kiểm duyệt ảnh bill và ảnh CCCD."],
    ["", "CRUD danh mục hệ thống", "Quản lý chi nhánh, phòng vật lý, loại phòng và giá lễ."],
    ["", "Phân quyền nhân sự song song", "Phân quyền nhân viên theo vai trò hoặc ghi đè cá nhân."],
    ["", "AI Brain Center & FAQ RAG", "Quản lý tri thức RAG, đồ thị suy luận và cấu hình chatbot."],
    ["", "Thống kê doanh thu & Báo cáo", "Thống kê doanh thu, tỷ lệ phòng và xuất file Excel/CSV."]
];

const children = [
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 200, after: 100 },
        children: [
            new TextRun({
                text: "DANH MỤC MÔ TẢ CHỨC NĂNG HỆ THỐNG",
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
                text: "WEBSITE ĐẶT LỊCH & QUẢN LÝ HOMESTAY SELF CHECK-IN",
                bold: true,
                size: 26 // 13pt
            })
        ]
    }),
    new Paragraph({
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Bảng dưới đây tổng hợp và mô tả ngắn gọn các phân hệ chức năng chính được phân rã của hệ thống WebHomestay.",
                italic: true
            })
        ]
    }),
    createFeaturesTable(headers, rows, colWidths),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Bảng 1: Bảng mô tả chức năng của hệ thống WebHomestay",
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
    const outputPath = path.join("c:", "Users", "admin", "Documents", "VS Tím", "web_homestay", "web_homestay", "Báo cáo", "mo_ta_chuc_nang_he_thong_rut_gon.docx");
    fs.writeFileSync(outputPath, buffer);
    console.log("Features document generated successfully at:", outputPath);
}).catch(err => {
    console.error("Error generating document:", err);
});

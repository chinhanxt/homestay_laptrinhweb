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

function createAbbreviationsTable(headers, rows, colWidths) {
    const tableRows = [
        new TableRow({
            children: headers.map((h, i) => makeCell(h, true, colWidths[i], "center"))
        }),
        ...rows.map((row, rIdx) => new TableRow({
            children: row.map((cellText, cIdx) => {
                const alignment = cIdx === 0 ? "center" : "both";
                const isBoldText = cIdx === 0;
                const cellObj = makeCell(cellText, false, colWidths[cIdx], alignment, isBoldText);
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

const headers = ["Ký hiệu viết tắt", "Tên đầy đủ / Thuật ngữ gốc", "Ý nghĩa / Diễn giải"];
const colWidths = [2000, 3000, 4026];

const rows = [
    ["LLM", "Large Language Model", "Mô hình ngôn ngữ lớn (lõi xử lý ngôn ngữ tự nhiên của chatbot, mặc định dùng Llama-3.3-70b-versatile)."],
    ["RAG", "Retrieval-Augmented Generation", "Tạo sinh tăng cường bằng truy xuất tri thức (cơ chế tra cứu tri thức FAQ và nội quy homestay)."],
    ["ORM", "Object-Relational Mapping", "Kỹ thuật ánh xạ cơ sở dữ liệu quan hệ sang đối tượng (được triển khai qua Entity Framework Core)."],
    ["MVC", "Model - View - Controller", "Mô hình kiến trúc phần mềm phân tầng dùng để xây dựng phân hệ Web Khách hàng và Admin Panel."],
    ["EF Core", "Entity Framework Core", "Thư viện ORM chính thức của Microsoft dành cho nền tảng .NET để giao tiếp PostgreSQL."],
    ["DBMS", "Database Management System", "Hệ quản trị cơ sở dữ liệu (hệ thống lưu trữ dữ liệu tập trung PostgreSQL)."],
    ["RBAC", "Role-Based Access Control", "Cơ chế kiểm soát quyền truy cập hệ thống dựa trên vai trò nhân sự (Manager, Staff, Cleaner)."],
    ["JSON", "JavaScript Object Notation", "Định dạng trao đổi dữ liệu gọn nhẹ (dùng lưu trữ cấu hình AI và ma trận quyền trong DB)."],
    ["HTTP / HTTPS", "Hypertext Transfer Protocol", "Giao thức truyền dữ liệu an toàn giữa Client và Server qua môi trường Web."],
    ["ERD", "Entity Relationship Diagram", "Sơ đồ quan hệ thực thể dùng để thiết kế kiến trúc các bảng dữ liệu PostgreSQL."],
    ["CRUD", "Create - Read - Update - Delete", "Bốn thao tác cơ bản tác động lên dữ liệu: Thêm, Đọc, Sửa, Xóa."],
    ["TTL", "Time-To-Live", "Thời gian tồn tại tối đa của dữ liệu trong bộ nhớ đệm Cache (như AIBookingSessionState)."],
    ["UI / UX", "User Interface / User Experience", "Thiết kế giao diện người dùng và nâng cao trải nghiệm khách hàng tương tác."],
    ["API", "Application Programming Interface", "Giao diện lập trình ứng dụng dùng kết nối các thành phần hệ thống và dịch vụ ngoài."],
    ["OTP", "One-Time Password", "Mật mã khóa số dùng một lần, tự động sinh cấp cho khách hàng để tự nhận phòng (Self Check-in)."],
    ["IoT", "Internet of Things", "Vạn vật kết nối (sử dụng kết nối điều khiển hệ thống khóa cửa thông minh Smart Lock)."]
];

const children = [
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 200, after: 100 },
        children: [
            new TextRun({
                text: "BẢNG CÁC TỪ VIẾT TẮT TRONG BÁO CÁO",
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
                text: "Tài liệu này định nghĩa các thuật ngữ viết tắt và ký hiệu kỹ thuật được sử dụng phổ biến trong toàn bộ nội dung báo cáo đồ án WebHomestay.",
                italic: true
            })
        ]
    }),
    createAbbreviationsTable(headers, rows, colWidths),
    new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 120, after: 240 },
        children: [
            new TextRun({
                text: "Bảng 1: Bảng ký hiệu viết tắt của báo cáo đồ án",
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
    const outputPaths = [
        path.join("c:", "Users", "admin", "Documents", "VS Tím", "web_homestay", "web_homestay", "Báo cáo", "bang_viet_tat.docx"),
        path.join("c:", "Users", "admin", "Documents", "VS Tím", "web_homestay", "web_homestay", "Báo cáo", "bang_viet_tat_updated.docx")
    ];
    outputPaths.forEach(outputPath => {
        try {
            fs.writeFileSync(outputPath, buffer);
            console.log("Abbreviations document generated successfully at:", outputPath);
        } catch (err) {
            console.warn(`Could not write to ${outputPath} (might be locked by MS Word):`, err.message);
        }
    });
}).catch(err => {
    console.error("Error generating document:", err);
});

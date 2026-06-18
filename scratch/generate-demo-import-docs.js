const {
    Document,
    Packer,
    Paragraph,
    TextRun,
    Table,
    TableRow,
    TableCell,
    WidthType,
    BorderStyle,
    ShadingType,
    AlignmentType,
    HeadingLevel,
} = require("docx");
const fs = require("fs");
const path = require("path");

const repoRoot = process.cwd();
const outputRoot = path.join(repoRoot, "docs", "demo-import-word");
const assetRoot = path.join(outputRoot, "assets-room-import");

const pageWidth = 12240;
const pageHeight = 15840;
const margin = 900;
const contentWidth = pageWidth - margin * 2;

const border = { style: BorderStyle.SINGLE, size: 2, color: "D0D7E2" };
const borders = { top: border, bottom: border, left: border, right: border };

function ensureDir(dir) {
    fs.mkdirSync(dir, { recursive: true });
}

function makeText(text, opts = {}) {
    return new TextRun({
        text,
        bold: opts.bold ?? false,
        italics: opts.italics ?? false,
        color: opts.color ?? "1F2A44",
        size: opts.size ?? 22,
        font: "Arial",
    });
}

function paragraph(text, opts = {}) {
    return new Paragraph({
        heading: opts.heading,
        alignment: opts.alignment ?? AlignmentType.LEFT,
        spacing: opts.spacing ?? { after: 120 },
        children: [makeText(text, opts.run ?? {})],
    });
}

function bullet(text) {
    return new Paragraph({
        bullet: { level: 0 },
        spacing: { after: 60 },
        children: [makeText(text)],
    });
}

function makeCell(text, width, options = {}) {
    return new TableCell({
        width: { size: width, type: WidthType.DXA },
        borders,
        shading: options.header
            ? { fill: "E8F0FE", type: ShadingType.CLEAR }
            : options.alt
                ? { fill: "F8FAFC", type: ShadingType.CLEAR }
                : undefined,
        margins: { top: 90, bottom: 90, left: 120, right: 120 },
        children: [
            new Paragraph({
                alignment: options.align ?? AlignmentType.LEFT,
                spacing: { after: 20 },
                children: [
                    makeText(text, {
                        bold: options.header ?? false,
                        size: options.header ? 20 : 19,
                        color: options.header ? "163B65" : "243247",
                    }),
                ],
            }),
        ],
    });
}

function createTable(headers, rows, widths) {
    return new Table({
        width: { size: contentWidth, type: WidthType.DXA },
        columnWidths: widths,
        rows: [
            new TableRow({
                children: headers.map((header, idx) =>
                    makeCell(header, widths[idx], { header: true, align: AlignmentType.CENTER })),
            }),
            ...rows.map((row, rowIndex) =>
                new TableRow({
                    children: row.map((cell, idx) =>
                        makeCell(String(cell ?? ""), widths[idx], {
                            alt: rowIndex % 2 === 1,
                            align: idx === 0 ? AlignmentType.CENTER : AlignmentType.LEFT,
                        })),
                })),
        ],
    });
}

function buildDoc(config) {
    return new Document({
        numbering: {
            config: [
                {
                    reference: "bullets",
                    levels: [
                        {
                            level: 0,
                            format: "bullet",
                            text: "•",
                            alignment: AlignmentType.LEFT,
                            style: { paragraph: { indent: { left: 720, hanging: 360 } } },
                        },
                    ],
                },
            ],
        },
        styles: {
            default: {
                document: {
                    run: { font: "Arial", size: 22, color: "243247" },
                    paragraph: { spacing: { after: 100 } },
                },
            },
            paragraphStyles: [
                {
                    id: "Heading1",
                    name: "Heading 1",
                    basedOn: "Normal",
                    next: "Normal",
                    quickFormat: true,
                    run: { font: "Arial", size: 30, bold: true, color: "163B65" },
                    paragraph: { spacing: { before: 160, after: 160 }, outlineLevel: 0 },
                },
                {
                    id: "Heading2",
                    name: "Heading 2",
                    basedOn: "Normal",
                    next: "Normal",
                    quickFormat: true,
                    run: { font: "Arial", size: 24, bold: true, color: "1F2A44" },
                    paragraph: { spacing: { before: 120, after: 120 }, outlineLevel: 1 },
                },
            ],
        },
        sections: [
            {
                properties: {
                    page: {
                        size: { width: pageWidth, height: pageHeight },
                        margin: { top: margin, right: margin, bottom: margin, left: margin },
                    },
                },
                children: [
                    new Paragraph({
                        alignment: AlignmentType.CENTER,
                        spacing: { after: 120 },
                        children: [makeText("WEB HOMESTAY", { bold: true, size: 18, color: "C18A3B" })],
                    }),
                    new Paragraph({
                        heading: HeadingLevel.HEADING_1,
                        alignment: AlignmentType.CENTER,
                        spacing: { after: 120 },
                        children: [makeText(config.title, { bold: true, size: 32, color: "163B65" })],
                    }),
                    new Paragraph({
                        alignment: AlignmentType.CENTER,
                        spacing: { after: 220 },
                        children: [makeText(config.subtitle, { italics: true, size: 20, color: "5F6F86" })],
                    }),
                    paragraph("Mục đích", { heading: HeadingLevel.HEADING_2 }),
                    ...config.purpose.map((item) => bullet(item)),
                    paragraph("Cột dữ liệu chuẩn", { heading: HeadingLevel.HEADING_2, spacing: { before: 160, after: 120 } }),
                    createTable(
                        ["STT", "Tên cột", "Bắt buộc", "Ví dụ / Ghi chú"],
                        config.columns.map((col, idx) => [idx + 1, col.name, col.required, col.note]),
                        [700, 2500, 1200, contentWidth - 700 - 2500 - 1200]
                    ),
                    paragraph("Dữ liệu mẫu để nhập", { heading: HeadingLevel.HEADING_2, spacing: { before: 180, after: 120 } }),
                    createTable(config.sampleHeaders, config.sampleRows, config.sampleWidths),
                    paragraph("Lưu ý import", { heading: HeadingLevel.HEADING_2, spacing: { before: 180, after: 120 } }),
                    ...config.notes.map((item) => bullet(item)),
                ],
            },
        ],
    });
}

const docs = [
    {
        file: "01-mau-chi-nhanh.docx",
        title: "Mẫu Chi Nhánh",
        subtitle: "Dùng để demo import danh sách chi nhánh homestay",
        purpose: [
            "Import hàng loạt chi nhánh mới bằng file Excel.",
            "Tên chi nhánh và địa chỉ là hai trường bắt buộc.",
            "Lead time đặt phòng tính theo giờ, nếu để trống hệ thống vẫn nhận mặc định.",
        ],
        columns: [
            { name: "Name", required: "Có", note: "Tên chi nhánh, không được trùng với chi nhánh đang hoạt động." },
            { name: "Address", required: "Có", note: "Địa chỉ hiển thị ngoài website." },
            { name: "Description", required: "Không", note: "Mô tả ngắn về phong cách hoặc khu vực." },
            { name: "Hotline", required: "Không", note: "Ví dụ: 0901000011." },
            { name: "Email", required: "Không", note: "Ví dụ: q1@lumistay.vn." },
            { name: "MapUrl", required: "Không", note: "Link Google Maps hoặc bản đồ nội bộ." },
            { name: "BookingLeadTimeHours", required: "Không", note: "Số giờ cần đặt trước, ví dụ 2 hoặc 3." },
        ],
        sampleHeaders: ["Name", "Address", "Description", "Hotline", "Email", "MapUrl", "BookingLeadTimeHours"],
        sampleRows: [
            ["LumiStay Phú Nhuận", "18 Hoa Sứ, Phú Nhuận, TP.HCM", "Chi nhánh gần sân bay, tiện cho khách công tác.", "0901000011", "phunhuan@lumistay.vn", "https://maps.google.com/?q=18+Hoa+Su+Phu+Nhuan", "2"],
            ["LumiStay Gò Vấp", "125 Quang Trung, Gò Vấp, TP.HCM", "Phù hợp khách đi ngắn ngày và cặp đôi.", "0901000012", "govap@lumistay.vn", "https://maps.google.com/?q=125+Quang+Trung+Go+Vap", "3"],
            ["LumiStay Thủ Đức", "9 Đặng Văn Bi, TP. Thủ Đức", "Không gian yên tĩnh cho khách ở linh hoạt theo giờ.", "0901000013", "thuduc@lumistay.vn", "https://maps.google.com/?q=9+Dang+Van+Bi+Thu+Duc", "2"],
        ],
        sampleWidths: [1800, 2200, 2000, 1200, 1900, 1900, 1360],
        notes: [
            "Cột Name không được trùng với chi nhánh đang tồn tại và chưa xóa mềm.",
            "Nếu BookingLeadTimeHours để trống, nên nhập 2 để demo đồng nhất.",
            "Email và MapUrl không bắt buộc nhưng có thì màn admin nhìn đầy đủ hơn.",
        ],
    },
    {
        file: "02-mau-phong-ngu.docx",
        title: "Mẫu Phòng Ngủ",
        subtitle: "Dùng để demo import phòng bằng Excel hoặc ZIP kèm ảnh",
        purpose: [
            "Import danh sách phòng theo đúng tên chi nhánh đã có trong hệ thống.",
            "Có thể upload file .xlsx hoặc file .zip chứa Excel cộng thư mục ảnh.",
            "AvatarFile và AdditionalFiles chỉ map được khi tên file ảnh trong ZIP khớp đúng nội dung trong Excel.",
        ],
        columns: [
            { name: "Name", required: "Có", note: "Tên phòng, không được trùng trong cùng một chi nhánh." },
            { name: "Description", required: "Không", note: "Mô tả ngắn để AI và trang phòng dùng lại." },
            { name: "PricePerHour", required: "Có", note: "Giá giờ cơ bản, dạng số." },
            { name: "PricePerDay", required: "Có", note: "Giá theo ngày, dạng số." },
            { name: "ExtraGuestFee", required: "Có", note: "Phụ thu khách vượt sức chứa chuẩn." },
            { name: "PriceWeekendPerHour", required: "Có", note: "Nếu không khác ngày thường vẫn nhập bằng PricePerHour." },
            { name: "PriceWeekendPerDay", required: "Có", note: "Nếu không khác ngày thường vẫn nhập bằng PricePerDay." },
            { name: "PriceHolidayPerHour", required: "Có", note: "Giá lễ theo giờ." },
            { name: "PriceHolidayPerDay", required: "Có", note: "Giá lễ theo ngày." },
            { name: "Capacity", required: "Có", note: "Sức chứa chuẩn." },
            { name: "MaxGuests", required: "Có", note: "Số khách tối đa." },
            { name: "Status", required: "Không", note: "Nên dùng Available để demo." },
            { name: "BranchName", required: "Có", note: "Phải khớp tuyệt đối tên chi nhánh có trong hệ thống." },
            { name: "AvatarFile", required: "Không", note: "Ví dụ: room-main-01.jpg." },
            { name: "AdditionalFiles", required: "Không", note: "Ngăn cách bằng dấu phẩy, ví dụ: room-extra-01.jpg, room-extra-02.jpg." },
        ],
        sampleHeaders: ["Name", "Description", "PricePerHour", "PricePerDay", "ExtraGuestFee", "PriceWeekendPerHour", "PriceWeekendPerDay", "PriceHolidayPerHour", "PriceHolidayPerDay", "Capacity", "MaxGuests", "Status", "BranchName", "AvatarFile", "AdditionalFiles"],
        sampleRows: [
            ["Lụa Mây PN-201", "Phòng sáng, nhẹ nhàng, phù hợp cặp đôi hoặc khách công tác.", "160000", "1100000", "100000", "185000", "1260000", "210000", "1450000", "2", "4", "Available", "LumiStay Phú Nhuận", "room-main-01.jpg", "room-extra-01.jpg, room-extra-02.jpg"],
            ["Hoàng Kim GV-501", "Phòng rộng, cao cấp, hợp khách nghỉ dưỡng ngắn ngày.", "280000", "2100000", "150000", "320000", "2450000", "360000", "2780000", "4", "6", "Available", "LumiStay Gò Vấp", "room-main-02.jpg", "room-extra-03.jpg, room-extra-04.jpg"],
            ["An Nhiên TD-101", "Phòng tiêu chuẩn, sạch đẹp, tối ưu đặt nhanh theo giờ.", "120000", "780000", "80000", "140000", "900000", "155000", "990000", "2", "3", "Available", "LumiStay Thủ Đức", "room-main-03.png", "room-extra-05.jpg"],
        ],
        sampleWidths: [1050, 1600, 740, 820, 760, 860, 920, 860, 920, 620, 650, 760, 1100, 980, 1322],
        notes: [
            "Khi demo ZIP, nén cùng cấp: file Excel và thư mục ảnh. Không để sai tên file ảnh.",
            "BranchName phải tồn tại trước, nên import Chi nhánh trước rồi mới import Phòng.",
            "Status để trống thì hệ thống vẫn tự dùng Available, nhưng demo nên điền rõ.",
        ],
    },
    {
        file: "03-mau-nhan-vien.docx",
        title: "Mẫu Nhân Viên",
        subtitle: "Dùng để demo import tài khoản quản lý và nhân sự vận hành",
        purpose: [
            "Import nhanh nhiều tài khoản nhân viên bằng Excel.",
            "Mật khẩu mặc định sau import là 123456.",
            "Permissions có thể để trống hoặc nhập danh sách key, ngăn cách bằng dấu phẩy.",
        ],
        columns: [
            { name: "Username", required: "Có", note: "Không được trùng tài khoản đang hoạt động." },
            { name: "FullName", required: "Có", note: "Tên hiển thị trong hệ thống." },
            { name: "Role", required: "Có", note: "Chỉ nhận SuperAdmin, Manager, Staff." },
            { name: "BranchName", required: "Không", note: "Nếu có thì phải khớp tên chi nhánh." },
            { name: "Permissions", required: "Không", note: "Ví dụ: bookings.view, matrix.view, chat.reply." },
        ],
        sampleHeaders: ["Username", "FullName", "Role", "BranchName", "Permissions"],
        sampleRows: [
            ["pn.manager", "Nguyễn Minh Phúc", "Manager", "LumiStay Phú Nhuận", "matrix.view, bookings.view, bookings.detail, rooms.view, chat.view, chat.reply"],
            ["gv.staff01", "Trần Mỹ Linh", "Staff", "LumiStay Gò Vấp", "matrix.view, bookings.view, bookings.detail, chat.view, chat.reply"],
            ["thuduc.admin", "Phạm Quốc An", "Manager", "LumiStay Thủ Đức", "matrix.view, bookings.view, branches.view, rooms.view, statistics.view, chat.view"],
        ],
        sampleWidths: [1700, 2200, 1200, 2400, contentWidth - 1700 - 2200 - 1200 - 2400],
        notes: [
            "Role nhập sai một ký tự cũng sẽ bị từ chối import.",
            "Nếu BranchName để trống thì tài khoản sẽ không gắn cố định vào chi nhánh nào.",
            "Permissions là danh sách key thật, ngăn cách bằng dấu phẩy, không cần nhập true/false.",
        ],
    },
    {
        file: "04-ngan-hang-tri-thuc-ai.docx",
        title: "Ngân Hàng Tri Thức AI",
        subtitle: "Dùng để demo import tri thức cho AI Brain Center",
        purpose: [
            "Import nhanh dữ liệu FAQ, chính sách và tri thức vận hành cho AI.",
            "Nếu Scope chưa có, hệ thống sẽ tự tạo scope mới khi import.",
            "Mỗi dòng tri thức có thể sinh embedding để dùng cho tìm kiếm ngữ nghĩa.",
        ],
        columns: [
            { name: "ScopeName", required: "Có", note: "Tên phạm vi tri thức, ví dụ Public Booking hoặc Cancellation." },
            { name: "Title", required: "Có", note: "Tiêu đề ngắn gọn, dễ tra cứu." },
            { name: "Content", required: "Có", note: "Nội dung tri thức chính." },
            { name: "Tags", required: "Không", note: "Ví dụ: dat-phong, theo-gio, uu-dai." },
            { name: "Priority", required: "Không", note: "Số càng cao càng ưu tiên, ví dụ 1-5." },
            { name: "IsActive", required: "Không", note: "Yes hoặc No. Mặc định nếu để trống là Yes." },
        ],
        sampleHeaders: ["ScopeName", "Title", "Content", "Tags", "Priority", "IsActive"],
        sampleRows: [
            ["Public Booking", "Đặt phòng theo giờ", "Khách có thể đặt theo giờ nếu phòng còn slot hợp lệ theo dữ liệu thật của room_slot_inventories.", "dat-phong, theo-gio, slot", "5", "Yes"],
            ["Cancellation", "Yêu cầu hủy cần ảnh xác nhận", "Khi gửi yêu cầu hủy, khách cần cung cấp ảnh xác nhận email hoặc bằng chứng hợp lệ để nhân viên duyệt nhanh.", "huy-don, refund, xac-nhan", "4", "Yes"],
            ["Operations", "Lead time chi nhánh", "Mỗi chi nhánh có thể có lead time riêng, nếu không có sẽ dùng lead time toàn hệ thống.", "lead-time, chi-nhanh", "3", "Yes"],
        ],
        sampleWidths: [1700, 1600, 3400, 1400, 800, 1060],
        notes: [
            "IsActive nhận Yes hoặc No, nếu để trống hệ thống hiểu là đang bật.",
            "Content nên viết đủ ý vì AI sẽ dùng nội dung này để trả lời hoặc suy luận.",
            "Tags không bắt buộc nhưng nên thêm để đội vận hành dễ lọc sau này.",
        ],
    },
    {
        file: "05-mau-khung-gio.docx",
        title: "Mẫu Khung Giờ",
        subtitle: "Dùng để demo import template khung giờ vận hành",
        purpose: [
            "Import các mẫu slot theo giờ hoặc theo khung cố định.",
            "Có thể dùng SeedStartTime cho slot lăn theo chu kỳ, hoặc FixedStart/FixedEnd cho slot cố định.",
            "Mã code của mẫu không được trùng với template đang tồn tại.",
        ],
        columns: [
            { name: "Name", required: "Có", note: "Tên mẫu hiển thị ở admin." },
            { name: "Code", required: "Có", note: "Mã định danh duy nhất, ví dụ combo_6h_demo." },
            { name: "DurationMinutes", required: "Có", note: "Thời lượng chính, đơn vị phút." },
            { name: "CleanupMinutes", required: "Không", note: "Thời gian dọn phòng giữa hai lượt." },
            { name: "SeedStartTime", required: "Không", note: "Ví dụ 08:00." },
            { name: "FixedStartTime", required: "Không", note: "Ví dụ 22:00 nếu là slot cố định." },
            { name: "FixedEndTime", required: "Không", note: "Ví dụ 06:00 hôm sau nếu là qua đêm." },
            { name: "CrossesMidnight", required: "Không", note: "Yes hoặc No." },
            { name: "IsActive", required: "Không", note: "Yes hoặc No. Mặc định nếu để trống là Yes." },
        ],
        sampleHeaders: ["Name", "Code", "DurationMinutes", "CleanupMinutes", "SeedStartTime", "FixedStartTime", "FixedEndTime", "CrossesMidnight", "IsActive"],
        sampleRows: [
            ["Combo 6 tiếng demo", "combo_6h_demo", "360", "30", "08:00", "", "", "No", "Yes"],
            ["Combo 8 tiếng demo", "combo_8h_demo", "480", "30", "08:00", "", "", "No", "Yes"],
            ["Ca đêm cố định", "fixed_night_demo", "480", "15", "", "22:00", "06:00", "Yes", "Yes"],
        ],
        sampleWidths: [1700, 1400, 1100, 1050, 1100, 1100, 1100, 1100, 810],
        notes: [
            "Nếu dùng slot rolling thì điền SeedStartTime, còn slot cố định thì điền FixedStartTime và FixedEndTime.",
            "CrossesMidnight chỉ nên để Yes khi khung giờ băng qua ngày hôm sau.",
            "Code là unique key, demo nên thêm hậu tố _demo để tránh đụng dữ liệu thật.",
        ],
    },
    {
        file: "06-mau-ngay-le.docx",
        title: "Mẫu Ngày Lễ",
        subtitle: "Dùng để demo import ngày lễ phục vụ ma trận giá và doanh thu",
        purpose: [
            "Import nhanh các ngày lễ để hệ thống áp dụng logic giá lễ.",
            "Ngày chỉ nhận một lần duy nhất, trùng ngày sẽ bị báo lỗi.",
            "Date nên nhập theo chuẩn yyyy-MM-dd để dễ kiểm soát khi demo.",
        ],
        columns: [
            { name: "Date", required: "Có", note: "Ngày lễ, dạng yyyy-MM-dd hoặc ô date thật trong Excel." },
            { name: "Description", required: "Không", note: "Mô tả như Lễ Quốc Khánh, Tết Dương Lịch." },
        ],
        sampleHeaders: ["Date", "Description"],
        sampleRows: [
            ["2026-09-02", "Quốc khánh 2/9"],
            ["2026-12-31", "Đêm cuối năm - áp giá cao điểm"],
            ["2027-01-01", "Tết Dương Lịch"],
        ],
        sampleWidths: [2200, contentWidth - 2200],
        notes: [
            "Không nhập giờ trong cột Date, chỉ dùng ngày.",
            "Nếu Excel đang format kiểu text, vẫn nên nhập chuẩn yyyy-MM-dd.",
            "Mẫu này nhỏ nhất nhưng rất hữu ích để demo thay đổi giá cuối tuần và ngày lễ.",
        ],
    },
];

async function writeDoc(config) {
    const doc = buildDoc(config);
    const buffer = await Packer.toBuffer(doc);
    const outputPath = path.join(outputRoot, config.file);
    fs.writeFileSync(outputPath, buffer);
    return outputPath;
}

async function main() {
    ensureDir(outputRoot);
    ensureDir(assetRoot);

    for (const config of docs) {
        await writeDoc(config);
    }
}

main().catch((error) => {
    console.error(error);
    process.exit(1);
});

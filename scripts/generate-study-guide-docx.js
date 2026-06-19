const fs = require("fs");
const path = require("path");
const {
  AlignmentType,
  BorderStyle,
  Document,
  Footer,
  Header,
  HeadingLevel,
  LevelFormat,
  Packer,
  PageNumber,
  Paragraph,
  ShadingType,
  Table,
  TableCell,
  TableRow,
  TextRun,
  WidthType,
} = require("docx");

const outputPath = path.resolve(__dirname, "..", "docs", "WebHomestay-HocNhanh-DuAn.docx");

const pageWidth = 11906;
const pageHeight = 16838;
const pageMargin = 900;
const contentWidth = pageWidth - pageMargin * 2;
const columnWidths = [1400, 1800, 2600, 2800, 1506];

const border = { style: BorderStyle.SINGLE, size: 1, color: "D9D9D9" };
const borders = { top: border, bottom: border, left: border, right: border };

function text(text, options = {}) {
  return new TextRun({
    text,
    font: options.font || "Arial",
    size: options.size || 22,
    bold: options.bold || false,
    color: options.color,
    italics: options.italics || false,
  });
}

function para(content, options = {}) {
  const children = Array.isArray(content) ? content : [text(content, options.textOptions)];
  return new Paragraph({
    children,
    alignment: options.alignment,
    spacing: options.spacing || { after: 120 },
    heading: options.heading,
    border: options.border,
    pageBreakBefore: options.pageBreakBefore || false,
    numbering: options.numbering,
  });
}

function cell(paragraphs, width, shading) {
  return new TableCell({
    width: { size: width, type: WidthType.DXA },
    borders,
    shading: shading ? { fill: shading, type: ShadingType.CLEAR } : undefined,
    margins: { top: 80, bottom: 80, left: 120, right: 120 },
    children: paragraphs,
    verticalAlign: "center",
  });
}

function simpleParagraphs(lines, options = {}) {
  return lines.map((line, index) =>
    para(line, {
      spacing: { after: index === lines.length - 1 ? 40 : 20 },
      textOptions: {
        size: options.size || 19,
        font: options.font || "Arial",
        bold: options.boldLines?.includes(index) || false,
        color: options.color,
      },
    }),
  );
}

const rows = [
  {
    part: ["Program.cs"],
    role: ["Điểm khởi động app, đăng ký DI, session, DbContext, service AI và auto migrate database."],
    files: ["WebHomestay/Program.cs"],
    flow: ["App start", "-> cấu hình service/session/db", "-> map route MVC", "-> chạy web"],
    answer: ["“Program.cs là nơi boot toàn bộ ứng dụng.”"],
  },
  {
    part: ["Controller"],
    role: ["Nhận request, gọi service xử lý nghiệp vụ rồi trả View hoặc JSON."],
    files: ["WebHomestay/Controllers/", "Ví dụ: AI/AIChatController.cs"],
    flow: ["Người dùng click/gửi form", "-> Controller", "-> Service", "-> View/JSON trả ra"],
    answer: ["“Controller nhận thao tác từ người dùng và điều phối xử lý.”"],
  },
  {
    part: ["Service"],
    role: ["Chứa nghiệp vụ chính: availability, booking, pricing, AI orchestration, permission."],
    files: ["WebHomestay/Services/", "Ví dụ: AvailabilityService.cs", "BookingCreationService.cs"],
    flow: ["Controller gọi Service", "-> Service làm rule/tính toán/truy vấn", "-> trả kết quả cho Controller"],
    answer: ["“Service là nơi xử lý logic chính của hệ thống.”"],
  },
  {
    part: ["View"],
    role: ["Hiển thị giao diện Razor cho user/admin; nhận model hoặc viewmodel từ controller."],
    files: ["WebHomestay/Views/", "wwwroot/js/", "wwwroot/css/"],
    flow: ["Controller return View(model)", "-> Razor render HTML", "-> JS/Bootstrap hoàn thiện tương tác"],
    answer: ["“View là phần giao diện hiển thị dữ liệu cho người dùng.”"],
  },
  {
    part: ["Model / Entity"],
    role: ["Đại diện dữ liệu thật trong hệ thống như phòng, booking, user, AI data."],
    files: ["WebHomestay/Models/"],
    flow: ["Service đọc/ghi entity", "-> DbContext map sang bảng", "-> PostgreSQL lưu dữ liệu"],
    answer: ["“Model đại diện cho dữ liệu và cấu trúc nghiệp vụ.”"],
  },
  {
    part: ["DbContext"],
    role: ["Cầu nối giữa C# entity và PostgreSQL; quản lý DbSet, mapping, quan hệ bảng."],
    files: ["WebHomestay/Data/ApplicationDbContext.cs"],
    flow: ["Service gọi DbContext", "-> LINQ/EF Core truy vấn", "-> dữ liệu đi/đến database"],
    answer: ["“DbContext giúp truy vấn, mapping và lưu dữ liệu xuống database.”"],
  },
  {
    part: ["ViewModel"],
    role: ["Gói đúng dữ liệu cần hiển thị cho một màn hình, tránh đưa nguyên entity ra View."],
    files: ["WebHomestay/Models/", "thường là các class *ViewModel*"],
    flow: ["Controller gom dữ liệu", "-> tạo ViewModel", "-> View render đúng màn hình cần"],
    answer: ["“ViewModel chỉ chứa dữ liệu cần cho một màn hình cụ thể.”"],
  },
  {
    part: ["LINQ"],
    role: ["Cú pháp truy vấn dữ liệu trong C#, dùng nhiều với EF Core để lọc, chọn, sắp xếp."],
    files: ["Hay nằm trong Services/Controllers", "liên quan DbContext và DbSet"],
    flow: ["Service viết LINQ", "-> EF dịch sang SQL", "-> PostgreSQL trả kết quả"],
    answer: ["“LINQ là cách truy vấn và lọc dữ liệu trong C#.”"],
  },
  {
    part: ["Migration"],
    role: ["Biến thay đổi ở model/db mapping thành thay đổi cấu trúc bảng trong database."],
    files: ["WebHomestay/Migrations/", "ApplicationDbContext.cs"],
    flow: ["Sửa model/mapping", "-> dotnet ef migrations add", "-> database update/startup migrate"],
    answer: ["“Migration dùng để tạo hoặc cập nhật cấu trúc database.”"],
  },
  {
    part: ["Booking & Availability"],
    role: ["Tìm phòng trống theo giờ/ngày, kiểm tra rule thời gian, tạo booking và tính giá."],
    files: [
      "Services/AvailabilityService.cs",
      "Services/BookingCreationService.cs",
      "Services/BookingTimeRules.cs",
      "Services/PricingService.cs",
    ],
    flow: ["User chọn thời gian", "-> AvailabilityService tìm phòng", "-> BookingCreationService + PricingService xử lý", "-> lưu booking"],
    answer: ["“Luồng booking thật nằm ở service, không chỉ dựa vào trạng thái phòng.”"],
  },
  {
    part: ["Public AI Chat"],
    role: ["Chat tư vấn đặt phòng công khai; hiểu nhu cầu, quyết định lúc nào show phòng/slot/form."],
    files: [
      "Controllers/AI/AIChatController.cs",
      "Services/AIBrainOrchestrator.cs",
      "Services/IBookingConductor.cs",
    ],
    flow: ["Khách chat /ai", "-> AIChatController", "-> AIBrainOrchestrator", "-> Booking Conductor", "-> trả text + block JSON"],
    answer: ["“AI công khai dùng chung AI Brain nhưng có Booking Conductor để dẫn luồng đặt phòng.”"],
  },
  {
    part: ["Admin AI Brain"],
    role: ["Trung tâm AI quản trị với pipeline Persona, Snapshot, RAG, Graph, Safety, Final Synthesizer."],
    files: [
      "Controllers/AdminAIController.cs",
      "Services/AIBrainOrchestrator.cs",
      "Services/AIModelClient.cs",
      "Views/AdminAI/Index.cshtml",
    ],
    flow: ["Admin vào /admin/ai", "-> Controller", "-> từng agent chạy tuần tự", "-> AIModelClient gọi model", "-> lưu AIConversationTrace"],
    answer: ["“Admin AI Brain là pipeline multi-agent, không phải chatbot đơn giản.”"],
  },
  {
    part: ["Permission / Auth"],
    role: ["Xác thực admin bằng session và kiểm tra quyền theo role template + account override."],
    files: [
      "Filters/AdminAuthorizeAttribute.cs",
      "Services/PermissionResolveService.cs",
      "Models/RolePermissionTemplate.cs",
    ],
    flow: ["Admin đăng nhập", "-> session có AdminUser/AdminRole", "-> filter check quyền", "-> cho/không cho vào action"],
    answer: ["“Dự án dùng session-based admin auth, không phải cookie auth middleware.”"],
  },
];

const tableRows = [
  new TableRow({
    tableHeader: true,
    children: [
      cell(simpleParagraphs(["Phần"], { size: 20, boldLines: [0] }), columnWidths[0], "D8E9F3"),
      cell(simpleParagraphs(["Chức năng"], { size: 20, boldLines: [0] }), columnWidths[1], "D8E9F3"),
      cell(simpleParagraphs(["Folder / File liên quan"], { size: 20, boldLines: [0] }), columnWidths[2], "D8E9F3"),
      cell(simpleParagraphs(["Luồng đi"], { size: 20, boldLines: [0] }), columnWidths[3], "D8E9F3"),
      cell(simpleParagraphs(["Câu trả lời nhanh"], { size: 20, boldLines: [0] }), columnWidths[4], "D8E9F3"),
    ],
  }),
  ...rows.map((row, index) => {
    const shading = index % 2 === 0 ? "F9FCFE" : "FFFFFF";
    return new TableRow({
      children: [
        cell(simpleParagraphs(row.part, { size: 18, boldLines: [0] }), columnWidths[0], shading),
        cell(simpleParagraphs(row.role, { size: 18 }), columnWidths[1], shading),
        cell(simpleParagraphs(row.files, { size: 17, font: "Consolas" }), columnWidths[2], shading),
        cell(simpleParagraphs(row.flow, { size: 17 }), columnWidths[3], shading),
        cell(simpleParagraphs(row.answer, { size: 18, color: "1F4E79" }), columnWidths[4], shading),
      ],
    });
  }),
];

const doc = new Document({
  styles: {
    default: {
      document: {
        run: {
          font: "Arial",
          size: 22,
          color: "1F1F1F",
        },
      },
    },
    paragraphStyles: [
      {
        id: "Heading1",
        name: "Heading 1",
        basedOn: "Normal",
        next: "Normal",
        quickFormat: true,
        run: { size: 32, bold: true, font: "Arial", color: "0F4C5C" },
        paragraph: { spacing: { before: 240, after: 160 }, outlineLevel: 0 },
      },
      {
        id: "Heading2",
        name: "Heading 2",
        basedOn: "Normal",
        next: "Normal",
        quickFormat: true,
        run: { size: 26, bold: true, font: "Arial", color: "2F5D62" },
        paragraph: { spacing: { before: 180, after: 120 }, outlineLevel: 1 },
      },
    ],
  },
  numbering: {
    config: [
      {
        reference: "study-steps",
        levels: [
          {
            level: 0,
            format: LevelFormat.DECIMAL,
            text: "%1.",
            alignment: AlignmentType.LEFT,
            style: { paragraph: { indent: { left: 540, hanging: 240 } } },
          },
        ],
      },
    ],
  },
  sections: [
    {
      properties: {
        page: {
          size: { width: pageWidth, height: pageHeight },
          margin: { top: pageMargin, right: pageMargin, bottom: pageMargin, left: pageMargin },
        },
      },
      headers: {
        default: new Header({
          children: [
            para([text("WEB HOMESTAY | Tài liệu học nhanh", { bold: true, size: 18, color: "5A5A5A" })], {
              alignment: AlignmentType.RIGHT,
              spacing: { after: 80 },
            }),
          ],
        }),
      },
      footers: {
        default: new Footer({
          children: [
            para(
              [
                text("Trang ", { size: 18, color: "5A5A5A" }),
                new TextRun({ children: [PageNumber.CURRENT], size: 18, color: "5A5A5A" }),
              ],
              { alignment: AlignmentType.CENTER, spacing: { after: 0 } },
            ),
          ],
        }),
      },
      children: [
        para([text("WEB HOMESTAY", { size: 34, bold: true, color: "0B3954" })], {
          alignment: AlignmentType.CENTER,
          spacing: { after: 80 },
        }),
        para([text("Tài liệu Word học nhanh để nắm dự án và tìm đúng file khi bị hỏi", { size: 22, color: "3D5A80" })], {
          alignment: AlignmentType.CENTER,
          spacing: { after: 180 },
        }),
        para(
          [
            text("Mục tiêu nhớ nhanh:", { bold: true, size: 20, color: "134074" }),
            text(" biết chức năng nằm ở đâu, file nào cần mở, và luồng request chạy qua đâu.", {
              size: 20,
              color: "134074",
            }),
          ],
          {
            spacing: { after: 100 },
            border: {
              top: { style: BorderStyle.SINGLE, size: 6, color: "98C1D9", space: 1 },
              bottom: { style: BorderStyle.SINGLE, size: 6, color: "98C1D9", space: 1 },
            },
          },
        ),
        para([text("Khung nhớ nhanh", { bold: true, size: 24, color: "2F5D62" })], {
          heading: HeadingLevel.HEADING_1,
        }),
        para("Đi từ ngoài vào trong: route/request -> Controller -> Service -> DbContext -> PostgreSQL -> View/JSON.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Nếu bị hỏi về giao diện, mở Views/ trước rồi xem thêm wwwroot/js/ tương ứng.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Nếu bị hỏi về nghiệp vụ booking, tập trung AvailabilityService, BookingCreationService, BookingTimeRules, PricingService.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Nếu bị hỏi về AI, tách rõ Public AI Chat và Admin AI Brain vì hai luồng dùng chung orchestrator nhưng mục tiêu khác nhau.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Nếu bị hỏi về quyền admin, nhớ session + AdminAuthorize + PermissionResolveService.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para([text("Bảng tra nhanh chức năng", { bold: true, size: 24, color: "2F5D62" })], {
          heading: HeadingLevel.HEADING_1,
        }),
        new Table({
          width: { size: contentWidth, type: WidthType.DXA },
          columnWidths,
          rows: tableRows,
        }),
        para([text("Ghi nhớ khi trả lời nhanh", { bold: true, size: 24, color: "2F5D62" })], {
          heading: HeadingLevel.HEADING_2,
          pageBreakBefore: true,
        }),
        para("Dự án là web homestay self check-in/self check-out, hướng tới vận hành online hoàn toàn.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Stack cốt lõi: ASP.NET Core MVC, PostgreSQL, HTML/CSS/Bootstrap, jQuery.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Hai mảng phải nhớ rõ: booking thật theo availability/slot và AI Assistant theo pipeline nhiều agent.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
        para("Khi bí đường đi, mở Program.cs trước để lần theo DI rồi nhảy sang controller/service tương ứng.", {
          numbering: { reference: "study-steps", level: 0 },
        }),
      ],
    },
  ],
});

fs.mkdirSync(path.dirname(outputPath), { recursive: true });

Packer.toBuffer(doc).then((buffer) => {
  fs.writeFileSync(outputPath, buffer);
  process.stdout.write(`Created ${outputPath}`);
});

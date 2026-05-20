# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quy định bắt buộc: đọc ngữ cảnh đầu tiên
Trước khi thực hiện bất kỳ công việc nào, phải đọc:
- [MANDATORY_CONTEXT.md](MANDATORY_CONTEXT.md)
- [docs/yeucau/TONG_QUAN_DU_AN.md](docs/yeucau/TONG_QUAN_DU_AN.md)

Ngữ cảnh kỹ thuật chính: ASP.NET Core MVC, PostgreSQL, HTML/CSS/Bootstrap/jQuery. Visual Studio là IDE chính để build/run/debug/database; VS Code dùng để chỉnh nhanh UI/JS và làm việc với AI.

## Mục tiêu sản phẩm
Dự án là hệ thống homestay self check-in/self check-out, thay quy trình đặt phòng thủ công qua tin nhắn bằng web tự động. Hai hướng nổi bật cần bảo toàn khi phát triển:
- AI Assistant tư vấn phòng theo nhu cầu, dữ liệu phòng trống thời gian thực, và hướng khách vào luồng đặt phòng chính thức.
- Tutorial Onboarding tương tác kiểu game để khách mới không bị bối rối.

## Lệnh phát triển thường dùng
Repo không có file solution ở root; chạy trực tiếp theo project:
- Build web app: `dotnet build WebHomestay/WebHomestay.csproj`
- Run web app: `dotnet run --project WebHomestay/WebHomestay.csproj`
- Run tests: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj`
- Run one test class/method: `dotnet test WebHomestay.Tests/WebHomestay.Tests.csproj --filter FullyQualifiedName~BookingTimeRulesTests`
- Add EF migration: `dotnet ef migrations add <Name> --project WebHomestay/WebHomestay.csproj`
- Apply EF migrations: `dotnet ef database update --project WebHomestay/WebHomestay.csproj`

Target framework hiện tại là `net10.0`; EF/Npgsql packages đang ở 8.0.0. App tự gọi `Database.Migrate()` khi startup.

## Kiến trúc cấp cao
- [WebHomestay/Program.cs](WebHomestay/Program.cs): cấu hình MVC, Session, PostgreSQL DbContext, DI services, AI model client, background cleanup service, auto-migrate database.
- [WebHomestay/Data/ApplicationDbContext.cs](WebHomestay/Data/ApplicationDbContext.cs): EF Core model mapping, lowercase PostgreSQL table/column names, relationships, AI tables, booking/slot tables.
- [WebHomestay/Controllers/](WebHomestay/Controllers/): ASP.NET MVC controllers. Admin controllers quản lý phòng, chi nhánh, booking, staff, settings, images, AI.
- [WebHomestay/Views/](WebHomestay/Views/): Razor views. Public flow nằm ở Home/Rooms/Bookings; admin UI nằm ở các thư mục Admin*.
- [WebHomestay/Services/](WebHomestay/Services/): domain services cho availability, slot generation/management, booking creation, pricing, settings, mail, image masking, statistics, AI.
- [WebHomestay/Models/](WebHomestay/Models/): EF entities và view models.
- [WebHomestay/wwwroot/](WebHomestay/wwwroot/): CSS/JS tĩnh, gồm UI admin/user và AI Brain Center.
- [WebHomestay.Tests/](WebHomestay.Tests/): xUnit tests dùng EF InMemory cho domain/service/admin view checks.

## Luồng booking và availability
Booking không chỉ dựa vào trạng thái phòng đơn giản. Các service liên quan:
- `AvailabilityService`: xác định phòng trống theo chi nhánh và khoảng thời gian.
- `SlotGenerationService`, `SlotManagementService`: tạo và quản lý inventory slot theo giờ/ngày.
- `BookingCreationService`, `BookingTimeRules`, `PricingService`: tạo booking, kiểm tra thời gian, tính giá.
Khi sửa booking/availability, ưu tiên thêm hoặc cập nhật test trong [WebHomestay.Tests/Services/](WebHomestay.Tests/Services/) hoặc [WebHomestay.Tests/Domain/](WebHomestay.Tests/Domain/).

## Kiến trúc AI chatbot hiện có
AI chatbot hiện nằm trong admin AI Brain Center và đã có hướng multi-agent/RAG/Graph cần giữ nguyên:
- [WebHomestay/Controllers/AdminAIController.cs](WebHomestay/Controllers/AdminAIController.cs): route `/admin/ai`, quản lý knowledge, graph, trace, final synthesizer config, test từng agent, `brain-chat`, `brain-preview`.
- [WebHomestay/Services/AIBrainOrchestrator.cs](WebHomestay/Services/AIBrainOrchestrator.cs): điều phối Persona, Live Snapshot, Knowledge Retrieval, Graph Reasoning, Guard, Final Synthesizer; lưu `AIConversationTrace`.
- [WebHomestay/Services/AIModelClient.cs](WebHomestay/Services/AIModelClient.cs): gọi provider tương thích OpenAI chat completions; hiện hỗ trợ Groq và OpenRouter/9router.
- [WebHomestay/Views/AdminAI/Index.cshtml](WebHomestay/Views/AdminAI/Index.cshtml) và [WebHomestay/wwwroot/js/admin-ai-brain-center.js](WebHomestay/wwwroot/js/admin-ai-brain-center.js): UI quản trị workflow, agent test, knowledge, graph, conversation trace, form designer.
- AI data tables trong DbContext: `AIKnowledgeCollections`, `AIKnowledgeArticles`, `AIBrainScopes`, `AIKnowledgeUnits`, `AIGraphNodes`, `AIGraphEdges`, `AIAgentDefinitions`, `AIConversationTraces`.

Quy tắc khi làm tiếp AI chatbot:
- Không làm mất kiến trúc tham vọng multi-agent + RAG + Graph của người dùng; cải tiến theo hướng hiện thực hóa nó, không thu gọn thành chatbot đơn giản.
- AI không được tự xác nhận booking, không bịa giá/phòng trống, không tạo mã khóa/check-in code; luôn dựa vào live snapshot và hướng khách qua luồng booking chính thức.
- Cấu hình phong cách trả lời và form của Final Synthesizer nằm trong `SystemSettings` (`AIFinalSynthesizerStyle`, `AIFinalSynthesizerFormSchema`, `AIFinalConditionOptions`); tránh hard-code lại nếu có thể cấu hình qua admin UI.
- API key AI nên lấy từ `key.md` ở thư mục cha hoặc cấu hình `AIModel:ApiKey`; không commit secret mới.

## Database và cấu hình
- Connection string mặc định trong [WebHomestay/appsettings.json](WebHomestay/appsettings.json) trỏ PostgreSQL local database `web_homestay`.
- App tự migrate khi chạy, nên thay đổi model có thể ảnh hưởng DB ngay khi startup.
- Khi thêm entity/field mới, cập nhật `ApplicationDbContext` mapping và tạo migration.
- File cấu hình hiện có chứa thông tin local/dev; không thêm secret thật vào repo.

## Quy tắc làm việc trong codebase
- Thay đổi tối thiểu, đúng yêu cầu; không refactor lan rộng hoặc chỉnh format không liên quan.
- Nếu yêu cầu chưa rõ, hỏi lại trước khi sửa.
- Với thay đổi UI/frontend, chạy app và kiểm tra luồng trong trình duyệt trước khi báo hoàn tất; nếu không kiểm tra được UI, nói rõ.
- Với thay đổi backend/domain, chạy test liên quan hoặc toàn bộ `dotnet test` nếu phạm vi ảnh hưởng rộng.
- Không xóa code chết không liên quan; chỉ dọn phần chính thay đổi của mình làm phát sinh.

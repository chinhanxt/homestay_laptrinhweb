# Đặc tả Sản phẩm: AI Brain Center Multi-Agent RAG+Graph

**Ngày:** 2026-05-14  
**Trạng thái:** Brainstorming Spec  
**Phạm vi:** Kiến trúc AI, UI Admin, kế hoạch triển khai  
**Định vị:** Hospitality Brain Center cho hệ thống homestay self check-in

---

## 1. Tầm nhìn sản phẩm

AI Brain Center không phải là một trang cấu hình chatbot thông thường. Đây là trung tâm xây dựng “bộ não vận hành số” cho toàn bộ hệ thống homestay.

Admin không chỉ nhập vài đoạn FAQ cho AI trả lời. Admin đang đưa kiến thức, quy trình vận hành, chính sách, dữ liệu phòng, kinh nghiệm tư vấn và logic chi nhánh vào một hệ thống AI có khả năng phối hợp nhiều agent để hỗ trợ khách hàng theo thời gian thực.

Mục tiêu là biến AI từ một chatbot hỏi đáp thành một hệ thống tư vấn đặt phòng có tư duy:

- Hiểu khách đang muốn gì.
- Biết hệ thống hiện còn phòng nào.
- Biết chính sách, tiện ích, quy định của từng chi nhánh.
- Biết cách tư vấn, so sánh, upsell và chốt phòng.
- Biết khi nào cần dùng dữ liệu thật, khi nào cần dùng kiến thức đã học.
- Có thể giải thích câu trả lời dựa trên nguồn tri thức và trạng thái hệ thống.

---

## 2. Tên concept

Tên đề xuất:

- **AI Brain Center**
- **Hospitality Brain Center**
- **Multi-Agent Hospitality Brain**
- **Branch Intelligence Brain**

Tên hiển thị trong Admin nên dùng:

> **AI Brain Center**  
> Bộ não AI vận hành, tư vấn và hỗ trợ đặt phòng cho toàn hệ thống.

---

## 3. Nguyên tắc thiết kế

### 3.1. Không xem AI là chatbot đơn lẻ

Chatbot chỉ là lớp giao tiếp cuối cùng với khách hàng. Phía sau chatbot là một hệ thống nhiều agent chuyên trách.

### 3.2. Dữ liệu thật là nguồn sự thật

AI không tự đoán phòng trống, giá, trạng thái booking hoặc thông tin vận hành. Những dữ liệu này phải được lấy từ database PostgreSQL thông qua service/backend.

### 3.3. Kiến thức có phạm vi

Mỗi tri thức phải thuộc một scope rõ ràng:

- Toàn hệ thống.
- Theo chi nhánh.
- Theo phòng.
- Theo loại khách hàng.
- Theo mùa, dịp lễ hoặc chiến dịch.

### 3.4. RAG dùng để nhớ, Graph dùng để hiểu quan hệ

- RAG giúp AI tìm lại nội dung liên quan từ kho kiến thức.
- Graph giúp AI hiểu quan hệ giữa chi nhánh, phòng, tiện ích, chính sách, tình huống và hành động.

### 3.5. Multi-agent dùng để chia vai trò tư duy

Mỗi agent có một nhiệm vụ rõ ràng. Không gom toàn bộ logic vào một prompt lớn.

---

## 4. Kiến trúc tổng quan

```text
Customer Message
      ↓
AI A - Orchestrator Agent
      ↓
 ┌───────────────┬────────────────┬────────────────┬────────────────┐
 ↓               ↓                ↓                ↓
AI B             AI C             AI D             AI Guard
Live System      Knowledge Brain  Customer Persona Safety/Policy
Agent            RAG+Graph Agent  Agent            Agent
 ↓               ↓                ↓                ↓
Room/Booking     Knowledge Store   Customer Intent  Rule Check
Database         + Graph Memory    Profile          Validation
 └───────────────┴────────────────┴────────────────┴────────────────┘
      ↓
AI E - Final Response Synthesizer
      ↓
AI Chatbot Response + Suggested Actions
```

---

## 5. Vai trò từng AI Agent

## 5.1. AI A - Orchestrator Agent

### Vai trò

AI A là bộ điều phối trung tâm. Nó không trực tiếp trả lời khách ngay, mà phân tích câu hỏi và chia việc cho các agent khác.

### Nhiệm vụ

- Nhận tin nhắn từ khách hàng.
- Xác định intent chính:
  - Hỏi phòng trống.
  - Hỏi giá.
  - Hỏi tiện ích.
  - Hỏi chính sách.
  - Cần tư vấn chọn phòng.
  - Muốn đặt phòng.
  - Muốn check-in/check-out.
- Xác định cần gọi agent nào.
- Gom kết quả từ các agent.
- Phát hiện mâu thuẫn giữa tri thức và dữ liệu thật.
- Quyết định dữ liệu nào có độ ưu tiên cao hơn.

### Input

- Tin nhắn khách hàng.
- Lịch sử hội thoại.
- Context chi nhánh nếu có.
- Context người dùng nếu đã đăng nhập.

### Output

- Danh sách task cho các agent khác.
- Context tổng hợp cho AI E.

---

## 5.2. AI B - Live System Agent

### Vai trò

AI B là agent nhìn vào hệ thống đang hoạt động. Nó không học kiến thức ngoài, mà tập trung lấy dữ liệu thật từ database và service nội bộ.

### Nhiệm vụ

- Kiểm tra phòng trống theo ngày/giờ.
- Kiểm tra phòng đã đặt.
- Lấy giá phòng.
- Lấy trạng thái phòng.
- Lấy thông tin chi nhánh.
- Lấy tiện ích thực tế của phòng.
- Lấy các booking liên quan nếu khách đã đăng nhập.

### Nguồn dữ liệu

- Rooms.
- Branches.
- Bookings.
- RoomTypes.
- RoomAmenities.
- Pricing rules.
- Availability matrix.

### Output

Dữ liệu có cấu trúc, ví dụ:

```json
{
  "availableRooms": [
    {
      "roomId": 12,
      "name": "Deluxe Balcony 202",
      "branch": "Quận 1",
      "price": 550000,
      "availableFrom": "2026-05-14T18:00:00",
      "availableTo": "2026-05-15T12:00:00",
      "amenities": ["Ban công", "Bồn tắm", "Netflix"]
    }
  ]
}
```

### Nguyên tắc

AI B không được bịa dữ liệu. Nếu không có dữ liệu, trả về trạng thái không đủ thông tin.

---

## 5.3. AI C - Knowledge Brain Agent

### Vai trò

AI C là bộ não tri thức dùng RAG + Graph. Đây là agent học từ kiến thức admin nhập vào.

### Nhiệm vụ

- Tìm kiến thức liên quan đến câu hỏi.
- Hiểu chính sách, quy định, FAQ.
- Tìm tri thức theo scope:
  - Global.
  - Branch.
  - Room.
  - Campaign.
- Kết hợp semantic search với graph relation.
- Trả về nguồn tri thức đã dùng.

### Nguồn dữ liệu

- Knowledge Article.
- Knowledge Collection.
- Branch Knowledge.
- Room Knowledge.
- Policy Node.
- FAQ Node.
- Sales Script Node.
- Graph Edge.

### Graph relation ví dụ

```text
Branch: Quận 1
  ├─ has_room → Deluxe Balcony 202
  ├─ has_policy → Check-in sau 14:00
  ├─ has_amenity → Gần phố đi bộ
  └─ has_sales_script → Phù hợp cặp đôi đi nghỉ cuối tuần
```

### Output

```json
{
  "matchedKnowledge": [
    {
      "title": "Chính sách check-in Quận 1",
      "scope": "Branch",
      "confidence": 0.91,
      "content": "Khách có thể self check-in sau 14:00..."
    }
  ],
  "graphContext": [
    "Chi nhánh Quận 1 có chính sách self check-in sau 14:00",
    "Deluxe Balcony 202 thuộc chi nhánh Quận 1"
  ]
}
```

---

## 5.4. AI D - Customer Persona Agent

### Vai trò

AI D phân tích khách hàng để hiểu nhu cầu thật đằng sau câu hỏi.

### Nhiệm vụ

- Xác định loại khách:
  - Cặp đôi.
  - Nhóm bạn.
  - Gia đình.
  - Người đi công tác.
  - Khách cần riêng tư.
  - Khách nhạy cảm về giá.
- Xác định budget.
- Xác định sở thích.
- Xác định mức độ sẵn sàng đặt phòng.
- Gợi ý chiến thuật tư vấn:
  - Tư vấn nhẹ nhàng.
  - So sánh lựa chọn.
  - Upsell.
  - Chốt booking.

### Output

```json
{
  "persona": "CoupleWeekend",
  "budgetLevel": "Medium",
  "intentStrength": "High",
  "preferredTone": "FriendlyConsultant",
  "salesStrategy": "Recommend cozy private room and mention self check-in convenience"
}
```

---

## 5.5. AI Guard - Safety/Policy Agent

### Vai trò

AI Guard kiểm tra câu trả lời, hành động và chính sách trước khi gửi cho khách.

### Nhiệm vụ

- Không tiết lộ dữ liệu cá nhân khách khác.
- Không xác nhận phòng trống nếu Live System Agent không trả dữ liệu.
- Không hứa giảm giá ngoài chính sách.
- Không đưa thông tin sai về check-in/check-out.
- Không tạo booking nếu thiếu thông tin bắt buộc.
- Kiểm tra câu trả lời có mâu thuẫn với policy không.

### Output

- Approved.
- Needs revision.
- Blocked with reason.

---

## 5.6. AI E - Final Response Synthesizer

### Vai trò

AI E là agent tổng hợp cuối cùng. Nó nhận dữ liệu từ các agent khác và tạo câu trả lời cho AI Chatbot.

### Nhiệm vụ

- Viết câu trả lời tự nhiên.
- Kết hợp dữ liệu phòng thật + tri thức + phong cách tư vấn.
- Đề xuất hành động tiếp theo.
- Nếu phù hợp, tạo suggestion:
  - Xem phòng.
  - Giữ phòng.
  - Đặt ngay.
  - Hỏi thêm giờ nhận phòng.

### Output

```json
{
  "message": "Hiện tại chi nhánh Quận 1 còn phòng Deluxe Balcony 202 phù hợp cho 2 người...",
  "suggestedActions": [
    {
      "type": "VIEW_ROOM",
      "label": "Xem phòng Deluxe Balcony 202",
      "targetId": 12
    },
    {
      "type": "START_BOOKING",
      "label": "Đặt phòng này",
      "targetId": 12
    }
  ],
  "sources": ["LiveSystem", "KnowledgeBrain"]
}
```

---

## 6. RAG + Graph Knowledge Design

## 6.1. Knowledge Unit

Mỗi đơn vị tri thức admin nhập vào không chỉ là text, mà có metadata.

### Fields đề xuất

- `Id`
- `Title`
- `Content`
- `KnowledgeType`
- `ScopeType`
- `ScopeId`
- `Priority`
- `Tags`
- `EffectiveFrom`
- `EffectiveTo`
- `Status`
- `CreatedAt`
- `UpdatedAt`

### KnowledgeType

- FAQ.
- Policy.
- BranchInfo.
- RoomInfo.
- SalesScript.
- CheckInGuide.
- CheckOutGuide.
- Promotion.
- EmergencyInstruction.
- InternalNote.

### ScopeType

- Global.
- Branch.
- Room.
- CustomerSegment.
- Campaign.

---

## 6.2. Graph Node

Graph Node đại diện cho một thực thể hoặc một khái niệm.

### Node types

- System.
- Branch.
- Room.
- RoomType.
- Amenity.
- Policy.
- FAQ.
- Promotion.
- CustomerSegment.
- SalesScript.
- BookingAction.

### Fields đề xuất

- `Id`
- `NodeType`
- `Name`
- `Description`
- `ReferenceType`
- `ReferenceId`
- `MetadataJson`

---

## 6.3. Graph Edge

Graph Edge mô tả quan hệ giữa các node.

### Edge types

- `HAS_ROOM`
- `HAS_POLICY`
- `HAS_AMENITY`
- `HAS_FAQ`
- `APPLIES_TO`
- `RECOMMENDED_FOR`
- `CONFLICTS_WITH`
- `REQUIRES`
- `CAN_TRIGGER_ACTION`

### Ví dụ

```text
Room Deluxe Balcony 202 RECOMMENDED_FOR CoupleWeekend
Branch Quận 1 HAS_POLICY SelfCheckInAfter14h
Policy ExtraGuestFee APPLIES_TO RoomType Family
Promotion WeekendDeal APPLIES_TO Branch Đà Lạt
```

---

## 6.4. Retrieval Flow

```text
User question
  ↓
Intent detection
  ↓
Semantic search trong Knowledge Units
  ↓
Graph traversal theo scope liên quan
  ↓
Rerank theo priority, scope, confidence
  ↓
Trả context cho Knowledge Brain Agent
```

### Quy tắc ưu tiên

1. Room-specific knowledge.
2. Branch-specific knowledge.
3. Campaign-specific knowledge.
4. Global knowledge.
5. Default fallback knowledge.

Nếu có mâu thuẫn, scope nhỏ hơn thắng scope lớn hơn. Ví dụ chính sách riêng của chi nhánh thắng chính sách global.

---

## 7. UI Admin: AI Brain Center

## 7.1. Mục tiêu UI

Trang Admin phải tạo cảm giác đây là trung tâm điều khiển một bộ não AI, không phải form cấu hình đơn giản.

Phong cách đề xuất:

- Bento Grid Premium.
- Dark glassmorphism hoặc light premium tùy theme hiện tại.
- Có trạng thái agent hoạt động.
- Có bản đồ tri thức trực quan.
- Có playground để test ngay.

---

## 7.2. Layout tổng quan

```text
┌──────────────────────────────────────────────────────────────┐
│ AI Brain Center                                              │
│ Bộ não vận hành, tư vấn và hỗ trợ đặt phòng bằng AI           │
├──────────────┬───────────────────────────────┬───────────────┤
│ Brain Scope  │ Brain Workspace               │ Agent Monitor │
│              │                               │               │
│ Global       │ Knowledge / Graph / Rules     │ AI A Online   │
│ Branch Q1    │                               │ AI B Online   │
│ Branch DL    │                               │ AI C Syncing  │
│ Room 202     │                               │ AI D Online   │
├──────────────┴───────────────────────────────┴───────────────┤
│ Testing Playground + Trace Viewer                            │
└──────────────────────────────────────────────────────────────┘
```

---

## 7.3. Các tab chính

### Tab 1: Brain Overview

Hiển thị tổng quan sức khỏe bộ não AI.

#### Cards

- Tổng số knowledge units.
- Số chi nhánh có brain riêng.
- Số phòng có tri thức riêng.
- Số graph nodes.
- Số graph edges.
- Lần đồng bộ gần nhất.
- Độ sẵn sàng của từng agent.

#### Agent status

```text
AI A Orchestrator       Online
AI B Live System        Connected
AI C Knowledge Brain    Indexed
AI D Customer Persona   Online
AI Guard                Active
AI E Synthesizer        Online
```

---

### Tab 2: Knowledge Studio

Nơi admin nhập và quản lý tri thức.

#### Chức năng

- Tạo knowledge article.
- Chọn scope.
- Chọn loại tri thức.
- Gắn tag.
- Gắn priority.
- Chọn ngày hiệu lực.
- Preview cách AI hiểu nội dung.

#### Form đề xuất

```text
Title
Knowledge Type
Scope Type
Scope Target
Priority
Tags
Content Markdown
Effective From / To
Status
```

#### Knowledge examples

- “Chính sách nhận phòng chi nhánh Quận 1”.
- “Script tư vấn phòng cho cặp đôi”.
- “Quy định phụ thu khách thứ 3”.
- “Hướng dẫn self check-in sau 22h”.
- “Lợi thế phòng Deluxe Balcony”.

---

### Tab 3: Graph Builder

Nơi admin nhìn và chỉnh quan hệ tri thức.

#### Chức năng

- Xem node theo loại.
- Tạo relation giữa các node.
- Gợi ý relation tự động từ nội dung knowledge.
- Highlight mâu thuẫn.
- Xem đường suy luận của AI.

#### Ví dụ UI

```text
[Branch: Quận 1] ──HAS_ROOM──> [Room: Deluxe 202]
[Room: Deluxe 202] ──HAS_AMENITY──> [Amenity: Balcony]
[Room: Deluxe 202] ──RECOMMENDED_FOR──> [Customer: Couple]
[Branch: Quận 1] ──HAS_POLICY──> [Policy: Self check-in after 14:00]
```

---

### Tab 4: Agent Command Center

Nơi admin xem vai trò và trạng thái từng agent.

#### Chức năng

- Bật/tắt agent theo môi trường demo.
- Xem system prompt/persona của từng agent.
- Xem agent được phép dùng tool/service nào.
- Xem lần chạy gần nhất.
- Xem lỗi hoặc cảnh báo.

#### Agent cards

- Orchestrator Agent.
- Live System Agent.
- Knowledge Brain Agent.
- Customer Persona Agent.
- Safety/Policy Agent.
- Final Response Synthesizer.

---

### Tab 5: Testing Playground

Nơi admin chat thử với AI và xem trace phía sau.

#### Chức năng

- Nhập câu hỏi như khách hàng.
- Chọn scope test:
  - Toàn hệ thống.
  - Chi nhánh cụ thể.
  - Phòng cụ thể.
- Xem câu trả lời cuối.
- Xem agent trace:
  - AI A gọi agent nào.
  - AI B lấy dữ liệu gì.
  - AI C tìm knowledge nào.
  - AI D phân loại khách ra sao.
  - AI Guard duyệt/chặn gì.
  - AI E tổng hợp thế nào.

#### Trace example

```text
User: Tối nay còn phòng nào riêng tư cho 2 người ở Quận 1 không?

AI A: Intent = Availability + Recommendation
AI B: Found 2 available rooms in Branch Q1
AI C: Retrieved policy SelfCheckInAfter14h + sales script CouplePrivateStay
AI D: Persona = CoupleWeekend, Budget = Unknown
AI Guard: Approved
AI E: Generated final answer with 2 room suggestions
```

---

### Tab 6: Brain Training Queue

Nơi admin xem tri thức nào cần xử lý, đồng bộ hoặc kiểm tra.

#### Trạng thái

- Draft.
- Ready to index.
- Indexed.
- Graph linked.
- Conflict detected.
- Expired.

#### Chức năng

- Re-index knowledge.
- Auto-suggest graph links.
- Resolve conflict.
- Archive old knowledge.

---

## 8. Database Design đề xuất

## 8.1. `AIBrainScope`

Đại diện cho phạm vi bộ não.

```text
Id
Name
ScopeType
ScopeRefId
Description
IsActive
CreatedAt
UpdatedAt
```

### ScopeType

- Global.
- Branch.
- Room.
- Campaign.

---

## 8.2. `AIKnowledgeUnit`

```text
Id
BrainScopeId
Title
Content
KnowledgeType
Priority
TagsJson
EffectiveFrom
EffectiveTo
Status
CreatedAt
UpdatedAt
```

---

## 8.3. `AIGraphNode`

```text
Id
BrainScopeId
NodeType
Name
Description
ReferenceType
ReferenceId
MetadataJson
CreatedAt
UpdatedAt
```

---

## 8.4. `AIGraphEdge`

```text
Id
BrainScopeId
SourceNodeId
TargetNodeId
EdgeType
Weight
MetadataJson
CreatedAt
UpdatedAt
```

---

## 8.5. `AIAgentDefinition`

```text
Id
AgentKey
DisplayName
RoleDescription
SystemInstruction
AllowedToolsJson
IsEnabled
CreatedAt
UpdatedAt
```

---

## 8.6. `AIConversationTrace`

Lưu trace để admin debug.

```text
Id
ConversationId
UserMessage
FinalResponse
SelectedScopeId
TraceJson
CreatedAt
```

---

## 8.7. `AIKnowledgeEmbedding`

Dành cho RAG khi tích hợp thật.

```text
Id
KnowledgeUnitId
ChunkIndex
ChunkText
EmbeddingVector
EmbeddingModel
CreatedAt
```

Giai đoạn đầu có thể chưa cần vector thật. Có thể mock bằng keyword search trước.

---

## 9. Backend Architecture trong ASP.NET Core MVC

## 9.1. Controllers

### `AdminAIBrainController`

Quản lý UI Admin.

Actions đề xuất:

```text
Index()
Overview()
KnowledgeStudio()
GraphBuilder()
AgentCommandCenter()
Playground()
TrainingQueue()
```

### `AIBrainApiController`

API nội bộ cho AJAX/jQuery.

Actions đề xuất:

```text
GetBrainOverview()
GetKnowledgeUnits(scopeId)
SaveKnowledgeUnit(request)
DeleteKnowledgeUnit(id)
GetGraph(scopeId)
SaveGraphNode(request)
SaveGraphEdge(request)
RunPlaygroundTest(request)
GetAgentStatus()
ReindexKnowledge(id)
```

---

## 9.2. Services

### `AIBrainOrchestratorService`

Điều phối flow multi-agent.

### `AILiveSystemAgentService`

Lấy dữ liệu phòng, chi nhánh, booking, giá.

### `AIKnowledgeBrainService`

Search knowledge và graph.

### `AICustomerPersonaService`

Phân tích persona khách hàng.

### `AISafetyGuardService`

Kiểm tra policy và dữ liệu nhạy cảm.

### `AIResponseSynthesizerService`

Tạo câu trả lời cuối.

### `AIBrainTraceService`

Lưu trace cho Admin xem lại.

---

## 9.3. ViewModels

```text
AIBrainOverviewViewModel
AIKnowledgeUnitViewModel
AIGraphNodeViewModel
AIGraphEdgeViewModel
AIAgentStatusViewModel
AIPlaygroundRequestViewModel
AIPlaygroundResultViewModel
AIConversationTraceViewModel
```

---

## 10. Multi-Agent Runtime Flow

## 10.1. Playground flow

```text
Admin nhập message test
  ↓
AIBrainApiController.RunPlaygroundTest
  ↓
AIBrainOrchestratorService.RunAsync
  ↓
AI A phân tích intent
  ↓
Gọi AI B nếu cần dữ liệu live
  ↓
Gọi AI C nếu cần knowledge
  ↓
Gọi AI D để phân tích khách
  ↓
Gọi AI Guard kiểm tra
  ↓
Gọi AI E tổng hợp
  ↓
Lưu trace
  ↓
Trả result về UI
```

---

## 10.2. Customer chatbot flow

```text
Khách gửi tin nhắn
  ↓
ChatController / ChatApiController
  ↓
AIBrainOrchestratorService
  ↓
Multi-agent execution
  ↓
Final response + suggested actions
  ↓
Chat UI hiển thị
```

---

## 11. Giai đoạn triển khai

## Phase 1: UI và mô hình Brain Center

Mục tiêu: Có giao diện Admin nổi bật và dữ liệu quản trị cơ bản.

### Làm

- Tạo trang AI Brain Center trong Admin.
- Tạo các tab:
  - Overview.
  - Knowledge Studio.
  - Agent Command Center.
  - Testing Playground.
- Tạo model/entity cơ bản:
  - AIBrainScope.
  - AIKnowledgeUnit.
  - AIAgentDefinition.
  - AIConversationTrace.
- Playground dùng mock response.

### Thành công khi

- Admin thấy được concept “bộ não AI”.
- Có thể nhập knowledge theo scope.
- Có thể chat thử và thấy agent trace giả lập.

---

## Phase 2: Live System Agent

Mục tiêu: AI B đọc được dữ liệu thật từ hệ thống.

### Làm

- Kết nối AI B với Rooms, Branches, Bookings.
- Tạo service kiểm tra phòng trống.
- Tạo output có cấu trúc cho available rooms.
- Playground hiển thị trace AI B thật.

### Thành công khi

- Câu hỏi “tối nay còn phòng nào ở chi nhánh X?” trả về dữ liệu thật.
- AI không bịa phòng nếu database không có.

---

## Phase 3: Knowledge Brain RAG đơn giản

Mục tiêu: AI C tìm được knowledge liên quan.

### Làm

- Tách content thành chunk.
- Search bằng keyword/full-text trước.
- Gắn scope và priority.
- Trả matched knowledge vào trace.

### Thành công khi

- Admin nhập chính sách chi nhánh.
- Chat test hỏi chính sách.
- AI lấy đúng knowledge theo scope.

---

## Phase 4: Graph Builder

Mục tiêu: Có quan hệ tri thức để AI hiểu cấu trúc hệ thống.

### Làm

- Tạo AIGraphNode.
- Tạo AIGraphEdge.
- UI hiển thị graph dạng đơn giản.
- Cho phép tạo relation thủ công.
- Knowledge Brain dùng graph để bổ sung context.

### Thành công khi

- Admin liên kết Branch → Room → Policy → SalesScript.
- AI trace hiển thị graph path đã dùng.

---

## Phase 5: Multi-Agent thật với LLM

Mục tiêu: Các agent có prompt riêng và phối hợp thật.

### Làm

- Tạo prompt riêng cho từng agent.
- Tích hợp API LLM.
- Thêm prompt caching nếu dùng Claude API.
- Lưu trace từng bước.
- Guard kiểm tra trước khi trả lời.

### Thành công khi

- Một câu hỏi khách chạy qua nhiều agent.
- Admin xem được trace rõ ràng.
- Câu trả lời cuối kết hợp live data + knowledge + persona.

---

## Phase 6: Production Hardening

Mục tiêu: An toàn, ổn định, dễ vận hành.

### Làm

- Phân quyền Admin.
- Kiểm tra dữ liệu nhạy cảm.
- Rate limit chatbot.
- Logging lỗi agent.
- Fallback khi LLM lỗi.
- Versioning knowledge.
- Review conflict knowledge.

### Thành công khi

- Hệ thống có thể demo ổn định.
- Admin debug được vì sao AI trả lời như vậy.
- AI không đưa ra hành động vượt quyền.

---

## 12. Demo Scenario nổi bật

## Scenario 1: Tư vấn phòng theo nhu cầu

### User

> Tối nay mình đi 2 người, muốn phòng riêng tư ở Quận 1, giá vừa phải, có self check-in không?

### Flow

- AI A xác định intent: availability + recommendation + policy.
- AI B tìm phòng trống ở Quận 1 tối nay.
- AI C tìm chính sách self check-in và script tư vấn cặp đôi.
- AI D xác định persona: CoupleWeekend, budget medium.
- AI Guard kiểm tra không bịa giá/phòng.
- AI E trả lời gợi ý 1-2 phòng phù hợp.

### Response mong muốn

AI trả lời như một lễ tân thông minh, có dữ liệu thật, có chính sách đúng và có nút đặt phòng.

---

## Scenario 2: Hỏi chính sách theo chi nhánh

### User

> Chi nhánh Đà Lạt có cho check-in khuya không?

### Flow

- AI A xác định intent: policy lookup.
- AI C tìm policy theo Branch Đà Lạt.
- AI B chỉ được gọi nếu cần dữ liệu chi nhánh.
- AI Guard kiểm tra scope.
- AI E trả lời ngắn gọn.

---

## Scenario 3: Admin kiểm tra vì sao AI trả lời

### Admin

Nhập câu test trong Playground.

### System

Hiển thị:

- Intent.
- Agent đã gọi.
- Dữ liệu phòng đã dùng.
- Knowledge article đã match.
- Graph path.
- Final response.

Điểm nổi bật là admin không chỉ thấy câu trả lời, mà thấy “đường suy nghĩ vận hành” của hệ thống AI.

---

## 13. Rủi ro và cách xử lý

### Rủi ro 1: Multi-agent quá nặng

Cách xử lý: Phase đầu dùng agent giả lập bằng service class và trace mock. Khi UI/logic ổn mới nối LLM.

### Rủi ro 2: AI bịa dữ liệu phòng

Cách xử lý: Live System Agent trả dữ liệu có cấu trúc. Final response chỉ được nói về phòng có trong output.

### Rủi ro 3: Knowledge mâu thuẫn

Cách xử lý: Dùng priority + scope rule. Room > Branch > Campaign > Global.

### Rủi ro 4: Graph UI phức tạp

Cách xử lý: Giai đoạn đầu hiển thị relation dạng list/card trước, chưa cần canvas graph phức tạp.

### Rủi ro 5: Khó demo nếu API AI chưa sẵn sàng

Cách xử lý: Playground có mock multi-agent trace để demo concept trước.

---

## 14. Định vị để trình bày

Câu mô tả ngắn:

> AI Brain Center là bộ não vận hành số cho homestay, kết hợp multi-agent, RAG và knowledge graph để biến dữ liệu phòng, chính sách và kinh nghiệm tư vấn thành một AI có khả năng hỗ trợ khách hàng theo thời gian thực.

Câu nhấn mạnh điểm khác biệt:

> Đây không phải chatbot FAQ. Đây là hệ thống nhiều AI chuyên trách cùng phối hợp: một AI điều phối, một AI đọc dữ liệu vận hành, một AI truy xuất tri thức RAG+Graph, một AI hiểu khách hàng, một AI kiểm tra an toàn và một AI tổng hợp câu trả lời cuối.

---

## 15. Kết luận

AI Brain Center là hướng phát triển nổi bật cho dự án Web Homestay Self Check-in. Nó mở rộng ý tưởng AI Assistant thành một hệ thống thông minh có cấu trúc, có khả năng giải thích, có dữ liệu thật và có tri thức riêng theo từng chi nhánh.

Spec này nên được dùng làm nền để triển khai UI Admin trước, sau đó lần lượt kết nối Live System Agent, Knowledge Brain, Graph Builder và LLM multi-agent runtime.

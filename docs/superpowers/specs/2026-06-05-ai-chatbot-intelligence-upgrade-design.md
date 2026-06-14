# AI Chatbot Intelligence Upgrade Design

## 1. Overview
Nâng cấp hệ thống AI Chatbot của Web Homestay (`AIBrainOrchestrator`) để nhận diện ý định đặt phòng chính xác hơn (Hourly vs Daily) và cải thiện logic trích xuất dữ liệu, tuân thủ phương châm "sử dụng thư viện có sẵn để đỡ phải xây dựng lại". 

## 2. Architecture & Approach
Chúng ta sẽ tiếp tục giữ nguyên cấu trúc đa agent (`AIBrainOrchestrator`, `ContextAwareBookingConductor`) nhưng thay thế các phần rule-based/regex thủ công hiện tại bằng:
- **Entity Extraction**: Tích hợp thư viện open-source `Microsoft.Recognizers.Text` để bóc tách ngày, tháng, thời gian, số lượng.
- **Intent Classification**: Tận dụng trực tiếp model LLM (Gemini 2.5 hoặc Llama 3.3 qua Groq) để phân loại ý định (Hourly/Daily Booking) nhằm đạt độ chính xác (confidence) cao nhất mà không cần tự train model.

## 3. Components

### 3.1. Intent Classifier (LLM-based)
- **Chức năng**: Phân loại tin nhắn của người dùng xem thuộc loại `Hourly_Booking`, `Daily_Booking` hay `Unknown`.
- **Cơ chế**: Thay thế hàm kiểm tra từ khóa tĩnh bằng một API call nhỏ gọi đến LLM với system prompt tối ưu chuyên biệt cho việc phân loại. Yêu cầu LLM trả về JSON định dạng `{ "intent": "Hourly_Booking", "confidence": 0.95 }`.
- **Fallback**: Nếu LLM lỗi hoặc quá tải, rơi về (fallback) dùng Regex (rule-based) cũ.

### 3.2. Entity Extractor (Microsoft.Recognizers.Text)
- **Chức năng**: Lấy thông tin số lượng khách (GuestCount), giờ (Time), ngày tháng (Date), khoảng thời gian (Duration).
- **Thư viện**: Cài đặt NuGet package `Microsoft.Recognizers.Text.DateTime` và `Microsoft.Recognizers.Text.Number`.
- **Lợi ích**: Chuẩn hóa đa ngôn ngữ (hỗ trợ bóc tách mốc thời gian linh hoạt như "ngày mai", "chiều nay", "3 tiếng"). Nếu tiếng Việt không hỗ trợ hoàn hảo, sẽ dùng tiếng Anh làm trung gian qua LLM hoặc hỗ trợ thêm regex phụ.

### 3.3. Booking Conductor & Room Display Logic
- **Cập nhật Session_State**: Dữ liệu trích xuất sẽ được điền vào `AIBookingSessionState` (ví dụ: `BookingMode`, `GuestCount`, `CheckInDate`).
- **Gợi ý phòng**: Dựa vào `Session_State`, Conductor sẽ truy vấn `AvailabilityService` và trả về `UI_Block` (roomCards, hourlySlots) tương ứng theo logic đã định nghĩa trong yêu cầu.

## 4. Error Handling & Fallback
- Nếu LLM không phản hồi hoặc lỗi API, hệ thống sẽ tự động chuyển về chế độ Regex-based cũ trong ContextAwareBookingConductor để không gián đoạn trải nghiệm người dùng.
- Thư viện Entity Extractor được xử lý trong `try/catch` block độc lập, không ném (throw) lỗi ra ngoài luồng chính.

## 5. Security & Dependencies
- Các thư viện mới: `Microsoft.Recognizers.Text.*`. Thư viện thuần C#, không giao tiếp với external server, an toàn 100%.
- API LLM: Vẫn gọi tới Groq/Gemini bằng key lưu tại `key.md`, đảm bảo luồng giao tiếp bảo mật như cũ.

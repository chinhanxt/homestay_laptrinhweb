# Public AI Contextual Room And Slot Gating Design

## Mục tiêu

Nâng cấp chatbot public booking để:

- Hiểu câu khách tự nhiên ngay từ đầu thay vì ép đi theo flow cố định.
- Chỉ hiển thị `roomCards`, `hourlySlots`, CTA đặt phòng khi đã đủ dữ liệu cha-con cần thiết.
- Giải thích rõ sức chứa chuẩn, số khách tối đa, phụ thu vượt chuẩn, giá cuối tuần và ngày lễ.
- Dùng database làm nguồn sự thật cho dữ liệu booking và pricing.
- Đưa toàn bộ policy đang chạy ở UI khách vào admin để cấu hình, kiểm tra và debug được.

## Phạm vi

In scope:

- Public AI chat tại `/ai/chat`.
- Booking conductor cho luồng khách.
- Runtime policy và knowledge guidance trong admin AI/settings.
- Payload backend trả về cho `roomCards`, `hourlySlots`, `bookingSummary`.
- Trace/debug để admin hiểu vì sao bot trả lời như vậy.

Out of scope:

- Thay đổi lớn giao diện room card hiện tại.
- Thay kiến trúc multi-agent thành chatbot đơn giản.
- Để LLM làm nguồn sự thật cho availability hay pricing.

## Vấn đề hiện tại

Luồng public chat hiện hoạt động tốt khi khách làm đúng kịch bản từng bước, nhưng gây khó chịu khi khách mô tả nhu cầu tự nhiên như:

- `tôi muốn tìm phòng ở quận 7 đi 3 người 14-16/6`
- `14/6 còn khung giờ nào`
- `14/6 còn 8-10h không`

Các rủi ro chính:

- Bot hiểu ý nhưng vẫn ép khách quay lại bước đầu.
- `roomCards` và `hourlySlots` có thể bị show không đúng thời điểm.
- Giải thích phụ thu, cuối tuần, lễ chưa được ràng buộc chặt với dữ liệu thật.
- Logic public chat có thể bị nhúng trong code nhưng admin không nhìn và điều chỉnh được.

## Nguyên tắc thiết kế

### 1. Intent-first, flow-second

Bot luôn cố hiểu ý định và dữ liệu từ câu khách trước. Flow từng bước chỉ là fallback khi dữ liệu chưa đủ hoặc ý định còn mơ hồ.

### 2. DB truth, knowledge guidance

Nguồn sự thật:

- `Room.Capacity`
- `Room.MaxGuests`
- `Room.ExtraGuestFee`
- `Room.PriceWeekendPerHour`
- `Room.PriceWeekendPerDay`
- `Room.PriceHolidayPerHour`
- `Room.PriceHolidayPerDay`
- bảng `Holidays`
- availability thực tế từ booking/slot data

Nguồn giải thích:

- `AIKnowledgeUnits`
- prompt/rule trong admin AI

Knowledge được phép giúp bot diễn đạt dễ hiểu hơn, nhưng không được ghi đè dữ liệu thật từ DB.

### 3. Thu gọn theo quan hệ cha-con

Bot phải thu gọn bài toán trước khi show block.

Cây phụ thuộc chuẩn:

- `branch` là cha của `room`
- `room` là cha của `slot`
- `date` hoặc `dateRange` là cha của availability
- `guestCount` là bộ lọc áp lên room và slot

Bot không được show block con nếu node cha chưa đủ rõ.

### 4. Hiển thị block ở bất kỳ turn nào nếu đủ điều kiện

Giữ lại các block cũ (`roomCards`, `hourlySlots`, `bookingSummary`, `bookingForm`, `paymentQr`), nhưng cho phép conductor bắn chúng ra ở bất kỳ bước nào khi state đã đủ.

## Hành vi mong muốn

### A. Câu khách tự nhiên

Ví dụ:

- `tôi muốn tìm phòng ở quận 7 đi 3 người 14-16/6`

Bot sẽ:

- extract khu vực/chi nhánh, số khách, ngày nhận/trả
- merge vào state
- xác định booking mode phù hợp
- nếu đủ dữ liệu tối thiểu thì show `roomCards` ngay
- nếu thiếu đúng một trường thì chỉ hỏi phần thiếu

Không reset khách về bước `bắt đầu tìm phòng`.

### B. Sức chứa và phụ thu

Khi khách vượt `Capacity` nhưng chưa vượt `MaxGuests`:

- vẫn show phòng
- cảnh báo rõ là vượt số khách chuẩn
- nói rõ có phụ thu thêm khách
- phụ thu tính và giải thích theo dữ liệu DB

Khi khách vượt `MaxGuests`:

- loại phòng khỏi danh sách gợi ý
- không cho chọn

### C. Cuối tuần và ngày lễ

Khi thời gian khách chọn rơi vào thứ 7, CN hoặc ngày lễ:

- bot giải thích rõ giá áp dụng khác ngày thường
- giá và label phải dựa trên dữ liệu DB
- nếu knowledge có text guidance thì dùng để diễn đạt mềm hơn, không thay số liệu DB

### D. Thuê giờ

Nếu khách chỉ hỏi:

- `14/6 còn khung giờ nào`

Bot sẽ coi đây là yêu cầu hourly theo ngày:

- cần ít nhất `branch + date + guestCount`
- ưu tiên show `roomCards` trước
- sau khi khách chọn phòng thì show `hourlySlots`

Nếu khách hỏi khung cụ thể:

- `14/6 còn 8-10h không`

Bot sẽ coi đây là yêu cầu hourly có slot cụ thể:

- nếu chưa có `branch/date/guestCount` thì hỏi đúng phần thiếu
- nếu chưa có room cụ thể thì lọc ra các phòng còn đúng khung đó và show `roomCards`
- nếu đã có room trong context thì kiểm tra trực tiếp room đó
- nếu không còn khung giờ này thì phải gợi ý khung gần nhất hoặc phòng khác còn đúng giờ

## Cổng điều kiện hiển thị

### Show `roomCards`

#### Daily booking

Điều kiện tối thiểu:

- `branch`
- `checkInDate`
- `checkOutDate`
- `guestCount`

#### Hourly booking, browse theo ngày

Điều kiện tối thiểu:

- `branch`
- `hourlyDate`
- `guestCount`

#### Hourly booking, hỏi khung cụ thể

Điều kiện tối thiểu:

- `branch`
- `hourlyDate`
- `guestCount`
- optional `requestedTimeRange`

Khi có `requestedTimeRange`, `roomCards` chỉ chứa các phòng còn khả dụng trong khung đó.

### Show `hourlySlots`

Điều kiện tối thiểu:

- `branch`
- `hourlyDate`
- `guestCount`
- `selectedRoomId`

### Show checkout CTA

#### Daily

- `branch`
- `checkInDate`
- `checkOutDate`
- `guestCount`
- `selectedRoomId`
- room còn available

#### Hourly

- `branch`
- `hourlyDate`
- `guestCount`
- `selectedRoomId`
- `selectedSlotId`
- slot còn available

## Thiết kế state

Mở rộng `AIBookingSessionState` hoặc container tương đương để lưu thêm:

- `RequestedTimeStart`
- `RequestedTimeEnd`
- `RequestedTimeLabel`
- `BranchConfidence`
- `NeedsWeekendPricingExplanation`
- `NeedsHolidayPricingExplanation`
- `HasExtraGuestSurcharge`
- `MissingRequiredFields`
- `LastRecommendationReason`
- `ActiveRoomContextId`

Mục tiêu của state mới:

- hiểu khách đang hỏi browse chung hay hỏi một slot cụ thể
- biết còn thiếu field cha nào
- giữ được ngữ cảnh phòng đang bàn tới để các câu như `phòng này ở 4 người được không` vẫn trả lời đúng

## Backend design

### 1. Entity extraction trước khi conductor quyết định

Thêm bước hydrate state từ câu khách:

- branch/branch alias
- guest count
- booking mode
- daily date range
- hourly date
- explicit time range như `8-10h`
- vague time range như `sáng`, `chiều`, `tối`

Kết quả extraction được merge dần vào state, không reset session.

### 2. Gating engine trong conductor

Conductor cần chuyển từ `show hay không show` sang `thiếu cha nào thì hỏi cha đó`.

Pseudo flow:

1. Extract entities
2. Merge state
3. Xác định intent
4. Tính `missing required fields` theo booking mode và intent
5. Nếu còn thiếu:
   - hỏi đúng field thiếu
   - chỉ show block cha phù hợp
6. Nếu đủ:
   - build `roomCards` hoặc `hourlySlots`
7. Trả thêm `response guidance` cho final synthesizer

### 3. Room recommendation result

Backend khi build `roomCards` phải trả thêm ngữ cảnh tư vấn, không chỉ dữ liệu phòng thô.

Mỗi room card nên có thêm:

- `fitsStandardOccupancy`
- `allowsRequestedGuests`
- `extraGuestCount`
- `extraGuestFeeApplied`
- `pricingTierLabel` như `weekday`, `weekend`, `holiday`
- `pricingExplanation`
- `recommendationReason`
- `requestedSlotAvailable` khi khách đang hỏi khung cụ thể

Rule:

- `guestCount > MaxGuests` => loại khỏi list
- `Capacity < guestCount <= MaxGuests` => giữ lại, đánh dấu phụ thu

### 4. Hourly slot search

Khi có `requestedTimeRange`, backend cần một nhánh lọc:

- nếu room chưa được chọn: tìm các room còn khả dụng trong khung đó
- nếu room đã được chọn: kiểm tra đúng room đó

Nếu không còn slot đúng yêu cầu:

- trả guidance để bot nói rõ không còn
- kèm các slot thay thế gần nhất nếu có

### 5. Explanation builder

Tạo một service chuyên dựng câu giải thích booking truth từ DB:

- sức chứa chuẩn / tối đa
- phụ thu thêm khách
- giá weekday / weekend / holiday
- lý do hiện hoặc loại phòng

Service này cấp dữ liệu cho:

- final synthesizer
- `bookingSummary`
- `roomCards`
- trace admin

## Admin design

Nguyên tắc: mọi policy bot đang dùng ở UI khách phải nhìn và chỉnh được từ admin.

### 1. Runtime policy public booking

Admin cần quản được:

- điều kiện tối thiểu để show `roomCards`
- điều kiện tối thiểu để show `hourlySlots`
- rule xử lý `Capacity < guestCount <= MaxGuests`
- rule xử lý `guestCount > MaxGuests`
- rule khi khách hỏi slot cụ thể mà slot không còn
- rule fallback khi giờ mơ hồ như `sáng`, `chiều`, `tối`

Không nhất thiết cho admin chỉnh code-level tree, nhưng UI admin phải hiển thị đúng policy đang chạy.

### 2. Knowledge guidance

Nếu knowledge bank chưa có, cần seed hoặc bổ sung ngay các unit public booking:

- giải thích sức chứa chuẩn và tối đa
- phụ thu vượt số khách chuẩn
- giá cuối tuần
- giá ngày lễ
- nguyên tắc AI tư vấn theo dữ liệu thật
- khi nào bot show phòng trước, khi nào show giờ trước

### 3. Debug and trace

Trace admin cần thấy:

- intent bot hiểu là gì
- entities extract được gì
- field cha nào còn thiếu
- vì sao room được show hoặc loại
- có phụ thu hay không
- có dính weekend/holiday hay không
- requested slot có còn hay không
- vì sao bot show `roomCards` hoặc `hourlySlots`

## Frontend public design

Frontend giữ nguyên tinh thần UI hiện tại, nhưng phải render được data mới.

### 1. Không ràng buộc block vào step đầu

`site.js` phải xử lý block theo payload backend trả về, không giả định người dùng đi từ bước đầu.

### 2. Room card rendering

Room card cũ được giữ lại nhưng thêm các trạng thái hiển thị:

- badge hoặc line `đúng chuẩn số khách`
- line `vượt chuẩn, có phụ thu`
- line `giá cuối tuần áp dụng`
- line `giá ngày lễ áp dụng`
- line `còn khung 8h-10h` khi khách hỏi slot cụ thể

Không hiển thị phòng vượt `MaxGuests`.

### 3. Booking summary rendering

`bookingSummary` cần hiện rõ hơn:

- số khách
- sức chứa chuẩn / tối đa
- phụ thu nếu có
- loại giá đang áp dụng
- lý do gợi ý

## Response behavior

Bot phải trả lời đúng ngữ cảnh hiện tại thay vì một script cứng.

Ví dụ:

- Nếu đủ dữ liệu và có phòng:
  - `Mình hiểu bạn cần phòng ở quận 7 cho 3 người từ 14/6 đến 16/6. Mình gửi các phòng đang phù hợp và lưu ý phòng nào vượt chuẩn sẽ có phụ thu nhé.`
- Nếu khách vượt chuẩn:
  - `Phòng này tiêu chuẩn 2 khách, tối đa 3 khách. Với 3 người vẫn ở được nhưng sẽ có phụ thu thêm khách.`
- Nếu khách hỏi giờ cụ thể nhưng hết:
  - `Khung 8h-10h ngày 14/6 hiện không còn trống. Mình thấy vẫn còn vài lựa chọn gần nhất, mình gửi bạn luôn nhé.`

## Error handling

- Nếu extract không chắc branch: hỏi lại branch thay vì show list sai.
- Nếu date parse mơ hồ: bot xác nhận lại ngắn gọn.
- Nếu availability đổi giữa lúc tư vấn và lúc chọn: bot nói rõ vừa hết chỗ và trả lựa chọn thay thế.
- Nếu knowledge thiếu: vẫn trả lời được từ DB truth.

## Testing

### Unit tests

- entity extraction cho daily natural language
- entity extraction cho hourly specific slot
- gating theo missing parent fields
- lọc room theo `MaxGuests`
- giữ room khi chỉ vượt `Capacity`
- pricing explanation cho weekday/weekend/holiday
- fallback slot suggestions khi slot yêu cầu không còn

### Integration tests

- `/ai/chat` với câu daily đủ dữ liệu -> trả `roomCards`
- `/ai/chat` với câu hourly ngày chung -> trả `roomCards`
- `/ai/chat` với câu hourly slot cụ thể -> trả `roomCards` hoặc `hourlySlots` đúng ngữ cảnh
- chọn room rồi hỏi tiếp `phòng này ở 4 người được không` -> bot bám đúng room context

## Triển khai đề xuất

1. Bổ sung extractor + state fields
2. Bổ sung gating rules trong conductor
3. Bổ sung explanation builder từ DB truth
4. Seed knowledge guidance public booking
5. Mở runtime policy/trace tương ứng ở admin
6. Mở render data mới ở `site.js`
7. Thêm test cho daily/hourly/surcharge/weekend/holiday

## Quyết định đã chốt với user

- Dùng database làm nguồn thật cho phụ thu, sức chứa, weekend và holiday pricing.
- Nếu vượt `Capacity` nhưng chưa vượt `MaxGuests`, vẫn show phòng và cảnh báo phụ thu rõ ràng.
- Nếu vượt `MaxGuests`, loại phòng khỏi gợi ý.
- Với thuê giờ, nếu khách chỉ hỏi ngày thì ưu tiên show phòng trước; nếu khách hỏi khung giờ cụ thể thì AI phải xử lý availability của đúng khung đó.
- Hệ thống phải thu gọn bài toán theo quan hệ cha-con trước khi show block.


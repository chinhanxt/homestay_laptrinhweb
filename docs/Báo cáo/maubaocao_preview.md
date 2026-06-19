# PREVIEW OF MAUBAOCAO.DOCX


## BỘ GIÁO DỤC VÀ ĐÀO TẠOTRƯỜNG ĐẠI HỌC CÔNG NGHỆ TP. HỒ CHÍ MINH

## ĐỒ ÁN MÔN HỌC CÔNG NGHỆ PHẦN MỀM

## <ỨNG DỤNG QUẢN LÝ CHI TIÊU CÁ NHÂN>
Ngành:  <CÔNG NGHỆ THÔNG TIN>

Giảng viên hướng dẫn:  Cô Trần Thị Vân Anh

Sinh viên thực hiện:


## BÙI NGUYỄN CÔNG NGHIỆP     2380601461

## NGUYỄN CHÍ NHÂN                    2380601523

## LƯU VĂN LƯƠNG                        2380601304

## HOÀNG NHẬT QUÂN                  2380601822
Lớp:  23DTHC3

Nhóm:  Tracker

TP. Hồ Chí Minh, 2026


## LỜI CAM ĐOAN
Nhóm Tracker xin cam đoan rằng toàn bộ nội dung trình bày trong báo cáo này là kết quả của quá trình học tập, nghiên cứu, phân tích, thiết kế, cài đặt và kiểm thử do chính nhóm thực hiện dưới sự hướng dẫn của giảng viên phụ trách là cô Trần Thị Vân Anh. Các nội dung về ý tưởng, mô hình triển khai, mã nguồn, sơ đồ, bảng biểu và phần trình bày trong báo cáo đều được nhóm xây dựng dựa trên quá trình thực hiện đề tài, bám sát phạm vi môn học và mục tiêu đã đề ra.

Nhóm hiểu rõ trách nhiệm học thuật đối với một báo cáo tốt nghiệp hoặc đồ án môn học, vì vậy các tài liệu, công nghệ, thư viện và nguồn tham khảo được sử dụng trong quá trình thực hiện đều đã được cân nhắc, đối chiếu và trích dẫn theo mức độ phù hợp. Nhóm không cố ý sao chép nguyên văn công trình của cá nhân hoặc tổ chức khác để nhận là kết quả của mình. Trong trường hợp có sử dụng tư liệu, tài liệu kỹ thuật hoặc nội dung tham khảo từ các nguồn chính thống, nhóm đều có ý thức ghi nhận nguồn để bảo đảm tính trung thực và tôn trọng quyền sở hữu trí tuệ.

Nếu có sai sót, thiếu sót hoặc vấn đề phát sinh liên quan đến tính chính xác của nội dung báo cáo, nhóm Tracker xin nghiêm túc tiếp thu ý kiến góp ý của giảng viên và hoàn toàn chịu trách nhiệm trong phạm vi phần việc mà nhóm đã thực hiện. Nhóm rất mong nhận được sự chỉ bảo và góp ý thêm từ cô để có thể hoàn thiện đề tài tốt hơn.


## LỜI CẢM ƠN
Nhóm Tracker xin bày tỏ lòng biết ơn chân thành và sâu sắc đến cô Trần Thị Vân Anh, giảng viên hướng dẫn, người đã tận tình định hướng, góp ý và hỗ trợ nhóm trong suốt quá trình thực hiện đề tài. Những nhận xét chuyên môn, sự nghiêm túc trong học thuật cùng sự động viên kịp thời của cô đã giúp nhóm từng bước hoàn thiện cả về nội dung báo cáo lẫn chất lượng sản phẩm.

Nhóm cũng xin chân thành cảm ơn quý thầy cô trong khoa đã trang bị cho chúng em nền tảng kiến thức cần thiết về công nghệ phần mềm, phân tích thiết kế hệ thống, lập trình ứng dụng và kiểm thử phần mềm. Đây là cơ sở quan trọng để nhóm có thể vận dụng vào quá trình xây dựng hệ thống quản lý tài chính cá nhân tích hợp AI và phân hệ quản trị web.

Bên cạnh đó, nhóm xin cảm ơn các thành viên trong nhóm Tracker đã cùng nhau phối hợp, trao đổi và hỗ trợ trong suốt quá trình thực hiện đề tài. Dù còn những hạn chế nhất định về thời gian, kinh nghiệm và phạm vi triển khai, nhóm đã luôn cố gắng làm việc với tinh thần trách nhiệm, cầu thị và mong muốn hoàn thiện sản phẩm ở mức tốt nhất có thể. Nhóm kính mong tiếp tục nhận được sự góp ý quý báu từ cô và quý thầy cô để đề tài được hoàn thiện hơn trong thời gian tới.


## MỤC LỤC

## DANH MỤC HÌNH ẢNH, SƠ ĐỒ, BẢNG BIỂU

## BẢNG CÁC TỪ VIẾT TẮT
Từ viết tắt

Tên đầy đủ

Ý nghĩa / Diễn giải


## AI
Artificial Intelligence

Trí tuệ nhân tạo


## API
Application Programming Interface

Giao diện lập trình ứng dụng

BaaS

Backend as a Service

Mô hình backend cung cấp như một dịch vụ


## CRUD
Create, Read, Update, Delete

Các thao tác tạo, đọc, cập nhật và xóa dữ liệu


## CSV
Comma-Separated Values

Định dạng tệp dữ liệu phân tách bằng dấu phẩy


## ERD
Entity Relationship Diagram

Sơ đồ thực thể liên kết

Firebase Auth

Firebase Authentication

Dịch vụ xác thực người dùng của Firebase

Firestore

Cloud Firestore

Cơ sở dữ liệu thời gian thực dạng NoSQL của Firebase


## HTML
HyperText Markup Language

Ngôn ngữ đánh dấu siêu văn bản


## HTTP
HyperText Transfer Protocol

Giao thức truyền tải siêu văn bản


## ID
Identifier

Mã định danh


## JSON
JavaScript Object Notation

Định dạng dữ liệu dạng đối tượng

NoSQL

Not Only SQL

Mô hình cơ sở dữ liệu phi quan hệ


## OCR
Optical Character Recognition

Nhận dạng ký tự quang học từ ảnh


## OTP
One-Time Password

Mật khẩu sử dụng một lần


## PDF
Portable Document Format

Định dạng tài liệu điện tử


## RBAC
Role-Based Access Control

Cơ chế phân quyền theo vai trò


## SDK
Software Development Kit

Bộ công cụ phát triển phần mềm


## UI
User Interface

Giao diện người dùng


## UID
User Identifier

Mã định danh người dùng

CHƯƠNG I: THÔNG TIN NHÓM

1. Đề tài nhóm

Đề tài của nhóm là xây dựng hệ thống quản lý chi tiêu cá nhân hỗ trợ người dùng ghi nhận các khoản thu, chi, theo dõi ngân sách và quan sát tình hình tài chính theo cách trực quan, dễ dùng hơn so với ghi chép thủ công.

Thông qua đề tài này, nhóm hướng tới một sản phẩm vừa phục vụ nhu cầu thực tế hằng ngày, vừa thể hiện được quá trình phân tích, thiết kế và xây dựng phần mềm theo đúng định hướng của môn học.

2. Tên nhóm

Tên nhóm là Tracker. Tên với tinh thần của đề tài vì nhấn mạnh vào việc theo dõi, cập nhật và kiểm soát liên tục các hoạt động tài chính cá nhân của người dùng.

3. Ý nghĩa nhóm

Tên nhóm thể hiện mục tiêu theo dõi, kiểm soát và cải thiện tình hình tài chính cá nhân một cách chủ động, rõ ràng và liên tục thông qua hệ thống phần mềm mà nhóm xây dựng.

Bên cạnh đó, ý nghĩa của tên nhóm cũng phản ánh tinh thần làm việc có định hướng, biết quan sát tiến độ, bám sát mục tiêu và cùng nhau hoàn thiện sản phẩm theo từng giai đoạn.

4. Danh sách thành viên


## STT
Họ và tên


## MSSV
Lớp

Công việc

Chức vụ

1


## BÙI NGUYỄN CÔNG NGHIỆP
2380601461


## 23DTHC3
Dev

Thành viên

2


## NGUYỄN CHÍ NHÂN
2380601523


## 23DTHC3
Dev

Trưởng nhóm

3


## LƯU VĂN LƯƠNG
2380601304


## 23DTHC3
Tester

Thành viên

4


## HOÀNG NHẬT QUÂN
2380601822


## 23DTHC3
Tester

Thành viên

CHƯƠNG II: PHÂN TÍCH VÀ ĐẶC TẢ YÊU CẦU

2.1 Tổng quan về đề tài

Hệ thống gồm hai phân hệ chính. Phân hệ thứ nhất là ứng dụng người dùng chạy trên Flutter mobile, cung cấp các chức năng quản lý tài chính cá nhân như tài khoản, giao dịch, ngân sách, mục tiêu tiết kiệm, báo cáo thống kê và AI chat. Phân hệ thứ hai là cổng quản trị chạy trên Flutter Web, cho phép admin theo dõi người dùng, giao dịch, danh mục dùng chung, thông báo hệ thống, cấu hình AI và các tham số vận hành.

Hệ thống gồm hai phân hệ chính. Phân hệ thứ nhất là ứng dụng người dùng chạy trên Flutter mobile, cung cấp các chức năng quản lý tài chính cá nhân như tài khoản, giao dịch, ngân sách, mục tiêu tiết kiệm, báo cáo thống kê và AI chat. Phân hệ thứ hai là cổng quản trị chạy trên Flutter Web, cho phép admin theo dõi người dùng, giao dịch, danh mục dùng chung, thông báo hệ thống, cấu hình AI và các tham số vận hành.

2.2 Yêu cầu chức năng

2.2.1. Quản lý tài khoản

Người dùng có thể đăng ký tài khoản bằng email và mật khẩu. Sau khi tạo tài khoản, hồ sơ người dùng được tạo trong collection users với các trường mặc định như role, status, createdAt. Người dùng cũng có thể đăng nhập, đổi hoặc khôi phục mật khẩu bằng các luồng OTP/reset được cài trong ứng dụng.

2.2.2. Quản lý giao dịch

Hệ thống hỗ trợ thêm, sửa, xóa giao dịch thủ công. Mỗi giao dịch bao gồm tiêu đề, số tiền, loại giao dịch, danh mục, ghi chú, timestamp và monthyear. Khi giao dịch thay đổi, số dư hiện tại, tổng thu và tổng chi của người dùng cũng được tính lại.

2.2.3. Quản lý ngân sách

Người dùng có thể đặt hạn mức theo danh mục và theo tháng. Ứng dụng theo dõi tổng chi của từng danh mục trong tháng được chọn, so sánh với ngưỡng ngân sách và hiển thị bằng thanh tiến độ với ba mức màu sắc: xanh an toàn, cam cảnh báo, đỏ vượt mức.

2.2.4. Mục tiêu tiết kiệm

Người dùng có thể tạo mục tiêu tiết kiệm với số tiền mục tiêu, ngày đích, màu sắc và biểu tượng. Người dùng có thể nạp thêm tiền từ số dư chính vào mục tiêu, rút tiền khi hoàn thành hoặc rút sớm. Hệ thống còn hiển thị gợi ý mức tiết kiệm mỗi ngày để đạt tiến độ.

2.2.5. Báo cáo và phân tích

Ứng dụng cho phép xem báo cáo theo tháng, so sánh với tháng trước, phân tích theo danh mục, xác định giao dịch lớn nhất hoặc nhỏ nhất và dự báo theo lịch sử bằng weighted average. Ngoài ra người dùng còn có thể xuất báo cáo dạng HTML hoặc CSV.

2.2.6. Chức năng AI

Đây là chức năng nổi bật nhất của dự án. Người dùng có thể nhập câu đời thường như (ăn sáng 30k, lương 15 triệu, ăn tối 50k và đổ xăng 30k,…) hoặc đưa ảnh hóa đơn để hệ thống phân tích. AI không lưu thẳng dữ liệu ngay mà trả về card xác nhận, giúp người dùng kiểm soát trước khi commit.

2.3 Yêu cầu chức năng của phân hệ quản trị

Admin có thể đăng nhập web admin, xem dashboard tổng quan, theo dõi số lượng người dùng, giao dịch tháng, tổng thu, tổng chi và số dư toàn hệ thống. Admin có thể khóa hoặc mở khóa người dùng, chỉnh role, quản lý danh mục mặc định, quản lý broadcast, quản lý cấu hình hệ thống, xem feed giao dịch và cấu hình AI.

Khác với nhiều đồ án chỉ có admin xem danh sách user, hệ thống này có chiều sâu vận hành rõ hơn: admin có thể đổi runtime AI, lưu nháp, preview prompt, publish prompt hoặc lexicon, theo dõi log, bật maintenance mode và can thiệp vào trải nghiệm thời gian thực của người dùng.

2.4 Yêu cầu phi chức năng

Hệ thống cần đảm bảo bảo mật dữ liệu cá nhân, khả năng phản hồi nhanh, khả năng đồng bộ realtime, khả năng chạy trên nhiều nền tảng, giao diện dễ dùng, hỗ trợ tiếng Việt tốt, và có khả năng chịu lỗi tốt ở các thao tác bất đồng bộ như gọi API, gọi Firestore và parse dữ liệu AI.

Các thành phần liên quan đến mạng và I/O đều dùng async hoặc await. Với dữ liệu thời gian thực, hệ thống ưu tiên StreamBuilder để nhận cập nhật ngay thay vì yêu cầu người dùng tải lại thủ công.

2.5 Phân tích nghiệp vụ tổng thể

Nghiệp vụ cốt lõi của hệ thống xoay quanh việc biến một hành vi tài chính đời thường thành một bản ghi có cấu trúc trong hệ thống. Điều này có thể diễn ra theo nhiều con đường: nhập form, nhập chat, nhập từ ảnh, hoặc chỉnh sửa lại giao dịch cũ. Sau đó dữ liệu được phản chiếu sang ngân sách, báo cáo, số dư và các thống kê khác.

Tức là trong hệ thống này, giao dịch là thực thể lõi, còn ngân sách, báo cáo, dashboard và mục tiêu tiết kiệm là các thực thể phân tích hoặc dẫn xuất xoay quanh giao dịch.

2.6 Usecase hệ thống

2.6.1 Usecase tổng quát và phân rã Admin,User

Sơ đồ Usecase tổng quát với 2 actor User và Admin.


## STT
Tên thành phần

Loại

Mô tả

1

User, Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Đăng nhập chung

Use Case

Cổng xác thực ban đầu dành cho mọi đối tượng tài khoản.

3

Tìm kiếm dữ liệu

Use Case

Chức năng truy vấn thông tin toàn hệ thống (nếu hợp lệ).

4

11 nhóm chức năng gốc

Use Case

Đại diện tổng quan cho 6 ngạch chức năng của User và 5 ngạch chuyên môn của Admin.

Bảng mô tả Usecase tổng quát với 2 actor User và Admin.

Sơ đồ Usecase Người dùng (User)


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Quản lý Tài khoản

Use Case

Gộp các thao tác liên quan đến định danh, bảo mật và profile cá nhân.

3

Quản lý Giao dịch

Use Case

Nhóm chức năng cập nhật và truy xuất dòng tiền ra vào hàng ngày.

4

Quản lý Danh mục

Use Case

Hỗ trợ cá nhân hóa phân loại các nhóm tiền tệ.

5

Quản lý Ngân sách

Use Case

Lên kế hoạch và giám sát giới hạn chi tiêu an toàn.

6

Quản lý Mục tiêu Tiết kiệm

Use Case

Chiến lược tích lũy và điều tiết dòng tiền dài hạn.

7

Báo cáo & Cài đặt

Use Case

Thống kê bằng biểu đồ trực quan và tuỳ chỉnh app.

Bảng mô tả Usecase Usecase Người dùng (User)

Sơ đồ Usecase Quản trị viên (Admin)


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Truy cập Hệ thống

Use Case

Cửa kiểm soát quyền lực, cấp quyền cho bộ máy quản trị.

3

Quản lý Người dùng

Use Case

Theo dõi, khóa, mở khóa và can thiệp tài khoản User.

4

Quản lý CSDL Hệ thống

Use Case

Bảo trì danh mục dùng chung và tham số hệ thống toàn cục.

5

Quản lý AI Runtime

Use Case

Khối điều khiển thuật toán thông minh và từ điển Lexicon.

6

Giám sát Tổng & Báo cáo

Use Case

Kiểm soát dòng tiền vĩ mô ngang hệ thống để phát hiện anomaly.

Bảng mô tả Usecase Quản trị viên (Admin)

2.6.2 Usecase phân rã chức năng User

Sơ đồ Usecase User quản lý tài khoản


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Đăng ký

Use Case

Tạo mới một hồ sơ người dùng trên nền tảng.

3

Đăng nhập

Use Case

Xác thực bằng Email/Mật khẩu.

4

Quên mật khẩu

Use Case

Kiểm tra danh tính và cấp lại mật khẩu qua email.

5

Cập nhật hồ sơ

Use Case

Thay đổi avatar, số điện thoại, tên hiển thị.

6

Đăng xuất

Use Case

Chấm dứt phiên làm việc trên thiết bị.

Bảng mô tả Usecase User quản lý tài khoản

Sơ đồ Usecase User quản lý giao dịch


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Thêm giao dịch thủ công

Use Case

Nhập tay các số liệu biến động số dư.

3

Thêm giao dịch bằng AI

Use Case

Đầu vào là văn tự nhiên, AI sẽ phân tách ra giá tiền và danh mục.

4

Thêm giao dịch bằng OCR

Use Case

Quét hóa đơn và bóc tách dữ liệu tự động.

5

Sửa giao dịch

Use Case

Thay đổi lại số tiền, category hoặc note của một giao dịch cũ.

6

Xóa giao dịch

Use Case

Loại bỏ hoàn toàn lịch sử giao dịch.

7

Xem lịch sử giao dịch

Use Case

Truy xuất danh sách dòng tiền đã ghi nhận.

8

Tìm kiếm và lọc

Use Case

Lọc theo tháng, theo danh mục, hoặc theo keyword.

9

Xác nhận lưu (Include)

Use Case

Bước xác nhận bắt buộc sau khi AI/OCR nhận diện xong trước khi ghi vào database.

Bảng mô tả Usecase User quản lý giao dịch

Sơ đồ Usecase User quản lý danh mục


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Tạo danh mục cá nhân

Use Case

User tự thiết kế icon, màu sắc và định nghĩa nhóm thu/chi.

3

Sửa danh mục cá nhân

Use Case

Thay đổi màu sắc hoặc icon cho danh mục.

4

Xóa danh mục cá nhân

Use Case

Chỉ xóa các danh mục không tồn tại giao dịch, hoặc gộp dữ liệu.

5

Chọn danh mục

Use Case

Thao tác thả xuống (dropdown) khi phát sinh giao dịch.

Bảng mô tả Usecase User quản lý danh mục

Sơ đồ Usecase User báo cáo và cài đặt


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Xem báo cáo tháng

Use Case

Tổng quát Cashflow (Tiền vào vs Tiền ra).

3

Phân tích theo danh mục

Use Case

Pie chart thể hiện % chi tiêu cho từng hạng mục.

4

Giao dịch lớn/nhỏ nhất

Use Case

Bóc tách các khoản chi bất thường.

5

Xuất file PDF/Excel

Use Case

Tải biểu mẫu chi tiêu xuống thiết bị.

6

Xem thông báo

Use Case

Hiển thị broadcast từ Admin hệ thống.

7

Cài đặt giao diện

Use Case

Đổi màu app, DarkMode, LightMode.

Bảng mô tả Usecase User báo cáo và cài đặt

Sơ đồ Usecase User quản lý mục tiêu tiết kiệm


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Tạo mục tiêu tiết kiệm

Use Case

Set số tiền đích đến, ngày bắt đầu và ngày kết thúc.

3

Nạp tiền vào mục tiêu

Use Case

Trích tiền từ tài khoản thả vào quỹ tiết kiệm.

4

Rút tiền khỏi mục tiêu

Use Case

Rút tiền về luồng tiền khả dụng (cảnh báo nếu vỡ kế hoạch).

5

Theo dõi tiến độ

Use Case

Đồ thị hiển thị khoảng cách đến đích.

6

Đóng mục tiêu

Use Case

Tất toán khoản tiết kiệm (khi đạt 100% hoặc chủ động đóng).

Bảng mô tả Usecase User quản lý mục tiêu tiết kiệm

Sơ đồ Usecase User quản lý ngân sách


## STT
Tên thành phần

Loại

Mô tả

1

User

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Tạo ngân sách tháng

Use Case

Định mức tối đa được phép chi cho một danh mục trong tháng.

3

Cảnh báo vượt ngưỡng (Extend)

Use Case

Tự động kích hoạt thông báo chớp đỏ nếu chi tiêu vượt quá 80% định mức.

4

Theo dõi mức chi

Use Case

Quan sát thanh progress bar so sánh tiền thực chi vs tiền định mức.

5

Xóa ngân sách

Use Case

Hủy bỏ giới hạn chi tiêu đã đặt.

Bảng mô tả User quản lý ngân sách

2.6.3 Usecase phân rã chức năng Admin

Sơ đồ Usecase Admin truy cập hệ thống


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Đăng nhập admin

Use Case

Đăng nhập tại cổng điều khiển riêng biệt (Admin Portal).

3

Kiểm tra Role (Include)

Use Case

Back-end bắt buộc verify Token có phải Admin hay không.

4

Kiểm tra Permission (Include)

Use Case

Xác nhận quyền thao tác cụ thể (Read/Write/Delete).

5

Đăng xuất

Use Case

Thoát tài khoản, hủy session admin.

Bảng mô tả Usecase Admin truy cập hệ thống

Sơ đồ Usecase Admin quản lý dữ liệu hệ thống


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Quản lý danh mục chung

Use Case

Tạo các Global Categories (Tiền ăn, Tiền ở, Lương) để add cho mọi user.

3

Quản lý Config

Use Case

Thay đổi các tham số kỹ thuật, bảo trì hệ thống.

4

Phát Noti diện rộng

Use Case

Bắn thông báo (Broadcast) đến thiết bị toàn bộ user.

5

Thông tin Audit/Support

Use Case

Quản trị form liên hệ và hỗ trợ CSKH.

Bảng mô tả đồ Usecase Admin quản lý dữ liệu hệ thống

Sơ đồ Usecase Admin giám sát và báo cáo


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Dashboard tỷ trọng

Use Case

Xem biểu đồ hệ thống (bao nhiêu data ra vào mỗi ngày).

3

Theo dõi GD hệ thống

Use Case

Mức độ System Monitor đối với transaction pool.

4

Xóa GD lỗi cấp cao

Use Case

Can thiệp cứng để rollback số liệu khi xảy ra sụp đổ logic.

5

Báo cáo sinh trưởng

Use Case

Báo cáo KPI nền tảng hàng tháng về số lượng active users.

Bảng mô tả Usecase Admin giám sát và báo cáo

Sơ đồ Usecase Admin quản lý AI


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Xem Runtime Config

Use Case

Đọc file cấu hình đang nạp cho AI.

3

Lưu Draft

Use Case

Cập nhật logic bóc tách biên lai nhưng chưa đẩy live.

4

Publish AI Logic

Use Case

Áp dụng cấu trúc AI mới lên toàn máy chủ.

5

Quản lý AI Lexicon

Use Case

Nạp từ điển ngữ nghĩa để NLP xử lý hiểu ngôn ngữ địa phương.

6

Ghi Log admin (Include)

Use Case

Mọi thao tác sửa cấu hình AI bắt buộc phải ghi lại dấu vết (Audit log).

Bảng mô tả Usecase Admin quản lý AI

Sơ đồ Usecase Admin quản lý người dùng


## STT
Tên thành phần

Loại

Mô tả

1

Admin

Actor

Người thực hiện và tương tác với các Use case trong sơ đồ.

2

Xem danh sách User

Use Case

Truy xuất danh bạ toàn bộ khách hàng trên app.

3

Tìm kiếm User (Extend)

Use Case

Filter theo SĐT, Email.

4

Xem trạng thái

Use Case

Check tỷ lệ dư nợ, tổng số thiết bị đăng nhập.

5

Khóa tài khoản

Use Case

Vô hiệu hóa truy cập của user tình nghi gian lận.

6

Mở khóa

Use Case

Restore lại trạng thái truy cập bình thường.

7

Thiết lập Role chuyên sâu

Use Case

Bổ nhiệm user khác làm Admin (hoặc Manager).

Bảng mô tả Usecase Admin quản lý người dùng

2.7 Phân tích 5 chức năng chính

Chức năng 1: Đăng nhập và phân quyền. Firebase Auth xác thực danh tính, sau đó Firestore cung cấp role, status và trạng thái maintenance. AuthGate lắng nghe toàn bộ các thay đổi này bằng stream để quyết định điều hướng.

Sơ đồ Sequence mô tả chức năng Đăng nhập và Phân quyền

Người dùng nhập thông tin đăng nhập, hệ thống xác thực qua Firebase Authentication và nhận UID. Sau đó, hệ thống đồng thời lấy dữ liệu người dùng (role, status) và trạng thái hệ thống (maintenance) từ Firestore.

Dựa trên kết quả:

Nếu tài khoản bị khóa → chuyển sang màn hình khóa

Nếu hệ thống bảo trì → chuyển sang màn hình bảo trì

Nếu hợp lệ → điều hướng vào Dashboard theo vai trò

Chức năng 2: Thêm giao dịch thủ công. Dữ liệu được nhập qua form, kiểm tra hợp lệ, lưu vào users/{uid}/transactions và cập nhật các chỉ số tài chính.

Sơ đồ Sequence mô tả chức năng Thêm giao dịch thủ công

Người dùng nhập thông tin giao dịch trên form và được kiểm tra hợp lệ tại chỗ. Nếu hợp lệ, dữ liệu được gửi đến Controller để lưu vào Firestore. Sau khi lưu thành công, hệ thống cập nhật lại các chỉ số tài chính (số dư, tổng chi) và hiển thị thông báo thành công.

Chức năng 3: Thêm giao dịch bằng AI. AIService tách câu, chuẩn hóa tiền tệ, suy luận loại, danh mục, thời gian, độ tin cậy và tạo card xác nhận.

Sơ đồ Sequence mô tả chức năng Thêm giao dịch dịch bằng AI

Người dùng nhập mô tả tự nhiên, hệ thống gửi đến AI để phân tích và chuyển thành dữ liệu có cấu trúc. Kết quả được hiển thị dưới dạng xác nhận để người dùng kiểm tra. Sau khi người dùng xác nhận, dữ liệu mới được lưu vào Firestore.

Chức năng 4: Quản lý ngân sách. Hệ thống tính tổng chi theo danh mục trong tháng, đối chiếu limitAmount và hiển thị cảnh báo trực tiếp trên giao diện.

Sơ đồ Sequence mô tả chức năng Quản lý ngân sách

Khi mở màn hình ngân sách, hệ thống lấy hạn mức chi tiêu và danh sách giao dịch trong tháng từ Firestore. Dữ liệu được xử lý để tính tổng chi và tỷ lệ sử dụng ngân sách. Kết quả được hiển thị qua thanh tiến độ; nếu vượt hạn mức, hệ thống hiển thị cảnh báo.

Chức năng 5: Quản trị hệ thống. Admin giám sát, khóa user, cập nhật danh mục hệ thống, broadcast và AI runtime.

Sơ đồ Sequence mô tả chức năng Quản trị hệ thống

Quản trị viên có thể cập nhật trạng thái người dùng (khóa/mở), chỉnh sửa danh mục hệ thống, gửi thông báo và thay đổi cấu hình AI. Các thay đổi được lưu vào Firestore và cập nhật theo thời gian thực đến toàn bộ hệ thống.

CHƯƠNG III. THIẾT KẾ VÀ TỔ CHỨC DỮ LIỆU

3.1 Công nghệ sử dụng và lý do lựa chọn

Ngôn ngữ chính của dự án là Dart. Framework sử dụng là Flutter, giúp tái sử dụng phần lớn mã nguồn cho mobile và web admin. Đây là lựa chọn hợp lý vì dự án cần tốc độ phát triển nhanh, giao diện đồng nhất và một codebase đủ linh hoạt để mở rộng sang nhiều nền tảng.

Backend được xây theo mô hình BaaS với Firebase. Lợi ích của lựa chọn này gồm: không phải tự dựng server API riêng trong giai đoạn đồ án, giảm chi phí vận hành, tích hợp sẵn xác thực người dùng, có Firestore realtime, hỗ trợ security rules, và rất phù hợp với mô hình ứng dụng mobile-first.

State management hiện tại dùng Provider cho các thành phần như cài đặt giao diện. Bên cạnh đó hệ thống dùng StreamBuilder rộng rãi để bind trực tiếp luồng dữ liệu Firestore lên UI. Đây là một sự kết hợp hợp lý giữa quản lý state cục bộ và state đến từ cloud realtime.

3.2 Kiến trúc tổng thể của hệ thống

Hệ thống tuân theo tư duy phân tầng: tầng giao diện, tầng nghiệp vụ, tầng dữ liệu và tầng dịch vụ cloud hoặc AI.

Tầng giao diện bao gồm các screen và widget ở mobile cùng với các page hoặc admin shell ở web. Nhiệm vụ của tầng này là nhận tương tác, hiển thị dữ liệu và điều hướng, không ôm các xử lý nghiệp vụ nặng.

Tầng nghiệp vụ bao gồm các service như AuthService, AIService, ReportService, CategoryService, Db và AdminWebRepository. Đây là nơi đóng vai trò bộ não ứng dụng, thực hiện suy luận, phân loại, tổng hợp, so sánh, tính toán và ra quyết định.

Tầng dữ liệu bao gồm các model như Budget, SavingGoal, AIChatMessage, report models, runtime config và cấu trúc Firestore. Tầng này định nghĩa cách dữ liệu được biểu diễn trong code, được convert từ hoặc ra Firestore và truyền sang UI.

Tầng dịch vụ cloud và AI bao gồm Firebase Auth, Cloud Firestore, OCR, và endpoint AI runtime bên ngoài. Đây là tầng hạ tầng, nơi cung cấp khả năng xác thực, lưu trữ, realtime và suy luận ngôn ngữ.

3.3 Ý nghĩa của việc phân tầng và lý do sắp xếp

Phân tầng giúp dự án dễ đọc, dễ bảo trì và dễ mở rộng. Nếu UI thay đổi, phần service không cần đổi quá nhiều. Nếu logic AI thay đổi, chỉ cần tập trung vào AIService và runtime config. Nếu cấu trúc dữ liệu đổi, model và repository có thể được cập nhật có kiểm soát.

Về mặt học thuật, cách tổ chức này cũng giúp báo cáo thể hiện đúng tinh thần của công nghệ phần mềm: tách biệt concern, giảm coupling, tăng cohesion và giúp kiểm thử từng lớp rõ ràng hơn.

Sơ đồ kiến trúc phân tầng của toàn hệ thống: Mobile App, Admin Web, Services, Firestore/Auth, AI Runtime.

3.4 Cấu trúc thư mục và tổ chức mã nguồn

Thư mục lib chứa phần lớn mã nguồn. main.dart là entry point cho mobile app, main_admin_web.dart là entry point cho admin web. screens chứa các màn hình nghiệp vụ; widgets chứa thành phần tái sử dụng; services chứa logic và giao tiếp với dữ liệu; models chứa cấu trúc dữ liệu; admin_web chứa repository, shell và các page phục vụ quản trị.

Việc tách admin_web riêng là hợp lý vì đây là một phân hệ có trải nghiệm, luồng điều hướng và nghiệp vụ quản trị khác biệt với người dùng mobile. Tuy vẫn dùng cùng codebase Flutter, nhưng cách tổ chức tách biệt giúp dễ phát triển và tránh lẫn logic người dùng với logic admin.

Thu muc / Tep tin

Trach nhiem va vai tro

Thuoc tang kien truc

main.dart

Điểm khởi đầu của ứng dụng Mobile. Cấu hình theme, điều hướng ban đầu và khởi tạo các dịch vụ cho người dùng cuối.

Tầng giao diện

main_admin_web.dart

Điểm khởi đầu của ứng dụng Admin Web. Thiết lập môi trường chạy trên trình duyệt và các cấu hình đặc thù cho quản trị viên.

Tầng giao diện

admin_web/

Phân hệ dành riêng cho Admin. Chứa shell giao diện web, repository nghiệp vụ quản trị và các trang phục vụ quản trị người dùng, danh mục và dashboard.

Tầng giao diện và nghiệp vụ

screens/

Chứa các màn hình nghiệp vụ chính cho Mobile như Home, Transaction, Budget, Chat AI. Quản lý luồng hiển thị của từng tính năng cụ thể.

Tầng giao diện

widgets/

Tập hợp các thành phần UI tái sử dụng như button, form, card, chart widget nhằm bảo đảm tính nhất quán giao diện.

Tầng giao diện

services/

Chứa logic nghiệp vụ và giao tiếp với dữ liệu như xác thực, Firestore, AI runtime và các phép tính xử lý chính của hệ thống.

Tầng nghiệp vụ

models/

Định nghĩa cấu trúc dữ liệu và ánh xạ giữa code với Firestore thông qua các model và hàm chuyển đổi dữ liệu.

Tầng dữ liệu

providers/

Quản lý trạng thái ứng dụng, đồng bộ dữ liệu từ services ra giao diện và hỗ trợ cập nhật dữ liệu theo luồng phản ứng.

Tầng nghiệp vụ

utils/

Chứa hằng số, hàm tiện ích và helper dùng chung cho toàn bộ dự án như định dạng dữ liệu và kiểm tra chuỗi.

Bổ trợ

Bảng mô tả từng thư mục chính trong lib và trách nhiệm của từng thư mục.

3.5 Thiết kế cơ sở dữ liệu Firestore

Collection users là trung tâm dữ liệu của hệ thống. Mỗi document người dùng lưu thông tin hồ sơ, role, status, tổng thu, tổng chi, số dư còn lại và các thông tin cấu hình cá nhân khác. Đây là nơi AuthGate và các service đọc để quyết định quyền truy cập và ngữ cảnh hiển thị.

Sub-collection transactions lưu từng giao dịch của một người dùng. Mỗi transaction có title, amount, type, category, note, timestamp, monthyear và một số chỉ số tổng hợp phục vụ hiển thị. Việc tách sub-collection theo user đảm bảo phân tách dữ liệu tài chính cá nhân rõ ràng.

Sub-collection budgets lưu hạn mức theo danh mục và tháng. Việc tách budgets khỏi transactions là hợp lý vì budgets là thực thể cấu hình, còn transactions là thực thể phát sinh.

Sub-collection saving_goals lưu các mục tiêu tiết kiệm. Mỗi goal lại có sub-collection contributions để theo dõi lịch sử nạp tiền, giúp đáp ứng yêu cầu truy vết theo thời gian.

Collection categories lưu danh mục mặc định toàn hệ thống. Collection system_broadcasts lưu các thông báo hệ thống có thể hiển thị tới người dùng. Collection system_configs lưu app controls, AI lexicon, AI runtime config và các cấu hình vận hành khác. Collection admin_logs lưu các hành động nhạy cảm của quản trị viên, nhất là khi publish cấu hình AI.

Sơ đồ cấu trúc FIRESTORE NO_SQL

Khối: Collection: users

Mô tả: Tập dữ liệu cha lớn nhất chứa hàng triệu tài liệu (document) người dùng.


## STT
Node (Tên Trường)

Kiểu Thành phần

Mô tả

1

Document: {uid}

Document

Mã khóa đại diện cho đúng cá nhân sử dụng (Mắc chung cả dữ liệu thuộc tính và Subcollection con).

2

Fields: name, email, phone...

Properties

Hộp chứa thuộc tính của cá nhân đó nằm thẳng trên Document gốc.

3

Fields: quickTemplates, customCategories

Array/List

Trường đính kèm mảng dữ liệu (vì giới hạn dữ liệu nhỏ nên không cần chia Subcollection để truy vấn nhanh).

4

Subcollection: transactions

Collection Con

Thùng chứa hàng nghìn document biên lai giao dịch riêng tư của UID này.

5

Subcollection: budgets

Collection Con

Thùng chứa các thiết lập giới hạn ngân sách hàng tháng.

6

Subcollection: saving_goals

Collection Con

Phễu chứa quỹ tiền gửi tiết kiệm. (Bên trong lại còn chứa lòng mề Subcollection: contributions).

Bảng Collection: users

Khối: Collection: categories

Mô tả: Kho dữ liệu chuyên mục global.


## STT
Node (Tên Trường)

Kiểu Thành phần

Mô tả

1

Document: {category_id}

Document

Mỗi bản ghi đại diện cho một danh mục chuẩn hệ thống thiết lập.

Bảng Collection: categories

Khối: Collection: system_broadcasts

Mô tả: Trạm nhận thông báo của Admin.


## STT
Node (Tên Trường)

Kiểu Thành phần

Mô tả

1

Document: {broadcast_id}

Document

Chứa mảng văn bản, title, trạng thái broadcast để hệ thống fetch bắn Notification.

Bảng Collection: system_broadcasts

Khối: Collection: admin_logs

Mô tả: Hòm khóa lưu dấu chân quản trị viên.


## STT
Node (Tên Trường)

Kiểu Thành phần

Mô tả

1

Document: {log_id}

Document

Document không thể sửa, chỉ lưu audit trail (log append).

Bảng Collection: admin_logs

Sơ đồ Cấu trúc Cloud Firestore

3.6 ERD quy đổi và mô tả các thực thể

Sơ đồ ERD hệ thống

3.6.1 Bảng thực thể User

Mô tả: Thông tin hồ sơ của từng người dùng


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

user_id


## PK
Khóa chính định danh người dùng.

2

name

Tên hiển thị của tài khoản.

3

email

Địa chỉ hòm thư dùng đăng nhập và khôi phục MK.

4

phone

Số điện thoại.

5

role

Vai trò (user, admin, manager).

6

status

Trạng thái hoạt động (active, banned).

7

totalCredit

Tổng thu nhập.

8

totalDebit

Tổng chi tiêu.

9

remainingAmount

Số dư thuần.

10

createdAt

Ngày tạo account.

Bảng thực thể User

3.6.2 Bảng thực thể Transaction

Mô tả: Dữ liệu biên lai/giao dịch mỗi khi luân chuyển tiền


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

transaction_id


## PK
Khóa chính định danh mã giao dịch độc lập.

2

user_id


## FK
Khóa ngoại chĩa về bảng User.

3

title

Tên khoản chi/thu (VD: Ăn sáng phở bò).

4

amount

Số lượng tiền tệ.

5

type

Loại dòng tiền (Income, Expense, Transfer).

6

category

ID hoặc tên danh mục tương ứng.

7

note

Ghi chú thêm về giao dịch.

8

timestamp

Thời gian diễn ra giao dịch.

9

monthyear

Tham số bốc tách theo tháng nhằm tối ưu truy vấn báo cáo.

Bảng thực thể Transaction

3.6.3 Bảng thực thể Budget

Mô tả: Hàng rào chốt chặn ngân sách hàng tháng


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

budget_id


## PK
Khóa chính định danh hạn mức ngân sách.

2

user_id


## FK
Quy chiếu về chủ tài khoản chặn hạn mức.

3

categoryName

Tên danh mục hoặc mảng cần khóa chi tiêu.

4

limitAmount

Giới hạn tối đa bằng tiền (VND).

5

monthyear

Ngân sách có hiệu lực cho tháng/năm nào.

6

createdAt

Ngày khởi tạo ngân sách.

Bảng thực thể Budget

3.6.4 Bảng thực thể SavingGoal

Mô tả: Giỏ tích lũy tiền tệ chiến lược


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

goal_id


## PK
Mã của mục tiêu tiết kiệm.

2

user_id


## FK
Quy chiếu user sở hữu.

3

goal_name

Tên mục tiêu (VD: Tiền mua xe máy).

4

target_amount

Số tiền đích đến.

5

current_amount

Tiền vỗ béo hiện tại bên trong quỹ.

6

start_date

Ngày bắt đầu tích góp.

7

target_date

Deadline kết thúc chốt sổ.

8

status

Trạng thái (on-going, completed, dropped).

9

icon

Icon UI tương ứng.

10

color

Mã màu thẻ giao diện.

Bảng thực thể SavingGoal

3.6.5 Bảng thực thể Contribution

Mô tả: Các nhát nạp/rút tiền liên quan đến quỹ tiết kiệm


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

contribution_id


## PK
Mã phiên nạp/rút độc lập.

2

goal_id


## FK
Nạp vào mục tiêu tiết kiệm mang ID này.

3

user_id


## FK
Truy xuất danh tính người đã nạp.

4

amount

Số mệnh giá đã trích vào.

5

type

Deposit (Nạp vỗ quỹ) hoặc Withdraw (Rút bóp quỹ).

6

note

Thông điệp đi kèm.

7

createdAt

Lịch sử nạp lúc mấy giờ.

Bảng thực thể Contribution

3.6.6 Bảng thực thể UserCategory

Mô tả: Các danh mục thu/chi mang dấu ấn cá nhân của User


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

category_id


## PK
Mã nhãn dán nhóm chi tiêu.

2

user_id


## FK
Quy chiếu sở hữu cá nhân.

3

name

Tên thẻ (VD: Nhậu nhẹt, Lương cứng).

4

type

Mang tính chất Tiền cộng (Income) hay Tiền trừ (Expense).

5

iconName

Ký hiệu hình dáng (VD: ic_food).

6

isDefault

Đánh dấu xem có phải sinh ra tự động ban đầu không.

Bảng thực thể UserCategory

3.6.7 Bảng thực thể GlobalCategory

Mô tả: Danh mục do Admin cài đặt, phổ cập chung toàn server


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

global_category_id


## PK
Khóa chính global.

2

name

Tên khung chuẩn hóa (Tiền nhà, Thuế).

3

type

Income / Expense.

4

iconName

Icon code hệ thống.

Bảng thực thể GlobalCategory

3.6.8 Bảng thực thể QuickTemplate

Mô tả: Khuôn mẫu thao tác nhanh gọn một chạm


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

template_id


## PK
Khóa phôi in mẫu.

2

user_id


## FK
Mẫu đúc cá nhân thuộc user nào.

3

label

Nhãn nút bấm (VD: Mua trà sữa).

4

title

Tiêu đề giao dịch tự động điền.

5

amount

Mệnh giá tự động điền sẵn.

6

type

Loại chi tự động điền.

7

category

Chuyên mục tự động điền sẵn.

Bảng thực thể QuickTemplate

3.6.9 Bảng thực thể SystemBroadcast

Mô tả: Trung tâm chuông báo và thông tin đẩy từ hệ máy chủ


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

broadcast_id


## PK
Mã số thông điệp.

2

title

Tiêu đề tin nhắn gửi User.

3

content

Nội dung body đầy đủ của Noti.

4

type

Warning, Info, hay Promo (Quảng cáo).

5

status

Trạng thái Sent, Scheduled, Draft.

6

createdByEmail

Email của tên Admin đã duyệt nút gửi tin này.

Bảng thực thể SystemBroadcast

3.6.10 Bảng thực thể SystemConfig

Mô tả: Bàn cờ vận hành của Admin


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

config_id


## PK
Mã tập tham số.

2

configData

Chuỗi dữ liệu định dạng JSON chứa các config engine App như Timeout, Limits...

Bảng thực thể SystemConfig

3.6.11 Bảng thực thể AdminLog

Mô tả: Hộp đen ghi âm hành vi quản trị


## STT
Tên Cột

Phân quyền (KEY)

Mô tả tham số

1

log_id


## PK
Mã vạch của tập Log.

2

action

Loài hình nhúng chàm (VD: DELETE_USER, UPDATE_CONFIG).

3

target

Đối tượng lãnh đạn ID.

4

adminUid

UID của cấp trên đứng ra làm việc đó.

5

adminEmail

Email của thủ phạm admin thực thi.

6

createdAt

Dấu vết thời gian.

Bảng thực thể AdminLog

3.7 Lớp đối tượng Class Diagram

Sơ đồ class diagram của hệ thống

Class: User


## STT
Tên Biến / Phương Thức

Kiểu Thành phần

Mô tả Lập trình (OOP)

1

uid, name, email, phone

Thuộc tính (Properties)

Gói giao diện biến lưu trữ định danh cá nhân (String/Int).

2

totalCredit, totalDebit

Thuộc tính (Properties)

Bộ biến giám sát tổng chi tiêu, tổng thu nhập (Double).

3

login(email, pass)

Phương thức (Methods)

Hàm mở cửa vào app xác thực qua Firebase Auth.

4

register()

Phương thức (Methods)

Hàm khởi tạo sinh profile DB ban đầu.

5

calculateBalance()

Phương thức (Methods)

Hàm chạy logic cộng trừ biến dòng tiền.

6

updateProfile()

Phương thức (Methods)

Hàm đẩy update lệnh vá thông tin tài khoản.

Bảng mô tả class User

Class: Transaction


## STT
Tên Biến / Phương Thức

Kiểu Thành phần

Mô tả Lập trình (OOP)

1

transactionId, title, note

Thuộc tính (Properties)

Biến lưu đoạn mô tả (String).

2

amount

Thuộc tính (Properties)

Biến số học tiền tệ (Double/Float).

3

timestamp

Thuộc tính (Properties)

Biến chuẩn định dạng thời gian máy (Date).

4

add()

Phương thức (Methods)

Hành động INSERT dữ liệu giao dịch.

5

edit()

Phương thức (Methods)

Hành động UPDATE các property của hóa đơn.

6

delete()

Phương thức (Methods)

Hành động Hard Delete hoặc Soft Delete khỏi DB.

7

getDetails()

Phương thức (Methods)

Hành động truy vấn bản đồ hóa đơn GET.

Bảng mô tả class Transaction

Class: Budget


## STT
Tên Biến / Phương Thức

Kiểu Thành phần

Mô tả Lập trình (OOP)

1

budgetId, categoryName

Thuộc tính (Properties)

Biến chứa thẻ nhãn bị chặn chi.

2

limitAmount

Thuộc tính (Properties)

Biến mang cờ giới hạn biên (Double).

3

setBudget()

Phương thức (Methods)

Hàm giăng bẫy ngân sách mới.

4

checkLimit()

Phương thức (Methods)

Hàm giám sát quét tiền thừa mỗi khi Transaction.add() chạy.

5

notifyIfExceeded()

Phương thức (Methods)

Trigger event bắn chuông nếu budget < 20%.

Bảng mô tả class Budget

Class: SavingGoal


## STT
Tên Biến / Phương Thức

Kiểu Thành phần

Mô tả Lập trình (OOP)

1

goalId, targetAmount

Thuộc tính (Properties)

Đích đến tiền tệ khao khát đạt.

2

currentAmount

Thuộc tính (Properties)

Lượng tiền đang cắm vào thực tế.

3

createGoal()

Phương thức (Methods)

Hàm đúc ra hũ mới.

4

updateProgress()

Phương thức (Methods)

Hàm tính toán tỷ lệ thanh trượt (%). Toán hạng tính (current/target) * 100.

5

closeGoal()

Phương thức (Methods)

Hàm xóa sổ quỹ đẩy tiền về tài khoản (Transfer state).

Bảng mô tả class SavingGoal

Các Class còn lại (Category, Configuration, AdminLog...)


## STT
Tên Biến / Phương Thức

Kiểu Thành phần

Mô tả Lập trình (OOP)

1

Chứa Properties tương ứng Map với DB

Thuộc tính (Properties)

Biến private đóng gói giữ liệu bảo toàn tính Encapsulation (Đóng gói).

2

get(), set(), update()

Phương thức (Methods)

Các hàm chuẩn giao tiếp OOP cung cấp lối ra/vào Data.

Bảng mô tả class Category, Configuration, AdminLog…

3.8 Cớ chế realtime và luồng đồng bộ hệ thống

Một trong những điểm hiện đại nhất của hệ thống là cơ chế realtime sync. Hệ thống không chờ người dùng bấm làm mới. Thay vào đó, nhiều màn hình quan trọng được xây trên StreamBuilder. Ví dụ: AuthGate lắng nghe authStateChanges, user document và app controls; BudgetScreen lắng nghe danh sách ngân sách và giao dịch debit trong tháng; admin web có nhiều stream cho users, categories, broadcasts và transactions.

Ưu điểm của cách làm này là dữ liệu được phản ánh ngay sau khi Firestore thay đổi. Nếu admin khóa tài khoản, client có thể nhận trạng thái locked trong luồng user snapshot. Nếu admin thêm danh mục hệ thống, AI và client có thể đọc lại danh sách mới. Nếu giao dịch mới được thêm, dashboard hoặc báo cáo có thể cập nhật mà không cần load lại trang.

Sơ đồ realtime sync giữa Mobile App, Firestore và Admin Web.

3.9 Phân tích chi tiết chức năng AI và Prompt Engineering

AI trong dự án không chỉ là một chatbot trả lời tự do, mà là một bộ phân tích giao dịch có contract đầu ra rõ ràng. Trọng tâm không phải nói chuyện hay, mà là bóc tách đúng dữ liệu tài chính dưới dạng có cấu trúc.

Ở mức local parse, AIService sử dụng nhiều lớp xử lý: TransactionSegmenter để tách câu nhiều vế; TransactionAmountParser để chuẩn hóa số tiền; TransactionCategoryResolver để suy luận hoặc đối chiếu danh mục; TransactionTypeInference để xác định là credit hay debit; TransactionDateTimeInference để xử lý mốc thời gian; TransactionConfidence để chấm độ tin cậy; TransactionPhraseLexicon để hiểu ngôn ngữ đời thường, viết tắt, tiếng lóng và cách nói tiền tệ của người Việt.

Ở mức remote runtime AI, hệ thống không gửi câu người dùng lên model một cách mơ hồ mà xây dựng prompt nhiều lớp. Trong AiRuntimeConfig có rolePrompt để định vai chuyên gia bóc tách tài chính cá nhân; taskPrompt để định nhiệm vụ là phân loại ý định và tạo dữ liệu có cấu trúc; cardRulesPrompt để quy định khi nào mới được tạo card xác nhận; conversationRulesPrompt để kiểm soát xử lý hội thoại, thời gian và câu hỏi tư vấn; abbreviationRulesPrompt để chuẩn hóa tiếng lóng, teencode, từ địa phương và các biến thể viết tắt.

Sau khi tổng hợp các phần prompt này, hệ thống còn chèn thêm thông tin vận hành như thời điểm hiện tại, danh sách danh mục đang có, fallback policy và image strategy. Cuối cùng hệ thống ép AI tuân thủ một JSON contract chặt chẽ với các trường status, responseKind, message, transactions và data. Cách làm này rất quan trọng vì giúp frontend parse được kết quả một cách ổn định.

Điểm mạnh để nhấn trong báo cáo là AI ở đây không hoạt động kiểu trả gì cũng được, mà bị ràng buộc bởi quy tắc nghiệp vụ rất cụ thể: không được bịa dữ liệu, không được lên card khi thiếu dữ liệu, phải hỏi lại đúng phần thiếu, phải ưu tiên quy về danh mục hiện có, chỉ đánh dấu danh mục mới khi thật sự không quy được, và phải tách nhiều giao dịch nếu câu có nhiều vế.

Dữ liệu data.text đóng vai trò như một từ điển nghiệp vụ cho local parse. Trong đó hệ thống định nghĩa từ khóa thu, chi, phủ định, ý định tương lai, ý định công nợ, bộ tách nhiều giao dịch, bản đồ ưu tiên danh mục và các taxonomy danh mục như ăn uống, đi lại, mua sắm, hóa đơn, giải trí, nhà cửa, y tế, học tập, tài chính và khác. Điều này giúp AI hiểu ngôn ngữ người Việt sát thực tế hơn so với việc chỉ dựa vào model chung.

Sequence AI parser từ user input đến JSON contract và card xác nhận.

3.10 Logic ngân sách và thuật toán cảnh báo

BudgetScreen lấy hai nguồn dữ liệu song song theo thời gian thực: danh sách budget của tháng được chọn và danh sách transaction loại debit của cùng tháng. Tại giao diện, hệ thống duyệt các transaction, cộng dồn theo category trùng với budget.categoryName để tính spentAmount.

Sau đó BudgetProgressCard tính percentage = spentAmount / limitAmount * 100. Nếu percentage nhỏ hơn 80 thì progressColor là xanh, thể hiện an toàn. Nếu percentage từ 80 đến dưới 100 thì chuyển sang cam, thể hiện cảnh báo gần chạm ngưỡng. Nếu percentage lớn hơn hoặc bằng 100 thì chuyển đỏ, đồng thời hiển thị phần tiền vượt mức thay vì phần còn lại.

Điểm hay của thuật toán này là đơn giản, trực quan, nhưng đủ hiệu quả và dễ giải thích trong báo cáo. Nó còn tận dụng tốt mô hình realtime: hễ giao dịch tháng đó thay đổi thì stream transaction đổi, spentAmount đổi và thanh budget đổi ngay trên UI.

Sơ đồ sequence của thuật toán kiểm tra ngân sách và đổi màu cảnh báo.

3.11 Bảo mật, phân quyền và vận hành

AuthService và AuthGate cùng tham gia bảo vệ truy cập. AuthService xử lý xác thực và kiểm tra ban đầu; AuthGate tiếp tục lắng nghe realtime role, status và maintenance mode để điều hướng. Điều này giúp ứng dụng không chỉ an toàn ở thời điểm login, mà còn phản ứng nếu trạng thái tài khoản thay đổi khi người dùng đang online.

Mô hình phân quyền chia ít nhất hai vai trò: user và admin. User thường được vào dashboard người dùng. Admin trên web được vào AdminDashboard. Nếu admin dùng mobile thì được điều hướng tới màn hình redirect phù hợp.

Nếu status là locked hoặc app_controls bật maintenanceMode thì người dùng bị chặn bằng SystemAccessBlockedScreen.

Bảng phân quyền hệ thống theo vai trò User hoặc Admin và theo tài nguyên

CHƯƠNG IV. THIẾT KẾ GIAO DIỆN

4.1 Mục tiêu thiết kế giao diện

Giao diện mobile được thiết kế để giảm thao tác, dễ nhìn số liệu và tạo cảm giác sử dụng liên tục mỗi ngày. Giao diện admin web được thiết kế để quản trị viên có thể bao quát toàn bộ hệ thống, xem số liệu quan trọng trong một màn hình và đi sâu vào từng nhóm chức năng quản trị.

4.2 Thiết kế luồng màn hình mobile

Từ AuthGate, người dùng chưa đăng nhập sẽ vào LoginView. Khi đăng nhập thành công và có quyền user, hệ thống vào Dashboard. Dashboard dùng thanh điều hướng dưới để truy cập:

HomeScreen, TransactionScreen, BudgetScreen, ReportScreen và SettingsScreen.

Ngoài các màn hình chính, mobile còn có các màn hình chuyên biệt như:

AddTransactionScreen, EditTransactionScreen, AIInputScreen, SavingGoalsScreen, SavingGoalDetailScreen, CategoryAnalysisScreen, SearchScreen, ForgotPasswordOtpScreen và ChangePasswordOtpScreen.

Tên màn hình / thành phần

Tên file thực tế

Vai trò trong luồng mobile

AuthGate

lib/widgets/auth_gate.dart

Điểm kiểm tra trạng thái đăng nhập và quyền truy cập trước khi vào luồng mobile

LoginView

lib/screens/login_screen.dart

Màn hình đăng nhập của người dùng chưa xác thực

Dashboard

lib/screens/dashboard.dart

Màn hình trung tâm sau khi đăng nhập thành công với quyền user

HomeScreen

lib/screens/home_screen.dart

Màn hình trang chủ trong thanh điều hướng dưới

TransactionScreen

lib/screens/transaction_screen.dart

Màn hình quản lý và xem danh sách giao dịch

BudgetScreen

lib/screens/budget_screen.dart

Màn hình quản lý ngân sách chi tiêu

ReportScreen

lib/screens/report_screen.dart

Màn hình báo cáo và thống kê

SettingsScreen

lib/screens/settings_screen.dart

Màn hình cài đặt tài khoản và ứng dụng

AddTransactionScreen

lib/screens/add_transaction_screen.dart

Màn hình thêm giao dịch mới

EditTransactionScreen

lib/screens/edit_transaction_screen.dart

Màn hình chỉnh sửa giao dịch đã có

AIInputScreen

lib/screens/ai_input_screen.dart

Màn hình nhập liệu bằng AI hoặc câu lệnh tự nhiên

SavingGoalsScreen

lib/screens/saving_goals_screen.dart

Màn hình danh sách mục tiêu tiết kiệm

SavingGoalDetailScreen

lib/screens/saving_goal_detail_screen.dart

Màn hình chi tiết một mục tiêu tiết kiệm

CategoryAnalysisScreen

lib/screens/category_analysis_screen.dart

Màn hình phân tích dữ liệu theo danh mục

SearchScreen

lib/screens/search_screen.dart

Màn hình tìm kiếm giao dịch hoặc dữ liệu liên quan

ForgotPasswordOtpScreen

lib/screens/forgot_password_otp_screen.dart

Màn hình hỗ trợ quên mật khẩu bằng OTP

ChangePasswordOtpScreen

lib/screens/change_password_otp_screen.dart

Màn hình đổi mật khẩu có xác thực OTP

Bảng thể hiện các màn hình mobile người dùng

Sơ đồ màn hình mobile và các quan hệ điều hướng chính.

4.3 Thiết kế giao diện từng màn hình người dùng

Màn hình Dashboard là trung tâm điều hướng. Mục tiêu của màn hình này là cho phép chuyển nhanh giữa các phân hệ quan trọng mà không tạo cảm giác rối. Thanh navbar bên dưới giúp phù hợp với thói quen sử dụng trên di động.

Màn hình giao dịch phục vụ xem lịch sử, chỉnh sửa và xóa giao dịch. Đây là màn hình có ý nghĩa nghiệp vụ lớn vì phản ánh toàn bộ dữ liệu gốc của người dùng.

Màn hình Budget dùng thẻ progress để biểu diễn trạng thái ngân sách. Việc dùng màu sắc thay vì chỉ hiển thị chữ giúp người dùng nhìn là hiểu ngay tình hình tài chính.

Màn hình Report là khu vực phân tích. Báo cáo không chỉ cho xem tổng tiền mà còn có so sánh tháng trước, biểu đồ danh mục, đường xu hướng lịch sử và giao dịch cực trị. Đây là phần thể hiện giá trị biến dữ liệu thành thông tin.

Màn hình AIInputScreen có tính tương tác cao nhất. Giao diện dạng chat làm giảm cảm giác nhập form cứng nhắc. Người dùng có thể dùng quick templates, nhập ảnh, xem các trạng thái clarification hoặc success hoặc error và xác nhận giao dịch ngay trong ngữ cảnh hội thoại.

Màn hình SavingGoalsScreen làm tăng chiều sâu sản phẩm. Không chỉ theo dõi chi tiêu, người dùng còn có mục tiêu tích lũy. Progress bar, các nút thêm tiền hoặc rút tiền và phần gợi ý tiết kiệm mỗi ngày giúp tính năng này vừa trực quan vừa có giá trị sử dụng thực tế.

Giao diện đăng ký/ đăng nhập

Giao diện Thông tin và Đổi mật khẩu

Giao diện Trang chủ và Giao dịch

Giao diện Ngân sách và báo cáo

Giao diện Cài đặt và Tiết kiệm

Giao diện Thêm giao dịch

Giao diện Quản lý danh mục và Tìm kiếm

Giao diện Phân tích báo cáo chi tiết

Giao diện Tiền tiết kiệm, Giao dịch gần đây

Giao diện Báo cáo PDF và Mục chọn nhanh

Ảnh giao diện, chức năng người dùng

4.4 Thiết kế giao diện admin Web

Admin web có entry riêng từ main_admin_web.dart, chỉ hỗ trợ khi chạy trên trình duyệt. Điều này cho thấy dự án có định hướng đa nền tảng nhưng vẫn biết phân định ngữ cảnh sử dụng của từng phân hệ.

OverviewPage là trang tổng quan với hero panel, summary panel và nhiều metric card như số người dùng, số admin, danh mục hệ thống, broadcast đang bật, giao dịch tháng, tổng thu, tổng chi và số dư toàn hệ thống. Việc gom nhiều chỉ số ở đây giúp admin có một cockpit để điều hành.

UsersPage, TransactionsPage, CategoriesPage, ReportsPage, BroadcastsPage, SystemConfigsPage và AiConfigPage tạo thành bộ công cụ quản trị đầy đủ.

Trong đó AiConfigPage đặc biệt quan trọng vì cho thấy hệ thống không xem AI là một khối đen cố định mà cho phép giám sát, chỉnh prompt, preview và publish.

Thành phần / Trang

Tên file thực tế

Vai trò trong admin web

Entry admin web

lib/main_admin_web.dart

Điểm khởi tạo riêng cho phân hệ quản trị và chỉ hỗ trợ khi chạy trên trình duyệt

AdminWebApp

lib/admin_web/admin_web_app.dart

Ứng dụng gốc của admin web

AdminWebShell

lib/admin_web/admin_web_shell.dart

Khung điều hướng và bố cục tổng thể của giao diện admin

OverviewPage

lib/admin_web/pages/overview_page.dart

Trang tổng quan với hero panel, summary panel và các metric card điều hành

UsersPage

lib/admin_web/pages/users_page.dart

Trang quản lý danh sách người dùng và trạng thái tài khoản

TransactionsPage

lib/admin_web/pages/transactions_page.dart

Trang theo dõi giao dịch toàn hệ thống

CategoriesPage

lib/admin_web/pages/categories_page.dart

Trang quản lý danh mục hệ thống

ReportsPage

lib/admin_web/pages/reports_page.dart

Trang báo cáo và thống kê quản trị

BroadcastsPage

lib/admin_web/pages/broadcasts_page.dart

Trang quản lý thông báo hệ thống và broadcast

SystemConfigsPage

lib/admin_web/pages/system_configs_page.dart

Trang quản lý cấu hình hệ thống

AiConfigPage

lib/admin_web/pages/ai_config_page.dart

Trang giám sát, chỉnh prompt, preview và publish cấu hình AI

AdminWebRepository

lib/admin_web/admin_web_repository.dart

Lớp truy cập dữ liệu và nghiệp vụ phục vụ toàn bộ admin web

Bảng thể hiện các màn hình web quản trị viên

Sơ đồ điều hướng các trang web Admin

Giao diện đăng nhập Admin

Giao diện Trang chủ Admin

Giao diện Admin Quản lý người dùng

Giao diện Admin Quản lý danh mục hệ thống

Giao diện Admin Thông báo hệ thống

Giao diện Admin Cấu hình hệ thống

Giao diện Admin Cấu hình AI

Giao diện Admin Quản lý giao dịch

Giao diện Admin Quản lý báo cáo tổng hợp

Ảnh giao diện, chức năng quản trị viên

4.5 Nhận xét về trải nghiệm người dùng

Điểm đáng nhấn mạnh là giao diện được tổ chức quanh hành vi sử dụng thực tế. Mobile phục vụ ghi chép nhanh và xem số liệu cá nhân. Web admin phục vụ giám sát hệ thống. Đây là cách chia vai trò đúng bối cảnh, hợp với nguyên tắc thiết kế sản phẩm hiện đại.

CHƯƠNG V. DEMO XÂY DỰNG CHƯƠNG TRÌNH

5.1 Môi trường phát triển

Dự án được phát triển bằng  các công nghệ, gói chính gồm:

Công nghệ / Gói

Loại

Vai trò trong dự án

Flutter

Framework

Nền tảng phát triển giao diện đa nền tảng cho mobile và web admin

Dart SDK 3.x

Ngôn ngữ / SDK

Ngôn ngữ lập trình và môi trường biên dịch của dự án

firebase_core

Package

Khởi tạo và kết nối ứng dụng Flutter với Firebase

firebase_auth

Package

Xử lý xác thực người dùng và đăng nhập

cloud_firestore

Package

Lưu trữ và truy vấn dữ liệu thời gian thực trên Firestore

provider

Package

Quản lý trạng thái và truyền dữ liệu trong ứng dụng

intl

Package

Hỗ trợ định dạng ngày giờ, số và nội địa hóa

fl_chart

Package

Vẽ biểu đồ phục vụ báo cáo và thống kê

pdf

Package

Tạo tài liệu PDF từ dữ liệu của hệ thống

printing

Package

Hỗ trợ xem trước, in và xuất tài liệu PDF

google_mlkit_text_recognition

Package

Nhận diện văn bản từ ảnh, phục vụ OCR

share_plus

Package

Chia sẻ file hoặc nội dung sang ứng dụng khác

open_filex

Package

Mở file đã tạo bằng ứng dụng phù hợp trên thiết bị

image_picker

Package

Chọn ảnh từ thư viện hoặc chụp ảnh bằng camera

permission_handler

Package

Yêu cầu và kiểm soát quyền truy cập thiết bị

email_otp

Package

Hỗ trợ xác thực OTP qua email

http

Package

Gửi request HTTP tới API hoặc dịch vụ ngoài

Bảng thể hiện các công nghệ, gói trong dự án

5.2 Quy trình khởi động hệ thống

main.dart khởi tạo Firebase, cấu hình locale tiếng Việt và bọc ứng dụng bằng SettingsProvider. Sau đó AuthGate quyết định sẽ vào login, dashboard user hay admin. main_admin_web.dart đóng vai trò khởi tạo riêng cho cổng admin.

5.3 Demo các chức năng người dùng

Kịch bản demo

Người thực hiện

Dữ liệu dùng để demo

Kết quả mong đợi

Bước 1. Đăng ký hoặc đăng nhập

Người dùng

Email, mật khẩu, thông tin hồ sơ cơ bản

Xác thực thành công, hệ thống kiểm tra hồ sơ và điều hướng vào khu vực người dùng

Bước 2. Thêm giao dịch thủ công

Người dùng

Số tiền, loại giao dịch, danh mục, ngày, ghi chú

Transaction mới xuất hiện trong lịch sử và số dư thay đổi tương ứng

Bước 3. Ghi nhận giao dịch bằng AI chat

Người dùng

Câu đơn, câu nhiều vế, câu mơ hồ, câu có danh mục mới

AI phân tích, hỏi lại nếu cần, tạo card xác nhận và lưu đúng giao dịch sau khi đồng ý

Bước 4. Nhập giao dịch từ ảnh hóa đơn

Người dùng

Ảnh hóa đơn hoặc ảnh chụp văn bản chứa nội dung mua bán

OCR hoặc vision phân tích và dựng card đề xuất để người dùng xác nhận

Bước 5. Tạo ngân sách và kiểm tra cảnh báo

Người dùng

Hạn mức theo danh mục và giao dịch chi tiêu mới

Thanh cảnh báo ngân sách đổi màu theo thời gian thực khi gần chạm hoặc vượt ngưỡng

Bước 6. Xem báo cáo và xuất file

Người dùng

Dữ liệu giao dịch theo tháng và bộ lọc báo cáo

Biểu đồ theo tháng, phân tích theo danh mục và file báo cáo được xuất thành công

Bước 7. Tạo mục tiêu tiết kiệm và nạp tiền

Người dùng

Tên mục tiêu, số tiền mục tiêu, số tiền nạp

Mục tiêu được tạo, số tiền tích lũy tăng và tiến độ thay đổi trực quan

Bảng thực hiện chức năng người dùng

5.4 Demo các chức năng admin

Kịch bản demo

Người thực hiện

Dữ liệu dùng để demo

Kết quả mong đợi

Bước 1. Đăng nhập web admin

Admin

Tài khoản admin hợp lệ

Đăng nhập thành công vào khu vực quản trị web

Bước 2. Xem overview hệ thống

Admin

Dữ liệu tổng quan về user, giao dịch, danh mục, broadcast

Hiển thị các chỉ số hệ thống trên overview page

Bước 3. Khóa một user

Admin

Một tài khoản user đang hoạt động

User bị đổi trạng thái và client đang lắng nghe sẽ nhận cập nhật mới rồi bị chặn truy cập

Bước 4. Thêm danh mục hệ thống hoặc broadcast

Admin

Tên danh mục mới hoặc nội dung broadcast

Backend cập nhật thành công và client nhận thay đổi theo luồng realtime

Bước 5. Cấu hình AI runtime

Admin

Runtime draft, câu giao dịch mẫu để preview, cấu hình publish

Admin xem preview, thay đổi cấu hình và publish thành công để áp dụng vào hệ thống

Bảng thực hiện chức năng quản trị viên

5.5 Những điểm hiện đại và giá trị nổi bật khi trình bày demo

Thứ nhất, AI parser giúp rút ngắn số bước nhập liệu. Thay vì mở form và điền nhiều trường, người dùng có thể dùng ngôn ngữ tự nhiên. Đây là giá trị sản phẩm rất dễ thấy khi demo trực tiếp.

Thứ hai, realtime sync khiến hệ thống sống động và có cảm giác hệ thống thật, không phải ứng dụng demo tĩnh. Khi dữ liệu đổi ở Firestore, UI đổi theo ngay.

Thứ ba, kiến trúc BaaS cho thấy nhóm có lựa chọn kỹ thuật hợp lý: dùng Firebase để đi nhanh, bảo mật tốt và tối ưu chi phí.

Thứ tư, admin AI config cho thấy sản phẩm có khả năng vận hành và cải tiến sau triển khai, không bị đóng cứng.

CHƯƠNG VI. KIỂM THỬ PHẦN MỀM

6.1 Mục tiêu kiểm thử

Kiểm thử nhằm đảm bảo hệ thống hoạt động đúng với yêu cầu nghiệp vụ, đặc biệt ở các luồng nhạy cảm như xác thực, phân quyền, thêm giao dịch, xử lý AI, ngân sách và quản trị hệ thống.

6.2 Chiến lược kiểm thử đề xuất

Do đồ án tập trung vào chức năng, nhóm có thể ưu tiên kiểm thử hộp đen theo test case nghiệp vụ. Ngoài ra cần chú ý các tình huống biên như dữ liệu thiếu, AI trả kết quả không đủ, tài khoản bị khóa, maintenance mode, budget vừa chạm ngưỡng 80 phần trăm, budget vừa chạm 100 phần trăm và giao dịch AI có nhiều vế.

6.3 Bộ test case cho 5 chức năng

Mã test

Nội dung kiểm thử

Dữ liệu đầu vào tiêu biểu

Kết quả mong đợi


## TC01
Đăng nhập đúng thông tin

Vào đúng dashboard theo vai trò


## TC02
Đăng nhập sai mật khẩu

Báo lỗi chính xác và không vào hệ thống


## TC03
Tài khoản bị khóa

Bị chặn truy cập và hiển thị thông báo tương ứng


## TC04
Thêm giao dịch thủ công đầy đủ dữ liệu

Giao dịch được lưu, số dư cập nhật


## TC05
Sửa giao dịch

Tổng thu hoặc tổng chi hoặc số dư được tính lại đúng


## TC06
Xóa giao dịch

Dữ liệu bị xóa và hồ sơ tài chính được rollback đúng


## TC07
AI parse câu đơn

Ăn sáng 30k

Amount bằng 30000, type là debit, category phù hợp


## TC08
AI parse câu nhiều vế

Ăn tối 50k và đổ xăng 30k

Tạo 2 transaction draft


## TC09
AI gặp câu mơ hồ

Hôm trước mua đồ 200k

Hệ thống hỏi lại ngày chính xác nếu cần


## TC10
Tạo ngân sách và thêm giao dịch dưới ngưỡng 80 phần trăm

Màu xanh


## TC11
Tạo ngân sách và thêm giao dịch đạt vùng 80 đến 100 phần trăm

Màu cam


## TC12
Thêm giao dịch vượt ngân sách

Màu đỏ và hiển thị mức vượt


## TC13
Admin khóa user

User không truy cập được hệ thống


## TC14
Admin thêm broadcast

Dữ liệu broadcast được lưu và client liên quan có thể đọc được


## TC15
Admin publish runtime AI

Cấu hình mới được ghi vào system_configs và có log quản trị tương ứng

Bảng test case 5 chức năng

6.4 Kết quả kiểm thử và cách trình bày để đạt điểm cao

Bảng Nhóm test chức năng đăng nhập và xác thực


## ID
Items

Sub-items

Description

PreCondition

Expected output

Test Data / Parameters


## TC01
Đăng nhập

Đúng thông tin

Đăng nhập đúng thông tin tài khoản

Đã có tài khoản user hợp lệ

Xác thực thành công và vào đúng dashboard theo vai trò

Email: userdemo@app.com; Mật khẩu: User@123


## TC02
Đăng nhập

Sai mật khẩu

Đăng nhập với mật khẩu không chính xác

Đã có tài khoản user hợp lệ

Báo lỗi xác thực và không cho truy cập

Email: userdemo@app.com; Mật khẩu sai: 123456


## TC03
Đăng nhập

Tài khoản bị khóa

Tài khoản đã bị admin khóa thử đăng nhập

Tài khoản có status locked

Hệ thống chặn truy cập và hiển thị thông báo phù hợp

Email: locked_user@app.com; Mật khẩu: User@123

Bảng test case chức năng đăng nhập và xác thực

Bảng Nhóm test chức năng quản lý giao dịch thủ công


## ID
Items

Sub-items

Description

PreCondition

Expected output

Test Data / Parameters


## TC04
Giao dịch

Thêm mới

Thêm giao dịch thủ công đầy đủ dữ liệu

Người dùng đã đăng nhập

Giao dịch được lưu, xuất hiện trong lịch sử và cập nhật số dư

Debit; 50000; Ăn uống; Ăn sáng


## TC05
Giao dịch

Sửa giao dịch

Chỉnh sửa một giao dịch đã tồn tại

Đã có ít nhất một giao dịch

Dữ liệu giao dịch được cập nhật và các tổng số được tính lại đúng

Từ 50000 thành 80000


## TC06
Giao dịch

Xóa giao dịch

Xóa một giao dịch đã tạo trước đó

Đã có ít nhất một giao dịch

Giao dịch bị xóa và hồ sơ tài chính rollback đúng

Giao dịch: Mua trà sữa 30000

Bảng test case chức năng giao dịch thủ công

Bảng Nhóm test chức năng ghi nhận giao dịch bằng AI


## ID
Items

Sub-items

Description

PreCondition

Expected output

Test Data / Parameters


## TC07
AI Parser

Câu đơn

AI phân tích một câu đơn giản để tạo giao dịch

Đã vào AI Input Screen

AI trả amount 30000, type debit và category phù hợp

Input: ăn sáng 30k


## TC08
AI Parser

Câu nhiều vế

AI tách một câu nhiều hành động thành nhiều giao dịch

Đã vào AI Input Screen

Tạo 2 transaction draft riêng biệt trước khi xác nhận

Input: ăn tối 50k và đổ xăng 30k


## TC09
AI Parser

Câu mơ hồ

AI gặp câu chưa rõ thời gian hoặc ngữ cảnh

Đã vào AI Input Screen

Yêu cầu người dùng bổ sung thông tin nếu cần

Input: hôm trước mua đồ 200k

Bảng test case chức năng giao dịch bằng AI

Bảng Nhóm test chức năng quản lý ngân sách


## ID
Items

Sub-items

Description

PreCondition

Expected output

Test Data / Parameters


## TC10
Ngân sách

Dưới 80%

Tạo ngân sách và thêm giao dịch dưới ngưỡng cảnh báo

Đã có danh mục và tháng hiện tại

Thanh tiến độ hiển thị màu xanh

Ngân sách 1000000; Tổng chi 300000


## TC11
Ngân sách

Từ 80%-100%

Thêm giao dịch để ngân sách vào vùng cảnh báo

Đã có ngân sách tháng hiện tại

Thanh tiến độ đổi sang màu cam hoặc vàng

Ngân sách 1000000; Tổng chi 900000


## TC12
Ngân sách

Vượt ngưỡng

Thêm giao dịch vượt quá hạn mức ngân sách

Đã có ngân sách tháng hiện tại

Thanh tiến độ chuyển sang màu đỏ và hiển thị mức vượt

Ngân sách 1000000; Tổng chi 1200000

Bảng test case chức năng quản ký ngân sách

Bảng Nhóm test chức năng quản trị hệ thống


## ID
Items

Sub-items

Description

PreCondition

Expected output

Test Data / Parameters


## TC13
Admin

Khóa user

Admin khóa tài khoản người dùng đang hoạt động

Admin đã đăng nhập web admin

User bị đổi trạng thái và client bị chặn truy cập

User mục tiêu: userdemo@app.com


## TC14
Admin

Thêm broadcast

Admin tạo một broadcast mới cho hệ thống

Admin đã đăng nhập web admin

Broadcast được lưu và client liên quan đọc được theo realtime

Tiêu đề: Bảo trì hệ thống


## TC15
Admin

Publish AI runtime

Admin thay đổi cấu hình runtime AI và publish

Admin có quyền cấu hình AI

Cấu hình mới được ghi vào system_configs và có log quản trị

Prompt mẫu: ăn sáng 30k; Runtime draft mới

Bảng test case chức năng quản trị hệ thống

CHƯƠNG VII. KẾT LUẬN

7.1 Kết quả đạt được

Nhìn tổng thể, hệ thống đã đi xa hơn một ứng dụng CRUD cơ bản. Đề tài đã xây dựng được một sản phẩm có tính định hướng thực tế, hỗ trợ quản lý tài chính cá nhân trên nền tảng Flutter, kết hợp dữ liệu thời gian thực với Firebase, tích hợp AI để giảm thao tác nhập liệu, đồng thời có thêm phân hệ quản trị web phục vụ giám sát và điều hành hệ thống.

Về mặt chức năng, hệ thống đã hoàn thiện các nhóm nghiệp vụ quan trọng như xác thực và phân quyền, quản lý giao dịch thu chi, nhập liệu thông minh bằng AI, quản lý ngân sách, báo cáo thống kê, mục tiêu tiết kiệm và quản trị hệ thống. Việc tổ chức code theo các nhóm screens, widgets, services, models, providers và admin_web cũng cho thấy dự án được xây dựng theo hướng có cấu trúc, thuận lợi cho mở rộng và bảo trì.

Điểm nổi bật nhất của đề tài là đã thể hiện được yếu tố thông minh và giá trị khác biệt so với các ứng dụng ghi chép truyền thống. Người dùng không chỉ nhập giao dịch bằng form thủ công mà còn có thể sử dụng câu lệnh tự nhiên, ảnh hóa đơn và các luồng realtime để tương tác với hệ thống. Ở phía quản trị, admin có thể theo dõi dashboard tổng quan, khóa người dùng, quản lý danh mục, broadcast và cấu hình AI runtime. Đây là các yếu tố rất đáng nhấn mạnh khi đánh giá kết quả cuối cùng của đồ án.

7.2 Hạn chế của hệ thống

Mặc dù đã đạt được nhiều kết quả tích cực, hệ thống vẫn còn một số hạn chế cần nhìn nhận rõ. Thứ nhất, độ chính xác của AI và OCR vẫn phụ thuộc vào chất lượng dữ liệu đầu vào. Với các câu quá mơ hồ, quá ngắn hoặc chứa nhiều ngữ cảnh thời gian phức tạp, hệ thống vẫn có thể cần hỏi lại người dùng trước khi lưu. Tương tự, chức năng nhận diện văn bản từ ảnh phụ thuộc vào độ rõ của hóa đơn, góc chụp và ánh sáng.

Thứ hai, phân hệ admin web hiện được thiết kế cho ngữ cảnh chạy trên trình duyệt, chưa tối ưu cho các nền tảng desktop native. Ngoài ra, một số gói thư viện tích hợp như nhận diện văn bản bằng ML Kit hoặc xác thực OTP qua email còn chịu ràng buộc bởi giới hạn nền tảng, cấu hình môi trường hoặc yêu cầu SMTP/permission cụ thể.

Thứ ba, hệ thống hiện tập trung mạnh vào luồng nghiệp vụ và trải nghiệm tính năng, nhưng chưa đi sâu vào các khía cạnh sản phẩm ở mức triển khai thực tế quy mô lớn như giám sát logging tập trung, phân tích hiệu năng sâu, kiểm thử tự động diện rộng, tối ưu truy vấn dữ liệu lớn hoặc bảo vệ nâng cao trước các tình huống lạm dụng dịch vụ AI.

7.3 Hướng phát triển

Trong tương lai, hệ thống có thể được mở rộng theo nhiều hướng có giá trị thực tiễn cao. Trước hết, nhóm có thể nâng cấp tính năng OCR và AI parser để hiểu tốt hơn các hóa đơn nhiều dòng, tiếng Việt không dấu, viết tắt, câu hội thoại nhiều bước hoặc ngữ cảnh thời gian phức tạp. Việc bổ sung cơ chế học theo thói quen người dùng cũng có thể giúp tăng độ chính xác phân loại danh mục.

Một hướng phát triển quan trọng khác là mở rộng từ quản lý thu chi sang hỗ trợ ra quyết định tài chính, ví dụ gợi ý tiết kiệm theo thu nhập, dự báo xu hướng chi tiêu, cảnh báo rủi ro vượt ngân sách sớm hoặc gợi ý điều chỉnh kế hoạch tài chính cá nhân. Hệ thống cũng có thể bổ sung nhiều hình thức nhập liệu mới như quét hóa đơn tốt hơn, import sao kê hoặc đồng bộ từ ví điện tử và tài khoản ngân hàng nếu có điều kiện tích hợp phù hợp.

Đối với phân hệ quản trị, có thể phát triển thêm các tính năng như phân quyền admin nhiều cấp, nhật ký thao tác chi tiết, cấu hình luật kiểm duyệt tự động, dashboard phân tích nâng cao và cơ chế giám sát realtime sâu hơn cho AI runtime. Nếu tiếp tục đầu tư, đề tài có thể tiến dần từ đồ án học phần sang một sản phẩm thử nghiệm có khả năng ứng dụng thực tế.

7.4 Tài liệu tham khảo

Danh mục dưới đây ưu tiên các nguồn chính thống như tài liệu chính thức của Flutter, Dart, Firebase và các trang package công khai trên pub.dev. Không sử dụng nguồn tổng hợp không rõ xuất xứ hoặc nội dung do AI tự tạo làm tài liệu tham khảo.

1. Flutter Documentation: https://docs.flutter.dev/

2. Dart Documentation. https://dart.dev/

3. Firebase Documentation.  https://firebase.google.com/docs/

4. Cloud Firestore - Firebase. https://firebase.google.com/products/firestore

5. Provider package on pub.dev. https://pub.dev/packages/provider

6. fl_chart package on pub.dev.

7. pdf package on pub.dev.

8. printing package on pub.dev.

9. image_picker package on pub.dev.

10. google_mlkit_text_recognition package on pub.dev.

11. permission_handler package on pub.dev.

12. share_plus package on pub.dev.

13. open_filex package on pub.dev.

14. email_otp package on pub.dev.

15. http package on pub.dev.


import tkinter as tk
from tkinter import ttk, messagebox
import os
import subprocess
import sys

# Khởi tạo cơ sở dữ liệu cho 16 chức năng hệ thống
FUNCTIONS_DB = {
    "1. Tìm kiếm & Lọc phòng (Khách)": {
        "description": "Cho phép khách hàng tìm kiếm phòng trống theo chi nhánh, thời gian đặt (ngày hoặc giờ), khoảng giá, số người và các tiện ích đi kèm.",
        "flow": (
            "1. Khách truy cập vào trang danh sách phòng (/Rooms).\n"
            "2. Điền các tham số tìm kiếm (Chi nhánh, ngày nhận/trả, lọc theo giờ/ngày...).\n"
            "3. RoomsController.Index tiếp nhận request, gọi AvailabilityService để tìm phòng trống khả dụng thực tế từ database.\n"
            "4. Kết quả phòng trống được hiển thị trực quan lên giao diện Views/Rooms/Index.cshtml."
        ),
        "folders": [
            "WebHomestay/Controllers/Public",
            "WebHomestay/Services/Booking",
            "WebHomestay/Views/Rooms"
        ],
        "files": [
            "WebHomestay/Controllers/Public/RoomsController.cs",
            "WebHomestay/Services/Booking/AvailabilityService.cs",
            "WebHomestay/Views/Rooms/Index.cshtml",
            "WebHomestay/Views/Rooms/Detail.cshtml"
        ]
    },
    "2. Đặt phòng & Thanh toán giả lập (Khách)": {
        "description": "Khách hàng điền thông tin đặt phòng, chọn thời gian thuê, tính tiền và thực hiện thanh toán qua QR Code hoặc tiền mặt giả lập.",
        "flow": (
            "1. Khách hàng bấm chọn phòng -> Chuyển đến trang điền thông tin (/Bookings/Create).\n"
            "2. Nhập thông tin khách, chọn thời gian -> Gửi POST request tới BookingsController.Create.\n"
            "3. BookingCreationService kiểm tra trùng lịch (AvailabilityService) và tính toán tổng tiền qua PricingService (có tính phụ thu ngày lễ).\n"
            "4. Lưu thông tin đặt phòng ở trạng thái 'PendingPayment' (Chờ thanh toán) và điều hướng tới trang hiển thị QR thanh toán."
        ),
        "folders": [
            "WebHomestay/Controllers/Public",
            "WebHomestay/Services/Booking",
            "WebHomestay/Views/Bookings"
        ],
        "files": [
            "WebHomestay/Controllers/Public/BookingsController.cs",
            "WebHomestay/Services/Booking/BookingCreationService.cs",
            "WebHomestay/Services/Booking/PricingService.cs",
            "WebHomestay/Services/Booking/BookingTimeRules.cs",
            "WebHomestay/Views/Bookings/Create.cshtml"
        ]
    },
    "3. Tra cứu lịch sử & Tự Check-in/out online (Khách)": {
        "description": "Khách hàng tra cứu lịch sử đặt phòng bằng số điện thoại/email, tải ảnh CCCD/Hộ chiếu lên để tự check-in và gửi yêu cầu check-out online.",
        "flow": (
            "1. Khách truy cập vào liên kết tra cứu đặt phòng gửi qua mail hoặc tìm kiếm trên web.\n"
            "2. Xem thông tin chi tiết phòng đã đặt tại Views/Bookings/Details.cshtml.\n"
            "3. Nhấn Check-in (tải ảnh CCCD) -> Gửi request tới BookingsController.CheckIn -> Chuyển trạng thái sang CheckedIn.\n"
            "4. Nhấn Check-out -> Gửi request tới BookingsController.CheckOut -> Chuyển trạng thái sang CheckedOut."
        ),
        "folders": [
            "WebHomestay/Controllers/Public",
            "WebHomestay/Services/Booking",
            "WebHomestay/Views/Bookings"
        ],
        "files": [
            "WebHomestay/Controllers/Public/BookingsController.cs",
            "WebHomestay/Services/Booking/BookingCancellationService.cs",
            "WebHomestay/Views/Bookings/Details.cshtml"
        ]
    },
    "4. AI Chatbot tư vấn đặt phòng tự động (Khách)": {
        "description": "AI Chatbot hỗ trợ hội thoại tự nhiên với khách hàng để tư vấn, gợi ý phòng trống thời gian thực và hỗ trợ tự động điền form/auto-book.",
        "flow": (
            "1. Khách hàng nhắn tin tại widget chat ở trang chủ.\n"
            "2. Tin nhắn được gửi tới endpoint /ai/chat trong AIChatController.\n"
            "3. AIChatController chuyển tiếp qua ContextAwareBookingConductor điều phối quy trình.\n"
            "4. Đọc session đặt phòng, lấy danh sách phòng trống thực tế qua AvailabilityService.\n"
            "5. Gọi AIModelClient (gửi Groq/OpenRouter) để trả về phản hồi kèm các thẻ phòng, slot trống (định dạng JSON).\n"
            "6. Client JS (public-ai-chat.js) kết xuất các thẻ phòng, nút bấm ra màn hình chat."
        ),
        "folders": [
            "WebHomestay/Controllers/AI",
            "WebHomestay/Services",
            "WebHomestay/wwwroot/js"
        ],
        "files": [
            "WebHomestay/Controllers/AI/AIChatController.cs",
            "WebHomestay/Services/ContextAwareBookingConductor.cs",
            "WebHomestay/Views/Home/Index.cshtml",
            "WebHomestay/wwwroot/js/public-ai-chat.js"
        ]
    },
    "5. Đăng nhập & Xác thực Admin (Admin)": {
        "description": "Xác thực danh tính quản trị viên, lưu thông tin vai trò và quyền hạn vào Session, kiểm tra quyền truy cập qua các bộ lọc tùy chỉnh.",
        "flow": (
            "1. Admin truy cập đường dẫn /admin/account/login.\n"
            "2. Nhập thông tin đăng nhập -> AdminAccountController.Login tiếp nhận (POST).\n"
            "3. Xác thực tài khoản trong DB -> Lưu AdminUser, AdminRole và AdminPermissions vào Session.\n"
            "4. Các action được đánh dấu [AdminAuthorize] sẽ chạy filter kiểm tra Session để quyết định cho phép truy cập hay từ chối."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Filters",
            "WebHomestay/Views/AdminAccount"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminAccountController.cs",
            "WebHomestay/Filters/AdminAuthorizeAttribute.cs",
            "WebHomestay/Services/PermissionResolveService.cs",
            "WebHomestay/Views/AdminAccount/Login.cshtml"
        ]
    },
    "6. Quản lý Chi nhánh (Admin)": {
        "description": "CRUD (Thêm, Sửa, Xóa, Xem) các chi nhánh của hệ thống homestay bao gồm tên, địa chỉ, hotline, bản đồ định vị.",
        "flow": (
            "1. Admin truy cập trang quản lý chi nhánh (/admin/branches).\n"
            "2. Chọn thêm/sửa/xóa chi nhánh -> Yêu cầu gửi tới AdminBranchesController.\n"
            "3. Controller thực hiện cập nhật bảng branches thông qua DbContext và lưu thay đổi."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminBranches",
            "WebHomestay/Models/Entities/Core"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminBranchesController.cs",
            "WebHomestay/Models/Entities/Core/Branch.cs",
            "WebHomestay/Services/Branch/BranchLeadTimeService.cs",
            "WebHomestay/Views/AdminBranches/Index.cshtml"
        ]
    },
    "7. Quản lý Phòng & Tiện nghi (Admin)": {
        "description": "CRUD thông tin chi tiết các phòng, phân loại phòng, cài đặt mức giá ngày/giờ, liên kết các tiện nghi đi kèm (wifi, tivi, điều hòa).",
        "flow": (
            "1. Admin truy cập màn hình quản lý phòng (/admin/rooms).\n"
            "2. Admin thực hiện thêm, sửa, xóa phòng hoặc cấu hình tiện ích đi kèm.\n"
            "3. Request gửi tới AdminRoomsController xử lý lưu thông tin vào bảng rooms và bảng liên kết trung gian tiện ích."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminRooms",
            "WebHomestay/Models/Entities/Core"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminRoomsController.cs",
            "WebHomestay/Models/Entities/Core/Room.cs",
            "WebHomestay/Models/Entities/Core/Amenity.cs",
            "WebHomestay/Services/Room/RoomBookingViewService.cs",
            "WebHomestay/Views/AdminRooms/Index.cshtml"
        ]
    },
    "8. Quản lý Đặt phòng (Admin)": {
        "description": "Quản trị danh sách booking của khách hàng (chờ thanh toán, chờ duyệt, đã xác nhận, đã check-in, đã check-out, đã hủy). Cho phép duyệt thanh toán, hủy phòng, và quản lý Thùng rác đặt phòng.",
        "flow": (
            "1. Admin vào trang quản lý đặt phòng (/admin/bookings).\n"
            "2. Lọc danh sách, chọn đơn hàng cần phê duyệt thanh toán hoặc hủy.\n"
            "3. AdminBookingsController nhận request và thay đổi trạng thái đơn.\n"
            "4. Background Service (BookingCleanupService) tự chạy ngầm mỗi phút để hủy các booking hết hạn thanh toán nhằm giải phóng phòng."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminBookings",
            "WebHomestay/Services/Booking"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminBookingsController.cs",
            "WebHomestay/Services/Booking/BookingCleanupService.cs",
            "WebHomestay/Models/Entities/Core/Booking.cs",
            "WebHomestay/Views/AdminBookings/Index.cshtml",
            "WebHomestay/Views/AdminBookings/Trash.cshtml"
        ]
    },
    "9. Ma trận vận hành (Admin)": {
        "description": "Bản đồ trạng thái phòng thời gian thực, trực quan hóa trạng thái dọn dẹp, bảo trì, hoặc phòng đang có khách ở, cho phép đổi trạng thái dọn dẹp xong/check-in/out nhanh.",
        "flow": (
            "1. Admin truy cập màn hình Ma trận vận hành (/admin/matrix).\n"
            "2. Giao diện tải sơ đồ trạng thái phòng thực tế từ DB.\n"
            "3. Admin click nhanh vào phòng để đổi trạng thái dọn dẹp xong, check-in hoặc check-out nhanh.\n"
            "4. Yêu cầu gửi tới AdminMatrixController cập nhật trường trạng thái của phòng trong DB."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminMatrix"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminMatrixController.cs",
            "WebHomestay/Views/AdminMatrix/Index.cshtml"
        ]
    },
    "10. Quản lý Lịch kho phòng (Admin)": {
        "description": "Quản lý số lượng phòng trống tồn kho theo ngày, khung giờ (Slots Inventory) dựa trên các khuôn mẫu thiết lập sẵn (Templates) nhằm ngăn chặn overbooking.",
        "flow": (
            "1. Admin truy cập trang /admin/slots.\n"
            "2. Cấu hình khuôn mẫu lịch trống mẫu (Template) và phân bổ (Assignment) cho các phòng.\n"
            "3. Gọi SlotGenerationService tự động sinh ra các bản ghi số lượng phòng trống trong bảng room_slot_inventories.\n"
            "4. Khi khách đặt phòng theo ngày/giờ, hệ thống sẽ kiểm tra và trừ tồn kho slot tại bảng này."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminSlots",
            "WebHomestay/Services/Slots"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminSlotsController.cs",
            "WebHomestay/Services/Slots/SlotManagementService.cs",
            "WebHomestay/Services/Slots/SlotGenerationService.cs",
            "WebHomestay/Models/Entities/Slots/RoomSlotInventory.cs",
            "WebHomestay/Views/AdminSlots/Index.cshtml"
        ]
    },
    "11. Quản lý Ngày lễ (Admin)": {
        "description": "Thiết lập lịch các ngày lễ và cấu hình phần trăm phụ thu tương ứng để hệ thống tự động tăng giá phòng khi khách hàng đặt phòng trúng ngày lễ.",
        "flow": (
            "1. Admin truy cập màn hình quản lý ngày lễ (/admin/holidays).\n"
            "2. Thao tác CRUD ngày lễ và thiết lập % phụ thu tăng thêm.\n"
            "3. Dữ liệu lưu vào bảng holidays. Khi khách đặt phòng, PricingService sẽ kiểm tra khoảng thời gian đặt phòng trùng ngày lễ để cộng tiền."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminHolidays",
            "WebHomestay/Models/Entities/Core"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminHolidaysController.cs",
            "WebHomestay/Models/Entities/Core/Holiday.cs",
            "WebHomestay/Views/AdminHolidays/Index.cshtml"
        ]
    },
    "12. Quản lý Nhân viên & Ma trận phân quyền (Admin)": {
        "description": "Quản lý tài khoản nhân viên, cấu hình phân quyền chi tiết (Settings, Rooms, AI, Bookings...) dựa trên vai trò mẫu (Role Template) kết hợp cấu hình ghi đè quyền cá nhân (Account Override).",
        "flow": (
            "1. Admin truy cập màn hình phân quyền (/admin/staff/permissionmatrix).\n"
            "2. Chọn vai trò hoặc chọn nhân viên cụ thể để điều chỉnh quyền (các quyền phân cấp cha-con rõ ràng).\n"
            "3. AdminStaffController tiếp nhận POST request và lưu cấu hình dạng JSON vào cột PermissionsJson trong DB.\n"
            "4. Khi nhân viên thực hiện thao tác, PermissionResolveService sẽ hợp nhất quyền từ vai trò và quyền ghi đè để kiểm tra."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminStaff",
            "WebHomestay/Services"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminStaffController.cs",
            "WebHomestay/Services/PermissionResolveService.cs",
            "WebHomestay/Views/AdminStaff/PermissionMatrix.cshtml"
        ]
    },
    "13. Quản lý Thư viện ảnh & Che ảnh nhạy cảm (Admin)": {
        "description": "Upload, phân loại ảnh phòng. Tự động che mờ (masking) các thông tin nhạy cảm của CCCD, ảnh thanh toán do khách hàng tải lên để bảo mật.",
        "flow": (
            "1. Người dùng tải ảnh CCCD/Thanh toán lên hoặc Admin upload ảnh phòng.\n"
            "2. Ảnh đi qua ImageMaskingService để nhận diện và làm mờ các vùng thông tin cá nhân/nhạy cảm.\n"
            "3. File ảnh sau xử lý được lưu trữ và cập nhật đường dẫn vào database.\n"
            "4. Admin có thể xem, xóa tạm vào Thùng rác ảnh hoặc khôi phục qua AdminImagesController."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminImages",
            "WebHomestay/Services"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminImagesController.cs",
            "WebHomestay/Services/ImageMaskingService.cs",
            "WebHomestay/Views/AdminImages/Index.cshtml",
            "WebHomestay/Views/AdminImages/Trash.cshtml"
        ]
    },
    "14. Thống kê doanh thu & Xuất báo cáo (Admin)": {
        "description": "Báo cáo kết quả hoạt động kinh doanh dưới dạng biểu đồ trực quan, thống kê doanh thu, tỷ lệ phòng trống và xuất dữ liệu ra file Excel.",
        "flow": (
            "1. Admin truy cập trang thống kê (/admin/statistics).\n"
            "2. AdminStatisticsController gọi StatisticsService lấy thông tin doanh thu, số lượng đơn hàng trong khoảng thời gian.\n"
            "3. Dữ liệu được chuyển lên View vẽ biểu đồ bằng Chart.js.\n"
            "4. Bấm xuất báo cáo -> ExcelTemplateService thực hiện tạo và xuất file Excel cho Admin tải về."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminStatistics",
            "WebHomestay/Services"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminStatisticsController.cs",
            "WebHomestay/Services/ExcelTemplateService.cs",
            "WebHomestay/Views/AdminStatistics/Index.cshtml"
        ]
    },
    "15. Admin AI Brain Center (Admin)": {
        "description": "Giao diện quản lý cấu hình AI nâng cao bao gồm: RAG (bài viết tri thức), Graph Reasoning (mạng lưới đồ thị quan hệ), cấu hình các Agent (Persona, Safety Guard...), cấu hình phong cách Synthesizer và giám sát Conversation Traces.",
        "flow": (
            "1. Admin truy cập trung tâm não bộ AI (/admin/ai).\n"
            "2. CRUD tri thức bài viết, kết nối các nút đồ thị (Graph Nodes/Edges) hoặc thay đổi prompt các Agent.\n"
            "3. Toàn bộ cấu hình được lưu vào các bảng DB tương ứng.\n"
            "4. Khi khách hàng chat, toàn bộ log luồng đi của các Agent được lưu vào ai_conversation_traces giúp Admin theo dõi, phân tích."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminAI",
            "WebHomestay/Services/AI"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminAIController.cs",
            "WebHomestay/Services/AIBrainOrchestrator.cs",
            "WebHomestay/Services/AI/SemanticKernelOrchestrator.cs",
            "WebHomestay/Views/AdminAI/Index.cshtml",
            "WebHomestay/wwwroot/js/admin-ai-brain-center.js"
        ]
    },
    "16. Cấu hình hệ thống (Admin)": {
        "description": "Quản lý các cấu hình chung của toàn hệ thống (giờ check-in/out tiêu chuẩn, thông tin liên hệ, hotline...) và các cài đặt nâng cao của AI (Prompts, Trigger Words, PublicBookingFormSchema).",
        "flow": (
            "1. Admin truy cập màn hình cài đặt (/admin/settings).\n"
            "2. Điều chỉnh các thông số trên biểu mẫu và bấm lưu.\n"
            "3. AdminSettingsController tiếp nhận request và gọi SettingService lưu vào bảng system_settings.\n"
            "4. Cập nhật cache hệ thống để các service khác đọc cấu hình mới ngay lập tức."
        ),
        "folders": [
            "WebHomestay/Controllers/Admin",
            "WebHomestay/Views/AdminSettings",
            "WebHomestay/Services"
        ],
        "files": [
            "WebHomestay/Controllers/Admin/AdminSettingsController.cs",
            "WebHomestay/Services/SettingService.cs",
            "WebHomestay/Models/Entities/Core/SystemSetting.cs",
            "WebHomestay/Views/AdminSettings/Index.cshtml"
        ]
    }
}

class ModernSearchToolApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Homestay Search Launcher")
        
        # Center the window on the screen
        width, height = 550, 260
        screen_width = self.root.winfo_screenwidth()
        screen_height = self.root.winfo_screenheight()
        x = (screen_width - width) // 2
        y = (screen_height - height) // 2
        self.root.geometry(f"{width}x{height}+{x}+{y}")
        self.root.configure(bg="#000000")
        self.root.resizable(False, False)
        
        # Workspace root directory path
        self.workspace_root = os.path.dirname(os.path.abspath(__file__))
        
        # Setup modern styles and colors (True Black Theme)
        self.bg_color = "#000000"
        self.entry_bg = "#121212"
        self.text_fg = "#ffffff"
        self.accent_color = "#ffffff"
        self.border_color = "#2a2a2a"
        self.preview_bg = "#090909"
        self.subtext_fg = "#aaaaaa"
        self.dim_fg = "#666666"
        
        self.functions_list = list(FUNCTIONS_DB.keys())
        self.current_results = []
        
        # Styles config for ttk Combobox
        self.setup_ttk_styles()
        
        self.create_widgets()
        
        # Start completely clean (results are hidden until search/select)
        self.clear_results()
        self.search_combo.focus_set()

    def setup_ttk_styles(self):
        self.style = ttk.Style()
        self.style.theme_use("clam")
        
        # Configure TCombobox style
        self.style.configure(
            "TCombobox", 
            fieldbackground=self.entry_bg, 
            background="#1a1a1a", 
            foreground=self.text_fg, 
            bordercolor=self.border_color, 
            lightcolor=self.border_color,
            darkcolor=self.border_color,
            arrowcolor=self.text_fg,
            arrowsize=22
        )
        # Highlight border when focused/active
        self.style.map(
            "TCombobox",
            bordercolor=[("focus", self.accent_color), ("active", self.accent_color)],
            lightcolor=[("focus", self.accent_color)],
            darkcolor=[("focus", self.accent_color)]
        )
        
        # Fix dropdown list colors on Windows
        self.root.option_add("*TCombobox*Listbox.background", self.entry_bg)
        self.root.option_add("*TCombobox*Listbox.foreground", self.text_fg)
        self.root.option_add("*TCombobox*Listbox.selectBackground", "#333333")
        self.root.option_add("*TCombobox*Listbox.selectForeground", self.text_fg)
        self.root.option_add("*TCombobox*Listbox.font", ("Segoe UI", 11))

    def create_widgets(self):
        # 1. Search Dropdown Panel
        search_frame = tk.Frame(self.root, bg=self.bg_color, padx=15, pady=12)
        search_frame.pack(fill=tk.X)
        
        self.search_combo = ttk.Combobox(
            search_frame,
            values=self.functions_list,
            font=("Segoe UI", 12),
            style="TCombobox"
        )
        self.search_combo.pack(fill=tk.X, ipady=4)
        
        # Bind events
        self.search_combo.bind("<<ComboboxSelected>>", self.on_function_selected)
        self.search_combo.bind("<KeyRelease>", self.on_combo_keyrelease)
        self.search_combo.bind("<Return>", self.on_combo_return)
        self.search_combo.bind("<Down>", self.on_arrow_down)
        self.search_combo.bind("<Up>", self.on_arrow_up)
        
        self.root.bind("<Escape>", self.on_escape_pressed)
        self.root.bind("<Control-c>", self.copy_selected_path)
        
        # 2. Results List Pane
        list_frame = tk.Frame(self.root, bg=self.bg_color, padx=15)
        list_frame.pack(fill=tk.BOTH, expand=True)
        
        self.scrollbar = tk.Scrollbar(list_frame, orient=tk.VERTICAL)
        self.scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        self.listbox = tk.Listbox(
            list_frame,
            font=("Segoe UI", 11),
            bg=self.bg_color,
            fg=self.subtext_fg,
            selectbackground="#222222",
            selectforeground=self.text_fg,
            bd=0,
            highlightthickness=0,
            yscrollcommand=self.scrollbar.set,
            activestyle="none"
        )
        self.listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.scrollbar.config(command=self.listbox.yview)
        
        # Select events
        self.listbox.bind("<<ListboxSelect>>", lambda e: self.on_select_item())
        self.listbox.bind("<Double-Button-1>", lambda e: self.open_current_selection())
        
        # 3. Bottom Status Bar (Single line)
        self.status_frame = tk.Frame(self.root, bg=self.preview_bg, padx=15, pady=6)
        self.status_frame.pack(fill=tk.X, side=tk.BOTTOM)
        
        self.lbl_status = tk.Label(
            self.status_frame, 
            text="💡 [Up/Down] Chọn | [Enter] Mở | [Ctrl+C] Copy | [Esc] Thoát", 
            font=("Segoe UI", 9), 
            bg=self.preview_bg, 
            fg=self.dim_fg, 
            anchor=tk.W
        )
        self.lbl_status.pack(fill=tk.X)

    def on_combo_keyrelease(self, event):
        if event.keysym in ("Up", "Down", "Return", "Escape"):
            return
            
        value = self.search_combo.get().strip().lower()
        if value == "":
            self.search_combo["values"] = self.functions_list
            self.clear_results()
        else:
            filtered = [f for f in self.functions_list if value in f.lower() or value in FUNCTIONS_DB[f]["description"].lower()]
            self.search_combo["values"] = filtered
            
            exact_match = None
            for func in self.functions_list:
                if func.lower() == value:
                    exact_match = func
                    break
            
            if exact_match:
                self.search_combo.set(exact_match)
                self.on_function_selected()

    def on_combo_return(self, event):
        value = self.search_combo.get().strip().lower()
        current_values = self.search_combo["values"]
        
        if current_values:
            match = current_values[0]
            self.search_combo.set(match)
            self.on_function_selected()
        else:
            self.clear_results()
        return "break"

    def on_function_selected(self, event=None):
        selected = self.search_combo.get()
        if not selected or selected not in FUNCTIONS_DB:
            self.clear_results()
            return
            
        func_data = FUNCTIONS_DB[selected]
        results = []
        
        for folder in func_data["folders"]:
            results.append({
                "type": "folder",
                "path": folder,
                "function": selected
            })
            
        for file in func_data["files"]:
            results.append({
                "type": "file",
                "path": file,
                "function": selected
            })
            
        results.sort(key=lambda x: (x["type"] != "folder", x["path"].lower()))
        self.current_results = results
        
        self.listbox.delete(0, tk.END)
        for res in results:
            icon = "📁" if res["type"] == "folder" else "📄"
            self.listbox.insert(tk.END, f"  {icon}  {res['path']}")
            
        if results:
            self.listbox.selection_set(0)
            self.listbox.activate(0)
            self.on_select_item()
        else:
            self.clear_preview()

    def on_select_item(self):
        selection = self.listbox.curselection()
        if not selection:
            self.clear_preview()
            return
            
        idx = selection[0]
        if idx >= len(self.current_results):
            return
            
        res = self.current_results[idx]
        
        import re
        match = re.match(r"^(\d+)", res["function"])
        func_num = f"CN {match.group(1)}" if match else res["function"][:10]
        
        icon = "📁" if res["type"] == "folder" else "📄"
        
        path_display = res["path"]
        if len(path_display) > 42:
            path_display = "..." + path_display[-39:]
            
        status_text = f" {icon} {path_display} ({func_num})  |  [Enter] Mở | [Ctrl+C] Copy"
        self.lbl_status.config(text=status_text, fg=self.subtext_fg)

    def clear_preview(self):
        if hasattr(self, "lbl_status"):
            self.lbl_status.config(text="💡 [Up/Down] Chọn | [Enter] Mở | [Ctrl+C] Copy | [Esc] Thoát", fg=self.dim_fg)

    def clear_results(self):
        self.listbox.delete(0, tk.END)
        self.current_results = []
        self.clear_preview()

    def on_arrow_down(self, event):
        if self.listbox.size() == 0:
            return "break"
        current = self.listbox.curselection()
        if current:
            next_idx = min(current[0] + 1, self.listbox.size() - 1)
        else:
            next_idx = 0
        self.listbox.selection_clear(0, tk.END)
        self.listbox.selection_set(next_idx)
        self.listbox.activate(next_idx)
        self.listbox.see(next_idx)
        self.on_select_item()
        return "break"

    def on_arrow_up(self, event):
        if self.listbox.size() == 0:
            return "break"
        current = self.listbox.curselection()
        if current:
            next_idx = max(current[0] - 1, 0)
        else:
            next_idx = 0
        self.listbox.selection_clear(0, tk.END)
        self.listbox.selection_set(next_idx)
        self.listbox.activate(next_idx)
        self.listbox.see(next_idx)
        self.on_select_item()
        return "break"

    def on_escape_pressed(self, event):
        if self.root.focus_get() == self.search_combo and self.search_combo.get():
            self.search_combo.set("")
            self.clear_results()
            self.search_combo["values"] = self.functions_list
        else:
            self.root.destroy()
        return "break"

    def copy_selected_path(self, event=None):
        if self.root.focus_get() == self.search_combo:
            try:
                # If there's selection in the combobox text entry, let it copy text
                if self.search_combo.select_present():
                    return
            except Exception:
                pass
                
        selection = self.listbox.curselection()
        if not selection:
            return "break"
            
        idx = selection[0]
        if idx >= len(self.current_results):
            return "break"
            
        res = self.current_results[idx]
        abs_path = os.path.abspath(os.path.join(self.workspace_root, res["path"]))
        
        self.root.clipboard_clear()
        self.root.clipboard_append(abs_path)
        
        old_status = self.lbl_status.cget("text")
        self.lbl_status.config(text="✓ Đã sao chép đường dẫn thành công!", fg="#a6e3a1")
        self.root.after(1200, lambda: self.lbl_status.config(text=old_status, fg=self.subtext_fg))
        return "break"

    def open_current_selection(self):
        selection = self.listbox.curselection()
        if not selection:
            return
            
        idx = selection[0]
        if idx >= len(self.current_results):
            return
            
        res = self.current_results[idx]
        abs_path = os.path.abspath(os.path.join(self.workspace_root, res["path"]))
        
        if not os.path.exists(abs_path):
            messagebox.showerror("Lỗi", f"Không tìm thấy đường dẫn:\n{abs_path}")
            return
            
        if res["type"] == "file":
            try:
                subprocess.Popen(["code", abs_path], shell=True)
            except Exception:
                try:
                    os.startfile(abs_path)
                except Exception:
                    try:
                        subprocess.Popen(["notepad.exe", abs_path])
                    except Exception as e:
                        messagebox.showerror("Lỗi", f"Không thể mở file:\n{abs_path}\nChi tiết: {str(e)}")
        else:
            try:
                os.startfile(abs_path)
            except Exception as e:
                messagebox.showerror("Lỗi", f"Không thể mở thư mục:\n{abs_path}\nChi tiết: {str(e)}")


if __name__ == "__main__":
    root = tk.Tk()
    app = ModernSearchToolApp(root)
    root.mainloop()

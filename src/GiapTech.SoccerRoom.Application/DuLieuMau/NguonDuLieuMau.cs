namespace GiapTech.SoccerRoom.Application.DuLieuMau;

/// <summary>
/// Dữ liệu tĩnh cho bộ mẫu: tên người, sân bãi, mô tả CLB.
///
/// Viết tay thay vì sinh máy ("Cầu thủ 1", "Đội A"): bộ dữ liệu để test tay thì phải ĐỌC ĐƯỢC.
/// Tên máy sinh làm mọi màn hình trông giống nhau và không ai nhận ra sai sót hiển thị — ví dụ
/// tên dài bị cắt, hay hai người trùng tên viết tắt.
///
/// Đặt ở Application (không phải Infrastructure) vì nó là dữ liệu nghiệp vụ, không phải chi
/// tiết hạ tầng.
/// </summary>
internal static class NguonDuLieuMau
{
    /// <summary>
    /// Tên cầu thủ CLB chính. Trộn đủ kiểu để bắt lỗi hiển thị:
    /// tên 4 chữ, tên 2 chữ, tên có dấu nặng/ngã, và một tên rất dài.
    /// </summary>
    public static readonly (string HoTen, int SoAo, string ViTri, string NgaySinh)[] CauThuMotDoi =
    [
        ("Nguyễn Văn Hùng",              1, "GK", "1995-03-12"),
        ("Trần Quốc Bảo",               2, "CB", "1997-07-04"),
        ("Lê Minh Tuấn",                3, "CB", "1994-11-22"),
        ("Phạm Đình Nghĩa",             4, "LB", "1998-01-30"),
        ("Hoàng Anh Dũng",              5, "RB", "1996-05-17"),
        ("Vũ Trọng Nhân",               6, "CM", "1999-09-09"),
        ("Đặng Hữu Phước",              7, "CM", "1993-12-25"),
        ("Bùi Thanh Sơn",               8, "LM", "2000-02-14"),
        ("Đỗ Quang Huy",                9, "ST", "1996-08-08"),
        ("Ngô Bá Khá",                 10, "ST", "1998-06-21"),
        ("Dương Tấn Lộc",              11, "RM", "1997-04-03"),
        ("Trịnh Công Thành",           12, "GK", "2001-10-10"),
        ("Lý Nhật Trường",             13, "CB", "1995-01-19"),
        ("Mai Xuân Kiên",              14, "CM", "1999-03-27"),
        ("Cao Việt Hoàng",             15, "ST", "2000-12-01"),
        ("Nguyễn Trần Lê Bảo Khánh",   16, "LM", "1994-07-15"),
        ("Hồ Sỹ Đạt",                  17, "RB", "2002-05-05"),
        ("Phan Duy Khương",            18, "CB", "1996-09-30"),
    ];

    /// <summary>Cầu thủ CLB thứ hai — tên khác hẳn để không lẫn khi so hai CLB.</summary>
    public static readonly (string HoTen, int SoAo, string ViTri, string NgaySinh)[] CauThuDoiHai =
    [
        ("Võ Thành Đạt",       1, "GK", "1996-02-11"),
        ("Huỳnh Bảo Long",     2, "CB", "1995-06-23"),
        ("Trương Minh Khôi",   3, "CB", "1998-08-14"),
        ("Đinh Gia Bảo",       4, "LB", "1999-04-06"),
        ("Tạ Quang Vinh",      5, "RB", "1997-11-28"),
        ("Lâm Chí Cường",      6, "CM", "1994-03-09"),
        ("Chu Văn Toàn",       7, "CM", "2000-07-19"),
        ("Đoàn Ngọc Sang",     8, "LM", "2001-01-25"),
        ("Kiều Hải Đăng",      9, "ST", "1996-10-02"),
        ("Nguyễn Phú Quý",    10, "ST", "1998-12-16"),
        ("Thái Bình Minh",    11, "RM", "1999-05-31"),
        ("Ưng Văn Lực",       12, "GK", "2002-09-12"),
        ("Bạch Đức Thắng",    13, "CB", "1995-08-20"),
        ("Quách Tiến Dũng",   14, "CM", "1997-02-07"),
        ("Lưu Hoàng Nam",     15, "ST", "2000-11-11"),
        ("Hà Trung Hiếu",     16, "RM", "1993-06-18"),
    ];

    /// <summary>
    /// Sân bóng thật ở Đà Nẵng. Dùng địa danh thật để dữ liệu đọc lên nghe được — "Sân A",
    /// "Sân B" không giúp gì khi kiểm màn hình.
    /// </summary>
    public static readonly string[] SanBong =
    [
        "Sân Hoà Xuân", "Sân Quân Khu 5", "Sân Chi Lăng", "Sân Tuyên Sơn",
        "Sân Hoà Quý", "Sân Thọ Quang", "Sân Cẩm Lệ", "Sân Đại học TDTT",
    ];

    /// <summary>Đối thủ ngoài hệ thống — đội phong trào không dùng app này.</summary>
    public static readonly string[] DoiThuNgoai =
    [
        "FC Sông Hàn", "FC Thanh Bình", "Anh Em FC", "FC Cầu Rồng",
        "Bạn Bè FC", "FC Non Nước", "Xóm Chài FC",
    ];

    /// <summary>
    /// Nhận xét sau trận — viết như thủ quỹ/trưởng nhóm thật viết, có cả trận thắng và trận
    /// thua. Nhận xét chung chung ("trận đấu tốt") làm màn đánh giá trông như dữ liệu giả.
    /// </summary>
    public static readonly string[] NhanXetThang =
    [
        "Hàng công dứt điểm tốt, giữ nhịp cả trận. Hậu vệ cánh lên tham gia tấn công hợp lý.",
        "Thắng nhưng 15 phút cuối để đối phương ép sân, cần chú ý thể lực.",
        "Chuyền một nhịp nhanh hơn hẳn mấy trận trước. Thủ môn cản được một quả rất khó.",
    ];

    public static readonly string[] NhanXetHoa =
    [
        "Cầm bóng nhiều mà thiếu người dứt điểm. Hai cơ hội rõ ràng bị bỏ lỡ.",
        "Đối thủ chơi phòng ngự số đông, mình bế tắc ở giữa sân.",
    ];

    public static readonly string[] NhanXetThua =
    [
        "Vào sân thiếu tập trung, để thủng lưới sớm rồi phải đuổi theo cả trận.",
        "Hàng phòng ngự bị khai thác ở khoảng trống giữa hai trung vệ.",
        "Thua vì thể lực, đội hình mỏng do nhiều người báo nghỉ sát giờ.",
    ];

    /// <summary>Ghi chú trận — thứ trưởng nhóm thật ghi để nhớ.</summary>
    public static readonly string[] GhiChuTran =
    [
        "Nhớ mang bộ áo trắng, đối thủ mặc đỏ.",
        "Sân xa 20km, hẹn tập trung sớm 30 phút.",
        "Thuê sân 2 tiếng, quá giờ phải trả thêm.",
        "Có 3 người báo về muộn, chuẩn bị phương án dự bị.",
        null!,
    ];

    /// <summary>Khoản chi thường gặp của CLB phong trào.</summary>
    public static readonly (string NoiDung, decimal SoTien, string NguoiChi)[] KhoanChiMau =
    [
        ("Thuê sân Hoà Xuân (2 tiếng)",      600_000m, "Nguyễn Văn Hùng"),
        ("Nước uống + đá cho cả đội",        150_000m, "Bùi Thanh Sơn"),
        ("Mua 2 quả bóng Adidas size 5",     700_000m, "Lê Minh Tuấn"),
        ("Trọng tài trận giao hữu",          200_000m, "Nguyễn Văn Hùng"),
        ("In số áo cho 5 cầu thủ mới",       350_000m, "Đỗ Quang Huy"),
        ("Băng keo, bình phun lạnh",         180_000m, "Trần Quốc Bảo"),
        ("Thuê sân Chi Lăng (trận lượt về)", 800_000m, "Nguyễn Văn Hùng"),
    ];

    /// <summary>
    /// Video sau trận. URL thật của YouTube/Drive nhưng là link công khai bất kỳ — mục đích chỉ
    /// để kiểm màn hình hiển thị và mở link được, không phải nội dung.
    /// </summary>
    public static readonly (string Ten, string Url, string? MoTa)[] VideoMau =
    [
        ("Toàn trận", "https://www.youtube.com/watch?v=dQw4w9WgXcQ", "Bản đầy đủ 90 phút"),
        ("Highlight bàn thắng", "https://www.youtube.com/watch?v=9bZkp7q19f0", "Cắt 3 phút"),
        ("Góc camera sau khung thành", "https://drive.google.com/file/d/1abcXYZ/view", null),
    ];

    /// <summary>
    /// CLB phụ trong Cộng đồng: khu vực khác nhau để test bộ lọc, mô tả viết như đội thật tự
    /// giới thiệu khi tìm đối.
    /// </summary>
    public static readonly (string Ten, string VietTat, string KhuVuc, string SanNha,
        string MoTa, string LienHe)[] ClbPhu =
    [
        ("FC Thanh Khê", "TKH", "Thanh Khê, Đà Nẵng", "Sân Quân Khu 5",
            "Đội 7 người, đá chiều chủ nhật hàng tuần. Trình độ trung bình, đá vui là chính.",
            "0905 111 222"),
        ("FC Ngũ Hành Sơn", "NHS", "Ngũ Hành Sơn, Đà Nẵng", "Sân Hoà Quý",
            "Đội 11 người sinh hoạt 3 năm, tìm đối cân sức. Đá tối thứ 4 và chiều CN.",
            "0905 333 444"),
        ("FC Liên Chiểu", "LCH", "Liên Chiểu, Đà Nẵng", "Sân Đại học TDTT",
            "Anh em công nhân khu công nghiệp, đá sau giờ làm. Sân nhà có đèn.",
            "0905 555 666"),
        ("Sơn Trà United", "STU", "Sơn Trà, Đà Nẵng", "Sân Thọ Quang",
            "Đội mới thành lập đầu năm, đang tìm đối để cọ xát. Chưa có nhiều trận.",
            "0905 777 888"),
        ("FC Cẩm Lệ", "CLE", "Cẩm Lệ, Đà Nẵng", "Sân Cẩm Lệ",
            "Đá 7 người, độ tuổi 30+. Ưu tiên đá sáng cuối tuần.",
            "0905 999 000"),
    ];
}

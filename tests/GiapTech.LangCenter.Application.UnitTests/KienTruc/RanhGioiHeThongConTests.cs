using System.Text.RegularExpressions;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Ranh giới giữa ba hệ thống con HRM · CRM · LMS — chốt ở
/// <c>docs/02-kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md</c>.
///
/// ADR-0005 chốt **một source**, KHÔNG tách ba. Nhưng "một source" không có nghĩa là các module
/// được gọi nhau tự do: mỗi lần một module với tay sang module khác là một sợi dây phải cắt nếu
/// ngày nào thật sự cần tách (ba dấu hiệu xét lại ghi trong ADR).
///
/// Test này giữ đường lui **rẻ**: mọi cầu nối chéo hệ thống phải được khai tường minh ở đây.
/// Thêm một cầu nối mới sẽ phải dừng lại viết ra lý do — không âm thầm bồi thêm dây.
///
/// Quét mã NGUỒN (regex trên file) chứ không reflection: quan hệ ở đây là "namespace nào gọi
/// namespace nào", mà sau khi biên dịch thì thông tin đó tan vào IL và không phân biệt được
/// `Crm` gọi `DaoTao` với `DaoTao` gọi `Crm`.
/// </summary>
public class RanhGioiHeThongConTests
{
    /// <summary>Thư mục Application ứng với từng hệ thống con.</summary>
    private static readonly Dictionary<string, string> ThuMucHeThong = new()
    {
        ["Crm"] = "Crm",
        ["Lms"] = "DaoTao",
        ["Hrm"] = "NhanSu",
        ["Ldp"] = "Ldp",
    };

    /// <summary>
    /// Cầu nối chéo hệ thống **được phép**, kèm lý do.
    ///
    /// Khoá: `file → namespace bị gọi`.
    /// </summary>
    private static readonly Dictionary<string, string> CauNoiDuocPhep = new()
    {
        ["YeuCauXepLopDtos.cs → DaoTao"] =
            "FR-21 là cầu nối CRM → LMS theo thiết kế: bán khoá xong xếp học viên vào lớp. "
            + "Handler duyệt phải gọi BaoDamThayLop của LMS để tôn trọng IPhamViLopHoc — viết "
            + "lại phép kiểm phạm vi ở CRM là hai bản sẽ trôi khỏi nhau. Xem ADR-0005.",

        ["NguoiDungDtos.cs → Crm"] =
            "FR-25 (13/09/2026): quản trị tạo hồ sơ học viên cho người đã mua khoá online ở "
            + "CRM, nối `KHACH_HANG.nguoi_dung_id` để một con người không thành hai hồ sơ. "
            + "Chỉ ĐỌC để kiểm tồn tại rồi GHI cột nối — không gọi handler nào của CRM, không "
            + "đọc số tiền (chốt 12/09: chỉ CRM nắm tiền).",

        ["HocVienTrongLopDtos.cs → Crm"] =
            "Chiều NGƯỢC của FR-21 (12/09/2026): danh sách học viên trong lớp hiện tên nhân "
            + "viên kinh doanh đã tạo hồ sơ khách, theo yêu cầu chủ sản phẩm. Chỉ ĐỌC "
            + "`KHACH_HANG.NguoiTao` qua IAppDbContext, không gọi handler nào của CRM. "
            + "Gác riêng bằng `KhachHang.Xem` vì endpoint này gác `LopHoc.Xem` — quyền mà giáo "
            + "viên và học viên cũng có.",

        ["ThongKeNhanSuDtos.cs → Crm"] =
            "FR-29 (16/09/2026): xếp hạng nhân viên kinh doanh theo DOANH THU và SỐ HỌC VIÊN "
            + "mang về — chủ sản phẩm yêu cầu tường minh, và hai chỉ số đó chỉ CRM có. Chỉ ĐỌC "
            + "`DANG_KY_KHOA_HOC` và `KHACH_HANG` qua IAppDbContext, dùng ĐÚNG cách quy doanh số "
            + "của FR-28 (`KhachHang.CreatedById`, `SoTien * TyGiaVeVnd`) — lấy mốc khác thì cùng "
            + "một người ra hai con số ở hai màn. Không gọi handler nào của CRM. Gác bằng "
            + "`ThongKeNhanSu.Xem`, tách khỏi `NhanSu.Xem`: đây là doanh số của cả đội.",

        ["ThongKeNhanSuDtos.cs → DaoTao"] =
            "FR-29 (16/09/2026): xếp hạng giáo viên / trợ giảng theo SỐ LỚP, SỐ BUỔI DẠY ĐỦ và "
            + "CHẤT LƯỢNG GIẢNG DẠY do học viên chấm — cả ba chỉ số chỉ LMS có. Chỉ ĐỌC "
            + "`LOP_HOC`, `LOP_HOC_TRO_GIANG`, `BUOI_HOC`, `NHAN_XET_BUOI_HOC`; không gọi handler "
            + "nào của LMS và KHÔNG đọc cột tiền nào (chốt 12/09: chỉ CRM nắm tiền). "
            + "Lưu ý khi sửa: `BUOI_HOC.giao_vien_id = null` nghĩa là *giáo viên chính của lớp*, "
            + "nên phải rơi về `LOP_HOC.giao_vien_chinh_id` — đếm thẳng cột đó thì mọi giáo viên "
            + "ra 0 buổi.",

        ["LienHeLandingCommands.cs → Crm"] =
            "FR-30 (24/09/2026): chuyển một liên hệ từ form trang đích thành KHÁCH HÀNG CRM — "
            + "đó chính là giá trị của landing, không có cầu nối này thì form chỉ là một bảng "
            + "chết. Chỉ GHI một `KHACH_HANG` mới với `Nguon = TuLanding`, không đọc và không "
            + "sửa khách hàng nào đang có, không gọi handler nào của CRM. "
            + "Gác riêng bằng `LienHeLanding.ChuyenCrm`, tách khỏi `Xem`: đọc số điện thoại "
            + "khách và tạo khách hàng trong CRM là hai quyền khác nhau.",
    };

    /// <summary>
    /// Entity của hệ thống khác mà một hệ thống KHÔNG được đọc thẳng qua `IAppDbContext`.
    ///
    /// Vì sao cần riêng test này: test namespace ở trên chỉ bắt `using ...Application.Crm` và
    /// `Crm.X` — nó **không bắt** `db.KhachHangs` vì mọi `DbSet` nằm chung trong `IAppDbContext`.
    /// Phát hiện 12/09/2026 khi thêm tên NVKD vào DTO học viên: cầu nối mới lọt qua lưới cũ mà
    /// test vẫn xanh.
    /// </summary>
    private static readonly Dictionary<string, string[]> DbSetCuaHeThong = new()
    {
        ["Crm"] = ["KhachHangs", "DangKyKhoaHocs", "ThuTienDangKys", "LichSuChamSocs"],
    };

    /// <summary>
    /// Thư mục KHÔNG thuộc hệ thống con nào — quét chúng bằng lưới nào, và vì sao.
    ///
    /// **Lỗ hổng thứ hai cùng loại, vá 13/09/2026.** Bản trước chỉ quét ba thư mục trong
    /// <see cref="ThuMucHeThong"/>, nên `QuanTri/`, `DangNhap/`, `Common/` **hoàn toàn ngoài
    /// lưới**: thêm `db.KhachHangs` vào `QuanTri/NguoiDung/NguoiDungDtos.cs` mà test vẫn xanh.
    ///
    /// Hôm qua vá lỗ hổng "namespace không thấy `db.X`"; hôm nay là "lưới không phủ hết thư
    /// mục". Cùng một bài học: **danh sách những-chỗ-được-kiểm phải là danh sách đóng**, tức
    /// mọi thư mục phải rơi vào một trong hai nhóm, và nhóm miễn trừ phải khai lý do.
    /// Canh bởi <see cref="Moi_thu_muc_phai_nam_trong_luoi_hoac_duoc_khai_mien_tru"/>.
    /// </summary>
    /// <summary>
    /// Thư mục **dùng chung nhiều hệ thống** — vẫn bị quét cầu nối như thư mục hệ thống con.
    ///
    /// Khác <see cref="ThuMucMienTru"/>: ở đây đọc DbSet của hệ thống khác vẫn phải khai cầu
    /// nối. `QuanTri/` chứa hồ sơ con người mà cả ba hệ thống dùng, nhưng nối sang `KHACH_HANG`
    /// của CRM là quyết định kiến trúc, không phải chuyện thường ngày.
    /// </summary>
    private static readonly Dictionary<string, string> ThuMucDungChung = new()
    {
        ["QuanTri"] =
            "Hồ sơ con người, tài khoản, phân quyền — cả ba hệ thống dùng. Nhưng đọc DbSet của "
            + "hệ thống khác ở đây VẪN phải khai cầu nối (FR-25 đã khai).",
    };

    private static readonly Dictionary<string, string> ThuMucMienTru = new()
    {
        ["Common"] =
            "Hạ tầng dùng chung (behavior, model, interface, ảnh) — không thuộc hệ thống nào "
            + "và mọi hệ thống đều dùng. Đọc DbSet ở đây là chuyện bình thường.",
        ["DangNhap"] =
            "Xác thực dùng chung cho cả ba hệ thống, đứng trước mọi phân nhóm chức năng.",
        ["ChuHeThong"] =
            "Site của chủ sản phẩm (ADR-0009) — đứng TRÊN mọi tenant nên không thuộc hệ thống "
            + "con nào. Nó quản vòng đời tenant (tạo, gắn domain), cố ý KHÔNG đọc dữ liệu "
            + "nghiệp vụ bên trong tenant; muốn đọc thì phải là một ADR mới.",
    };

    /// <summary>Mọi thư mục con của `Application/` trừ những cái đã khai miễn trừ.</summary>
    private static IEnumerable<DirectoryInfo> ThuMucCanQuet(DirectoryInfo app)
        => app.GetDirectories()
            .Where(d => d.Name is not ("bin" or "obj"))
            .Where(d => !ThuMucMienTru.ContainsKey(d.Name));

    /// <summary>
    /// CHIỀU NGƯỢC — mỗi thư mục con của `Application/` phải được **PHÂN LOẠI TƯỜNG MINH**:
    /// hệ thống con (`ThuMucHeThong`), hoặc miễn trừ có khai lý do (`ThuMucMienTru`).
    ///
    /// Thư mục dùng chung như `QuanTri/` vẫn bị quét cầu nối, nhưng phải khai để người thêm thư
    /// mục mới **dừng lại một nhịp** mà quyết định nó thuộc loại nào — thay vì mặc định rơi vào
    /// nhóm "quét mọi thứ" rồi bất ngờ thấy test đỏ ở chỗ không liên quan.
    ///
    /// Bản đầu của test này (13/09/2026) **không thể đỏ**: nó hỏi "thư mục có được quét không",
    /// mà `ThuMucCanQuet` = mọi thư mục trừ miễn trừ, nên câu trả lời luôn là có. Phát hiện khi
    /// tiêm đột biến — tạo thư mục `HocTapTrucTuyen/` mới, test vẫn xanh.
    /// </summary>
    [Fact]
    public void Moi_thu_muc_phai_nam_trong_luoi_hoac_duoc_khai_mien_tru()
    {
        var app = GocApplication();

        var daPhanLoai = ThuMucHeThong.Values
            .Concat(ThuMucMienTru.Keys)
            .Concat(ThuMucDungChung.Keys)
            .ToHashSet();

        var chuaPhanLoai = app.GetDirectories()
            .Where(d => d.Name is not ("bin" or "obj"))
            // Thư mục không có file .cs nào thì không có gì để kiểm.
            .Where(d => d.GetFiles("*.cs", SearchOption.AllDirectories).Length > 0)
            .Where(d => !daPhanLoai.Contains(d.Name))
            .Select(d => d.Name)
            .ToList();

        Assert.True(
            chuaPhanLoai.Count == 0,
            "Thư mục sau chưa được phân loại:\n"
            + string.Join("\n", chuaPhanLoai)
            + "\n\nKhai vào MỘT trong ba danh sách kèm lý do:\n"
            + "  ThuMucHeThong  — là một hệ thống con (Crm/Lms/Hrm)\n"
            + "  ThuMucDungChung — nhiều hệ thống dùng, VẪN bị quét cầu nối\n"
            + "  ThuMucMienTru  — hạ tầng, không quét\n"
            + "Để trống là tạo một vùng mù trong lưới.");

        // Miễn trừ phải TRỎ TỚI thư mục có thật — đổi tên thư mục mà quên sửa danh sách thì
        // miễn trừ thành vô nghĩa, và thư mục mới lặng lẽ ra ngoài lưới.
        var mienTruMa = ThuMucMienTru.Keys
            .Where(t => !app.GetDirectories().Any(d => d.Name == t)).ToList();

        Assert.True(
            mienTruMa.Count == 0,
            $"Miễn trừ trỏ tới thư mục không tồn tại: {string.Join(", ", mienTruMa)}");
    }

    private static DirectoryInfo GocApplication()
    {
        // Đi ngược từ thư mục test lên tới thư mục chứa .sln, rồi vào src/.
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !d.GetFiles("*.sln").Any()) d = d.Parent;

        Assert.NotNull(d);
        var app = new DirectoryInfo(Path.Combine(d!.FullName, "src", "GiapTech.LangCenter.Application"));
        Assert.True(app.Exists, $"Không thấy thư mục Application ở {app.FullName}");
        return app;
    }

    /// <summary>
    /// Chiều thứ hai: đọc thẳng `DbSet` của hệ thống khác qua `IAppDbContext` cũng là cầu nối,
    /// dù không có `using` nào. Phải khai như mọi cầu nối khác.
    /// </summary>
    [Fact]
    public void Khong_doc_thang_DbSet_cua_he_thong_khac_ngoai_cau_noi_da_khai()
    {
        var app = GocApplication();
        var viPham = new List<string>();

        // Quét MỌI thư mục không miễn trừ, không chỉ ba thư mục hệ thống con: `QuanTri/` từng
        // lọt hoàn toàn khỏi lưới (13/09/2026). Thư mục dùng chung thì `tenHeThong` là null nên
        // mọi DbSet của mọi hệ thống đều bị soi.
        foreach (var dir in ThuMucCanQuet(app))
        {
            var tenHeThong = ThuMucHeThong
                .FirstOrDefault(x => x.Value == dir.Name).Key;

            foreach (var (heThongKhac, dbSets) in DbSetCuaHeThong)
            {
                if (heThongKhac == tenHeThong) continue;

                foreach (var f in dir.GetFiles("*.cs", SearchOption.AllDirectories))
                {
                    var noiDung = File.ReadAllText(f.FullName);
                    foreach (var ds in dbSets)
                    {
                        if (!Regex.IsMatch(noiDung, $@"\bdb\.{ds}\b")) continue;

                        var khoa = $"{f.Name} → {heThongKhac}";
                        if (!CauNoiDuocPhep.ContainsKey(khoa)) viPham.Add($"{khoa} (db.{ds})");
                    }
                }
            }
        }

        Assert.True(
            viPham.Count == 0,
            "Đọc thẳng DbSet của hệ thống khác mà chưa khai cầu nối:\n"
            + string.Join("\n", viPham.Distinct())
            + "\n\nMọi DbSet nằm chung trong IAppDbContext nên compiler không chặn, và test "
            + "namespace cũng không thấy. Nếu đây là cầu nối nghiệp vụ thật → thêm vào "
            + "CauNoiDuocPhep kèm lý do.");
    }

    [Fact]
    public void Cac_he_thong_con_khong_goi_cheo_nhau_ngoai_cau_noi_da_khai()
    {
        var app = GocApplication();
        var viPham = new List<string>();

        foreach (var (tenHeThong, thuMuc) in ThuMucHeThong)
        {
            var dir = new DirectoryInfo(Path.Combine(app.FullName, thuMuc));
            if (!dir.Exists) continue;

            // Các hệ thống KHÁC mà file trong thư mục này không được gọi tới.
            var thuMucKhac = ThuMucHeThong
                .Where(x => x.Key != tenHeThong)
                .Select(x => x.Value)
                .ToList();

            foreach (var f in dir.GetFiles("*.cs", SearchOption.AllDirectories))
            {
                var noiDung = File.ReadAllText(f.Name == "" ? f.FullName : f.FullName);

                foreach (var khac in thuMucKhac)
                {
                    // Khớp `using ...Application.DaoTao` và cả `DaoTao.LopHoc.X` dùng trực tiếp.
                    var goi = Regex.IsMatch(
                        noiDung,
                        $@"(using\s+GiapTech\.LangCenter\.Application\.{khac}\b)|(\b{khac}\.[A-Z])");
                    if (!goi) continue;

                    var khoa = $"{f.Name} → {khac}";
                    if (!CauNoiDuocPhep.ContainsKey(khoa)) viPham.Add(khoa);
                }
            }
        }

        Assert.True(
            viPham.Count == 0,
            $"Module gọi chéo hệ thống mà chưa khai: {string.Join(", ", viPham.Distinct())}.\n"
            + "ADR-0005 chốt MỘT source, nhưng mỗi cầu nối chéo là một sợi dây phải cắt nếu sau "
            + "này tách. Chọn một:\n"
            + "  1. Nếu là logic dùng chung của cả ba → chuyển vào Application/Common.\n"
            + "  2. Nếu là cầu nối nghiệp vụ thật (như FR-21) → thêm vào CauNoiDuocPhep kèm lý "
            + "do, và ghi vào ADR-0005.");
    }

    /// <summary>
    /// Chiều ngược: cầu nối đã khai mà nay không còn thì phải xoá khỏi danh sách.
    ///
    /// Danh sách chỉ có giá trị khi nó khớp thực tế — một mục lạc hậu làm người đọc sau tưởng
    /// vẫn còn dây, và làm việc đánh giá "tách có đắt không" sai.
    /// </summary>
    [Fact]
    public void Danh_sach_cau_noi_khong_chua_muc_da_lac_hau()
    {
        var app = GocApplication();
        var thucTe = new HashSet<string>();

        // Quét CÙNG phạm vi với test chính. Lệch phạm vi thì cầu nối khai ở thư mục dùng chung
        // (`QuanTri/`) bị báo "lạc hậu" ngay sau khi khai — đã xảy ra 13/09/2026 với FR-25.
        foreach (var dir in ThuMucCanQuet(app))
        {
            var tenHeThong = ThuMucHeThong.FirstOrDefault(x => x.Value == dir.Name).Key;

            foreach (var khac in ThuMucHeThong.Where(x => x.Key != tenHeThong).Select(x => x.Value))
            {
                foreach (var f in dir.GetFiles("*.cs", SearchOption.AllDirectories))
                {
                    var noiDung = File.ReadAllText(f.FullName);
                    if (Regex.IsMatch(noiDung,
                        $@"(using\s+GiapTech\.LangCenter\.Application\.{khac}\b)|(\b{khac}\.[A-Z])"))
                        thucTe.Add($"{f.Name} → {khac}");

                    // Quét CẢ cầu nối kiểu đọc thẳng DbSet — nếu không thì cầu nối loại đó bị
                    // báo "lạc hậu" ngay sau khi khai (12/09/2026).
                    var tenHtKhac = ThuMucHeThong.First(x => x.Value == khac).Key;
                    if (DbSetCuaHeThong.TryGetValue(tenHtKhac, out var dbSets)
                        && dbSets.Any(ds => Regex.IsMatch(noiDung, $@"\bdb\.{ds}\b")))
                        thucTe.Add($"{f.Name} → {khac}");
                }
            }
        }

        var lacHau = CauNoiDuocPhep.Keys.Where(k => !thucTe.Contains(k)).ToList();

        Assert.True(
            lacHau.Count == 0,
            $"Cầu nối đã khai nhưng không còn trong mã: {string.Join(", ", lacHau)}. "
            + "Bỏ khỏi CauNoiDuocPhep — danh sách phải khớp thực tế mới dùng được để đánh giá "
            + "chi phí tách hệ thống.");
    }
}

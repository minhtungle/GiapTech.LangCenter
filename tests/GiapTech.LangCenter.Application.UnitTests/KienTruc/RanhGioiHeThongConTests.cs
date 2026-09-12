using System.Text.RegularExpressions;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Ranh giới giữa ba hệ thống con HRM · CRM · LMS — chốt ở
/// <c>docs/kien-truc/adr/0005-mot-source-va-doi-ten-langcenter.md</c>.
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

        ["HocVienTrongLopDtos.cs → Crm"] =
            "Chiều NGƯỢC của FR-21 (12/09/2026): danh sách học viên trong lớp hiện tên nhân "
            + "viên kinh doanh đã tạo hồ sơ khách, theo yêu cầu chủ sản phẩm. Chỉ ĐỌC "
            + "`KHACH_HANG.NguoiTao` qua IAppDbContext, không gọi handler nào của CRM. "
            + "Gác riêng bằng `KhachHang.Xem` vì endpoint này gác `LopHoc.Xem` — quyền mà giáo "
            + "viên và học viên cũng có.",
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

        foreach (var (tenHeThong, thuMuc) in ThuMucHeThong)
        {
            var dir = new DirectoryInfo(Path.Combine(app.FullName, thuMuc));
            if (!dir.Exists) continue;

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

        foreach (var (tenHeThong, thuMuc) in ThuMucHeThong)
        {
            var dir = new DirectoryInfo(Path.Combine(app.FullName, thuMuc));
            if (!dir.Exists) continue;

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

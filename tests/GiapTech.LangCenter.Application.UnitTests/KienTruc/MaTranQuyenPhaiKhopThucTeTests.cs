using System.Reflection;
using System.Text.RegularExpressions;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Application.UnitTests.KienTruc;

/// <summary>
/// Canh **ma trận phân quyền khớp thực tế** (14/09/2026).
///
/// Trước đây màn phân quyền hiện mọi thao tác cho mọi chức năng (`Enum.GetNames<HanhDong>()`),
/// nên **31/108 ô bật cũng không làm gì**: `NhatKyHeThong.Xoa` (nhật ký không xoá được),
/// `ChucVu.Xem` (bị `NhanSu.Xem` thay), `HocOnline.Sua`… Người cấu hình quyền không có cách nào
/// biết ô nào có tác dụng, nên tick bừa cho chắc — đúng thứ làm phân quyền mất ý nghĩa.
///
/// Bộ test này theo cùng khuôn với năm test kiến trúc khác: **cả hai chiều**, để danh sách khai
/// không lạc hậu theo mã.
/// </summary>
public class MaTranQuyenPhaiKhopThucTeTests
{
    /// <summary>
    /// Cặp (chức năng, thao tác) được khai nhưng CHƯA endpoint nào dùng — kèm lý do.
    ///
    /// Ô ở đây vẫn hiện trên màn phân quyền và người dùng tick được, nên mỗi cái phải có lý do
    /// thật: hoặc nó là quyền phạm vi (không qua `[RequirePermission]`), hoặc nó gác một
    /// endpoint sắp có. Không được dùng danh sách này để lách việc dọn ô chết.
    /// </summary>
    private static readonly Dictionary<string, string> ChuaDungNhungCoLyDo = new()
    {
        ["LopHocToanTrungTam.Xem"] =
            "Quyền PHẠM VI, không phải quyền gọi endpoint. `IPhamViLopHoc` đọc nó để quyết "
            + "định thấy lớp mình dạy hay toàn trung tâm. Xem `ChucNang.PhamViDuLieu`.",
        ["KhoaOnline.Xem"] =
            "Quyền PHẠM VI, không gác endpoint. `PhamViKhoaOnline.LocKhoa` đọc nó để phân biệt "
            + "người SOẠN nội dung (thấy mọi khoá, kể cả nháp) với người HỌC (chỉ thấy khoá "
            + "mình được ghi danh, cộng khoá có bài công khai). Endpoint đọc gác bằng "
            + "`HocOnline.Xem` để học viên đọc được mà không soạn được.",

        ["NhanXetBuoiHoc.Xem"] =
            "Quyền MỞ RỘNG PHẠM VI trong handler, không gác endpoint. `GET buoi-hoc/{id}/"
            + "nhan-xet` gác bằng `.TuLam` (thao tác hẹp nhất — ai cũng đọc được nhận xét của "
            + "mình); `LayNhanXetBuoiHocHandler` đọc thêm `.Xem` để quyết định trả nhận xét "
            + "của MỌI người hay chỉ của mình. Gác endpoint bằng `.Xem` sẽ chặn học viên đọc "
            + "nhận xét chính họ. Canh bởi `NhanXetRiengTuTests`.",

        ["LopHocToanTrungTam.Sua"] =
            "Cùng lẽ với `.Xem`: cấp Xem mà không cấp Sua = xem mọi lớp nhưng chỉ sửa lớp "
            + "mình phụ trách.",
    };

    private static IEnumerable<(string ChucNang, string HanhDong)> CapDangDung()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !d.GetFiles("*.sln").Any()) d = d.Parent;
        Assert.NotNull(d);

        var api = new DirectoryInfo(Path.Combine(
            d!.FullName, "src", "GiapTech.LangCenter.API", "Controllers"));
        Assert.True(api.Exists, $"Không thấy thư mục Controllers ở {api.FullName}");

        foreach (var f in api.GetFiles("*.cs", SearchOption.AllDirectories))
        foreach (Match m in Regex.Matches(
                     File.ReadAllText(f.FullName),
                     @"RequirePermission\(ChucNang\.(\w+),\s*HanhDong\.(\w+)\)"))
            yield return (m.Groups[1].Value, m.Groups[2].Value);
    }

    /// <summary>
    /// Mọi cặp endpoint đang dùng phải được khai trong `ThaoTacCua`.
    ///
    /// Thiếu khai thì ô đó **không hiện trên màn phân quyền** — không ai cấp được, và endpoint
    /// trả 403 cho tất cả kể cả quản trị. Triệu chứng rất khó chẩn: đăng nhập được, màn cũ chạy
    /// bình thường, chỉ một endpoint hỏng.
    /// </summary>
    [Fact]
    public void Moi_quyen_endpoint_dang_dung_phai_duoc_khai()
    {
        var thieu = CapDangDung()
            .Distinct()
            .Where(c => !ChucNang.ThaoTacCua(c.ChucNang)
                .Any(h => h.ToString() == c.HanhDong))
            .Select(c => $"{c.ChucNang}.{c.HanhDong}")
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            thieu.Count == 0,
            "Endpoint gác bằng quyền CHƯA KHAI trong `ChucNang.ThaoTacTheoChucNang`:\n  "
            + string.Join("\n  ", thieu)
            + "\n\nÔ không khai thì không hiện trên màn phân quyền ⇒ không ai cấp được ⇒ "
            + "endpoint trả 403 cho mọi người, kể cả quản trị.");
    }

    /// <summary>
    /// CHIỀU NGƯỢC — mọi cặp đã khai phải có endpoint dùng, hoặc nằm trong danh sách khai lý do.
    ///
    /// Không có chiều này thì danh sách khai phình ra theo thời gian và ta quay về đúng chỗ cũ:
    /// một ma trận đầy ô không ai biết có tác dụng gì.
    /// </summary>
    [Fact]
    public void Moi_quyen_da_khai_phai_co_endpoint_dung_hoac_khai_ly_do()
    {
        var dangDung = CapDangDung().Distinct().ToHashSet();

        var oChet = ChucNang.TatCa
            .SelectMany(cn => ChucNang.ThaoTacCua(cn).Select(hd => (cn, hd: hd.ToString())))
            .Where(x => !dangDung.Contains(x))
            .Select(x => $"{x.cn}.{x.hd}")
            .Where(k => !ChuaDungNhungCoLyDo.ContainsKey(k))
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            oChet.Count == 0,
            "Quyền đã khai nhưng KHÔNG endpoint nào dùng:\n  "
            + string.Join("\n  ", oChet)
            + "\n\nNgười cấu hình sẽ tick một ô không có tác dụng. Bỏ khỏi "
            + "`ThaoTacTheoChucNang`, hoặc thêm vào `ChuaDungNhungCoLyDo` kèm lý do thật.");
    }

    /// <summary>
    /// Danh sách khai lý do không được lạc hậu: mục nào nay đã có endpoint dùng thì bỏ ra.
    /// </summary>
    [Fact]
    public void Danh_sach_khai_ly_do_khong_chua_muc_da_lac_hau()
    {
        var dangDung = CapDangDung()
            .Select(c => $"{c.ChucNang}.{c.HanhDong}")
            .ToHashSet();

        var lacHau = ChuaDungNhungCoLyDo.Keys.Where(dangDung.Contains).OrderBy(x => x).ToList();

        Assert.True(
            lacHau.Count == 0,
            $"Đã có endpoint dùng, bỏ khỏi `ChuaDungNhungCoLyDo`: {string.Join(", ", lacHau)}");
    }

    /// <summary>
    /// Chức năng chưa có API (`ThaoTacCua` rỗng) phải **thật sự** không endpoint nào dùng.
    ///
    /// Khai rỗng mà vẫn có endpoint gác bằng nó là cách chắc chắn tạo ra một endpoint không ai
    /// gọi được.
    /// </summary>
    [Fact]
    public void Chuc_nang_khai_rong_thi_khong_endpoint_nao_duoc_dung()
    {
        var rong = ChucNang.TatCa.Where(cn => ChucNang.ThaoTacCua(cn).Count == 0).ToHashSet();
        var viPham = CapDangDung()
            .Where(c => rong.Contains(c.ChucNang))
            .Select(c => $"{c.ChucNang}.{c.HanhDong}")
            .Distinct()
            .ToList();

        Assert.True(
            viPham.Count == 0,
            "Chức năng khai RỖNG (chưa có API) nhưng vẫn gác endpoint:\n  "
            + string.Join("\n  ", viPham));
    }

    /// <summary>
    /// Nhóm quyền mặc định không được cấp ô mà màn phân quyền KHÔNG hiện.
    ///
    /// Ba nhóm khai tường minh (`CuaGiaoVien`, `CuaTroGiang`, `CuaHocVien`) từng cấp
    /// <c>BaiKiemTra</c>, <c>BaiLamKiemTra</c>, <c>ThongKe</c> — ba chức năng chưa có endpoint
    /// nào, nên `ThaoTacCua` trả rỗng và ma trận không hiện cột nào cho chúng. Hệ quả: hàng
    /// `QUYEN_CHUC_NANG` tồn tại trong DB mà người quản trị **không thấy và không bỏ được** —
    /// đúng loại quyền ẩn mà cả đợt dọn 14/09/2026 nhắm tới. Nhóm quản trị không mắc vì nó
    /// sinh từ `ThaoTacCua`; ba nhóm kia khai tay nên phải có test canh.
    ///
    /// Đọc bằng cách phân tích mã nguồn, không tham chiếu assembly: `Application.UnitTests`
    /// không được phụ thuộc `Infrastructure` (quy tắc #10) — xem `LuatPhuThuocTests`.
    /// </summary>
    [Fact]
    public void Nhom_quyen_mac_dinh_khong_cap_o_khong_hien_tren_man_phan_quyen()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !d.GetFiles("*.sln").Any()) d = d.Parent;
        Assert.NotNull(d);

        var f = new FileInfo(Path.Combine(
            d!.FullName, "src", "GiapTech.LangCenter.Infrastructure",
            "Persistence", "Seed", "NhomQuyenMacDinh.cs"));
        Assert.True(f.Exists, $"Không thấy {f.FullName}");

        // Chỉ lấy dòng khai quyền `(ChucNang.X, ...)`, bỏ chú thích để không ăn tên trong văn bản.
        var noiDung = string.Join('\n', File.ReadAllLines(f.FullName)
            .Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));

        var khaiTrongNhom = Regex.Matches(noiDung, @"\(ChucNang\.(\w+),")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        // Tự kiểm: regex phải thật sự bắt được gì đó, nếu không test luôn xanh một cách vô nghĩa.
        Assert.True(
            khaiTrongNhom.Count > 5,
            $"Chỉ bắt được {khaiTrongNhom.Count} chức năng trong `NhomQuyenMacDinh.cs` — "
            + "regex hỏng hoặc file đã đổi cấu trúc. Sửa test, đừng bỏ qua.");

        var oAn = khaiTrongNhom
            .Where(cn => ChucNang.ThaoTacCua(cn).Count == 0)
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            oAn.Count == 0,
            "Nhóm quyền mặc định cấp chức năng mà `ThaoTacCua` trả RỖNG:\n  "
            + string.Join("\n  ", oAn)
            + "\n\nMa trận không hiện cột nào cho chúng ⇒ hàng `QUYEN_CHUC_NANG` nằm trong DB "
            + "mà người quản trị không thấy, không bỏ được. Khi chức năng đó có API thật thì "
            + "khai vào `ThaoTacTheoChucNang` trước, rồi mới cấp cho nhóm.");
    }

    /// <summary>
    /// Chức năng mà **tầng phạm vi** đọc (`CoQuyenAsync` ngoài `[RequirePermission]`) phải còn
    /// sống trong bảng khai.
    ///
    /// Vì sao cần riêng test này: hai test trên chỉ quét `[RequirePermission]` trong
    /// `Controllers/`. Quyền đọc ở tầng phạm vi **không xuất hiện ở đó**, nên với chúng test
    /// "ô chết" ở trên báo *đúng* rằng không endpoint nào dùng — và người sửa (tôi, 14/09/2026)
    /// nghe theo mà **xoá mất `KhoaOnline.Xem`**. Hậu quả: `PhamViKhoaOnline.LocKhoa` dùng quyền
    /// đó để phân biệt người SOẠN với người HỌC, mất nó thì không ai là người soạn nữa và khoá
    /// vừa tạo biến mất khỏi màn của chính người tạo. 489 test backend xanh hết; chỉ E2E bắt được.
    ///
    /// Test này quét ngược từ mã nguồn tầng phạm vi, nên xoá một chức năng đang được tầng đó
    /// đọc sẽ đỏ ngay — không phải đợi E2E.
    /// </summary>
    [Fact]
    public void Chuc_nang_tang_pham_vi_doc_phai_con_trong_bang_khai()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !d.GetFiles("*.sln").Any()) d = d.Parent;
        Assert.NotNull(d);

        var thuMuc = new[]
        {
            Path.Combine(d!.FullName, "src", "GiapTech.LangCenter.Infrastructure", "Identity"),
            Path.Combine(d.FullName, "src", "GiapTech.LangCenter.Application"),
        };

        var goiTen = new List<(string File, string ChucNang)>();
        foreach (var tm in thuMuc.Select(x => new DirectoryInfo(x)).Where(x => x.Exists))
        foreach (var f in tm.GetFiles("*.cs", SearchOption.AllDirectories))
        {
            var noiDung = File.ReadAllText(f.FullName);
            // Bắt `CoQuyenAsync(... ChucNang.X ...)` kể cả khi xuống dòng giữa tham số.
            foreach (Match m in Regex.Matches(
                         noiDung, @"CoQuyenAsync\s*\([^;]{0,200}?ChucNang\.(\w+)", RegexOptions.Singleline))
                goiTen.Add((f.Name, m.Groups[1].Value));
        }

        // Tự kiểm: tầng phạm vi CÓ tồn tại, nên quét ra 0 chỗ nghĩa là regex hỏng.
        Assert.True(
            goiTen.Count > 0,
            "Không quét được lời gọi `CoQuyenAsync` nào ở tầng phạm vi — regex hỏng hoặc mã đã "
            + "chuyển chỗ. Sửa test, đừng bỏ qua: đây chính là test canh thứ bị xoá nhầm.");

        var chet = goiTen
            .Where(x => ChucNang.ThaoTacCua(x.ChucNang).Count == 0)
            .Select(x => $"{x.ChucNang} (đọc ở {x.File})")
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            chet.Count == 0,
            "Tầng phạm vi đọc chức năng KHÔNG còn thao tác nào trong bảng khai:\n  "
            + string.Join("\n  ", chet)
            + "\n\nChức năng không hiện trên màn phân quyền ⇒ không ai cấp được ⇒ phép lọc "
            + "phạm vi luôn rơi vào nhánh hẹp nhất. Không có endpoint nào 403 nên rất khó chẩn: "
            + "dữ liệu chỉ đơn giản là biến mất khỏi danh sách.");
    }

    /// <summary>
    /// Giá trị số của `HanhDong` cơ bản **không được đổi**: `QUYEN_CHUC_NANG.hanh_dong` lưu số
    /// nguyên, nên đổi 2 thành 3 là âm thầm biến quyền "Sửa" của mọi nhóm thành "Xoá".
    /// </summary>
    [Theory]
    [InlineData(HanhDong.Xem, 0)]
    [InlineData(HanhDong.Them, 1)]
    [InlineData(HanhDong.Sua, 2)]
    [InlineData(HanhDong.Xoa, 3)]
    public void Gia_tri_so_cua_hanh_dong_co_ban_khong_doi(HanhDong hanhDong, int mong)
        => Assert.Equal(mong, (int)hanhDong);

    /// <summary>
    /// Mọi chức năng trong `TatCa` phải có mặt ở đúng MỘT nhóm: hệ thống con, hoặc dùng chung.
    ///
    /// Thiếu phân loại thì nó không hiện ở sidebar nào — người dùng có quyền mà không thấy menu.
    /// </summary>
    [Fact]
    public void Moi_chuc_nang_phai_thuoc_dung_mot_nhom()
    {
        var loi = new List<string>();

        foreach (var cn in ChucNang.TatCa)
        {
            var coHeThong = ChucNang.HeThongCua(cn) is not null;
            var laDungChung = ChucNang.DungChung.Contains(cn);

            if (coHeThong == laDungChung)
                loi.Add($"{cn}: {(coHeThong ? "ở CẢ HAI nhóm" : "KHÔNG ở nhóm nào")}");
        }

        Assert.True(loi.Count == 0, string.Join("\n", loi));
    }
}

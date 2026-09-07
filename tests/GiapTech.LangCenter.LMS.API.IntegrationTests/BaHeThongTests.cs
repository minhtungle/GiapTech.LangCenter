using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Domain.Common;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

/// <summary>
/// Ba hệ thống con HRM · CRM · LMS — nhóm chức năng phân quyền, KHÔNG phải ba ứng dụng.
///
/// Trọng tâm: `/toi/he-thong` phải trả đúng hệ thống người dùng vào được, vì frontend dùng nó
/// để dựng bộ chuyển và lọc sidebar. Trả thiếu thì người dùng mất lối vào cả một hệ thống; trả
/// thừa thì họ bấm vào và gặp sidebar trống.
/// </summary>
public class BaHeThongTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<string> QuyenId(HttpClient admin, string ten)
        => (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    /// <summary>Tạo nhóm quyền chỉ gồm đúng các chức năng truyền vào.</summary>
    private static async Task<string> TaoNhomQuyen(
        HttpClient admin, string ten, params string[] chucNangs)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/quyen", new
        {
            TenQuyen = ten,
            MoTa = "test",
            ChucNangs = chucNangs.Select(cn => new { TenChucNang = cn, HanhDongs = new[] { "Xem" } })
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Guid>()).ToString();
    }

    private static async Task<Guid> TaoNguoiDung(
        HttpClient c, string username, string[] quyenIds)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<List<string>> HeThongCuaToi(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/toi/he-thong"))
            .GetProperty("ma").EnumerateArray().Select(x => x.GetString()!).ToList();

    // ---------- /toi/he-thong ----------

    /// <summary>Quản trị có toàn quyền → vào được cả ba hệ thống, đúng thứ tự enum.</summary>
    [Fact]
    public async Task Quan_tri_vao_duoc_ca_ba_he_thong()
    {
        var admin = await Client();
        Assert.Equal(["Hrm", "Crm", "Lms"], await HeThongCuaToi(admin));
    }

    /// <summary>
    /// Người chỉ có quyền LMS **không** được trả về Hrm/Crm — nếu không, bộ chuyển hiện ba lựa
    /// chọn mà hai trong số đó dẫn tới sidebar trống.
    /// </summary>
    [Fact]
    public async Task Chi_co_quyen_lms_thi_chi_vao_duoc_lms()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ LMS", ChucNang.LopHoc, ChucNang.TaiLieu);
        await TaoNguoiDung(admin, "chi-lms", [q]);

        var c = await Client("chi-lms", "matkhau123");
        Assert.Equal(["Lms"], await HeThongCuaToi(c));
    }

    /// <summary>Có quyền ở hai hệ thống thì trả đúng hai — không suy rộng ra cái thứ ba.</summary>
    [Fact]
    public async Task Co_quyen_hai_he_thong_thi_tra_ve_hai()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "HRM và CRM",
            ChucNang.NhanVienKinhDoanh, ChucNang.DoanhThu);
        await TaoNguoiDung(admin, "hrm-crm", [q]);

        var c = await Client("hrm-crm", "matkhau123");
        Assert.Equal(["Hrm", "Crm"], await HeThongCuaToi(c));
    }

    /// <summary>
    /// **Chức năng dùng chung KHÔNG mở lối vào hệ thống nào.**
    ///
    /// Người chỉ quản trị tài khoản mà "vào được" cả ba thì ba lối vào đều chỉ hiện đúng cụm
    /// Quản trị — ba lựa chọn giống hệt nhau, bộ chuyển thành vô nghĩa. Đây là khẳng định dễ
    /// làm sai nhất của thiết kế này.
    /// </summary>
    [Fact]
    public async Task Chuc_nang_dung_chung_khong_mo_loi_vao_he_thong()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ quản trị tài khoản",
            ChucNang.TaiKhoan, ChucNang.PhanQuyen, ChucNang.NhatKyHeThong);
        await TaoNguoiDung(admin, "chi-quan-tri", [q]);

        var c = await Client("chi-quan-tri", "matkhau123");
        Assert.Empty(await HeThongCuaToi(c));
    }

    // ---------- /quyen/danh-muc ----------

    /// <summary>
    /// Danh mục trả nhóm hệ thống để frontend dựng tab. Nhóm ở BACKEND chứ không để frontend
    /// khai lại bản đồ — hai bản đồ hai nơi sẽ trôi khỏi nhau.
    /// </summary>
    [Fact]
    public async Task Danh_muc_tra_ve_nhom_theo_he_thong()
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        var nhoms = d.GetProperty("heThongs").EnumerateArray().ToList();

        // Ba hệ thống + một nhóm dùng chung.
        Assert.Equal(4, nhoms.Count);
        Assert.Equal(
            ["Hrm", "Crm", "Lms", "DungChung"],
            nhoms.Select(x => x.GetProperty("ma").GetString()));

        var dungChung = nhoms.Single(x => x.GetProperty("dungChung").GetBoolean());
        Assert.Equal("DungChung", dungChung.GetProperty("ma").GetString());

        // Ba nhóm hệ thống không được đánh dấu dùng chung.
        Assert.Equal(3, nhoms.Count(x => !x.GetProperty("dungChung").GetBoolean()));
    }

    /// <summary>
    /// Gộp bốn nhóm phải phủ kín `chucNangs` — không thiếu, không lặp. Thiếu thì chức năng đó
    /// **không xuất hiện ở tab nào** và admin không có cách cấp quyền cho nó qua UI.
    /// </summary>
    [Fact]
    public async Task Cac_nhom_phu_kin_danh_muc_chuc_nang()
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        var tatCa = d.GetProperty("chucNangs").EnumerateArray()
            .Select(x => x.GetString()!).OrderBy(x => x).ToList();

        var gom = d.GetProperty("heThongs").EnumerateArray()
            .SelectMany(n => n.GetProperty("chucNangs").EnumerateArray())
            .Select(x => x.GetString()!)
            .ToList();

        Assert.Equal(tatCa.Count, gom.Count);
        Assert.Equal(tatCa, gom.OrderBy(x => x));
    }

    /// <summary>
    /// Ba chức năng mới của HRM/CRM phải có trong danh mục — nếu không thì không cấp quyền
    /// được cho chúng, và mục sidebar tương ứng không bao giờ hiện.
    /// </summary>
    [Theory]
    [InlineData(ChucNang.NhanVienKinhDoanh)]
    [InlineData(ChucNang.GiaoVienNhanSu)]
    [InlineData(ChucNang.DoanhThu)]
    public async Task Chuc_nang_moi_co_trong_danh_muc(string chucNang)
    {
        var admin = await Client();
        var d = await admin.GetFromJsonAsync<JsonElement>("/api/v1/quyen/danh-muc");

        Assert.Contains(chucNang,
            d.GetProperty("chucNangs").EnumerateArray().Select(x => x.GetString()));
    }

    /// <summary>
    /// Nhóm "Quản trị viên" của tenant mới phải có cả ba chức năng mới — seeder lặp
    /// `ChucNang.TatCa` nên tự có, nhưng test này canh việc ai đó chuyển sang liệt kê tay.
    /// </summary>
    [Fact]
    public async Task Nhom_quan_tri_co_du_chuc_nang_moi()
    {
        var admin = await Client();
        var quyens = await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen");
        var quanTri = quyens!.Single(q => q.GetProperty("tenQuyen").GetString() == "Quản trị viên");

        var co = quanTri.GetProperty("chucNangs").EnumerateArray()
            .Select(x => x.GetProperty("tenChucNang").GetString()!).ToHashSet();

        Assert.Contains(ChucNang.NhanVienKinhDoanh, co);
        Assert.Contains(ChucNang.GiaoVienNhanSu, co);
        Assert.Contains(ChucNang.DoanhThu, co);
    }

    /// <summary>
    /// Cách ly tenant: `/toi/he-thong` suy từ quyền của CHÍNH phiên hiện tại, nên tài khoản
    /// tenant B không mượn được quyền của tenant A.
    /// </summary>
    [Fact]
    public async Task He_thong_cua_toi_khong_ro_ri_qua_tenant()
    {
        var admin = await Client();
        var q = await TaoNhomQuyen(admin, "Chỉ HRM cách ly", ChucNang.NhanVienKinhDoanh);
        await TaoNguoiDung(admin, "chi-hrm-cach-ly", [q]);

        var cA = await Client("chi-hrm-cach-ly", "matkhau123");
        Assert.Equal(["Hrm"], await HeThongCuaToi(cA));

        // Admin tenant B có toàn quyền của RIÊNG tenant B — vẫn cả ba, nhưng do quyền của
        // chính nó, không phải mượn từ A.
        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123" });
        dn.EnsureSuccessStatusCode();
        cB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await dn.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("accessToken").GetString());

        Assert.Equal(["Hrm", "Crm", "Lms"], await HeThongCuaToi(cB));
    }
}

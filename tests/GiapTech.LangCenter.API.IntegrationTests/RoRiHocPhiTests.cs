using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Canh RÒ RỈ SỐ TIỀN qua DTO của module khác.
///
/// `HocPhiTests` canh module `/hoc-phi` và nó vốn được bảo vệ đúng. Lỗ hổng thật nằm chỗ khác:
/// số tiền từng nằm trong `LopHocDto.HocPhi` và `HocVienTrongLopDto.HocPhiApDung` — hai DTO đi
/// qua `IPhamViLopHoc`, tầng mà giáo viên VÀ học viên đều lọt. Cổng
/// `[RequirePermission(HocPhi, ...)]` không cứu được vì hai endpoint đó gác bằng `LopHoc.Xem`.
///
/// Hệ quả thật, đo được bằng curl trước khi vá: giáo viên đọc được mức miễn giảm của từng học
/// viên, và học viên đọc được học phí của bạn cùng lớp.
///
/// **Thêm trường tiền vào bất kỳ DTO nào không thuộc module học phí → thêm một test ở đây.**
/// </summary>
public class RoRiHocPhiTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private static async Task<Guid> TaoNguoi(
        HttpClient c, string username, string loai, string[] quyenIds)
    {
        var res = await c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = quyenIds, PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Lớp có giáo viên, trợ giảng và hai học viên — mỗi người một mức học phí khác nhau.</summary>
    private async Task<(Guid Lop, Guid Hv1, Guid Hv2)> DungLop(HttpClient c, string nhan)
    {
        var qGv = await QuyenTheoTen(c, "Giáo viên");
        var qTg = await QuyenTheoTen(c, "Trợ giảng");
        var qHv = await QuyenTheoTen(c, "Học viên");

        var gv = await TaoNguoi(c, $"gvrr-{nhan}", "GiaoVien", [qGv]);
        var tg = await TaoNguoi(c, $"tgrr-{nhan}", "TroGiang", [qTg]);
        var hv1 = await TaoNguoi(c, $"hvrr1-{nhan}", "HocVien", [qHv]);
        var hv2 = await TaoNguoi(c, $"hvrr2-{nhan}", "HocVien", [qHv]);

        var taoLop = await c.PostAsJsonAsync("/api/v1/lop-hoc", new
        {
            Ten = $"Lớp rò rỉ {nhan}", GiaoVienChinhId = gv, HinhThuc = "Offline",
            HocPhi = 9_000_000m, TroGiangIds = new[] { tg }
        });
        taoLop.EnsureSuccessStatusCode();
        var lop = await taoLop.Content.ReadFromJsonAsync<Guid>();

        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv1 } })).EnsureSuccessStatusCode();
        // Học viên 2 được miễn giảm — con số này là thứ nhạy cảm nhất, không ai ngoài admin
        // và chính họ được biết.
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoc-vien",
            new { HocVienIds = new[] { hv2 }, HocPhiApDung = 1_000_000m })).EnsureSuccessStatusCode();

        // Hoàn tất để công bố: lớp nháp chỉ người tạo thấy, giáo viên và học viên sẽ nhận 404.
        (await c.PostAsJsonAsync($"/api/v1/lop-hoc/{lop}/hoan-tat",
            new { NgayKhaiGiang = DateTimeOffset.UtcNow.AddDays(1) })).EnsureSuccessStatusCode();

        return (lop, hv1, hv2);
    }

    private static decimal? Tien(JsonElement e, string truong)
    {
        var p = e.GetProperty(truong);
        return p.ValueKind == JsonValueKind.Null ? null : p.GetDecimal();
    }

    [Fact]
    public async Task Giao_vien_khong_thay_hoc_phi_cua_lop()
    {
        var c = await Client();
        await DungLop(c, "gvlop");

        var cGv = await Client("gvrr-gvlop", "matkhau123");
        var ds = await cGv.GetFromJsonAsync<JsonElement>("/api/v1/lop-hoc");

        var lops = ds.GetProperty("duLieu").EnumerateArray().ToList();
        Assert.NotEmpty(lops);   // giáo viên VẪN thấy lớp — chỉ không thấy tiền
        Assert.All(lops, l => Assert.Null(Tien(l, "hocPhi")));
    }

    /// <summary>
    /// Chi tiết là chỗ dễ quên: danh sách lọc đúng mà chi tiết không lọc thì gõ thẳng id vào
    /// URL là đọc được — cùng loại bẫy với IDOR.
    /// </summary>
    [Fact]
    public async Task Giao_vien_khong_thay_hoc_phi_o_man_chi_tiet_lop()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "gvct");

        var cGv = await Client("gvrr-gvct", "matkhau123");
        var l = await cGv.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");

        Assert.Null(Tien(l, "hocPhi"));
    }

    /// <summary>
    /// **Rò rỉ nặng nhất trước khi vá.** `HocPhiApDung` tiết lộ ai được miễn giảm và giảm bao
    /// nhiêu — giáo viên không có việc gì phải biết điều đó.
    /// </summary>
    [Fact]
    public async Task Giao_vien_khong_thay_muc_hoc_phi_rieng_cua_hoc_vien()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "gvmuc");

        var cGv = await Client("gvrr-gvmuc", "matkhau123");
        var ds = await cGv.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");

        Assert.Equal(2, ds!.Count);   // vẫn thấy đủ học viên để điểm danh
        Assert.All(ds, h => Assert.Null(Tien(h, "hocPhiApDung")));
    }

    [Fact]
    public async Task Tro_giang_khong_thay_bat_ky_so_tien_nao()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "tg");

        var cTg = await Client("tgrr-tg", "matkhau123");

        var l = await cTg.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.Null(Tien(l, "hocPhi"));

        var ds = await cTg.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");
        Assert.All(ds!, h => Assert.Null(Tien(h, "hocPhiApDung")));
    }

    /// <summary>
    /// Học viên thấy mức của CHÍNH MÌNH nhưng không thấy của bạn cùng lớp — nếu không thì cả
    /// lớp biết ai được giảm học phí.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_chi_thay_muc_hoc_phi_cua_chinh_minh()
    {
        var c = await Client();
        var (lop, hv1, hv2) = await DungLop(c, "hv");

        var cHv1 = await Client("hvrr1-hv", "matkhau123");
        var ds = await cHv1.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");

        var minh = ds!.Single(h => h.GetProperty("hocVienId").GetGuid() == hv1);
        var ban = ds!.Single(h => h.GetProperty("hocVienId").GetGuid() == hv2);

        Assert.Equal(9_000_000m, Tien(minh, "hocPhiApDung"));
        Assert.Null(Tien(ban, "hocPhiApDung"));
    }

    /// <summary>Học viên cũng không thấy mức chuẩn của lớp — chỉ mức áp dụng cho họ mới có nghĩa.</summary>
    [Fact]
    public async Task Hoc_vien_khong_thay_hoc_phi_chuan_cua_lop()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "hvchuan");

        var cHv = await Client("hvrr1-hvchuan", "matkhau123");
        var l = await cHv.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");

        Assert.Null(Tien(l, "hocPhi"));
    }

    /// <summary>Chiều ngược lại: vá xong admin phải vẫn thấy đủ, nếu không là vá hỏng.</summary>
    [Fact]
    public async Task Quan_tri_van_thay_du_moi_so_tien()
    {
        var c = await Client();
        var (lop, hv1, hv2) = await DungLop(c, "admin");

        var l = await c.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.Equal(9_000_000m, Tien(l, "hocPhi"));

        var ds = await c.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");
        Assert.Equal(
            9_000_000m,
            Tien(ds!.Single(h => h.GetProperty("hocVienId").GetGuid() == hv1), "hocPhiApDung"));
        Assert.Equal(
            1_000_000m,
            Tien(ds!.Single(h => h.GetProperty("hocVienId").GetGuid() == hv2), "hocPhiApDung"));
    }
}

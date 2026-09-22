using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
    private async Task<HttpClient> Client(string user = "manager", string mk = "manager123456")
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
                Username = username, MatKhau = "matkhau123456",
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

        var cGv = await Client("gvrr-gvlop", "matkhau123456");
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

        var cGv = await Client("gvrr-gvct", "matkhau123456");
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

        var cGv = await Client("gvrr-gvmuc", "matkhau123456");
        var ds = await cGv.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");

        Assert.Equal(2, ds!.Count);   // vẫn thấy đủ học viên để điểm danh
        Assert.All(ds, h => Assert.Null(Tien(h, "hocPhiApDung")));
    }

    [Fact]
    public async Task Tro_giang_khong_thay_bat_ky_so_tien_nao()
    {
        var c = await Client();
        var (lop, _, _) = await DungLop(c, "tg");

        var cTg = await Client("tgrr-tg", "matkhau123456");

        var l = await cTg.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
        Assert.Null(Tien(l, "hocPhi"));

        var ds = await cTg.GetFromJsonAsync<List<JsonElement>>($"/api/v1/lop-hoc/{lop}/hoc-vien");
        Assert.All(ds!, h => Assert.Null(Tien(h, "hocPhiApDung")));
    }

    /// <summary>
    /// **KHÔNG AI** đọc được tiền học qua API của LMS — kể cả quản trị viên (12/09/2026).
    ///
    /// Chốt với chủ sản phẩm: *"LMS không quản lý tiền học nữa, cũng không hiển thị tiền. Bảo
    /// mật thông tin — chỉ CRM mới nắm được số tiền."*
    ///
    /// Thay ba test cũ (`Hoc_vien_chi_thay_muc_hoc_phi_cua_chinh_minh`,
    /// `Quan_tri_van_thay_du_moi_so_tien`) vốn khẳng định **có người thấy được** — nay sai.
    /// Quy tắc mới **chặt hơn**: không còn nhánh nào trả ra số tiền, nên không còn chỗ để sai.
    /// </summary>
    [Fact]
    public async Task Khong_ai_thay_tien_hoc_qua_API_cua_LMS_ke_ca_quan_tri()
    {
        var c = await Client();
        var (lop, hv1, hv2) = await DungLop(c, "khongai");

        foreach (var (nhan, client) in new[]
                 {
                     ("quản trị", c),
                     ("giáo viên", await Client("gvrr-khongai", "matkhau123456")),
                     ("trợ giảng", await Client("tgrr-khongai", "matkhau123456")),
                     ("học viên", await Client("hvrr1-khongai", "matkhau123456")),
                 })
        {
            var l = await client.GetFromJsonAsync<JsonElement>($"/api/v1/lop-hoc/{lop}");
            Assert.True(Tien(l, "hocPhi") is null, $"{nhan} KHÔNG được thấy học phí lớp");

            var ds = await client.GetFromJsonAsync<List<JsonElement>>(
                $"/api/v1/lop-hoc/{lop}/hoc-vien");
            Assert.All(ds!, h => Assert.Null(Tien(h, "hocPhiApDung")));
        }

        // CHIỀU NGƯỢC: dữ liệu VẪN CÒN trong DB — chỉ ẩn khỏi API, không xoá. CRM và báo cáo
        // đọc từ đây. Thiếu phép kiểm này thì xoá sạch cột tiền cũng làm test xanh.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lopDb = await db.LopHocs.IgnoreQueryFilters().FirstAsync(x => x.Id == lop);
        Assert.Equal(9_000_000m, lopDb.HocPhi);

        var hvs = await db.LopHocHocViens.IgnoreQueryFilters()
            .Where(x => x.LopHocId == lop).ToListAsync();
        Assert.Equal(9_000_000m, hvs.Single(x => x.HocVienId == hv1).HocPhiApDung);
        Assert.Equal(1_000_000m, hvs.Single(x => x.HocVienId == hv2).HocPhiApDung);
    }

    /// <summary>
    /// Tên NHÂN VIÊN KINH DOANH trong `HocVienTrongLopDto` là dữ liệu CRM đi nhờ DTO của LMS
    /// (12/09/2026) — đúng cái bẫy đã làm rò rỉ học phí ở chính file test này.
    ///
    /// Endpoint gác bằng `LopHoc.Xem`, quyền mà GIÁO VIÊN và HỌC VIÊN đều có. Nếu không gác
    /// riêng bằng `KhachHang.Xem` thì họ đọc được ai bán khách nào — thông tin nội bộ của bộ
    /// phận kinh doanh.
    ///
    /// Có cả CHIỀU NGƯỢC (admin THẤY được): thiếu nó thì trả null cho mọi người cũng xanh, và
    /// tính năng coi như không tồn tại.
    /// </summary>
    [Fact]
    public async Task Giao_vien_va_hoc_vien_khong_thay_ten_nhan_vien_kinh_doanh()
    {
        var admin = await Client();
        var (lop, hv1, _) = await DungLop(admin, "nvkd");

        // Khách hàng do admin tạo, nối với hồ sơ học viên hv1 → NVKD chính là admin.
        var khach = await admin.PostAsJsonAsync("/api/v1/khach-hang", new
        {
            HoTen = "Khách của NVKD", SoDienThoai = "0988000111", NguoiDungId = hv1
        });
        khach.EnsureSuccessStatusCode();

        // ADMIN (có KhachHang.Xem) PHẢI thấy — chiều ngược.
        var cuaAdmin = await admin.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lop}/hoc-vien");
        var dongAdmin = cuaAdmin!.Single(x => x.GetProperty("hocVienId").GetGuid() == hv1);
        Assert.False(
            string.IsNullOrEmpty(dongAdmin.GetProperty("tenNhanVienKinhDoanh").GetString()),
            "Admin có KhachHang.Xem thì phải thấy tên nhân viên kinh doanh.");

        // GIÁO VIÊN không có KhachHang.Xem → null ở MỌI dòng.
        var gv = await Client($"gvrr-nvkd", "matkhau123456");
        var cuaGv = await gv.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lop}/hoc-vien");
        Assert.All(cuaGv!, x => Assert.Equal(
            JsonValueKind.Null, x.GetProperty("tenNhanVienKinhDoanh").ValueKind));

        // HỌC VIÊN cũng không — kể cả với dòng của CHÍNH MÌNH. Khác học phí (họ thấy số của
        // mình): ai bán mình không phải thông tin của mình.
        var hocVien = await Client($"hvrr1-nvkd", "matkhau123456");
        var cuaHv = await hocVien.GetFromJsonAsync<List<JsonElement>>(
            $"/api/v1/lop-hoc/{lop}/hoc-vien");
        Assert.All(cuaHv!, x => Assert.Equal(
            JsonValueKind.Null, x.GetProperty("tenNhanVienKinhDoanh").ValueKind));
    }
}

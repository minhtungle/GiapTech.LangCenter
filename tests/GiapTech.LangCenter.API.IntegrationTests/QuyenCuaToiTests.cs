using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// `GET /toi/quyen` — frontend dùng để ẩn menu và nút.
///
/// Endpoint này KHÔNG gác `[RequirePermission]`: ai đăng nhập cũng phải biết quyền của chính
/// mình, nếu không frontend không dựng nổi menu. An toàn vì nó **không nhận tham số id** —
/// không có đường dò quyền người khác.
///
/// Nhắc lại cho rõ: ẩn ở frontend là tiện lợi, không phải bảo vệ. Mọi endpoint vẫn tự gác
/// quyền của nó — xem `PhanQuyenVaCachLyTests` và `RoRiHocPhiTests`.
/// </summary>
public class QuyenCuaToiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Client(string user, string mk)
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

    private static async Task<HashSet<string>> Quyen(HttpClient c)
    {
        var ds = await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/toi/quyen");
        return ds!
            .Select(x => $"{x.GetProperty("chucNang").GetString()}:{x.GetProperty("hanhDong").GetString()}")
            .ToHashSet();
    }

    private async Task<string> QuyenTheoTen(HttpClient c, string ten)
        => (await c.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == ten)
            .GetProperty("id").GetString()!;

    private async Task<HttpClient> TaoVaDangNhap(string username, string loai, string tenNhom)
    {
        var admin = await Client("manager", "manager123");
        var quyen = await QuyenTheoTen(admin, tenNhom);

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Người {username}",
            LoaiNguoiDung = loai,
            TaiKhoan = new
            {
                Username = username, MatKhau = "matkhau123",
                QuyenIds = new[] { quyen }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        return await Client(username, "matkhau123");
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_khong_goi_duoc()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/toi/quyen");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>Quản trị có đủ quyền — nếu không thì màn quản trị sẽ trống trơn.</summary>
    [Fact]
    public async Task Quan_tri_thay_du_quyen()
    {
        var q = await Quyen(await Client("manager", "manager123"));

        Assert.Contains("HocPhi:Xem", q);
        Assert.Contains("LopHocToanTrungTam:Xem", q);
        Assert.Contains("TaiKhoan:Them", q);
        Assert.Contains("PhanQuyen:Sua", q);
    }

    /// <summary>
    /// Giáo viên không có `HocPhi` — đây chính là thứ frontend dùng để ẩn menu "Học phí" và
    /// tab Học phí, thay vì để họ bấm vào rồi mới nhận thông báo.
    /// </summary>
    [Fact]
    public async Task Giao_vien_khong_co_quyen_hoc_phi()
    {
        var q = await Quyen(await TaoVaDangNhap("gvqt", "GiaoVien", "Giáo viên"));

        Assert.DoesNotContain("HocPhi:Xem", q);
        Assert.DoesNotContain("LopHocToanTrungTam:Xem", q);
        // Nhưng vẫn đủ quyền dạy học.
        Assert.Contains("LopHoc:Xem", q);
        Assert.Contains("DiemDanh:Sua", q);
        Assert.Contains("BaiTap:Them", q);
    }

    /// <summary>
    /// Học viên CÓ `HocPhi.Xem` (để tra công nợ của mình) nhưng KHÔNG có
    /// `LopHocToanTrungTam` — cặp này quyết định họ thấy sổ của cả lớp hay chỉ của mình.
    /// Frontend dùng đúng cặp đó ở `useQuyen().xemTienCaLop()`.
    /// </summary>
    [Fact]
    public async Task Hoc_vien_co_HocPhi_Xem_nhung_khong_thay_ca_lop()
    {
        var q = await Quyen(await TaoVaDangNhap("hvqt", "HocVien", "Học viên"));

        Assert.Contains("HocPhi:Xem", q);
        Assert.DoesNotContain("LopHocToanTrungTam:Xem", q);
        // Và không có quyền ghi sổ.
        Assert.DoesNotContain("HocPhi:Them", q);
        Assert.DoesNotContain("HocPhi:Sua", q);
        Assert.DoesNotContain("HocPhi:Xoa", q);
    }

    /// <summary>
    /// `/toi/cau-hinh` trả múi giờ trung tâm. MỌI vai trò phải gọi được: giáo viên và học viên
    /// chính là người xem lịch nhiều nhất, mà `/thiet-lap` gác bằng `ThietLapChung.Xem`.
    ///
    /// Không có nó thì lịch vẽ theo múi giờ máy người xem — lệch giờ làm buổi nhảy sang ô ngày
    /// khác.
    /// </summary>
    [Fact]
    public async Task Moi_vai_tro_doc_duoc_mui_gio_trung_tam()
    {
        foreach (var c in new[]
                 {
                     await Client("manager", "manager123"),
                     await TaoVaDangNhap("gvtz", "GiaoVien", "Giáo viên"),
                     await TaoVaDangNhap("hvtz", "HocVien", "Học viên"),
                 })
        {
            var ch = await c.GetFromJsonAsync<JsonElement>("/api/v1/toi/cau-hinh");
            Assert.Equal("Asia/Ho_Chi_Minh", ch.GetProperty("muiGio").GetString());
        }
    }

    [Fact]
    public async Task Chua_dang_nhap_thi_khong_doc_duoc_cau_hinh()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/toi/cau-hinh");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>Trả quyền của CHÍNH mình, không phải của người gọi khác — không có tham số id.</summary>
    [Fact]
    public async Task Moi_nguoi_nhan_dung_quyen_cua_minh()
    {
        var qGv = await Quyen(await TaoVaDangNhap("gvqt2", "GiaoVien", "Giáo viên"));
        var qAdmin = await Quyen(await Client("manager", "manager123"));

        Assert.NotEqual(qGv.Count, qAdmin.Count);
        Assert.True(qAdmin.Count > qGv.Count);
    }
}

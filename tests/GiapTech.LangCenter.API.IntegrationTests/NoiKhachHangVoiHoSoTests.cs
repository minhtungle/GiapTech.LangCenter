using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-25 — nối hồ sơ học viên (LMS) với khách hàng đã có (CRM).
///
/// Bối cảnh: người mua khoá online là `KHACH_HANG` ở CRM, chưa phải học viên. Quản trị tạo tài
/// khoản học viên cho họ và **nối lại** để một con người không thành hai hồ sơ ở hai hệ thống.
///
/// **Không có liên kết tự động nào** giữa đơn hàng và quyền học (chốt 13/09/2026): CRM ghi tiền,
/// LMS cấp quyền, người điều phối là con người. Nên test ở đây chỉ canh phép NỐI, không canh
/// luồng "mua xong tự có tài khoản" — luồng đó cố ý không tồn tại.
/// </summary>
public class NoiKhachHangVoiHoSoTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Guid> TaoKhach(HttpClient c, string hoTen, string sdt)
    {
        var res = await c.PostAsJsonAsync("/api/v1/khach-hang",
            new { HoTen = hoTen, SoDienThoai = sdt });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Guid>();
    }

    private static Task<HttpResponseMessage> TaoHocVien(
        HttpClient c, string hoTen, Guid? khachHangId = null)
        => c.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = hoTen, LoaiNguoiDung = "HocVien", KhachHangId = khachHangId
        });

    private static async Task<JsonElement> Khach(HttpClient c, Guid id)
        => await c.GetFromJsonAsync<JsonElement>($"/api/v1/khach-hang/{id}");

    [Fact]
    public async Task Tao_hoc_vien_kem_khach_hang_thi_noi_hai_ho_so()
    {
        var c = await Client();
        var khach = await TaoKhach(c, "Khách mua khoá online", "0911000001");

        // Trạng thái đầu: chưa nối hồ sơ nào. Không có khẳng định này thì test xanh cả khi
        // `nguoiDungId` đã có sẵn giá trị từ trước, và phép nối chẳng làm gì cả.
        var truoc = await Khach(c, khach);
        Assert.Equal(JsonValueKind.Null, truoc.GetProperty("nguoiDungId").ValueKind);

        var res = await TaoHocVien(c, "Học viên online", khach);
        res.EnsureSuccessStatusCode();
        var nguoiDungId = await res.Content.ReadFromJsonAsync<Guid>();

        var sau = await Khach(c, khach);
        Assert.Equal(nguoiDungId, sau.GetProperty("nguoiDungId").GetGuid());
    }

    /// <summary>
    /// Ô chọn khách hàng là **TUỲ CHỌN** — học viên học thử hay được tặng khoá thì không có đơn
    /// nào ở CRM. Bắt buộc nối sẽ biến ca hợp lệ thành ca không nhập được.
    /// </summary>
    [Fact]
    public async Task Tao_hoc_vien_khong_kem_khach_hang_van_duoc()
    {
        var c = await Client();
        var res = await TaoHocVien(c, "Học viên học thử");
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Quy tắc #1 — `KHACH_HANG.nguoi_dung_id` là quan hệ **một-một**. Ghi đè sẽ âm thầm cắt hồ
    /// sơ cũ khỏi lịch sử mua hàng của chính họ, và không có gì báo vì cả hai đều là Guid hợp lệ.
    /// </summary>
    [Fact]
    public async Task Khach_da_noi_ho_so_khac_thi_bi_chan()
    {
        var c = await Client();
        var khach = await TaoKhach(c, "Khách đã có hồ sơ", "0911000002");

        (await TaoHocVien(c, "Hồ sơ thứ nhất", khach)).EnsureSuccessStatusCode();

        var res = await TaoHocVien(c, "Hồ sơ thứ hai", khach);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("KHACH_HANG_DA_NOI_HO_SO_KHAC", await res.Content.ReadAsStringAsync());

        // CHIỀU NGƯỢC — không có phần này thì test xanh cả khi phép nối hỏng hoàn toàn:
        // khách vẫn phải trỏ đúng hồ sơ ĐẦU TIÊN, không bị ghi đè cũng không bị xoá.
        var sau = await Khach(c, khach);
        Assert.Equal(JsonValueKind.String, sau.GetProperty("nguoiDungId").ValueKind);
    }

    /// <summary>Khách của trung tâm khác thì không nối được — cách ly tenant ở tầng ghi.</summary>
    [Fact]
    public async Task Khong_noi_duoc_khach_cua_tenant_khac()
    {
        var cA = await Client();
        var khachA = await TaoKhach(cA, "Khách của A", "0911000003");

        var cB = factory.CreateClient();
        var dn = await cB.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamB, Username = "manager", MatKhau = "manager123456" });
        dn.EnsureSuccessStatusCode();
        var token = (await dn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var cBauth = factory.CreateClient();
        cBauth.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 400 KHACH_HANG_KHONG_HOP_LE, không phải 403: Query Filter làm khách của A trở nên
        // KHÔNG TỒN TẠI với B, nên B không suy ra được là mã đó có thật ở đâu đó.
        var res = await TaoHocVien(cBauth, "Học viên của B", khachA);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("KHACH_HANG_KHONG_HOP_LE", await res.Content.ReadAsStringAsync());
    }
}

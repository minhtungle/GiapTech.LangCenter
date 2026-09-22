using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// Nhận diện trung tâm ở màn ĐĂNG NHẬP và trong sidebar (22/09/2026).
///
/// Yêu cầu chủ sản phẩm: *"nhập đúng mã trung tâm tại đăng nhập sẽ load đúng thông tin trung
/// tâm như trong thiết lập (logo, tên, ...), bên trong giao diện quản trị cũng vậy"*.
///
/// Hai ranh giới bảo mật phải giữ, và đó là trọng tâm của bộ test này:
///
/// 1. **Endpoint logo ẩn danh chỉ trả logo** — người gọi đưa MÃ TRUNG TÂM, không đưa khoá ảnh.
///    Mở `GET /anh/{khoa}` cho ẩn danh là mở luôn ảnh học viên, ảnh CCCD, ảnh QR chuyển khoản.
/// 2. **Endpoint tra tên không trả địa chỉ/liên hệ/id** — ai dò trúng mã 7 ký tự cũng đọc được.
/// </summary>
public class NhanDienTrungTamTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    /// <summary>
    /// Tra mã trả nhận diện công khai — và **KHÔNG** trả thứ không nên lộ.
    ///
    /// Chiều "không trả" quan trọng hơn: thêm một field vào DTO là lộ nó cho mọi người dò được
    /// mã, mà thêm thì dễ và không ai nhận ra.
    /// </summary>
    [Fact]
    public async Task Tra_ma_trung_tam_chi_tra_nhan_dien_cong_khai()
    {
        var res = await factory.CreateClient()
            .GetAsync($"/api/v1/auth/ten-trung-tam/{factory.MaTrungTamA}");
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("tenTrungTam").GetString()));
        Assert.True(body.TryGetProperty("tenVietTat", out _));
        Assert.True(body.TryGetProperty("coLogo", out _));

        /*
          Chiều LOẠI — những thứ dứt khoát không được trả cho người chưa đăng nhập.

          `diaChi` **đã rời khỏi danh sách cấm** ngày 22/09 (lần 2): chủ sản phẩm yêu cầu banner
          ở màn đăng nhập hiện thông tin trung tâm, và địa chỉ là thứ vẫn in trên biển hiệu.
          Nhưng `lienHe` thì GIỮ NGUYÊN trong danh sách cấm — số điện thoại là thứ người dò
          dùng được ngay, khác hẳn một dòng địa chỉ.

          `logoUrl`/`khoaLogo` vẫn cấm dù có `coLogo`: khoá mang `tenantId` ở đầu, trả ra là
          tặng người chưa đăng nhập một id thật.
        */
        foreach (var cam in new[] { "id", "lienHe", "soTaiKhoan", "logoUrl", "khoaLogo", "anhBiaUrl" })
            Assert.False(body.TryGetProperty(cam, out _), $"KHÔNG được trả `{cam}`");
    }

    /// <summary>
    /// Endpoint logo ẩn danh đọc được logo, nhưng endpoint ảnh dùng chung **vẫn khoá**.
    ///
    /// Đây là ranh giới của cả tính năng: nếu ai đó "tiện tay" mở `GET /anh/{khoa}` cho ẩn danh
    /// thì test này đỏ.
    /// </summary>
    [Fact]
    public async Task Logo_doc_duoc_an_danh_nhung_anh_dung_chung_van_khoa()
    {
        var admin = await Client();

        // Lấy khoá ảnh thật bằng cách tải một logo lên.
        var anh = new ByteArrayContent(AnhPngNhoNhat());
        anh.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { anh, "tep", "logo.png" } };

        var tai = await admin.PostAsync("/api/v1/anh/trung-tam/logo", form);
        tai.EnsureSuccessStatusCode();

        var anDanh = factory.CreateClient();

        // 1) Logo: đọc được, KHÔNG cần token.
        var logo = await anDanh.GetAsync($"/api/v1/auth/logo/{factory.MaTrungTamA}");
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);

        // 2) Ảnh dùng chung: VẪN phải 401 khi không có token — đây là chốt chặn.
        var khoa = (await (await admin.GetAsync("/api/v1/thiet-lap"))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("logoUrl").GetString()!;

        var anhChung = await anDanh.GetAsync($"/api/v1/anh/{khoa}");
        Assert.Equal(HttpStatusCode.Unauthorized, anhChung.StatusCode);
    }

    /// <summary>
    /// Mã sai và trung tâm chưa có logo đều trả 404 — không phân biệt.
    ///
    /// Phân biệt (404 vs 204 chẳng hạn) là cho người dò biết mã nào có thật, tức thu hẹp không
    /// gian dò từ 27 tỷ xuống danh sách trung tâm có thật.
    /// </summary>
    [Fact]
    public async Task Ma_sai_va_chua_co_logo_deu_tra_404()
    {
        var c = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync("/api/v1/auth/logo/ZZZZZZZ")).StatusCode);

        // Tenant B trong ApiFactory không tải logo — cùng phải 404, không phải 200 rỗng.
        Assert.Equal(HttpStatusCode.NotFound,
            (await c.GetAsync($"/api/v1/auth/logo/{factory.MaTrungTamB}")).StatusCode);
    }

    /// <summary>
    /// `/toi/cau-hinh` trả nhận diện cho **MỌI vai trò** — sidebar của giáo viên/học viên cũng
    /// cần logo.
    ///
    /// Không dùng `/thiet-lap` cho việc này vì endpoint đó gác `ThietLapChung.Xem`: giáo viên
    /// nhận 403. Test kiểm cả hai vế để bản sửa "cho giáo viên quyền xem thiết lập" bị bắt —
    /// cách đó mở luôn số tài khoản ngân hàng và ảnh QR cho họ.
    /// </summary>
    [Fact]
    public async Task Moi_vai_tro_doc_duoc_nhan_dien_nhung_khong_doc_duoc_thiet_lap()
    {
        var admin = await Client();
        var quyenGv = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/v1/quyen"))!
            .Single(q => q.GetProperty("tenQuyen").GetString() == "Giáo viên")
            .GetProperty("id").GetString()!;

        (await admin.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = "GV nhận diện", LoaiNguoiDung = "GiaoVien",
            TaiKhoan = new
            {
                Username = "gv-nhan-dien", MatKhau = "matkhau123456",
                QuyenIds = new[] { quyenGv }, PhaiDoiMatKhau = false
            }
        })).EnsureSuccessStatusCode();

        var gv = await Client("gv-nhan-dien", "matkhau123456");

        var ch = await gv.GetFromJsonAsync<JsonElement>("/api/v1/toi/cau-hinh");
        Assert.False(string.IsNullOrWhiteSpace(ch.GetProperty("tenTrungTam").GetString()));
        Assert.True(ch.TryGetProperty("khoaLogo", out _));
        // Múi giờ vẫn còn — thêm trường mới không được làm mất trường cũ (quy tắc #1).
        Assert.False(string.IsNullOrWhiteSpace(ch.GetProperty("muiGio").GetString()));

        // Chiều NGƯỢC: giáo viên vẫn KHÔNG đọc được thiết lập đầy đủ.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gv.GetAsync("/api/v1/thiet-lap")).StatusCode);
    }

    /// <summary>PNG 1×1 hợp lệ — đủ để kho ảnh nhận, không cần tệp thật.</summary>
    private static byte[] AnhPngNhoNhat() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
}

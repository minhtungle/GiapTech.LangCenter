using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Chính sách mật khẩu** (22/09/2026) — mục 6 của đợt rà soát bảo mật.
///
/// Trước đây tối thiểu **6 ký tự**, không blocklist. Cộng với việc **không khoá tài khoản** sau
/// nhiều lần sai (mục 4, vá cùng đợt), 6 ký tự là ngắn một cách nguy hiểm.
///
/// Nâng lên **12** theo hướng của NIST SP 800-63B: độ dài là yếu tố quan trọng nhất, còn luật
/// "phải có hoa/thường/số/ký tự đặc biệt" thì phản tác dụng — người dùng đáp ứng bằng
/// `Matkhau@123`, dễ đoán hơn một cụm từ dài.
///
/// Quy tắc từng nằm rải ở **5 nơi** (mỗi nơi một `MinimumLength(6)` chép tay). Nay gom vào
/// `ChinhSachMatKhau`; test này đi qua **từng đường vào** để chắc không chỗ nào sót.
/// </summary>
public class ChinhSachMatKhauTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> Manager()
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("accessToken").GetString());
        return c;
    }

    /// <summary>
    /// Hằng số phải là **12**, không phải 6.
    ///
    /// Chốt thẳng con số: test chỉ so thông báo lỗi sẽ không bắt được việc ai đó hạ ngưỡng
    /// xuống cho "đỡ phiền" — phản chứng 21/08 từng lọt đúng kiểu đó với hạn mức rate limit.
    /// </summary>
    [Fact]
    public void Do_dai_toi_thieu_la_12()
        => Assert.Equal(12, ChinhSachMatKhau.DoDaiToiThieu);

    /// <summary>
    /// Mật khẩu 6 ký tự — từng HỢP LỆ — nay bị từ chối ở đường tạo tài khoản.
    /// </summary>
    [Fact]
    public async Task Tao_tai_khoan_voi_mat_khau_6_ky_tu_bi_TU_CHOI()
    {
        var c = await Manager();

        var res = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = $"mkngan{Guid.NewGuid():N}"[..18],
            MatKhau = "123456",
            QuyenIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>Chiều ngược: đủ 12 ký tự thì tạo được — không chặn nhầm mật khẩu hợp lệ.</summary>
    [Fact]
    public async Task Tao_tai_khoan_voi_mat_khau_du_dai_thi_DUOC()
    {
        var c = await Manager();

        var res = await c.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = $"mkdu{Guid.NewGuid():N}"[..16],
            MatKhau = "mat-khau-du-dai-123",
            QuyenIds = Array.Empty<Guid>(),
        });

        // Chỉ khẳng định KHÔNG bị chặn vì độ dài. Không dùng `EnsureSuccess` vì lệnh này còn
        // các ràng buộc khác (quyền, người dùng gắn kèm) không thuộc phạm vi test này.
        Assert.NotEqual(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Đường **tự đổi mật khẩu** cũng phải áp chính sách.
    ///
    /// Đây là đường dễ sót nhất: nó nằm trong allowlist của `BuocDoiMatKhauMiddleware`, tức
    /// gọi được ngay cả khi tài khoản chưa vào được hệ thống. Sót ở đây thì người dùng đặt
    /// mật khẩu 6 ký tự ngay ở lần đăng nhập đầu tiên, và mọi chỗ khác siết chặt đều vô nghĩa.
    /// </summary>
    [Fact]
    public async Task Tu_doi_mat_khau_cung_ap_chinh_sach()
    {
        var c = await Manager();

        var res = await c.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "manager123456", MatKhauMoi = "ngan12" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Đường **đặt lại qua token** (quên mật khẩu) cũng phải áp.
    ///
    /// Sót ở đây thì ai cũng hạ mật khẩu của chính mình xuống 6 ký tự bằng luồng quên mật khẩu
    /// — vòng qua mọi chốt chặn khác.
    /// </summary>
    [Fact]
    public async Task Dat_lai_qua_token_cung_ap_chinh_sach()
    {
        var c = factory.CreateClient();

        // Token sai cũng được: validator chạy TRƯỚC khi tra token, nên 400 ở đây là do độ dài.
        var res = await c.PostAsJsonAsync("/api/v1/auth/dat-lai-mat-khau",
            new { Token = "token-bat-ky", MatKhauMoi = "ngan12" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Mật khẩu **quá dài** bị chặn — PBKDF2 băm chuỗi vài MB tốn CPU thật, đó là kênh gây tải
    /// rẻ tiền nhất mà một endpoint ẩn danh có thể mở ra.
    /// </summary>
    [Fact]
    public async Task Mat_khau_qua_DAI_cung_bi_chan()
    {
        var c = await Manager();

        var res = await c.PostAsJsonAsync("/api/v1/auth/doi-mat-khau",
            new { MatKhauCu = "manager123456", MatKhauMoi = new string('a', 5000) });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

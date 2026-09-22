using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Models;
using Microsoft.IdentityModel.Tokens;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **ĐĂNG XUẤT THẬT** (22/09/2026) — đợt rà soát bảo mật phát hiện `POST /auth/dang-xuat`
/// **không tồn tại**: nút "Đăng xuất" ở frontend chỉ xoá `localStorage`.
///
/// Hệ quả là bấm đăng xuất xong, access token vẫn sống **60 phút** và refresh token **30
/// ngày** — ai đọc được máy đó sau khi người dùng đứng dậy (quầy lễ tân, extension, bản sao
/// profile) vẫn vào được, trong khi người dùng tin rằng mình đã thoát ra.
///
/// Ba việc phải làm cùng nhau, và mỗi việc có một test riêng ở đây: thu hồi refresh token,
/// chặn access token còn hạn, xoá cache phiên. Thiếu một việc thì "đăng xuất" vẫn là nói dối.
/// </summary>
public class DangXuatTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<(string Access, string Refresh)> DangNhap(string user = "manager", string mk = "manager123")
    {
        var c = factory.CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("accessToken").GetString()!,
                body.GetProperty("refreshToken").GetString()!);
    }

    private HttpClient Voi(string accessToken)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

    /// <summary>
    /// Điều cốt lõi: sau khi đăng xuất, **access token còn hạn không dùng được nữa**.
    ///
    /// Đây chính là thứ trước đây thiếu. Access token sống 60 phút, nên nếu chỉ thu hồi refresh
    /// token thì người cầm máy vẫn thao tác thoải mái gần một tiếng sau khi "đã đăng xuất".
    /// </summary>
    [Fact]
    public async Task Dang_xuat_roi_thi_access_token_con_han_KHONG_dung_duoc_nua()
    {
        var (access, _) = await DangNhap();
        Assert.Equal(HttpStatusCode.OK, (await Voi(access).GetAsync("/api/v1/toi/quyen")).StatusCode);

        var raa = await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null);
        Assert.Equal(HttpStatusCode.NoContent, raa.StatusCode);

        // KHÔNG chờ cache 10 giây — đăng xuất phải có hiệu lực NGAY.
        var sau = await Voi(access).GetAsync("/api/v1/toi/quyen");
        Assert.Equal(HttpStatusCode.Unauthorized, sau.StatusCode);
    }

    /// <summary>
    /// Việc thứ hai: refresh token bị thu hồi, không tự dựng lại phiên được.
    ///
    /// Thiếu cái này thì chặn access token thành vô nghĩa — kẻ cầm refresh token chỉ cần gọi
    /// `lam-moi-token` là có cặp token mới tinh, hợp lệ thêm 60 phút.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_roi_thi_KHONG_lam_moi_token_duoc_nua()
    {
        var (access, refresh) = await DangNhap();

        await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null);

        var lamMoi = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/lam-moi-token", new { RefreshToken = refresh });

        Assert.NotEqual(HttpStatusCode.OK, lamMoi.StatusCode);
    }

    /// <summary>
    /// Chiều ngược: đăng xuất **không được khoá vĩnh viễn** tài khoản.
    ///
    /// Cách cài đặt là ghi một giá trị đánh dấu vào `PhienHienTai`; nếu `DangNhapCommand` không
    /// ghi đè giá trị đó thì người dùng đăng xuất xong sẽ **không đăng nhập lại được** — lỗi
    /// nặng hơn hẳn lỗi đang chữa. Test này chặn đúng ca đó.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_roi_dang_nhap_lai_van_vao_duoc_binh_thuong()
    {
        var (access, _) = await DangNhap();
        await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null);

        var (accessMoi, _) = await DangNhap();

        Assert.Equal(HttpStatusCode.OK, (await Voi(accessMoi).GetAsync("/api/v1/toi/quyen")).StatusCode);
    }

    /// <summary>
    /// Đăng xuất **hai lần** vẫn 204 — thao tác lặp lại được (idempotent).
    ///
    /// Xảy ra thật: người dùng bấm hai lần, hoặc hai tab cùng đăng xuất. Đăng xuất là thao tác
    /// dọn dẹp — báo lỗi cho người đang muốn thoát ra là vô nghĩa, và frontend sẽ phải xử lý
    /// một nhánh lỗi không dẫn tới hành động nào khác ngoài… vẫn đăng xuất.
    ///
    /// Hoạt động được là nhờ `/auth/*` **cố ý** nằm ngoài `PhienDuyNhatMiddleware` (nếu không
    /// thì chính lệnh đăng nhập cũng bị chặn, và người bị đẩy ra không có đường quay lại).
    /// Ở đây ngoại lệ đó cho kết quả đúng: token chỉ còn dùng được cho mỗi việc tự huỷ mình.
    ///
    /// Tôi từng đoán lần hai sẽ 401 và viết test theo phỏng đoán đó — sai. Giữ lại ghi chú này
    /// vì hành vi thật **tốt hơn** phỏng đoán, và người đọc sau có thể cũng đoán nhầm như vậy.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_hai_lan_van_tra_204()
    {
        var (access, _) = await DangNhap();

        Assert.Equal(HttpStatusCode.NoContent,
            (await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null)).StatusCode);

        var lanHai = await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null);
        Assert.Equal(HttpStatusCode.NoContent, lanHai.StatusCode);

        // Nhưng endpoint NGHIỆP VỤ thì vẫn chặn — đó mới là chỗ cơ chế phiên có tác dụng.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Voi(access).GetAsync("/api/v1/toi/quyen")).StatusCode);
    }

    /// <summary>
    /// **Token KHÔNG có `jti` bị CHẶN** (mục 8 của đợt rà soát 22/09/2026).
    ///
    /// Bản 20/09 cho qua token thiếu `jti`/`TaiKhoanId`, để không đá hàng loạt người đang mở
    /// app lúc triển khai. Access token chỉ sống 60 phút nên những token đó đã chết từ lâu, và
    /// nhánh ấy trở thành **đường vòng fail-open**: ai phát được token không có `jti` thì bỏ
    /// qua sạch cả cơ chế một-phiên **lẫn đăng xuất** — token đăng xuất rồi vẫn dùng tiếp.
    ///
    /// Vì vậy mục 8 **bắt buộc** làm cùng lúc với endpoint đăng xuất, không phải dọn dẹp cho
    /// gọn. Test này viết sau khi một **mutation SỐNG**: gỡ bản vá mà cả bộ test vẫn xanh, tức
    /// lúc đó chưa có gì canh nó.
    ///
    /// Token ở đây **ký bằng đúng khoá thật** của môi trường test, hợp lệ về mọi mặt — chỉ
    /// thiếu `jti`. Nếu ký sai khoá thì 401 đến từ tầng xác thực và test sẽ xanh giả.
    /// </summary>
    [Fact]
    public async Task Token_KHONG_co_jti_bi_chan_chu_khong_cho_qua()
    {
        // Lấy thông tin thật của `manager` từ một lần đăng nhập bình thường.
        var (access, _) = await DangNhap();
        var that = new JwtSecurityTokenHandler().ReadJwtToken(access);

        // Dựng lại token y hệt NHƯNG bỏ `jti`.
        var claims = that.Claims
            .Where(c => c.Type != JwtRegisteredClaimNames.Jti)
            .ToList();

        // Chắc chắn còn `TaiKhoanId` — nếu không thì test chặn nhầm nhánh khác.
        Assert.Contains(claims, c => c.Type == ClaimTenant.TaiKhoanId);

        var khoa = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtSecret));
        var khongJti = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: that.Issuer,
            audience: that.Audiences.First(),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(khoa, SecurityAlgorithms.HmacSha256)));

        var res = await Voi(khongJti).GetAsync("/api/v1/toi/quyen");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>Chưa đăng nhập thì không đăng xuất được — 401, không phải 500.</summary>
    [Fact]
    public async Task Chua_dang_nhap_thi_401()
    {
        var res = await factory.CreateClient().PostAsync("/api/v1/auth/dang-xuat", null);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>
    /// Đăng xuất **chỉ đá phiên của chính mình**, không đụng tài khoản khác.
    ///
    /// Nghe hiển nhiên, nhưng cách cài đặt ghi thẳng vào cột `PhienHienTai` và thu hồi refresh
    /// token theo `TaiKhoanId` — sai một điều kiện `Where` là đá cả trung tâm ra ngoài.
    /// </summary>
    [Fact]
    public async Task Dang_xuat_KHONG_anh_huong_tai_khoan_khac()
    {
        // `player` bị buộc đổi mật khẩu nên không gọi được endpoint nghiệp vụ; điều cần kiểm
        // ở đây chỉ là token của nó **không bị 401 vì cơ chế phiên** sau khi manager đăng xuất.
        var (accessKhac, _) = await DangNhap("player", "player123");
        var (access, _) = await DangNhap();

        await Voi(access).PostAsync("/api/v1/auth/dang-xuat", null);

        var resKhac = await Voi(accessKhac).GetAsync("/api/v1/toi/quyen");
        Assert.NotEqual(HttpStatusCode.Unauthorized, resKhac.StatusCode);
    }
}

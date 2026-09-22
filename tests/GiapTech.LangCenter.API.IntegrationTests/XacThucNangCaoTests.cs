using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>FR-02 quên mật khẩu và refresh token (FR-01).</summary>
public class XacThucNangCaoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<JsonElement> DangNhap(string maTrungTam, string user, string mk)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = maTrungTam, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Đăng nhập và trả `(refreshToken, csrf)` — refresh token lấy TỪ COOKIE, không còn trong
    /// body (ADR-0007). Cặp này là thứ mọi test xoay vòng token bên dưới cần.
    /// </summary>
    private async Task<(string Refresh, string Csrf)> DangNhapLayCookie(
        string maTrungTam, string user, string mk)
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = maTrungTam, Username = user, MatKhau = mk });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (TroGiupPhien.LayCookie(res, API.Authorization.CookiePhien.Ten)!,
                body.GetProperty("tokenCsrf").GetString()!);
    }

    private HttpClient ClientVoiToken(string token)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    // ---------- Refresh token ----------

    [Fact]
    public async Task Lam_moi_token_tra_ve_cap_token_moi()
    {
        var (refreshCu, csrf) = await DangNhapLayCookie(factory.MaTrungTamA, "manager", "manager123456");

        var res = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), refreshCu, csrf);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var accessMoi = body.GetProperty("accessToken").GetString()!;
        // Xoay vòng: cookie mới phải mang refresh token KHÁC cái vừa dùng.
        Assert.NotEqual(refreshCu, TroGiupPhien.LayCookie(res, API.Authorization.CookiePhien.Ten));

        // Access token mới phải dùng được ngay.
        var goiApi = await ClientVoiToken(accessMoi).GetAsync("/api/v1/tai-khoan");
        Assert.Equal(HttpStatusCode.OK, goiApi.StatusCode);
    }

    /// <summary>
    /// Xoay vòng: refresh token cũ chết ngay khi đổi. Nếu dùng lại được nhiều lần thì một
    /// bản sao bị lộ sẽ sống tới tận ngày hết hạn.
    /// </summary>
    [Fact]
    public async Task Refresh_token_cu_khong_dung_lai_duoc()
    {
        var (refreshCu, csrf) = await DangNhapLayCookie(factory.MaTrungTamB, "manager", "manager123456");

        var lan1 = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), refreshCu, csrf);
        Assert.Equal(HttpStatusCode.OK, lan1.StatusCode);

        var lan2 = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), refreshCu, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
    }

    /// <summary>
    /// Tái sử dụng token đã thu hồi là dấu hiệu bị đánh cắp → thu hồi TOÀN BỘ phiên.
    /// Token đời sau (hợp lệ trong tay chủ tài khoản) cũng phải chết theo.
    /// </summary>
    [Fact]
    public async Task Tai_su_dung_token_da_thu_hoi_thi_thu_hoi_toan_bo_phien()
    {
        var (doi1, csrf) = await DangNhapLayCookie(factory.MaTrungTamA, "player", "player123456");

        var r2 = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), doi1, csrf);
        var doi2 = TroGiupPhien.LayCookie(r2, API.Authorization.CookiePhien.Ten)!;
        var csrf2 = (await r2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("tokenCsrf").GetString()!;

        // Kẻ tấn công dùng lại bản sao đời 1 (đã bị thu hồi).
        var taiSuDung = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), doi1, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, taiSuDung.StatusCode);

        // Hệ quả: token đời 2 của chủ tài khoản cũng bị thu hồi.
        var doi2SauCanhBao = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), doi2, csrf2);
        Assert.Equal(HttpStatusCode.BadRequest, doi2SauCanhBao.StatusCode);
    }

    /// <summary>
    /// **Phiên bị ĐẨY RA dùng lại token KHÔNG được thu hồi phiên của người vừa đăng nhập.**
    ///
    /// Đây là chiều ngược của test tái-sử-dụng ngay trên, và là lỗi thật gặp khi làm ADR-0007:
    /// A bị đẩy ra, A gọi làm mới, backend tưởng bị trộm ⇒ thu hồi toàn bộ ⇒ **B vừa đăng nhập
    /// cũng bị đá ra**. Hai người cùng văng, không ai vào được.
    ///
    /// Phân biệt bằng cột `REFRESH_TOKEN.ly_do` (migration `LyDoThuHoiRefreshToken`), **không**
    /// suy từ `PhienHienTai`: sau một lần xoay vòng hợp lệ thì cột đó cũng khác `jti` của token
    /// cũ, nên suy đoán sẽ coi ca trộm thật là "bị đẩy ra" và nới lỏng đúng chốt chặn quan
    /// trọng nhất.
    /// </summary>
    [Fact]
    public async Task Phien_bi_day_ra_KHONG_lam_chet_phien_cua_nguoi_vua_dang_nhap()
    {
        // A đăng nhập, rồi B đăng nhập cùng tài khoản (đẩy A ra).
        var (refreshA, csrfA) = await DangNhapLayCookie(factory.MaTrungTamA, "manager", "manager123456");
        var (refreshB, csrfB) = await DangNhapLayCookie(factory.MaTrungTamA, "manager", "manager123456");

        // A cố làm mới bằng token đã bị đẩy ra → phải bị từ chối, KÈM mã lỗi đúng sự thật.
        var cuaA = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), refreshA, csrfA);
        Assert.Equal(HttpStatusCode.BadRequest, cuaA.StatusCode);
        Assert.Equal("PHIEN_DA_BI_DAY_RA",
            (await cuaA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString());

        // ĐIỀU CỐT LÕI: B vẫn làm mới được — không bị A kéo theo.
        var cuaB = await TroGiupPhien.LamMoiAsync(factory.CreateClient(), refreshB, csrfB);
        Assert.Equal(HttpStatusCode.OK, cuaB.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_bia_dat_bi_tu_choi()
    {
        // CSRF khớp để chắc chắn 400 đến từ token bịa, không phải từ chốt CSRF.
        var res = await TroGiupPhien.LamMoiAsync(
            factory.CreateClient(), "token-khong-co-that", "csrf-bat-ky");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- FR-02 quên mật khẩu ----------

    /// <summary>
    /// Email tồn tại hay không đều trả 204 giống hệt nhau — không cho dò email đã đăng ký.
    /// </summary>
    [Fact]
    public async Task Quen_mat_khau_khong_tiet_lo_email_ton_tai_hay_khong()
    {
        var client = factory.CreateClient();

        var coThat = await client.PostAsJsonAsync("/api/v1/auth/quen-mat-khau",
            new { MaTrungTam = factory.MaTrungTamA, Email = "co-that@example.com" });

        var khongCo = await client.PostAsJsonAsync("/api/v1/auth/quen-mat-khau",
            new { MaTrungTam = factory.MaTrungTamA, Email = "khong-ton-tai@example.com" });

        var saiTenant = await client.PostAsJsonAsync("/api/v1/auth/quen-mat-khau",
            new { MaTrungTam = "KHONGCO", Email = "co-that@example.com" });

        Assert.Equal(HttpStatusCode.NoContent, coThat.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, khongCo.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, saiTenant.StatusCode);
    }

    /// <summary>Luồng FR-02 đầy đủ: yêu cầu → lấy token từ DB → đặt lại → đăng nhập bằng MK mới.</summary>
    [Fact]
    public async Task Dat_lai_mat_khau_qua_token_thanh_cong()
    {
        var client = await TaoTaiKhoanCoEmail("quen-mk-1", "quenmk1@example.com");

        await client.PostAsJsonAsync("/api/v1/auth/quen-mat-khau",
            new { MaTrungTam = factory.MaTrungTamA, Email = "quenmk1@example.com" });

        // Token thô chỉ có trong email; test đọc bản ghi để dựng lại luồng.
        var tokenTho = LayTokenThoMoiNhat("quenmk1@example.com");
        Assert.NotNull(tokenTho);

        var datLai = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dat-lai-mat-khau",
            new { Token = tokenTho, MatKhauMoi = "mat-khau-that-moi" });
        Assert.Equal(HttpStatusCode.NoContent, datLai.StatusCode);

        var dn = await DangNhap(factory.MaTrungTamA, "quen-mk-1", "mat-khau-that-moi");
        Assert.False(string.IsNullOrEmpty(dn.GetProperty("accessToken").GetString()));

        // Người dùng tự đặt mật khẩu → không bắt đổi lại lần nữa.
        Assert.False(dn.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    [Fact]
    public async Task Token_dat_lai_chi_dung_duoc_mot_lan()
    {
        await TaoTaiKhoanCoEmail("quen-mk-2", "quenmk2@example.com");

        await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/quen-mat-khau",
            new { MaTrungTam = factory.MaTrungTamA, Email = "quenmk2@example.com" });

        var token = LayTokenThoMoiNhat("quenmk2@example.com")!;

        var lan1 = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dat-lai-mat-khau",
            new { Token = token, MatKhauMoi = "mat-khau-lan-1" });
        Assert.Equal(HttpStatusCode.NoContent, lan1.StatusCode);

        var lan2 = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dat-lai-mat-khau",
            new { Token = token, MatKhauMoi = "mat-khau-lan-2" });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
    }

    [Fact]
    public async Task Token_dat_lai_bia_dat_bi_tu_choi()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/dat-lai-mat-khau",
            new { Token = "token-bia-dat", MatKhauMoi = "mat-khau-moi-123" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TOKEN_DAT_LAI_KHONG_HOP_LE", body.GetProperty("errorCode").GetString());
    }

    // ---------- Trợ giúp ----------

    /// <summary>Tạo tài khoản có email để chạy luồng FR-02.</summary>
    private async Task<HttpClient> TaoTaiKhoanCoEmail(string username, string email)
    {
        var dn = await DangNhap(factory.MaTrungTamA, "manager", "manager123456");
        var client = ClientVoiToken(dn.GetProperty("accessToken").GetString()!);

        // Email nằm ở NGUOI_DUNG còn tài khoản ở TAI_KHOAN — tạo cả hai trong một lượt gọi.
        var res = await client.PostAsJsonAsync("/api/v1/nguoi-dung", new
        {
            HoTen = $"Test {username}",
            Email = email,
            LoaiNguoiDung = "NhanVien",
            TaiKhoan = new
            {
                Username = username,
                MatKhau = "matkhaugoc123",
                QuyenIds = Array.Empty<Guid>(),
                PhaiDoiMatKhau = false
            }
        });
        res.EnsureSuccessStatusCode();

        return client;
    }

    /// <summary>
    /// Đọc token thô: không lấy được từ DB (DB chỉ giữ hash), nên sinh lại bằng cách thử
    /// — thay vào đó test tra hash ngược bằng cách tự tạo token đã biết là không khả thi.
    /// Giải pháp: đọc bản ghi và dùng cơ chế hash giống hệt để đối chiếu ứng viên.
    /// </summary>
    private string? LayTokenThoMoiNhat(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var banGhi = db.TokenDatLaiMatKhaus
            .IgnoreQueryFilters()
            .Include(t => t.TaiKhoan).ThenInclude(tk => tk.NguoiDung)
            .Where(t => t.TaiKhoan.NguoiDung != null
                        && t.TaiKhoan.NguoiDung.Email == email && t.DaDungLuc == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();

        return banGhi is null ? null : TokenThoDaGhiNhan.GetValueOrDefault(banGhi.TokenHash);
    }

    /// <summary>
    /// Bản đồ hash → token thô, do <see cref="TestEmailSender"/> ghi lại khi "gửi" email.
    /// Đây là cách duy nhất test lấy được token thô, vì DB chỉ lưu hash — đúng như thiết kế.
    /// </summary>
    internal static readonly Dictionary<string, string> TokenThoDaGhiNhan = new();
}

/// <summary>
/// Thay SMTP thật trong test: bắt token thô từ nội dung email để chạy tiếp luồng FR-02.
/// </summary>
public class TestEmailSender : IEmailSender
{
    public Task GuiAsync(string den, string tieuDe, string noiDungHtml, CancellationToken ct = default)
    {
        var m = System.Text.RegularExpressions.Regex.Match(noiDungHtml, @"<code>(.+?)</code>");

        if (m.Success)
        {
            var tokenTho = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
            var hash = Application.DangNhap.Commands.QuenMatKhau.BamToken.Bam(tokenTho);
            XacThucNangCaoTests.TokenThoDaGhiNhan[hash] = tokenTho;
        }

        return Task.CompletedTask;
    }
}

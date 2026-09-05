using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.LMS.API.IntegrationTests;

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
        var dn = await DangNhap(factory.MaTrungTamA, "manager", "manager123");
        var refreshCu = dn.GetProperty("refreshToken").GetString()!;

        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = refreshCu });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var accessMoi = body.GetProperty("accessToken").GetString()!;
        Assert.NotEqual(refreshCu, body.GetProperty("refreshToken").GetString());

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
        var dn = await DangNhap(factory.MaTrungTamB, "manager", "manager123");
        var refreshCu = dn.GetProperty("refreshToken").GetString()!;

        var lan1 = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = refreshCu });
        Assert.Equal(HttpStatusCode.OK, lan1.StatusCode);

        var lan2 = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = refreshCu });
        Assert.Equal(HttpStatusCode.BadRequest, lan2.StatusCode);
    }

    /// <summary>
    /// Tái sử dụng token đã thu hồi là dấu hiệu bị đánh cắp → thu hồi TOÀN BỘ phiên.
    /// Token đời sau (hợp lệ trong tay chủ tài khoản) cũng phải chết theo.
    /// </summary>
    [Fact]
    public async Task Tai_su_dung_token_da_thu_hoi_thi_thu_hoi_toan_bo_phien()
    {
        var dn = await DangNhap(factory.MaTrungTamA, "player", "player123");
        var doi1 = dn.GetProperty("refreshToken").GetString()!;

        var r2 = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = doi1 });
        var doi2 = (await r2.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString()!;

        // Kẻ tấn công dùng lại bản sao đời 1 (đã bị thu hồi).
        var taiSuDung = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = doi1 });
        Assert.Equal(HttpStatusCode.BadRequest, taiSuDung.StatusCode);

        // Hệ quả: token đời 2 của chủ tài khoản cũng bị thu hồi.
        var doi2SauCanhBao = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = doi2 });
        Assert.Equal(HttpStatusCode.BadRequest, doi2SauCanhBao.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_bia_dat_bi_tu_choi()
    {
        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/lam-moi-token", new { RefreshToken = "token-khong-co-that" });

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
        var dn = await DangNhap(factory.MaTrungTamA, "manager", "manager123");
        var client = ClientVoiToken(dn.GetProperty("accessToken").GetString()!);

        await client.PostAsJsonAsync("/api/v1/tai-khoan", new
        {
            Username = username,
            MatKhau = "matkhaugoc123",
            HoTen = "Test",
            Email = email,
            QuyenIds = Array.Empty<Guid>(),
            PhaiDoiMatKhau = false
        });

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
            .Include(t => t.NguoiDung)
            .Where(t => t.NguoiDung.Email == email && t.DaDungLuc == null)
            .OrderByDescending(t => t.NgayTao)
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

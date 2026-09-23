using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// ADR-0009 — tài khoản chủ hệ thống.
///
/// Điều được canh: **hai chiều không thủng**. Token chủ không vào được API nghiệp vụ, và token
/// tenant không vào được API site chủ. Mỗi chiều một cơ chế khác nhau, nên phải kiểm riêng.
/// </summary>
public class ChuHeThongTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MatKhauChu = "chu-he-thong-123456";

    /// <summary>
    /// Tạo tài khoản chủ trong DB test và đăng nhập, trả access token.
    ///
    /// Tạo thẳng bằng DbContext chứ không qua API: cố ý **không có endpoint tạo tài khoản
    /// chủ** — tài khoản đầu tiên sinh lúc triển khai, các tài khoản sau do chủ sản phẩm thêm
    /// tay. Không endpoint nghĩa là không có bề mặt tấn công.
    /// </summary>
    private async Task<string> DangNhapChuAsync(HttpClient client, string username = "chu")
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            if (!db.QuanTriHeThongs.Any(q => q.Username == username))
            {
                db.QuanTriHeThongs.Add(new QuanTriHeThong
                {
                    Username = username,
                    PasswordHash = hasher.Bam(MatKhauChu),
                    HoTen = "Chủ sản phẩm",
                    HoatDong = true,
                    PhaiDoiMatKhau = false
                });
                db.SaveChanges();
            }
        }

        var res = await client.PostAsJsonAsync(
            "/api/v1/chu-he-thong/dang-nhap",
            new { Username = username, MatKhau = MatKhauChu });
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    // ---------------------------------------------------------------------------------
    // Chiều 1: token chủ KHÔNG vào được API nghiệp vụ
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Test quan trọng nhất của ADR-0009.
    ///
    /// Token chủ không mang `tenant_id`, nên `TenantMiddleware` chặn 401 ở MỌI endpoint nghiệp
    /// vụ — kể cả endpoint thêm sau này mà người viết chưa từng nghe tới ADR-0009. Đó là lý do
    /// hàng rào đặt ở sự VẮNG MẶT của claim, không đặt ở một lớp kiểm phải nhớ gọi.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/toi/he-thong")]
    [InlineData("/api/v1/toi/cau-hinh")]
    [InlineData("/api/v1/hoc-vien")]
    [InlineData("/api/v1/lop-hoc")]
    public async Task Token_chu_khong_goi_duoc_api_nghiep_vu(string duong)
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client);

        var req = new HttpRequestMessage(HttpMethod.Get, duong);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var noi = await res.Content.ReadFromJsonAsync<LoiDto>();
        Assert.Equal("TOKEN_THIEU_TENANT", noi?.ErrorCode);
    }

    /// <summary>Token chủ KHÔNG được mang claim tenant — canh `TokenService` không lỡ gắn.</summary>
    [Fact]
    public async Task Token_chu_khong_mang_claim_tenant()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-claim");

        // Giải phần payload của JWT (không cần xác thực chữ ký — chỉ đọc nội dung).
        var payload = token.Split('.')[1];
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        Assert.DoesNotContain("tenant_id", json);
        Assert.Contains("\"loai\":\"chu\"", json);
    }

    // ---------------------------------------------------------------------------------
    // Chiều 2: token tenant KHÔNG vào được API site chủ
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Chiều ngược lại, do `[ChiChuHeThong]` chặn. Một quản trị viên của tenant — người có
    /// MỌI quyền trong trung tâm của họ — vẫn không được chạm vào site chủ.
    /// </summary>
    [Fact]
    public async Task Token_tenant_khong_goi_duoc_api_site_chu()
    {
        var client = factory.CreateClient();
        var phien = await TroGiupPhien.DangNhapAsync(client, factory);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chu-he-thong/trung-tam");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", phien.Access);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var noi = await res.Content.ReadFromJsonAsync<LoiDto>();
        Assert.Equal("KHONG_PHAI_CHU_HE_THONG", noi?.ErrorCode);
    }

    /// <summary>Không có token thì cũng không vào được — 401, không phải 403.</summary>
    [Fact]
    public async Task Khong_co_token_thi_khong_goi_duoc_api_site_chu()
    {
        var res = await factory.CreateClient().GetAsync("/api/v1/chu-he-thong/trung-tam");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------------------------------------------------------------------------------
    // Chiều thuận: chủ hệ thống làm được việc của mình
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Chiều thuận — không có test này thì một thay đổi làm `[ChiChuHeThong]` chặn MỌI thứ
    /// vẫn "xanh" ở các test trên, và site chủ chết trên VPS.
    /// </summary>
    [Fact]
    public async Task Chu_he_thong_xem_duoc_danh_sach_moi_trung_tam()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-xem");

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chu-he-thong/trung-tam");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var ds = await res.Content.ReadFromJsonAsync<List<TrungTamDto>>();

        // Phải thấy CẢ HAI tenant — đây là endpoint duy nhất đọc mọi tenant.
        Assert.NotNull(ds);
        Assert.Contains(ds!, t => t.MaTrungTam == factory.MaTrungTamA);
        Assert.Contains(ds!, t => t.MaTrungTam == factory.MaTrungTamB);
    }

    /// <summary>Gắn domain rồi gỡ — gỡ phải đưa về `null`, không phải chuỗi rỗng.</summary>
    [Fact]
    public async Task Gan_roi_go_domain()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-domain");

        async Task<HttpResponseMessage> Gan(object body)
        {
            var req = new HttpRequestMessage(
                HttpMethod.Put, $"/api/v1/chu-he-thong/trung-tam/{factory.TenantAId}/domain")
            {
                Content = JsonContent.Create(body)
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(req);
        }

        var gan = await Gan(new { DomainQuanTri = "  QuanTri-A.Example.COM.  ", DomainLanding = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, gan.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = db.Tenants.Single(x => x.Id == factory.TenantAId);
            // Đã chuẩn hoá: thường, bỏ khoảng trắng, bỏ dấu chấm cuối.
            Assert.Equal("quantri-a.example.com", t.DomainQuanTri);
        }

        var go = await Gan(new { DomainQuanTri = (string?)null, DomainLanding = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, go.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var t = db.Tenants.Single(x => x.Id == factory.TenantAId);
            // `null`, KHÔNG phải chuỗi rỗng: với UNIQUE INDEX thì nhiều NULL là hợp lệ còn
            // nhiều '' thì đụng nhau — tenant thứ hai gỡ domain sẽ lỗi.
            Assert.Null(t.DomainQuanTri);
        }
    }

    /// <summary>Hai domain của cùng một trung tâm không được trùng nhau.</summary>
    [Fact]
    public async Task Hai_domain_cua_cung_trung_tam_khong_duoc_trung()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-trung");

        var req = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/chu-he-thong/trung-tam/{factory.TenantBId}/domain")
        {
            Content = JsonContent.Create(
                new { DomainQuanTri = "trung.example.com", DomainLanding = "trung.example.com" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------------------------------------------------------------------------------
    // Đăng nhập
    // ---------------------------------------------------------------------------------

    /// <summary>Tài khoản bị vô hiệu hoá không đăng nhập được.</summary>
    [Fact]
    public async Task Tai_khoan_chu_bi_vo_hieu_hoa_khong_dang_nhap_duoc()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.QuanTriHeThongs.Add(new QuanTriHeThong
            {
                Username = "chu-khoa",
                PasswordHash = hasher.Bam(MatKhauChu),
                HoTen = "Đã khoá",
                HoatDong = false
            });
            db.SaveChanges();
        }

        var res = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/chu-he-thong/dang-nhap",
            new { Username = "chu-khoa", MatKhau = MatKhauChu });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    /// <summary>
    /// Username không tồn tại và sai mật khẩu phải trả **cùng một mã lỗi**.
    ///
    /// Phân biệt được thì người dò biết username nào có thật — mà chỉ có vài tài khoản chủ,
    /// nên thu hẹp được là thu hẹp rất nhiều.
    /// </summary>
    [Fact]
    public async Task Sai_username_va_sai_mat_khau_tra_cung_ma_loi()
    {
        var client = factory.CreateClient();
        await DangNhapChuAsync(client, "chu-dom");

        var khongCo = await client.PostAsJsonAsync(
            "/api/v1/chu-he-thong/dang-nhap",
            new { Username = "khong-ton-tai-bao-gio", MatKhau = MatKhauChu });
        var saiMk = await client.PostAsJsonAsync(
            "/api/v1/chu-he-thong/dang-nhap",
            new { Username = "chu-dom", MatKhau = "sai-mat-khau-hoan-toan" });

        Assert.Equal(khongCo.StatusCode, saiMk.StatusCode);
        Assert.Equal(
            (await khongCo.Content.ReadFromJsonAsync<LoiDto>())?.ErrorCode,
            (await saiMk.Content.ReadFromJsonAsync<LoiDto>())?.ErrorCode);
    }

    private record LoiDto(string ErrorCode);

    private record TrungTamDto(Guid Id, string MaTrungTam, string TenTrungTam);
}

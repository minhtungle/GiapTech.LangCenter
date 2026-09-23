using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    // ---------------------------------------------------------------------------------
    // Tạo trung tâm từ site chủ (thay nợ N3)
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Tạo trung tâm: phải trả mã + mật khẩu admin, và tài khoản đó phải đăng nhập được thật.
    ///
    /// Kiểm ĐĂNG NHẬP ĐƯỢC chứ không chỉ kiểm response có trường mật khẩu: trước 22/09/2026
    /// seeder băm một chuỗi còn controller trả một chuỗi khác — hai bên trùng nhau do tình cờ.
    /// Chỉ có đăng nhập thật mới bắt được kiểu lệch đó.
    /// </summary>
    [Fact]
    public async Task Tao_trung_tam_roi_dang_nhap_duoc_bang_mat_khau_tra_ve()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-tao");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/trung-tam")
        {
            Content = JsonContent.Create(new
            {
                TenTrungTam = "Trung tâm Kiểm Thử",
                DomainQuanTri = (string?)null,
                DomainLanding = (string?)null
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var moi = await res.Content.ReadFromJsonAsync<JsonElement>();
        var ma = moi.GetProperty("maTrungTam").GetString()!;
        var username = moi.GetProperty("username").GetString()!;
        var matKhau = moi.GetProperty("matKhauAdmin").GetString()!;

        Assert.Equal(7, ma.Length);
        Assert.False(string.IsNullOrWhiteSpace(matKhau));

        // Đăng nhập thật bằng bộ ba vừa nhận.
        var dn = await client.PostAsJsonAsync(
            "/api/v1/auth/dang-nhap",
            new { MaTrungTam = ma, Username = username, MatKhau = matKhau });

        Assert.Equal(HttpStatusCode.OK, dn.StatusCode);
        var phien = await dn.Content.ReadFromJsonAsync<JsonElement>();
        // Tài khoản mới phải bị buộc đổi mật khẩu — mật khẩu này đã đi qua tay người tạo.
        Assert.True(phien.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    /// <summary>Hai trung tâm tạo liên tiếp phải có mật khẩu KHÁC nhau (CSPRNG, không phải hằng).</summary>
    [Fact]
    public async Task Hai_trung_tam_moi_co_mat_khau_khac_nhau()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-hai");

        async Task<string> Tao(string ten)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/trung-tam")
            {
                Content = JsonContent.Create(new { TenTrungTam = ten })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var r = await client.SendAsync(req);
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("matKhauAdmin").GetString()!;
        }

        Assert.NotEqual(await Tao("TT Một"), await Tao("TT Hai"));
    }

    /// <summary>Token tenant KHÔNG tạo được trung tâm — đây là chỗ nợ N3 từng mở cho mọi người.</summary>
    [Fact]
    public async Task Token_tenant_khong_tao_duoc_trung_tam()
    {
        var client = factory.CreateClient();
        var phien = await TroGiupPhien.DangNhapAsync(client, factory);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/trung-tam")
        {
            Content = JsonContent.Create(new { TenTrungTam = "Trung tâm lậu" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", phien.Access);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    /// <summary>
    /// Trung tâm tạo từ site chủ phải có `created_by_id` = NULL ở mọi bản ghi con.
    ///
    /// Vì sao test này tồn tại: token chủ đặt id tài khoản chủ vào `NameIdentifier`, và
    /// `GanTenantVaDauVetAudit` lấy đúng claim đó gán vào `CreatedById` — cột có khoá ngoại
    /// tới `NGUOI_DUNG`, nơi tài khoản chủ KHÔNG có hàng nào. Trên PostgreSQL thì INSERT chết
    /// với "violates foreign key constraint fk_chuc_vu_nguoi_dung_created_by_id" (gặp thật
    /// 23/09/2026 khi thử bằng curl).
    ///
    /// **Test in-memory không bắt được lỗi gốc** — provider đó không ép khoá ngoại. Nên ở đây
    /// kiểm thứ bắt được: giá trị phải là NULL. Null sai thì khoá ngoại sẽ chết ở DB thật.
    /// </summary>
    [Fact]
    public async Task Tao_trung_tam_tu_site_chu_thi_created_by_id_phai_null()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-audit");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/trung-tam")
        {
            Content = JsonContent.Create(new { TenTrungTam = "TT Kiểm Audit" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var id = (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Null(db.Tenants.Single(t => t.Id == id).CreatedById);
        // Bản ghi CON mới là chỗ khoá ngoại thật sự nổ — TENANT không có FK này.
        Assert.All(
            db.NguoiDungs.IgnoreQueryFilters().Where(x => x.TenantId == id).ToList(),
            x => Assert.Null(x.CreatedById));
        Assert.All(
            db.TaiKhoans.IgnoreQueryFilters().Where(x => x.TenantId == id).ToList(),
            x => Assert.Null(x.CreatedById));
    }

    // ---------------------------------------------------------------------------------
    // Dọn tenant E2E (nợ N11) — endpoint XOÁ HÀNG LOẠT, canh kỹ
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Cờ TẮT (mặc định) ⇒ 404, endpoint coi như không tồn tại.
    ///
    /// Đây là chốt quan trọng nhất: quên cấu hình ở môi trường thật phải dẫn tới "không dọn
    /// được" chứ không phải "ai cũng xoá được".
    /// </summary>
    [Fact]
    public async Task Don_tenant_e2e_tra_404_khi_co_tat()
    {
        var client = factory.CreateClient();
        var token = await DangNhapChuAsync(client, "chu-don-tat");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/don-tenant-e2e");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>Token tenant không gọi được, kể cả khi cờ bật.</summary>
    [Fact]
    public async Task Token_tenant_khong_don_duoc_tenant_e2e()
    {
        var client = factory.CreateClient();
        var phien = await TroGiupPhien.DangNhapAsync(client, factory);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chu-he-thong/don-tenant-e2e");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", phien.Access);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private record LoiDto(string ErrorCode);

    private record TrungTamDto(Guid Id, string MaTrungTam, string TenTrungTam);
}

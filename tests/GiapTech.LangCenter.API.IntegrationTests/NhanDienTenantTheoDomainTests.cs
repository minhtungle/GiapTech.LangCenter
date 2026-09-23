using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GiapTech.LangCenter.API.Middleware;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// ADR-0008 — nhận diện tenant qua domain.
///
/// Điều được canh ở đây KHÔNG phải "tính năng chạy đúng", mà là **hỏng thì hỏng theo hướng
/// đóng**. Global Query Filter của dự án có nhánh `TenantIdHienTai == null` tắt filter hoàn
/// toàn (cần cho seeder/migration). Chừng nào mọi endpoint còn đòi JWT thì nhánh đó vô hại;
/// domain và trang landing công khai làm tiền đề ấy sai.
///
/// Nên mọi test dưới đây đều hỏi cùng một câu: *khi có gì đó không khớp, hệ thống TỪ CHỐI hay
/// lặng lẽ phục vụ dữ liệu của trung tâm khác?*
/// </summary>
public class NhanDienTenantTheoDomainTests(DomainApiFactory factory)
    : IClassFixture<DomainApiFactory>
{
    private const string DomainQuanTri = "trungtam-a.giaptex.com";
    private const string DomainLanding = "trungtam-a.edu.vn";

    /// <summary>
    /// Đặt header y như nginx làm. Test phải đi qua đúng đường mà production đi, nếu không
    /// nó chỉ đang kiểm một đường riêng của test.
    /// </summary>
    private static void GiaLapNginx(HttpRequestMessage req, string domain) =>
        req.Headers.Add(DomainRequest.TenHeader, domain);

    // ---------------------------------------------------------------------------------
    // 1. Host giả không đổi được tenant  (test quan trọng nhất)
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Khi KHÔNG đứng sau reverse proxy, header nhận diện domain do client tự đặt phải bị
    /// bỏ qua hoàn toàn.
    ///
    /// Đây là ca tấn công trực tiếp: gửi `X-Tenant-Domain` của trung tâm khác để tự chọn
    /// tenant. Nếu qua được thì mọi thứ còn lại của ADR-0008 vô nghĩa.
    /// </summary>
    [Fact]
    public async Task Header_domain_do_client_tu_dat_khong_duoc_tin_khi_khong_co_proxy()
    {
        // factory mặc định: SAU_REVERSE_PROXY = false (giống chạy local)
        var client = factory.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/ten-trung-tam/{factory.MaTrungTamA}");
        GiaLapNginx(req, DomainQuanTri);   // client tự đặt — phải bị lờ đi

        var res = await client.SendAsync(req);

        // Mã trung tâm có THẬT, nên 200 là bằng chứng header đã bị lờ đi hoàn toàn.
        // Dùng mã thật chứ không dùng mã bịa: với mã bịa thì endpoint cũng trả 404 và test
        // sẽ "xanh" mà không chứng minh được gì.
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>
    /// Chiều ngược của test trên: khi ĐỨNG SAU proxy, header đó phải có tác dụng.
    ///
    /// Không có test này thì một thay đổi làm hàm đọc domain luôn trả `null` vẫn xanh — và
    /// tính năng chết âm thầm trên VPS.
    /// </summary>
    [Fact]
    public async Task Sau_proxy_thi_header_domain_co_tac_dung()
    {
        var f = factory.SauProxy();
        var client = f.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/ten-trung-tam/{f.MaTrungTamA}");
        GiaLapNginx(req, "khong-ai-gan-domain-nay.example.com");

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var noi = await res.Content.ReadFromJsonAsync<LoiDto>();
        Assert.Equal("DOMAIN_CHUA_GAN", noi?.ErrorCode);
    }

    // ---------------------------------------------------------------------------------
    // 2. Domain lạ KHÔNG được trả dữ liệu của mọi tenant
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Domain không khớp tenant nào → **từ chối**, tuyệt đối không chạy tiếp với tenant rỗng.
    ///
    /// Đây chính là nhánh `TenantIdHienTai == null`: chạy tiếp thì Query Filter tắt và
    /// endpoint trả dữ liệu của mọi trung tâm. Hỏng kiểu này im lặng — trang vẫn hiện, chỉ là
    /// hiện nội dung của trung tâm khác.
    /// </summary>
    [Fact]
    public async Task Domain_la_bi_tu_choi_chu_khong_chay_tiep_voi_tenant_rong()
    {
        var f = factory.SauProxy();
        var client = f.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/ten-trung-tam/{f.MaTrungTamA}");
        GiaLapNginx(req, "domain-chua-ai-gan.example.com");

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------------------------------------------------------------------------------
    // 3. Token của tenant A đi vào domain của tenant B
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Token hợp lệ nhưng thuộc trung tâm khác với domain đang gọi → từ chối.
    ///
    /// Chọn bừa một bên đều sai: tin token thì domain mất tác dụng ràng buộc; tin domain thì
    /// người dùng thao tác trên trung tâm mình không định vào.
    /// </summary>
    [Fact]
    public async Task Token_cua_tenant_khac_khong_dung_duoc_tren_domain_nay()
    {
        var f = factory.SauProxy();
        var (client, token) = await f.DangNhapTenantBAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/toi/he-thong");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        GiaLapNginx(req, DomainQuanTri);   // domain của tenant A

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var noi = await res.Content.ReadFromJsonAsync<LoiDto>();
        Assert.Equal("TOKEN_KHONG_THUOC_DOMAIN", noi?.ErrorCode);
    }

    /// <summary>
    /// Chiều ngược: token ĐÚNG tenant của domain thì phải đi lọt.
    ///
    /// Không có test này thì một thay đổi làm middleware chặn mọi thứ vẫn "xanh" ở ba test
    /// trên — và cả hệ thống chết trên VPS.
    /// </summary>
    [Fact]
    public async Task Token_dung_tenant_cua_domain_thi_di_lot()
    {
        var f = factory.SauProxy();
        var (client, token) = await f.DangNhapTenantAAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/toi/he-thong");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        GiaLapNginx(req, DomainQuanTri);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------------------------------------------------------------------------------
    // 4. Hai đường ra cùng một tenant
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Cùng một tenant, vào bằng domain và vào bằng mã trung tâm, phải ra cùng dữ liệu.
    ///
    /// Canh việc hai đường không lệch nhau — nếu lệch thì một trong hai đang nhìn sai tenant.
    /// </summary>
    [Fact]
    public async Task Vao_bang_domain_va_bang_ma_ra_cung_mot_tenant()
    {
        var f = factory.SauProxy();

        var (clientDomain, tokenA) = await f.DangNhapTenantAAsync();
        var reqDomain = new HttpRequestMessage(HttpMethod.Get, "/api/v1/toi/cau-hinh");
        reqDomain.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        GiaLapNginx(reqDomain, DomainQuanTri);
        var resDomain = await clientDomain.SendAsync(reqDomain);

        // Cùng token, nhưng không có header domain (đường mã trung tâm, như local)
        var reqMa = new HttpRequestMessage(HttpMethod.Get, "/api/v1/toi/cau-hinh");
        reqMa.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resMa = await clientDomain.SendAsync(reqMa);

        Assert.Equal(HttpStatusCode.OK, resDomain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resMa.StatusCode);
        Assert.Equal(
            await resDomain.Content.ReadAsStringAsync(),
            await resMa.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------
    // 5. Domain LANDING không phải cửa đăng nhập quản trị
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Domain landing giải ra đúng tenant, nhưng phải được đánh dấu **không phải** cửa quản trị.
    ///
    /// Gộp hai thứ lại thì trang công khai vô tình thành cửa vào hệ thống nội bộ.
    /// </summary>
    [Fact]
    public async Task Domain_landing_giai_ra_tenant_nhung_khong_phai_cua_quan_tri()
    {
        var f = factory.SauProxy();
        using var scope = f.Services.CreateScope();
        var giai = scope.ServiceProvider
            .GetRequiredService<Application.Common.Interfaces.IGiaiTenantTheoDomain>();

        var quanTri = await giai.TraAsync(DomainQuanTri);
        var landing = await giai.TraAsync(DomainLanding);

        Assert.NotNull(quanTri);
        Assert.NotNull(landing);
        Assert.Equal(quanTri!.TenantId, landing!.TenantId);   // cùng một trung tâm
        Assert.True(quanTri.LaDomainQuanTri);
        Assert.False(landing.LaDomainQuanTri);
    }

    // ---------------------------------------------------------------------------------
    // 6. Chuẩn hoá domain
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Trình duyệt và proxy gửi domain ở nhiều dạng; tất cả phải tra ra cùng một tenant.
    ///
    /// Riêng CỔNG thì KHÔNG được cắt: ở local `localhost:5173` và `localhost:9999` là hai
    /// ứng dụng khác nhau, gộp lại là gộp nhầm.
    /// </summary>
    [Theory]
    [InlineData("trungtam-a.giaptex.com")]
    [InlineData("TrungTam-A.GiapTex.Com")]     // hoa/thường
    [InlineData("trungtam-a.giaptex.com.")]    // dấu chấm cuối FQDN
    [InlineData(" trungtam-a.giaptex.com ")]   // khoảng trắng thừa
    public async Task Domain_duoc_chuan_hoa_truoc_khi_tra(string dang)
    {
        var f = factory.SauProxy();
        using var scope = f.Services.CreateScope();
        var giai = scope.ServiceProvider
            .GetRequiredService<Application.Common.Interfaces.IGiaiTenantTheoDomain>();

        var kq = await giai.TraAsync(dang);

        Assert.NotNull(kq);
        Assert.True(kq!.LaDomainQuanTri);
    }

    // ---------------------------------------------------------------------------------
    // 7. Endpoint cho màn đăng nhập biết có nên ẩn ô mã không
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Trên domain đã gắn: trả 200 kèm tên trung tâm ⇒ frontend ẩn ô mã.
    /// </summary>
    [Fact]
    public async Task Tren_domain_da_gan_thi_tra_ve_trung_tam()
    {
        var f = factory.SauProxy();
        f.BaoDamDaGanDomain();
        var client = f.CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/trung-tam-theo-domain");
        GiaLapNginx(req, DomainQuanTri);

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<TrungTamDto>();
        Assert.False(string.IsNullOrWhiteSpace(dto?.TenTrungTam));
    }

    /// <summary>
    /// Đường mặc định (không có domain): trả 204 ⇒ frontend hiện ô mã như cũ.
    ///
    /// 204 chứ không 404: "ở đây không gắn domain nào" là câu trả lời BÌNH THƯỜNG của đường
    /// mặc định. Dùng 404 sẽ làm log đầy lỗi giả mỗi lần ai mở màn đăng nhập.
    /// </summary>
    [Fact]
    public async Task Khong_co_domain_thi_tra_204_de_frontend_hien_o_ma()
    {
        // factory mặc định = không sau proxy = giống local
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/v1/auth/trung-tam-theo-domain");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    /// <summary>
    /// Endpoint này KHÔNG nhận tham số, nên client không chọn được trung tâm.
    ///
    /// Canh việc không ai "tiện tay" thêm tham số mã trung tâm vào sau này — làm vậy là biến
    /// nó thành bản sao của `ten-trung-tam/{ma}` nhưng KHÔNG có ràng buộc độ dài 7 ký tự.
    /// </summary>
    [Fact]
    public async Task Khong_the_chon_trung_tam_qua_query_string()
    {
        var f = factory.SauProxy();
        f.BaoDamDaGanDomain();
        var client = f.CreateClient();

        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/auth/trung-tam-theo-domain?maTrungTam={f.MaTrungTamB}&tenantId={f.TenantBId}");
        GiaLapNginx(req, DomainQuanTri);   // domain của tenant A

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<TrungTamDto>();

        // Phải là tenant A (chủ domain), không phải tenant B mà client cố chỉ định.
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenA = db.Tenants.Single(t => t.Id == f.TenantAId).TenTrungTam;
        Assert.Equal(tenA, dto?.TenTrungTam);
        Assert.Equal(f.MaTrungTamA, dto?.MaTrungTam);
    }

    private record TrungTamDto(string MaTrungTam, string TenTrungTam, string? TenVietTat, bool CoLogo);

    private record LoiDto(string ErrorCode);
}

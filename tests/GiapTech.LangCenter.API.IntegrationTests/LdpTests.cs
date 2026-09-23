using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-30 — trang đích công khai.
///
/// Điều được canh không phải "tính năng chạy" mà là **thứ gì ra được Internet**. Đây là module
/// đầu tiên cố ý phục vụ người chưa đăng nhập, nên mọi test dưới đây hỏi cùng một câu:
/// *nội dung này có được phép lộ không?*
/// </summary>
public class LdpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<string> TokenAdminAsync(HttpClient client)
        => (await TroGiupPhien.DangNhapAsync(client, factory)).Access;

    private static HttpRequestMessage Req(HttpMethod m, string url, string token)
    {
        var r = new HttpRequestMessage(m, url);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return r;
    }

    /// <summary>
    /// Request của KHÁCH VÃNG LAI, mô phỏng đường `/t/{mã}`: không token, gửi mã trung tâm
    /// qua header — đúng cách frontend công khai gọi khi trung tâm chưa trỏ domain riêng.
    /// </summary>
    private HttpRequestMessage ReqKhach(HttpMethod m, string url, string? ma = null)
    {
        var r = new HttpRequestMessage(m, url);
        r.Headers.Add("X-Ma-Trung-Tam", ma ?? factory.MaTrungTamA);
        return r;
    }

    // ---------------------------------------------------------------------------------
    // Chưa xuất bản thì KHÔNG ra Internet
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Test quan trọng nhất: soạn nội dung xong mà chưa bấm xuất bản thì đường công khai
    /// **404**, không phải trả nội dung nháp.
    /// </summary>
    [Fact]
    public async Task Trang_chua_xuat_ban_thi_duong_cong_khai_tra_404()
    {
        /*
          Dùng tenant C, KHÔNG dùng A.

          Mọi test trong lớp này chia sẻ một `ApiFactory`, nên tenant A bị các test khác xuất
          bản trước và test này đỏ tuỳ thứ tự chạy. Đó là lớp lỗi khó chịu nhất: đỏ ngẫu
          nhiên, chạy riêng lại xanh. Tenant C không test nào khác đụng tới.
        */
        var client = factory.CreateClient();
        var token = (await TroGiupPhien.DangNhapAsync(
            client, factory, maTrungTam: factory.MaTrungTamC)).Access;

        // Gọi màn soạn để hệ thống tự tạo trang (mặc định DaXuatBan = false).
        var soan = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));
        soan.EnsureSuccessStatusCode();

        var congKhai = await client.SendAsync(
            ReqKhach(HttpMethod.Get, "/api/v1/ldp/cong-khai", factory.MaTrungTamC));

        Assert.Equal(HttpStatusCode.NotFound, congKhai.StatusCode);
    }

    /// <summary>Chiều ngược: xuất bản rồi thì ai cũng đọc được, không cần token.</summary>
    [Fact]
    public async Task Xuat_ban_roi_thi_khach_vang_lai_doc_duoc()
    {
        var client = factory.CreateClient();
        var token = await TokenAdminAsync(client);

        await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));

        var xb = Req(HttpMethod.Put, "/api/v1/ldp/xuat-ban", token);
        xb.Content = JsonContent.Create(new
        {
            XuatBan = true, TieuDeSeo = "Trung tâm ABC", MoTaSeo = "Học tiếng Anh"
        });
        (await client.SendAsync(xb)).EnsureSuccessStatusCode();

        // KHÔNG gắn Authorization — đây là khách vãng lai đi qua đường `/t/{mã}`.
        var res = await client.SendAsync(ReqKhach(HttpMethod.Get, "/api/v1/ldp/cong-khai"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Trung tâm ABC", dto.GetProperty("tieuDeSeo").GetString());
    }

    // ---------------------------------------------------------------------------------
    // Khối TẮT không được lộ nội dung
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Khối tắt phải **biến mất khỏi phản hồi**, không phải gửi kèm cờ `hien=false`.
    ///
    /// Gửi kèm là để lộ nội dung trung tâm cố ý giấu — chỉ cần mở DevTools là thấy.
    /// </summary>
    [Fact]
    public async Task Khoi_tat_khong_xuat_hien_trong_phan_hoi_cong_khai()
    {
        var client = factory.CreateClient();
        var token = await TokenAdminAsync(client);

        var soan = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));
        var trang = await soan.Content.ReadFromJsonAsync<JsonElement>();
        var khoi = trang.GetProperty("khois")[0];
        var khoiId = khoi.GetProperty("id").GetGuid();

        // Tắt khối đầu, đặt một nội dung nhận diện được
        var sua = Req(HttpMethod.Put, $"/api/v1/ldp/khoi/{khoiId}", token);
        sua.Content = JsonContent.Create(new
        {
            Hien = false, ThuTu = 0, TieuDe = "BI-MAT-KHONG-DUOC-LO",
            MoTa = (string?)null, KhoaAnh = (string?)null,
            NhanNut = (string?)null, DuongDanNut = (string?)null
        });
        (await client.SendAsync(sua)).EnsureSuccessStatusCode();

        var xb = Req(HttpMethod.Put, "/api/v1/ldp/xuat-ban", token);
        xb.Content = JsonContent.Create(new { XuatBan = true, TieuDeSeo = (string?)null, MoTaSeo = (string?)null });
        (await client.SendAsync(xb)).EnsureSuccessStatusCode();

        var res = await client.SendAsync(ReqKhach(HttpMethod.Get, "/api/v1/ldp/cong-khai"));
        var body = await res.Content.ReadAsStringAsync();

        // Kiểm trên CHUỖI THÔ, không qua DTO: nếu ai đó thêm trường mới làm lộ nội dung thì
        // kiểm qua DTO sẽ không thấy, còn chuỗi thô thì thấy.
        Assert.DoesNotContain("BI-MAT-KHONG-DUOC-LO", body);
    }

    // ---------------------------------------------------------------------------------
    // DTO công khai KHÔNG chứa định danh nội bộ
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Phản hồi công khai không được chứa `id` của khối/mục, cũng không chứa khoá ảnh.
    ///
    /// Canh đúng bài học 07/09/2026 (rò rỉ học phí) áp vào chỗ khán giả là cả Internet: DTO
    /// công khai tách hẳn DTO quản trị để thêm trường bên này không chạm bên kia. Test đọc
    /// chuỗi thô nên nó bắt được cả trường thêm sau này.
    /// </summary>
    [Fact]
    public async Task Phan_hoi_cong_khai_khong_chua_id_hay_khoa_anh()
    {
        var client = factory.CreateClient();
        var token = await TokenAdminAsync(client);

        var soan = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));
        var trang = await soan.Content.ReadFromJsonAsync<JsonElement>();
        var khoiId = trang.GetProperty("khois")[0].GetProperty("id").GetGuid();

        var xb = Req(HttpMethod.Put, "/api/v1/ldp/xuat-ban", token);
        xb.Content = JsonContent.Create(new { XuatBan = true, TieuDeSeo = (string?)null, MoTaSeo = (string?)null });
        (await client.SendAsync(xb)).EnsureSuccessStatusCode();

        var body = await (await client.SendAsync(
            ReqKhach(HttpMethod.Get, "/api/v1/ldp/cong-khai"))).Content.ReadAsStringAsync();

        // Không có id khối trong thân phản hồi (id chỉ xuất hiện trong URL ảnh, và chỉ khi
        // khối có ảnh — ở đây chưa có ảnh nào).
        Assert.DoesNotContain(khoiId.ToString(), body);
        Assert.DoesNotContain("khoaAnh", body);
        Assert.DoesNotContain("\"hien\"", body);
    }

    // ---------------------------------------------------------------------------------
    // Form liên hệ — endpoint ẩn danh GHI
    // ---------------------------------------------------------------------------------

    [Fact]
    public async Task Khach_vang_lai_gui_duoc_lien_he()
    {
        var client = factory.CreateClient();

        var req = ReqKhach(HttpMethod.Post, "/api/v1/ldp/lien-he");
        req.Content = JsonContent.Create(new
        {
            HoTen = "Nguyễn Văn Khách",
            SoDienThoai = "0901234567",
            QuanTam = "IELTS",
            LoiNhan = "Xin tư vấn giúp"
        });
        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var token = await TokenAdminAsync(client);
        var ds = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/lien-he", token));
        var body = await ds.Content.ReadAsStringAsync();

        Assert.Contains("Nguyễn Văn Khách", body);
    }

    /// <summary>
    /// Honeypot: bot điền trường ẩn ⇒ **bỏ qua im lặng**, vẫn trả 204.
    ///
    /// Trả lỗi là nói cho người viết bot biết họ bị phát hiện, rồi họ sửa bot bỏ qua trường
    /// đó. Trả "đã nhận" thì bot tưởng thành công.
    /// </summary>
    [Fact]
    public async Task Honeypot_bo_qua_im_lang_va_khong_ghi_gi()
    {
        var client = factory.CreateClient();

        var req = ReqKhach(HttpMethod.Post, "/api/v1/ldp/lien-he");
        req.Content = JsonContent.Create(new
        {
            HoTen = "BOT-KHONG-DUOC-GHI",
            SoDienThoai = "0900000000",
            Website = "http://spam.example.com"   // trường ẩn — người thật không thấy
        });
        var res = await client.SendAsync(req);

        // 204 y như gửi thật: không tiết lộ rằng bot bị phát hiện.
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        var token = await TokenAdminAsync(client);
        var ds = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/lien-he", token));

        Assert.DoesNotContain("BOT-KHONG-DUOC-GHI", await ds.Content.ReadAsStringAsync());
    }

    /// <summary>Chuyển liên hệ sang CRM — cầu nối LDP → CRM, và bấm hai lần không tạo hai khách.</summary>
    [Fact]
    public async Task Chuyen_sang_crm_va_bam_hai_lan_khong_tao_hai_khach()
    {
        var client = factory.CreateClient();

        var guiReq = ReqKhach(HttpMethod.Post, "/api/v1/ldp/lien-he");
        guiReq.Content = JsonContent.Create(new
        {
            HoTen = "Khách Chuyển CRM", SoDienThoai = "0911222333", QuanTam = "TOEIC"
        });
        (await client.SendAsync(guiReq)).EnsureSuccessStatusCode();

        var token = await TokenAdminAsync(client);
        var ds = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/lien-he", token));
        var danhSach = await ds.Content.ReadFromJsonAsync<JsonElement>();
        var lienHeId = danhSach.EnumerateArray()
            .First(x => x.GetProperty("hoTen").GetString() == "Khách Chuyển CRM")
            .GetProperty("id").GetGuid();

        var lan1 = await client.SendAsync(
            Req(HttpMethod.Post, $"/api/v1/ldp/lien-he/{lienHeId}/chuyen-crm", token));
        lan1.EnsureSuccessStatusCode();
        var id1 = (await lan1.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();

        var lan2 = await client.SendAsync(
            Req(HttpMethod.Post, $"/api/v1/ldp/lien-he/{lienHeId}/chuyen-crm", token));
        lan2.EnsureSuccessStatusCode();
        var id2 = (await lan2.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();

        // Bấm hai lần (mạng chậm, người dùng sốt ruột) không được đẻ ra khách hàng trùng.
        Assert.Equal(id1, id2);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var khach = db.KhachHangs.IgnoreQueryFilters()
            .Where(k => k.HoTen == "Khách Chuyển CRM").ToList();

        Assert.Single(khach);
        Assert.Equal(Domain.Enums.NguonKhachHang.TuLanding, khach[0].Nguon);
    }

    // ---------------------------------------------------------------------------------
    // Cách ly tenant
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Trang của tenant A không được lộ qua đường công khai của tenant B.
    ///
    /// Đây là ca mà nhánh `TenantIdHienTai == null` sẽ gây ra nếu middleware hỏng: gom nội
    /// dung mọi trung tâm vào một trang.
    /// </summary>
    [Fact]
    public async Task Trang_cua_tenant_khac_khong_lo_qua_duong_cong_khai()
    {
        var client = factory.CreateClient();
        var token = await TokenAdminAsync(client);   // tenant A

        var soan = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));
        var trang = await soan.Content.ReadFromJsonAsync<JsonElement>();
        var khoiId = trang.GetProperty("khois")[0].GetProperty("id").GetGuid();

        var sua = Req(HttpMethod.Put, $"/api/v1/ldp/khoi/{khoiId}", token);
        sua.Content = JsonContent.Create(new
        {
            Hien = true, ThuTu = 0, TieuDe = "NOI-DUNG-CUA-TENANT-A",
            MoTa = (string?)null, KhoaAnh = (string?)null,
            NhanNut = (string?)null, DuongDanNut = (string?)null
        });
        (await client.SendAsync(sua)).EnsureSuccessStatusCode();

        var xb = Req(HttpMethod.Put, "/api/v1/ldp/xuat-ban", token);
        xb.Content = JsonContent.Create(new { XuatBan = true, TieuDeSeo = (string?)null, MoTaSeo = (string?)null });
        (await client.SendAsync(xb)).EnsureSuccessStatusCode();

        // Tenant B tạo trang riêng và xuất bản
        var clientB = factory.CreateClient();
        var tokenB = (await TroGiupPhien.DangNhapAsync(
            clientB, factory, maTrungTam: factory.MaTrungTamB)).Access;

        await clientB.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", tokenB));
        var xbB = Req(HttpMethod.Put, "/api/v1/ldp/xuat-ban", tokenB);
        xbB.Content = JsonContent.Create(new { XuatBan = true, TieuDeSeo = (string?)null, MoTaSeo = (string?)null });
        (await clientB.SendAsync(xbB)).EnsureSuccessStatusCode();

        // Đọc công khai TRONG phạm vi tenant B — không được thấy nội dung của A.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var soKhoiCuaB = db.KhoiLdps.IgnoreQueryFilters()
            .Count(k => k.TenantId == factory.TenantBId && k.TieuDe == "NOI-DUNG-CUA-TENANT-A");

        Assert.Equal(0, soKhoiCuaB);
    }

    /// <summary>Khối chỉ chứa mục con ở bốn loại; ba loại còn lại phải từ chối.</summary>
    [Fact]
    public async Task Khoi_khong_chua_muc_con_thi_tu_choi_them_muc()
    {
        var client = factory.CreateClient();
        var token = await TokenAdminAsync(client);

        var soan = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/ldp/trang-dich", token));
        var trang = await soan.Content.ReadFromJsonAsync<JsonElement>();

        // Hero = 0, không chứa mục con
        // Enum serialize thành CHUỖI ("Hero"), không phải số — đọc `GetInt32()` sẽ ném
        // InvalidOperationException. Đã viết sai một lần.
        var hero = trang.GetProperty("khois").EnumerateArray()
            .First(k => k.GetProperty("loai").GetString() == nameof(LoaiKhoiLdp.Hero));

        var them = Req(HttpMethod.Post,
            $"/api/v1/ldp/khoi/{hero.GetProperty("id").GetGuid()}/muc", token);
        them.Content = JsonContent.Create(new { TieuDe = "Không được thêm" });

        var res = await client.SendAsync(them);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

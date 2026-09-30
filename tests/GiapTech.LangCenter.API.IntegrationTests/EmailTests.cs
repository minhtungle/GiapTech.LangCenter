using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// FR-31 — cấu hình email và mẫu nội dung.
///
/// Điều được canh: **mật khẩu SMTP không bao giờ ra khỏi server**, và cấu hình của hai trung
/// tâm không lẫn vào nhau.
/// </summary>
public class EmailTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static HttpRequestMessage Req(HttpMethod m, string url, string token)
    {
        var r = new HttpRequestMessage(m, url);
        r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return r;
    }

    private async Task<string> TokenAsync(HttpClient client, string? maTrungTam = null)
        => (await TroGiupPhien.DangNhapAsync(client, factory, maTrungTam: maTrungTam)).Access;

    private static HttpRequestMessage LuuCauHinh(string token, string? matKhau)
    {
        var r = Req(HttpMethod.Put, "/api/v1/email/thiet-lap", token);
        r.Content = JsonContent.Create(new
        {
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            SmtpUser = "no-reply@example.com",
            MatKhau = matKhau,
            SmtpNguoiGui = "no-reply@example.com",
            SmtpTenNguoiGui = "Trung tâm Thử",
        });
        return r;
    }

    // ---------------------------------------------------------------------------------
    // Mật khẩu không ra khỏi server
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Test quan trọng nhất của FR-31: API **không bao giờ** trả mật khẩu, kể cả cho quản trị
    /// viên của chính trung tâm đó.
    ///
    /// Kiểm hai lớp, vì một lớp thôi thì lọt:
    ///
    /// 1. Bản rõ không có trong phản hồi — điều hiển nhiên.
    /// 2. **Không có TRƯỜNG nào mang mật khẩu**, kể cả dạng đã mã hoá.
    ///
    /// Điểm 2 mới là cái khó: phiên bản đầu của test này chỉ kiểm bản rõ, và một mutant trả
    /// thẳng `SmtpMatKhauMaHoa` ra body đã đi lọt. Bản mã lọt ra ngoài vẫn là rò rỉ — nó nằm
    /// lại trong cache trình duyệt, log proxy, và chỉ còn chờ khoá rò theo. Cấu hình email
    /// không có lý do gì để đem theo mật khẩu ở bất kỳ dạng nào.
    ///
    /// Duyệt tên trường thay vì so cả chuỗi: bắt được cả trường mới mà người thêm chưa nghĩ tới.
    /// </summary>
    [Fact]
    public async Task Api_khong_bao_gio_tra_mat_khau_smtp()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client);
        const string matKhau = "MAT-KHAU-SMTP-KHONG-DUOC-LO";

        (await client.SendAsync(LuuCauHinh(token, matKhau))).EnsureSuccessStatusCode();

        var res = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/email/thiet-lap", token));
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.DoesNotContain(matKhau, body);

        // Bản mã lấy thẳng từ DB — nếu nó xuất hiện trong phản hồi thì có trường đang rò.
        string? banMa;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            banMa = db.Tenants.Single(t => t.Id == factory.TenantAId).SmtpMatKhauMaHoa;
        }
        Assert.NotNull(banMa);
        Assert.DoesNotContain(banMa, body);

        // Và không trường nào ĐƯỢC PHÉP tên như thể chứa mật khẩu — chặn cả trường thêm sau
        // này mà người thêm không nghĩ tới việc nó đi ra ngoài.
        var json = JsonDocument.Parse(body).RootElement;
        foreach (var truong in json.EnumerateObject())
        {
            // `coMatKhau` là CỜ bool được phép — miễn trừ đích danh, không miễn trừ theo mẫu
            // tên, để một trường mới kiểu `matKhauCu` vẫn bị bắt.
            if (truong.Name == "coMatKhau")
            {
                Assert.Equal(JsonValueKind.True, truong.Value.ValueKind);
                continue;
            }

            Assert.False(
                truong.Name.Contains("matKhau", StringComparison.OrdinalIgnoreCase),
                $"Trường '{truong.Name}' mang mật khẩu ra khỏi server. Cấu hình email chỉ được "
                + "trả cờ `coMatKhau`, không trả giá trị ở bất kỳ dạng nào — kể cả đã mã hoá.");
        }

        // Có cờ báo "đã có mật khẩu" — để giao diện hiện đúng trạng thái.
        Assert.Contains("\"coMatKhau\":true", body);
    }

    /// <summary>
    /// Chốt chính của ADR-0010: mật khẩu lưu xuống DB **không phải bản rõ**.
    ///
    /// Đọc thẳng DB chứ không qua API — API có thể đúng trong khi tầng lưu trữ sai.
    /// </summary>
    [Fact]
    public async Task Mat_khau_luu_xuong_db_da_duoc_ma_hoa()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client);
        const string matKhau = "BAN-RO-KHONG-DUOC-NAM-TRONG-DB";

        (await client.SendAsync(LuuCauHinh(token, matKhau))).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var luu = db.Tenants.Single(t => t.Id == factory.TenantAId).SmtpMatKhauMaHoa;

        Assert.NotNull(luu);
        Assert.NotEqual(matKhau, luu);
        Assert.DoesNotContain(matKhau, luu);
    }

    /// <summary>
    /// Để trống ô mật khẩu khi cập nhật ⇒ **giữ nguyên giá trị cũ**, không xoá.
    ///
    /// Đây là ngoại lệ có chủ ý với quy tắc #1: trường này không hiển thị được nên form không
    /// thể gửi lại nó. Không có test này thì một lần sửa tên người gửi sẽ âm thầm xoá mật
    /// khẩu, và email ngừng đi mà không ai biết vì sao.
    /// </summary>
    [Fact]
    public async Task De_trong_mat_khau_thi_giu_nguyen_gia_tri_cu()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client, factory.MaTrungTamB);

        (await client.SendAsync(LuuCauHinh(token, "mat-khau-ban-dau"))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var truoc = db.Tenants.Single(t => t.Id == factory.TenantBId).SmtpMatKhauMaHoa;
            Assert.NotNull(truoc);
        }

        // Lưu lại với ô mật khẩu TRỐNG
        (await client.SendAsync(LuuCauHinh(token, null))).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sau = db.Tenants.Single(t => t.Id == factory.TenantBId).SmtpMatKhauMaHoa;
            Assert.NotNull(sau);   // vẫn còn, không bị xoá
        }
    }

    /// <summary>Lần đầu cấu hình mà không nhập mật khẩu ⇒ từ chối, không lưu cấu hình què.</summary>
    [Fact]
    public async Task Lan_dau_cau_hinh_khong_co_mat_khau_thi_tu_choi()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client, factory.MaTrungTamC);

        var res = await client.SendAsync(LuuCauHinh(token, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------------------------------------------------------------------------------
    // Cách ly tenant
    // ---------------------------------------------------------------------------------

    /// <summary>Cấu hình của tenant A không lọt sang tenant B.</summary>
    [Fact]
    public async Task Cau_hinh_cua_hai_tenant_khong_lan_vao_nhau()
    {
        var clientA = factory.CreateClient();
        var tokenA = await TokenAsync(clientA);

        var rA = Req(HttpMethod.Put, "/api/v1/email/thiet-lap", tokenA);
        rA.Content = JsonContent.Create(new
        {
            SmtpHost = "smtp.rieng-cua-a.com",
            SmtpPort = 465,
            SmtpUser = "a@a.com",
            MatKhau = "mk-a",
            SmtpNguoiGui = "a@a.com",
            SmtpTenNguoiGui = (string?)null,
        });
        (await clientA.SendAsync(rA)).EnsureSuccessStatusCode();

        var clientB = factory.CreateClient();
        var tokenB = await TokenAsync(clientB, factory.MaTrungTamB);
        var resB = await clientB.SendAsync(
            Req(HttpMethod.Get, "/api/v1/email/thiet-lap", tokenB));

        Assert.DoesNotContain("smtp.rieng-cua-a.com", await resB.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------
    // Mẫu email
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Danh sách mẫu **luôn đủ mọi loại**, kể cả loại chưa soạn — lúc đó trả nội dung mặc
    /// định kèm `daSoan: false`.
    ///
    /// Không có điều này thì loại chưa soạn biến mất khỏi màn hình và không ai biết nó tồn
    /// tại, nên không bao giờ soạn.
    /// </summary>
    [Fact]
    public async Task Danh_sach_mau_luon_du_moi_loai_ke_ca_chua_soan()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client);

        var res = await client.SendAsync(Req(HttpMethod.Get, "/api/v1/email/mau", token));
        var ds = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(
            Enum.GetValues<Domain.Entities.LoaiMauEmail>().Length,
            ds.GetArrayLength());

        // Mỗi mẫu phải có sẵn nội dung mặc định — không có ô trắng nào.
        foreach (var m in ds.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("tieuDe").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("noiDungHtml").GetString()));
            Assert.True(m.GetProperty("bien").GetArrayLength() > 0);
        }
    }

    /// <summary>Soạn rồi xoá ⇒ quay về mẫu mặc định, không thành rỗng.</summary>
    [Fact]
    public async Task Xoa_mau_thi_quay_ve_mau_mac_dinh()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client, factory.MaTrungTamC);
        const string loai = "TraLoiLienHe";

        var luu = Req(HttpMethod.Put, $"/api/v1/email/mau/{loai}", token);
        luu.Content = JsonContent.Create(new
        {
            TieuDe = "TIEU-DE-TU-SOAN",
            NoiDungHtml = "<p>nội dung tự soạn</p>",
            DangDung = true,
        });
        (await client.SendAsync(luu)).EnsureSuccessStatusCode();

        var sauKhiSoan = await (await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/email/mau", token))).Content.ReadAsStringAsync();
        Assert.Contains("TIEU-DE-TU-SOAN", sauKhiSoan);

        (await client.SendAsync(
            Req(HttpMethod.Delete, $"/api/v1/email/mau/{loai}", token))).EnsureSuccessStatusCode();

        var sauKhiXoa = await (await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/email/mau", token))).Content.ReadAsStringAsync();

        Assert.DoesNotContain("TIEU-DE-TU-SOAN", sauKhiXoa);
        // Vẫn còn nội dung — là mẫu mặc định, không phải rỗng.
        Assert.Contains("\"daSoan\":false", sauKhiXoa);
    }

    /// <summary>
    /// Hai mẫu cần job nền phải được đánh dấu `tuGuiDuoc: false`.
    ///
    /// Không có cờ này thì trung tâm soạn xong rồi ngồi đợi một email không bao giờ tới —
    /// và không có gì trên màn hình nói cho họ biết.
    /// </summary>
    [Fact]
    public async Task Mau_can_job_nen_duoc_danh_dau_chua_tu_gui_duoc()
    {
        var client = factory.CreateClient();
        var token = await TokenAsync(client);

        var ds = await (await client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/email/mau", token)))
            .Content.ReadFromJsonAsync<JsonElement>();

        var theoLoai = ds.EnumerateArray()
            .ToDictionary(
                m => m.GetProperty("loai").GetString()!,
                m => m.GetProperty("tuGuiDuoc").GetBoolean());

        Assert.True(theoLoai["ChaoMungHocVien"]);
        Assert.True(theoLoai["TraLoiLienHe"]);
        Assert.False(theoLoai["NhacNoHocPhi"]);
        Assert.False(theoLoai["NhacLichHoc"]);
    }
}

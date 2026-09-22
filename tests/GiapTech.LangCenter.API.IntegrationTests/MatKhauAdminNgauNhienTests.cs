using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Mật khẩu admin của trung tâm mới phải NGẪU NHIÊN** (22/09/2026).
///
/// Trước đây `TenantSeeder` băm sẵn hằng `"123456"` và controller tự viết `matKhau = "123456"`
/// vào response — hai chỗ không liên quan nhau về mã, chỉ tình cờ cùng giá trị. Nghĩa là **mọi
/// trung tâm** đều có `admin`/`123456` trong DB cho tới khi ai đó đăng nhập lần đầu.
///
/// Chốt `PhaiDoiMatKhau` **không** cứu được: người vào bằng cặp mặc định vẫn lấy được token,
/// rồi gọi thẳng `/auth/doi-mat-khau` (đường dẫn nằm trong allowlist của
/// `BuocDoiMatKhauMiddleware`) để tự đặt mật khẩu của mình ⇒ chiếm trung tâm. Rà soát bảo mật
/// 22/09/2026 xếp đây là một trong hai mục nghiêm trọng nhất.
/// </summary>
public class MatKhauAdminNgauNhienTests
{
    /// <summary>
    /// Điều cốt lõi: hai trung tâm tạo liên tiếp **không** nhận cùng mật khẩu.
    ///
    /// Đây là test giết được phiên bản cũ. Nó cũng bắt được lỗi tinh vi hơn: dùng
    /// <c>Random</c> thay vì CSPRNG — <c>Random</c> gieo theo thời gian nên hai lần gọi sát
    /// nhau rất dễ ra cùng chuỗi.
    /// </summary>
    [Fact]
    public async Task Hai_trung_tam_tao_lien_tiep_co_mat_khau_KHAC_nhau()
    {
        using var f = new ApiFactory();
        var c = f.CreateClient();

        var mks = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var res = await c.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
                new { TenTrungTam = $"Trung tâm ngẫu nhiên {i}" });
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            mks.Add(body.GetProperty("matKhau").GetString()!);
        }

        Assert.Equal(mks.Count, mks.Distinct().Count());
    }

    /// <summary>Không bao giờ là "123456" nữa — chốt thẳng giá trị cũ.</summary>
    [Fact]
    public async Task Mat_khau_KHONG_con_la_123456()
    {
        using var f = new ApiFactory();

        var res = await f.CreateClient().PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm không dùng mật khẩu mặc định" });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        var mk = body.GetProperty("matKhau").GetString()!;
        Assert.NotEqual("123456", mk);
        // Đủ dài để không dò được — mục 6 của cùng đợt rà soát nâng tối thiểu lên 12.
        Assert.True(mk.Length >= 12, $"Mật khẩu sinh ra quá ngắn: {mk.Length} ký tự");
    }

    /// <summary>
    /// Chiều ngược, **quan trọng không kém**: mật khẩu trả về phải ĐĂNG NHẬP ĐƯỢC.
    ///
    /// Đây là ca hỏng thật nếu chỉ sửa nửa vời — đổi seeder sinh ngẫu nhiên nhưng để controller
    /// vẫn trả `"123456"` thì hệ thống "an toàn" hơn mà **không ai vào được trung tâm vừa tạo**.
    /// Test này buộc hai chỗ phải nói cùng một giá trị.
    /// </summary>
    [Fact]
    public async Task Mat_khau_tra_ve_PHAI_dang_nhap_duoc()
    {
        using var f = new ApiFactory();
        var c = f.CreateClient();

        var tao = await c.PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm đăng nhập thử" });
        var body = await tao.Content.ReadFromJsonAsync<JsonElement>();

        var dn = await c.PostAsJsonAsync("/api/v1/auth/dang-nhap", new
        {
            MaTrungTam = body.GetProperty("maTrungTam").GetString(),
            Username = body.GetProperty("username").GetString(),
            MatKhau = body.GetProperty("matKhau").GetString(),
        });

        Assert.Equal(HttpStatusCode.OK, dn.StatusCode);
        var phien = await dn.Content.ReadFromJsonAsync<JsonElement>();
        // Vẫn buộc đổi mật khẩu lần đầu — mật khẩu ngẫu nhiên KHÔNG thay thế chốt đó.
        Assert.True(phien.GetProperty("phaiDoiMatKhau").GetBoolean());
    }

    /// <summary>
    /// Mật khẩu **không được lưu ở dạng đọc được** — DB chỉ giữ bản băm.
    ///
    /// Sinh ngẫu nhiên mà lưu thô thì chỉ đổi một lỗ hổng lấy một lỗ hổng khác.
    /// </summary>
    [Fact]
    public async Task DB_chi_luu_HASH_khong_luu_mat_khau_tho()
    {
        using var f = new ApiFactory();

        var res = await f.CreateClient().PostAsJsonAsync("/api/v1/dang-ky-trung-tam",
            new { TenTrungTam = "Trung tâm kiểm hash" });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var maTrungTam = body.GetProperty("maTrungTam").GetString();
        var mkTho = body.GetProperty("matKhau").GetString();

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var hash = db.TaiKhoans
            .IgnoreQueryFilters()
            .Where(t => t.Tenant.MaTrungTam == maTrungTam && t.Username == "admin")
            .Select(t => t.PasswordHash)
            .Single();

        Assert.NotEqual(mkTho, hash);
        Assert.DoesNotContain(mkTho!, hash);
    }
}

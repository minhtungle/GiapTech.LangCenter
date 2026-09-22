using System.Net.Http.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Nhật ký hành vi ĐĂNG NHẬP** (22/09/2026) — yêu cầu chủ sản phẩm *"nhớ log cả hành vi
/// đăng nhập"*.
///
/// Hoá ra đăng nhập **đã** được ghi (mọi lệnh `...Command` đều đi qua `NhatKyBehavior`), nhưng
/// đo trên DB thật thì thấy hai lỗ hổng:
///
/// 1. **0 bản ghi thất bại** trên 61 bản ghi đăng nhập. `GhiNhatKy` bỏ qua khi không có tenant
///    trong context, mà đăng nhập thất bại thì chưa có JWT ⇒ không có tenant ⇒ mất sạch. Đúng
///    những lần cần nhất để phát hiện dò mật khẩu.
/// 2. **Username ghi nhầm**: `ICurrentUser` còn mang danh tính của request TRƯỚC trong cùng
///    kết nối, nên có dòng `username` lệch hẳn với `ThamSo`.
///
/// Chữa bằng `ILenhXacThuc`: lệnh tự khai mã trung tâm + username, behavior tra ra `TenantId`.
/// </summary>
public class NhatKyDangNhapTests
{
    private static async Task<List<NhatKyHeThong>> NhatKyDangNhap(ApiFactory f)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.NhatKyHeThongs
            .IgnoreQueryFilters()
            .Where(n => n.TenLenh == "DangNhapCommand")
            .ToListAsync();
    }

    private static async Task ThuDangNhap(ApiFactory f, string username, string matKhau)
        => await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = f.MaTrungTamA, Username = username, MatKhau = matKhau });

    /// <summary>
    /// Điều cốt lõi: **đăng nhập SAI phải được ghi lại**.
    ///
    /// Không có nó thì nhật ký chỉ kể chuyện thành công — vô dụng đúng lúc cần điều tra.
    /// </summary>
    [Fact]
    public async Task Dang_nhap_SAI_mat_khau_duoc_ghi_nhat_ky()
    {
        using var f = new ApiFactory();

        await ThuDangNhap(f, "manager", "mat-khau-sai-hoan-toan");

        var nhatKy = await NhatKyDangNhap(f);
        var that = Assert.Single(nhatKy.Where(n => !n.ThanhCong));

        Assert.Equal("DANG_NHAP_THAT_BAI", that.MaLoi);
        Assert.Equal("manager", that.Username);
    }

    /// <summary>
    /// Ghi cả khi **username KHÔNG tồn tại** — đây mới là dấu vết của người đang dò.
    ///
    /// Người dò thử hàng loạt tên; nếu chỉ ghi tài khoản có thật thì phần lớn nỗ lực của họ
    /// vô hình trong log.
    /// </summary>
    [Fact]
    public async Task Ghi_ca_khi_username_KHONG_ton_tai()
    {
        using var f = new ApiFactory();

        await ThuDangNhap(f, "ke-dang-do-mat-khau", "thu-xem");

        var nhatKy = await NhatKyDangNhap(f);
        var that = Assert.Single(nhatKy.Where(n => !n.ThanhCong));

        Assert.Equal("ke-dang-do-mat-khau", that.Username);
    }

    /// <summary>
    /// Username phải là **tên người dùng GÕ VÀO**, không phải của request trước.
    ///
    /// Đây là lỗi đã đo được trên DB thật: cùng một `HttpClient` gọi hai lần với hai tài khoản
    /// khác nhau thì bản ghi thứ hai mang username của lần thứ nhất. Log chỉ sai người là log
    /// dẫn người điều tra đi nhầm hướng — tệ hơn không có log.
    /// </summary>
    [Fact]
    public async Task Username_la_nguoi_GO_VAO_khong_phai_request_truoc()
    {
        using var f = new ApiFactory();
        var c = f.CreateClient(); // CÙNG một client cho cả hai lần — tái hiện đúng ca lỗi

        await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = f.MaTrungTamA, Username = "manager", MatKhau = "manager123456" });
        await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = f.MaTrungTamA, Username = "player", MatKhau = "player123456" });

        var nhatKy = await NhatKyDangNhap(f);

        Assert.Contains(nhatKy, n => n.Username == "manager");
        Assert.Contains(nhatKy, n => n.Username == "player");
    }

    /// <summary>
    /// **Không được ghi mật khẩu** vào nhật ký, kể cả ở lần thất bại.
    ///
    /// Ghi lần thất bại là thêm một chỗ mật khẩu có thể rò ra — mà mật khẩu gõ sai thường chỉ
    /// sai một ký tự so với mật khẩu thật. `NhatKyBehavior.LocTruongNhayCam` lo phần này; test
    /// đứng đây để nếu ai đó đổi cách ghi thì đỏ ngay.
    /// </summary>
    [Fact]
    public async Task KHONG_ghi_mat_khau_vao_nhat_ky_ke_ca_khi_that_bai()
    {
        using var f = new ApiFactory();

        await ThuDangNhap(f, "manager", "mat-khau-bi-mat-khong-duoc-ghi");

        var nhatKy = await NhatKyDangNhap(f);

        Assert.All(nhatKy, n =>
            Assert.DoesNotContain("mat-khau-bi-mat-khong-duoc-ghi", n.ThamSo ?? ""));
    }

    /*
      ---------------------------------------------------------------------------------------
      KHÔNG có test cho ĐỊA CHỈ IP — và đây là quyết định, không phải bỏ sót.

      `ThongTinYeuCau` đọc IP từ `HttpContext.Connection.RemoteIpAddress`, mà máy chủ test
      trong bộ nhớ của `WebApplicationFactory` **không đặt** giá trị đó. Test sẽ đỏ vì giới hạn
      của môi trường test chứ không vì sản phẩm sai — và nếu ép cho xanh bằng cách giả lập
      `IThongTinYeuCau` thì nó chỉ kiểm chính cái giả lập đó, không kiểm gì thật.

      ĐÃ KIỂM CHỨNG BẰNG TAY trên API thật ngày 22/09/2026: ba lần đăng nhập (sai mật khẩu /
      username không tồn tại / đúng) đều ghi `dia_chi_ip = ::1`.

      Nếu sau này cần canh tự động, chỗ đúng là test E2E — ở đó có máy chủ HTTP thật.
      ---------------------------------------------------------------------------------------
    */

    /// <summary>
    /// **Tài khoản bị khoá tạm** cũng để lại vết.
    ///
    /// Đây là tín hiệu rõ nhất của một cuộc dò mật khẩu đang diễn ra — mất nó thì cơ chế khoá
    /// chặn được tấn công nhưng không ai biết là đã có tấn công.
    /// </summary>
    [Fact]
    public async Task Bi_khoa_tam_cung_duoc_ghi_lai()
    {
        using var f = new ApiFactory();

        for (var i = 0; i < 11; i++)
            await ThuDangNhap(f, "manager", $"sai-{i}");

        var nhatKy = await NhatKyDangNhap(f);

        Assert.Contains(nhatKy, n => n.MaLoi == "TAI_KHOAN_BI_KHOA_TAM");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Khoá tạm sau nhiều lần đăng nhập sai** (22/09/2026) — mục 4 của đợt rà soát bảo mật.
///
/// Rate limit hiện có giới hạn 10 request/phút **mỗi IP**, nhưng không chặn được kiểu tấn công
/// phổ biến hơn: botnet hoặc proxy pool, mỗi IP thử 10 lần/phút vào **cùng một tài khoản**.
/// Mỗi IP đều dưới hạn mức nên không bao giờ bị chặn, còn tổng số lần thử thì vô hạn. Cộng với
/// chính sách mật khẩu tối thiểu ngắn, bản rà soát xếp đây là **rủi ro thực tế nhất**.
/// </summary>
public class ChongDoMatKhauTests
{
    /// <summary>
    /// Mỗi test một factory RIÊNG.
    ///
    /// Bộ đếm là singleton sống theo vòng đời ứng dụng, nên dùng chung `IClassFixture` thì test
    /// này khoá tài khoản của test kia và kết quả phụ thuộc thứ tự chạy.
    /// </summary>
    private static ApiFactory Moi() => new();

    private static async Task<HttpResponseMessage> ThuDangNhap(
        ApiFactory f, string username, string matKhau)
        => await f.CreateClient().PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = f.MaTrungTamA, Username = username, MatKhau = matKhau });

    private static async Task<string?> MaLoiCua(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("errorCode", out var m) ? m.GetString() : null;
    }

    /// <summary>
    /// Điều cốt lõi: sai đủ ngưỡng thì **mật khẩu ĐÚNG cũng không vào được**.
    ///
    /// Đây là chỗ phân biệt "có khoá thật" với "chỉ đổi thông báo lỗi": nếu chỉ đếm mà vẫn cho
    /// đăng nhập thì kẻ dò chẳng bị cản gì.
    /// </summary>
    [Fact]
    public async Task Sai_qua_nguong_thi_mat_khau_DUNG_cung_bi_chan()
    {
        using var f = Moi();

        for (var i = 0; i < 10; i++)
            await ThuDangNhap(f, "manager", $"sai-{i}");

        var dung = await ThuDangNhap(f, "manager", "manager123456");

        Assert.NotEqual(HttpStatusCode.OK, dung.StatusCode);
        Assert.Equal("TAI_KHOAN_BI_KHOA_TAM", await MaLoiCua(dung));
    }

    /// <summary>
    /// Chiều ngược, **quan trọng không kém**: chưa tới ngưỡng thì mật khẩu đúng vẫn vào được.
    ///
    /// Người dùng thật gõ sai vài lần là chuyện bình thường (Caps Lock, nhớ nhầm mật khẩu cũ).
    /// Khoá quá sớm thì tổng đài nhận việc nhiều hơn là chặn được tấn công.
    /// </summary>
    [Fact]
    public async Task Sai_vai_lan_roi_dung_thi_van_vao_duoc()
    {
        using var f = Moi();

        for (var i = 0; i < 3; i++)
            await ThuDangNhap(f, "manager", $"sai-{i}");

        var dung = await ThuDangNhap(f, "manager", "manager123456");

        Assert.Equal(HttpStatusCode.OK, dung.StatusCode);
    }

    /// <summary>
    /// Đăng nhập ĐÚNG phải **xoá bộ đếm**.
    ///
    /// Thiếu bước đó thì bộ đếm cộng dồn qua nhiều phiên: một người hay quên mật khẩu sẽ bị
    /// khoá sau vài ngày dùng bình thường mà không hiểu vì sao. Test đi đúng kịch bản ấy —
    /// sai 6 lần, đúng 1 lần, rồi lại sai 6 lần: nếu không xoá đếm thì tổng 12 > 10 và lần
    /// đăng nhập đúng cuối cùng sẽ bị chặn.
    /// </summary>
    [Fact]
    public async Task Dang_nhap_dung_XOA_bo_dem()
    {
        using var f = Moi();

        for (var i = 0; i < 6; i++) await ThuDangNhap(f, "manager", $"sai-a-{i}");
        Assert.Equal(HttpStatusCode.OK, (await ThuDangNhap(f, "manager", "manager123456")).StatusCode);

        for (var i = 0; i < 6; i++) await ThuDangNhap(f, "manager", $"sai-b-{i}");

        var cuoi = await ThuDangNhap(f, "manager", "manager123456");
        Assert.Equal(HttpStatusCode.OK, cuoi.StatusCode);
    }

    /// <summary>
    /// Khoá **chỉ tài khoản bị dò**, không lan sang tài khoản khác.
    ///
    /// Sai điều kiện khoá đếm là khoá cả trung tâm — biến biện pháp phòng thủ thành lỗi từ
    /// chối dịch vụ toàn hệ thống.
    /// </summary>
    [Fact]
    public async Task Khoa_KHONG_lan_sang_tai_khoan_khac()
    {
        using var f = Moi();

        for (var i = 0; i < 12; i++)
            await ThuDangNhap(f, "manager", $"sai-{i}");

        // `player` chưa từng sai lần nào → không được dính khoá.
        var khac = await ThuDangNhap(f, "player", "player123456");

        Assert.NotEqual("TAI_KHOAN_BI_KHOA_TAM", await MaLoiCua(khac));
    }

    /// <summary>
    /// Đếm cả khi **username KHÔNG tồn tại**.
    ///
    /// Nếu chỉ đếm tài khoản có thật thì người dò thử thoải mái với username sai — và tệ hơn,
    /// hai nhánh hành xử khác nhau, tạo lại đúng kênh dò mà `BamGia()` vừa bịt ở mục 3.
    /// </summary>
    [Fact]
    public async Task Dem_ca_khi_username_KHONG_ton_tai()
    {
        using var f = Moi();

        for (var i = 0; i < 10; i++)
            await ThuDangNhap(f, "ma-khong-he-co", $"sai-{i}");

        var them = await ThuDangNhap(f, "ma-khong-he-co", "van-sai");

        Assert.Equal("TAI_KHOAN_BI_KHOA_TAM", await MaLoiCua(them));
    }

    /// <summary>
    /// Bộ đếm phải là **SINGLETON**.
    ///
    /// Đăng ký nhầm `Scoped` thì mỗi request có một bộ đếm mới, luôn bằng 0, và cơ chế thành
    /// vô dụng — **không có lỗi nào báo**, mọi test khác vẫn xanh. Kiểm thẳng vào đăng ký DI
    /// vì đó là chỗ duy nhất sai được mà không ai thấy.
    /// </summary>
    [Fact]
    public void Bo_dem_phai_la_SINGLETON()
    {
        using var f = Moi();

        var mo = f.Services.GetRequiredService<IChongDoMatKhau>();
        var mo2 = f.Services.GetRequiredService<IChongDoMatKhau>();

        Assert.Same(mo, mo2);
    }
}

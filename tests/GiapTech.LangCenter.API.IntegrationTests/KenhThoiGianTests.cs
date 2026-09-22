using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace GiapTech.LangCenter.API.IntegrationTests;

/// <summary>
/// **Chống dò qua KÊNH THỜI GIAN** ở đăng nhập và quên mật khẩu (22/09/2026).
///
/// Cả hai endpoint đều cố ý trả **phản hồi giống hệt nhau** dù tài khoản/email có tồn tại hay
/// không. Nhưng mã trạng thái giống nhau là chưa đủ: nếu nhánh "có thật" chạy lâu hơn hẳn thì
/// người dò vẫn phân biệt được bằng đồng hồ, và biện pháp chống dò coi như vô hiệu.
///
/// ## Vì sao test kiểu này dễ "xanh/đỏ thất thường" — và cách tránh
///
/// Đo thời gian trong test là chuyện nhạy cảm: máy CI bận, GC chạy, lần gọi đầu phải JIT. Nên
/// ở đây:
///
/// - **Làm nóng trước** — lần gọi đầu của mỗi nhánh bị bỏ, không tính giờ.
/// - **Lấy TRUNG VỊ**, không lấy trung bình — một lần giật vì GC không kéo lệch kết quả.
/// - **Ngưỡng rộng** (hệ số, không phải mili-giây tuyệt đối). Test này không đo chính xác vài
///   mili-giây; nó bắt loại lỗi **một nhánh bỏ hẳn phép băm** — chênh lệch khi đó là hàng
///   chục lần, không phải vài phần trăm.
///
/// Nói cách khác: thà bỏ lọt một chênh lệch nhỏ còn hơn có một test đỏ ngẫu nhiên rồi bị ai đó
/// tắt đi — lúc ấy thì không còn gì canh cả.
/// </summary>
public class KenhThoiGianTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Số lần đo mỗi nhánh (chưa kể lần làm nóng).</summary>
    private const int SoLanDo = 7;

    /// <summary>
    /// Ngưỡng rộng có chủ ý — xem chú thích đầu lớp. Bỏ hẳn PBKDF2 ở một nhánh cho chênh lệch
    /// hàng chục lần, nên hệ số 5 vẫn bắt được mà không đỏ oan khi máy bận.
    /// </summary>
    private const double HeSoChoPhep = 5.0;

    private static async Task<double> TrungViMiliGiay(Func<Task> hanhDong)
    {
        await hanhDong(); // làm nóng: JIT + kết nối + cache — không tính giờ

        var mau = new List<double>();
        for (var i = 0; i < SoLanDo; i++)
        {
            var dh = Stopwatch.StartNew();
            await hanhDong();
            dh.Stop();
            mau.Add(dh.Elapsed.TotalMilliseconds);
        }

        mau.Sort();
        return mau[mau.Count / 2];
    }

    /// <summary>
    /// Đăng nhập: **username không tồn tại** không được nhanh hơn hẳn **username có thật, sai
    /// mật khẩu**.
    ///
    /// Bản trước: nhánh không tìm thấy `throw` ngay, không chạy PBKDF2 (~100k vòng). Nay nhánh
    /// đó gọi `IPasswordHasher.BamGia()` để tốn thời gian tương đương.
    /// </summary>
    [Fact]
    public async Task Dang_nhap_user_khong_ton_tai_KHONG_nhanh_hon_han_user_co_that()
    {
        var c = factory.CreateClient();

        async Task SaiMatKhau() => await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "sai-mat-khau" });

        async Task KhongCoUser() => await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "khong-he-ton-tai", MatKhau = "sai-mat-khau" });

        var coThat = await TrungViMiliGiay(SaiMatKhau);
        var khongCo = await TrungViMiliGiay(KhongCoUser);

        Assert.True(
            coThat <= khongCo * HeSoChoPhep,
            $"Nhánh 'user có thật' ({coThat:0.0}ms) chậm hơn 'user không tồn tại' ({khongCo:0.0}ms) "
            + $"quá {HeSoChoPhep} lần ⇒ đo thời gian là dò được username. "
            + "Nhánh không tìm thấy có còn gọi IPasswordHasher.BamGia() không?");
    }

    /// <summary>Tương tự cho MÃ TRUNG TÂM sai — cùng một kênh rò, cùng một cách vá.</summary>
    [Fact]
    public async Task Dang_nhap_ma_trung_tam_sai_KHONG_nhanh_hon_han()
    {
        var c = factory.CreateClient();

        async Task SaiMatKhau() => await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = factory.MaTrungTamA, Username = "manager", MatKhau = "sai-mat-khau" });

        async Task SaiMaTrungTam() => await c.PostAsJsonAsync("/api/v1/auth/dang-nhap",
            new { MaTrungTam = "ZZZZZZZ", Username = "manager", MatKhau = "sai-mat-khau" });

        var coThat = await TrungViMiliGiay(SaiMatKhau);
        var khongCo = await TrungViMiliGiay(SaiMaTrungTam);

        Assert.True(
            coThat <= khongCo * HeSoChoPhep,
            $"Nhánh 'trung tâm có thật' ({coThat:0.0}ms) chậm hơn 'mã sai' ({khongCo:0.0}ms) "
            + $"quá {HeSoChoPhep} lần ⇒ dò được mã trung tâm nào tồn tại.");
    }

    /*
      ---------------------------------------------------------------------------------------
      KHÔNG có test đo thời gian cho QUÊN MẬT KHẨU — và đây là quyết định, không phải bỏ sót.

      Kênh rò ở đó thật và còn to hơn ở đăng nhập: nhánh "email có thật" từng gửi SMTP **đồng
      bộ** (hàng trăm ms tới vài giây) trong khi cả hai nhánh đều trả 204. Đã vá bằng cách đưa
      việc gửi email ra khỏi đường trả lời (xem `QuenMatKhauCommand`).

      Nhưng **môi trường test không đo được bản vá đó**: `ApiFactory` thay `IEmailSender` bằng
      `TestEmailSender` chạy trong bộ nhớ, nên nhánh "có thật" vốn đã nhanh sẵn — test sẽ xanh
      kể cả khi gỡ bản vá. Một test xanh-bất-kể-đúng-sai còn tệ hơn không có test: nó làm người
      đọc tin rằng chỗ đó đang được canh.

      Muốn canh thật thì phải dựng SMTP giả có độ trễ, tức thêm hạ tầng test chỉ để phục vụ một
      phép đo — chưa đáng ở thời điểm này. Ghi lại đây để người sau biết chỗ trống này là cố ý.

      Phần "cả hai nhánh cùng trả 204" vẫn có test canh:
      `XacThucNangCaoTests.Quen_mat_khau_khong_tiet_lo_email_ton_tai_hay_khong`.
      ---------------------------------------------------------------------------------------
    */
}

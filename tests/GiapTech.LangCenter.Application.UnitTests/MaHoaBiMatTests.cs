using System.Security.Cryptography;
using GiapTech.LangCenter.Infrastructure.ThongBao;
using GiapTech.LangCenter.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace GiapTech.LangCenter.Application.UnitTests;

/// <summary>
/// ADR-0010 — mã hoá bí mật lưu trong DB.
///
/// Điều được canh: **mật khẩu SMTP không bao giờ nằm dạng bản rõ trong DB**, và hỏng thì hỏng
/// theo hướng đóng (từ chối lưu) chứ không âm thầm lưu thô.
/// </summary>
public class MaHoaBiMatTests
{
    private static MaHoaBiMat Voi(string? khoaBase64)
    {
        var cauHinh = new ConfigurationBuilder()
            .AddInMemoryCollection(khoaBase64 is null
                ? []
                : new Dictionary<string, string?> { [MaHoaBiMat.KhoaCauHinh] = khoaBase64 })
            .Build();

        return new MaHoaBiMat(cauHinh);
    }

    private static string KhoaHopLe() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Ma_hoa_roi_giai_ma_ra_dung_chuoi_ban_dau()
    {
        var m = Voi(KhoaHopLe());
        const string banRo = "mật-khẩu-smtp-có-dấu-tiếng-Việt-123!@#";

        Assert.Equal(banRo, m.GiaiMa(m.MaHoa(banRo)));
    }

    /// <summary>
    /// Chốt chính của ADR-0010: bản mã KHÔNG chứa bản rõ.
    ///
    /// Nghe hiển nhiên, nhưng đây đúng là thứ hỏng khi ai đó "tối ưu" bằng cách chỉ mã hoá
    /// một phần, hoặc lỡ ghép bản rõ vào để gỡ lỗi.
    /// </summary>
    [Fact]
    public void Ban_ma_khong_chua_ban_ro()
    {
        var m = Voi(KhoaHopLe());
        const string banRo = "CHUOI-BI-MAT-KHONG-DUOC-LO";

        Assert.DoesNotContain(banRo, m.MaHoa(banRo));
    }

    /// <summary>
    /// Mã hoá cùng một chuỗi hai lần phải ra hai bản mã KHÁC nhau.
    ///
    /// Nonce phải ngẫu nhiên mỗi lần. Dùng lại nonce với cùng khoá làm AES-GCM mất hoàn toàn
    /// bảo đảm an toàn — đây là cách sai kinh điển nhất của chế độ này, và nó không gây lỗi
    /// nào để ai đó nhận ra.
    /// </summary>
    [Fact]
    public void Hai_lan_ma_hoa_cung_chuoi_ra_hai_ban_ma_khac_nhau()
    {
        var m = Voi(KhoaHopLe());

        Assert.NotEqual(m.MaHoa("cùng một chuỗi"), m.MaHoa("cùng một chuỗi"));
    }

    /// <summary>
    /// Chưa cấu hình khoá ⇒ **NÉM**, không âm thầm trả bản rõ.
    ///
    /// Một lỗi rõ ràng lúc cấu hình tốt hơn một cơ sở dữ liệu đầy mật khẩu trần mà không ai
    /// biết. Đây là hỏng-theo-hướng-đóng.
    /// </summary>
    [Fact]
    public void Chua_co_khoa_thi_tu_choi_ma_hoa()
    {
        var m = Voi(null);

        Assert.False(m.DaCoKhoa);
        Assert.Throws<InvalidOperationException>(() => m.MaHoa("gì đó"));
    }

    /// <summary>Bản mã bị SỬA phải bị phát hiện — đây là lý do chọn GCM thay vì CBC.</summary>
    [Fact]
    public void Ban_ma_bi_sua_thi_giai_ma_tra_null()
    {
        var m = Voi(KhoaHopLe());
        var banMa = m.MaHoa("nội dung gốc");

        var byteMa = Convert.FromBase64String(banMa);
        byteMa[^1] ^= 0xFF;   // lật một byte cuối

        Assert.Null(m.GiaiMa(Convert.ToBase64String(byteMa)));
    }

    /// <summary>
    /// Giải mã bằng khoá KHÁC trả `null`, không ném.
    ///
    /// Ca thật: người vận hành xoay `EMAIL_KHOA_MA_HOA` mà quên rằng dữ liệu cũ mã bằng khoá
    /// cũ. Trả null để nơi gọi xử lý như "chưa cấu hình" — làm sập luồng gửi email vì một
    /// cấu hình hỏng là phản ứng quá đà.
    /// </summary>
    [Fact]
    public void Giai_ma_bang_khoa_khac_tra_null()
    {
        var banMa = Voi(KhoaHopLe()).MaHoa("bí mật");

        Assert.Null(Voi(KhoaHopLe()).GiaiMa(banMa));
    }

    /// <summary>Khoá sai độ dài hoặc không phải base64 ⇒ coi như chưa cấu hình, không nổ lúc khởi động.</summary>
    [Theory]
    [InlineData("khong-phai-base64!!!")]
    [InlineData("YWJj")]                                  // 3 byte — AES cần 16/24/32
    [InlineData("")]
    public void Khoa_khong_hop_le_thi_coi_nhu_chua_cau_hinh(string khoa)
    {
        Assert.False(Voi(khoa).DaCoKhoa);
    }
}

/// <summary>
/// ADR-0010 — chọn kiểu mã hoá TLS theo số cổng SMTP.
///
/// Điều được canh: **cổng 465 phải là SSL ngầm, không phải STARTTLS**. Nhầm hai thứ này thì
/// máy chủ trả `Syntax error, command unrecognized` — một thông báo không hề gợi ý nguyên
/// nhân, và người dùng sẽ đi kiểm mật khẩu (thứ vốn đúng) thay vì kiểm cổng.
/// </summary>
public class BaoMatCongSmtpTests
{
    /// <summary>
    /// 465 = SSL/TLS ngầm — mã hoá ngay từ byte đầu tiên.
    ///
    /// Đây là lý do phải bỏ `System.Net.Mail.SmtpClient`: nó chỉ làm được STARTTLS, nên cổng
    /// 465 KHÔNG BAO GIỜ gửi được. Đã thử thật với Gmail ngày 30/09/2026.
    /// </summary>
    [Fact]
    public void Cong_465_la_ssl_ngam()
        => Assert.Equal(
            MailKit.Security.SecureSocketOptions.SslOnConnect,
            SmtpEmailSender.BaoMatCuaCong(465));

    /// <summary>587 = STARTTLS — mở kết nối thường rồi nâng cấp. Cổng Gmail khuyến nghị.</summary>
    [Fact]
    public void Cong_587_la_starttls()
        => Assert.Equal(
            MailKit.Security.SecureSocketOptions.StartTls,
            SmtpEmailSender.BaoMatCuaCong(587));

    /// <summary>
    /// Cổng khác ⇒ dùng TLS nếu máy chủ có, không thì vẫn gửi.
    ///
    /// Không ép TLS: máy chủ thư nội bộ trong LAN thường không có chứng chỉ, ép thì trung tâm
    /// tự dựng máy chủ riêng sẽ không gửi được gì.
    /// </summary>
    [Theory]
    [InlineData(25)]
    [InlineData(2525)]
    [InlineData(1025)]
    public void Cong_khac_thi_dung_tls_neu_co(int cong)
        => Assert.Equal(
            MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
            SmtpEmailSender.BaoMatCuaCong(cong));
}

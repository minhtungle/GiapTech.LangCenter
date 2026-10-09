namespace GiapTech.LangCenter.Application.UnitTests.Email;

/// <summary>
/// Định dạng mới của trình soạn email phải SỐNG SÓT qua PreMailer (09/10/2026).
///
/// Thanh công cụ thêm gạch chân, căn lề, màu chữ/nền, cỡ chữ và bảng. Mỗi thứ đi qua
/// `SmtpEmailSender.ChuanBiHtml` — nơi bọc khung `&lt;table&gt;` rồi inline CSS. PreMailer có
/// thể gộp hoặc bỏ thuộc tính; test này canh rằng thứ người dùng bấm thật sự tới được thư.
/// </summary>
public class DinhDangEmailTests
{
    /// <summary>HTML y hệt thứ Tiptap sinh ra với bộ nút mới.</summary>
    private const string ThanTiptap = """
        <p style="text-align: center"><span style="color: #b91c1c">Đỏ, căn giữa</span></p>
        <p><u>Gạch chân</u> và <s>gạch ngang</s>.</p>
        <p><span style="font-size: 20px">Chữ lớn</span>
           <span style="background-color: #fef08a">nền vàng</span></p>
        <table border="1" cellpadding="6" cellspacing="0" width="100%">
        <tbody><tr><th><p>Khoá</p></th><th><p>Học phí</p></th></tr>
        <tr><td><p>IELTS</p></td><td><p>5.000.000đ</p></td></tr></tbody></table>
        """;

    [Theory]
    [InlineData("#b91c1c", "màu chữ đỏ")]
    [InlineData("text-align", "căn lề")]
    [InlineData("<u>", "gạch chân")]
    [InlineData("<s>", "gạch ngang")]
    [InlineData("font-size: 20px", "cỡ chữ")]
    [InlineData("#fef08a", "màu nền chữ")]
    [InlineData("<table", "bảng")]
    [InlineData("<th", "ô tiêu đề bảng")]
    public void Dinh_dang_song_sot_qua_PreMailer(string dau, string ten)
    {
        var ra = GoiChuanBiHtml(ThanTiptap);
        Assert.True(ra.Contains(dau, StringComparison.OrdinalIgnoreCase),
            $"Mất {ten} ('{dau}') sau khi inline CSS — người dùng bấm nút mà thư không có.");
    }

    /// <summary>
    /// Gọi `ChuanBiHtml` qua reflection: nó `private static` và nằm trong `SmtpEmailSender`,
    /// vốn cần cả `IAppDbContext` lẫn logger để dựng. Đổi nó thành `public` chỉ để test là
    /// nới phạm vi một thứ không nơi nào khác gọi.
    /// </summary>
    private static string GoiChuanBiHtml(string noiDung)
    {
        var ph = typeof(Infrastructure.ThongBao.SmtpEmailSender).GetMethod("ChuanBiHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)ph.Invoke(null, [noiDung])!;
    }
}

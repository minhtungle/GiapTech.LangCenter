using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.QuanTri.Phoi;
using GiapTech.LangCenter.Infrastructure.Phoi;

namespace GiapTech.LangCenter.Application.UnitTests.Phoi;

/// <summary>
/// Đọc key và điền biến vào .docx.
///
/// Test dựng docx tối giản ngay trong mã thay vì kèm tệp mẫu: tệp nhị phân trong repo thì
/// không ai đọc được diff khi nó đổi, và phôi thật của trung tâm chứa dữ liệu nghiệp vụ.
///
/// Điều quan trọng nhất các test này canh: **Word cắt rời `{{key}}` thành nhiều `w:t`**, nên
/// phép thay chuỗi thẳng trên XML không khớp. Mọi test dưới đây đều dựng XML theo đúng cách
/// Word sinh ra, kể cả `w:proofErr` xen giữa.
/// </summary>
public class PhoiDocxTests
{
    private static Stream Docx(string thanDoan)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = zip.CreateEntry("word/document.xml");
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write($"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                <w:body>{thanDoan}</w:body></w:document>
                """);
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>Một `w:t` chứa trọn key — ca dễ nhất.</summary>
    private static string DoanLien(string noiDung)
        => $"<w:p><w:r><w:t>{noiDung}</w:t></w:r></w:p>";

    /// <summary>
    /// Key bị cắt làm ba run, có `w:proofErr` xen giữa — ĐÚNG như phôi thật của trung tâm.
    /// </summary>
    private static string DoanCatRoi(string key)
        => "<w:p>"
           + "<w:r><w:t>{{</w:t></w:r>"
           + "<w:proofErr w:type=\"spellStart\"/>"
           + $"<w:r><w:t>{key}</w:t></w:r>"
           + "<w:proofErr w:type=\"spellEnd\"/>"
           + "<w:r><w:t>}}</w:t></w:r>"
           + "</w:p>";

    private static string VanBan(Stream docx)
    {
        docx.Position = 0;
        using var zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
        using var r = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        return Regex.Replace(r.ReadToEnd(), "<[^>]+>", "");
    }

    [Fact]
    public void Doc_duoc_key_bi_Word_cat_roi()
    {
        var kq = new PhoiDocx().DocKey(Docx(DoanCatRoi("hoc_vien-ho_ten")));

        // Nếu đọc thẳng trên XML thô thì danh sách này RỖNG — đó là cái bẫy test này canh.
        Assert.Equal(["hoc_vien-ho_ten"], kq);
    }

    [Fact]
    public void Dien_duoc_gia_tri_vao_key_bi_cat_roi()
    {
        var ra = new PhoiDocx().DienGiaTri(
            Docx(DoanCatRoi("hoc_vien-ho_ten")),
            new Dictionary<string, string?> { ["hoc_vien-ho_ten"] = "Nguyễn Văn A" });

        var vb = VanBan(ra);
        Assert.Contains("Nguyễn Văn A", vb);
        Assert.DoesNotContain("{{", vb);
    }

    /// <summary>Key lặp nhiều lần chỉ trả về MỘT dòng, giữ thứ tự xuất hiện.</summary>
    [Fact]
    public void Key_trung_chi_tra_ve_mot_lan_va_giu_thu_tu()
    {
        var kq = new PhoiDocx().DocKey(Docx(
            DoanLien("{{b}}") + DoanLien("{{a}}") + DoanLien("{{b}}")));

        Assert.Equal(["b", "a"], kq);
    }

    /// <summary>
    /// Key KHÔNG có giá trị giữ nguyên `{{...}}`, không bị xoá thành rỗng.
    ///
    /// Chuỗi rỗng làm câu cụt mà không ai biết vì sao; `{{thieu}}` nằm giữa bản in thì người
    /// cầm báo lại ngay. Cùng quy ước với mẫu email.
    /// </summary>
    [Fact]
    public void Key_thieu_gia_tri_thi_giu_nguyen()
    {
        var ra = new PhoiDocx().DienGiaTri(
            Docx(DoanLien("Chào {{co}} và {{thieu}}")),
            new Dictionary<string, string?> { ["co"] = "A" });

        var vb = VanBan(ra);
        Assert.Contains("Chào A và {{thieu}}", vb);
    }

    /// <summary>
    /// Giá trị chứa `&amp;` `&lt;` `&gt;` phải được thoát, nếu không XML hỏng và Word báo lỗi tệp.
    ///
    /// "Công ty A &amp; B" là tên thật, không phải ca hiếm.
    /// </summary>
    [Fact]
    public void Gia_tri_co_ky_tu_XML_khong_lam_hong_tep()
    {
        var ra = new PhoiDocx().DienGiaTri(
            Docx(DoanLien("{{ten}}")),
            new Dictionary<string, string?> { ["ten"] = "Công ty A & B <Z>" });

        ra.Position = 0;
        using var zip = new ZipArchive(ra, ZipArchiveMode.Read, leaveOpen: true);
        using var r = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = r.ReadToEnd();

        // XML phải còn parse được — đây mới là điều quan trọng, không phải chuỗi hiển thị.
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        Assert.Contains("Công ty A & B <Z>", doc.Root!.Value);
    }

    /// <summary>Tệp không phải .docx ⇒ mã lỗi đọc được, không phải exception lạ.</summary>
    [Fact]
    public void Tep_khong_phai_docx_thi_bao_ma_loi()
    {
        var rac = new MemoryStream("day khong phai zip"u8.ToArray());

        var ex = Assert.Throws<AppException>(() => new PhoiDocx().DocKey(rac));
        Assert.Equal("PHOI_KHONG_DOC_DUOC", ex.Ma);
    }

    /// <summary>Zip hợp lệ nhưng thiếu `word/document.xml` ⇒ cũng từ chối.</summary>
    [Fact]
    public void Zip_thieu_document_xml_thi_bao_ma_loi()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            zip.CreateEntry("khac.txt");
        ms.Position = 0;

        var ex = Assert.Throws<AppException>(() => new PhoiDocx().DocKey(ms));
        Assert.Equal("PHOI_KHONG_DOC_DUOC", ex.Ma);
    }

    /// <summary>
    /// Giới hạn 10 MB của phôi CHẶT HƠN mức 20 MB của kho tệp dùng chung.
    ///
    /// Hai hằng dễ trôi khỏi nhau: ai đó hạ `MinioLuuTruTep.KichThuocToiDa` xuống dưới 10 MB
    /// thì giới hạn riêng của phôi thành vô nghĩa (kho từ chối trước), và thông báo lỗi người
    /// dùng thấy sẽ nói "tối đa 20 MB" trong khi thực tế thấp hơn.
    /// </summary>
    [Fact]
    public void Gioi_han_phoi_phai_chat_hon_kho_dung_chung()
    {
        Assert.True(
            ChotPhoi.KichThuocToiDa < Infrastructure.LuuTru.MinioLuuTruTep.KichThuocToiDa,
            "Giới hạn riêng của phôi phải nhỏ hơn giới hạn kho tệp dùng chung, "
            + "nếu không nó không có tác dụng.");
    }

    [Theory]
    [InlineData(0)]                      // tệp rỗng
    [InlineData(10 * 1024 * 1024 + 1)]   // hơn đúng 1 byte
    public void Kich_thuoc_ngoai_khoang_thi_tu_choi(long bytes)
        => Assert.Throws<AppException>(() => ChotPhoi.KiemKichThuoc(bytes));

    [Theory]
    [InlineData(1)]
    [InlineData(10 * 1024 * 1024)]       // đúng mức trần vẫn phải qua
    public void Kich_thuoc_trong_khoang_thi_qua(long bytes)
        => ChotPhoi.KiemKichThuoc(bytes);

    /// <summary>
    /// `DocKey` KHÔNG được làm stream hết dùng — nơi gọi còn phải tải chính tệp đó lên kho.
    ///
    /// Đã xảy ra thật 09/10/2026: handler gọi `DocKey(stream)` rồi `TaiLen(stream)` trên cùng
    /// một stream của HTTP request. `ZipArchive` seek khắp tệp, nên `TaiLen` đọc tiếp từ vị
    /// trí còn sót và lưu một tệp THIẾU ĐẦU — phôi 4.476.206 byte thành 4.457.755 byte. Lỗi
    /// chỉ lộ ra lúc xuất file, với thông báo không liên quan gì tới nguyên nhân:
    /// "Offset to Central Directory cannot be held in an Int64".
    ///
    /// Test này canh đúng điều kiện để sửa đó còn đúng: sau `DocKey`, đọc lại từ vị trí 0
    /// phải ra TRỌN tệp.
    /// </summary>
    [Fact]
    public void Doc_key_xong_van_doc_lai_duoc_tron_tep()
    {
        var docx = Docx(DoanLien("{{a}}"));
        var dai = docx.Length;

        new PhoiDocx().DocKey(docx);

        docx.Position = 0;
        using var sau = new MemoryStream();
        docx.CopyTo(sau);
        Assert.Equal(dai, sau.Length);
    }

    /// <summary>
    /// Điền giá trị KHÔNG được sửa phôi gốc.
    ///
    /// `ZipArchiveMode.Update` ghi đè tại chỗ, mà stream truyền vào đọc từ MinIO — sửa nó là
    /// hỏng phôi gốc của trung tâm, và hỏng im lặng.
    /// </summary>
    [Fact]
    public void Dien_gia_tri_khong_sua_phoi_goc()
    {
        var goc = Docx(DoanLien("{{ten}}"));
        var truoc = ((MemoryStream)goc).ToArray();

        new PhoiDocx().DienGiaTri(goc, new Dictionary<string, string?> { ["ten"] = "X" });

        goc.Position = 0;
        Assert.Equal(truoc, ((MemoryStream)goc).ToArray());
    }
}

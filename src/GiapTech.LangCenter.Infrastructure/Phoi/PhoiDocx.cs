using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;

namespace GiapTech.LangCenter.Infrastructure.Phoi;

/// <summary>
/// Đọc key và điền giá trị vào phôi .docx bằng **thư viện chuẩn .NET** (09/10/2026).
///
/// ## Vì sao không dùng DocumentFormat.OpenXml
///
/// Đã thử trên phôi thật của chủ sản phẩm: đọc key và thay biến chạy đúng chỉ với
/// `System.IO.Compression` + regex, và `textutil` của macOS mở được bản sinh ra. Thêm một
/// NuGet 400 KB cho việc một file 200 dòng làm được là trả giá bảo trì không đổi lấy gì.
///
/// Nếu sau này cần đọc bảng, chèn ảnh hay sinh tài liệu từ đầu thì mới đáng — lúc đó đổi
/// cài đặt sau interface, nơi gọi không biết.
///
/// ## Cái bẫy chính: Word CẮT RỜI chuỗi `{{key}}`
///
/// Trong phôi thật, `{{hoc_vien-so_dien_thoai}}` nằm trong **ba** `&lt;w:r&gt;` riêng biệt vì
/// Word chèn `&lt;w:proofErr&gt;` (đánh dấu chính tả) vào giữa:
///
/// <code>
/// &lt;w:r&gt;&lt;w:t&gt;{{&lt;/w:t&gt;&lt;/w:r&gt;
/// &lt;w:proofErr w:type="spellStart"/&gt;
/// &lt;w:r&gt;&lt;w:t&gt;hoc_vien-so_dien_thoai&lt;/w:t&gt;&lt;/w:r&gt;
/// &lt;w:proofErr w:type="spellEnd"/&gt;
/// &lt;w:r&gt;&lt;w:t&gt;}}&lt;/w:t&gt;&lt;/w:r&gt;
/// </code>
///
/// Nên `xml.Replace("{{key}}", giaTri)` **không khớp gì cả** — đo được: 0 key tìm thấy trên
/// XML thô, 4 key sau khi gỡ thẻ. Cách làm: trong mỗi đoạn `&lt;w:p&gt;`, nối nội dung mọi
/// `&lt;w:t&gt;` lại, thay biến trên chuỗi đã nối, rồi đặt toàn bộ kết quả vào `&lt;w:t&gt;`
/// ĐẦU TIÊN và làm rỗng các ô còn lại.
///
/// Hệ quả phải chấp nhận: định dạng trong một đoạn bị gộp về định dạng của run đầu. Chấp
/// nhận được vì biến thường nằm trong một ô bảng hoặc một dòng có định dạng đồng nhất; giữ
/// định dạng từng run đòi phải ánh xạ ngược vị trí ký tự, phức tạp hơn nhiều so với lợi ích.
/// </summary>
public class PhoiDocx : IPhoiDocx
{
    /// <summary>Phần XML chứa nội dung người dùng thấy. Header/footer xử lý cùng cách.</summary>
    private static readonly string[] PhanCoNoiDung =
        ["word/document.xml", "word/header1.xml", "word/header2.xml", "word/header3.xml",
         "word/footer1.xml", "word/footer2.xml", "word/footer3.xml"];

    private static readonly Regex MauKey = new(@"\{\{([^}]{1,60})\}\}", RegexOptions.Compiled);
    private static readonly Regex MauThe = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MauDoan =
        new(@"<w:p[ >].*?</w:p>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MauO =
        new(@"(<w:t(?:\s[^>]*)?>)(.*?)(</w:t>)", RegexOptions.Compiled | RegexOptions.Singleline);

    public IReadOnlyList<string> DocKey(Stream docx)
    {
        var kq = new List<string>();
        var da = new HashSet<string>(StringComparer.Ordinal);

        foreach (var xml in DocCacPhan(docx))
        {
            // Gỡ thẻ TRƯỚC khi tìm — xem ghi chú ở đầu lớp.
            var thuan = MauThe.Replace(xml, "");
            foreach (Match m in MauKey.Matches(thuan))
            {
                var k = m.Groups[1].Value.Trim();
                if (k.Length > 0 && da.Add(k)) kq.Add(k);
            }
        }

        return kq;
    }

    public Stream DienGiaTri(Stream docx, IReadOnlyDictionary<string, string?> giaTri)
    {
        // Làm việc trên BẢN SAO trong bộ nhớ: `ZipArchiveMode.Update` ghi đè tại chỗ, mà
        // `docx` ở đây là stream đọc từ MinIO — sửa nó là sửa phôi gốc.
        var ms = new MemoryStream();
        docx.Position = 0;
        docx.CopyTo(ms);
        ms.Position = 0;

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            foreach (var ten in PhanCoNoiDung)
            {
                var e = zip.GetEntry(ten);
                if (e is null) continue;

                string xml;
                using (var r = new StreamReader(e.Open(), Encoding.UTF8)) xml = r.ReadToEnd();

                var moi = ThayTrongXml(xml, giaTri);
                if (moi == xml) continue;

                // Xoá rồi tạo lại: ghi đè tại chỗ để lại đuôi của nội dung cũ khi bản mới
                // ngắn hơn, và file hỏng theo cách Word không mở được.
                e.Delete();
                var e2 = zip.CreateEntry(ten);
                using var w = new StreamWriter(e2.Open(), new UTF8Encoding(false));
                w.Write(moi);
            }
        }

        ms.Position = 0;
        return ms;
    }

    private static IEnumerable<string> DocCacPhan(Stream docx)
    {
        docx.Position = 0;
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            // Không phải zip — đổi đuôi một file .pdf thành .docx là ra lỗi này.
            throw new AppException("PHOI_KHONG_DOC_DUOC", "Tệp không phải .docx hợp lệ");
        }

        using (zip)
        {
            if (zip.GetEntry("word/document.xml") is null)
                throw new AppException("PHOI_KHONG_DOC_DUOC", "Thiếu word/document.xml");

            foreach (var ten in PhanCoNoiDung)
            {
                var e = zip.GetEntry(ten);
                if (e is null) continue;
                using var r = new StreamReader(e.Open(), Encoding.UTF8);
                yield return r.ReadToEnd();
            }
        }
    }

    private static string ThayTrongXml(string xml, IReadOnlyDictionary<string, string?> giaTri)
        => MauDoan.Replace(xml, mp =>
        {
            var doan = mp.Value;
            var os = MauO.Matches(doan);
            if (os.Count == 0) return doan;

            var noi = string.Concat(os.Select(m => m.Groups[2].Value));
            if (!noi.Contains("{{", StringComparison.Ordinal)) return doan;

            var sau = MauKey.Replace(noi, mk =>
                giaTri.TryGetValue(mk.Groups[1].Value.Trim(), out var v) && v is not null
                    ? Escape(v)
                    : mk.Value);          // key lạ giữ nguyên — xem ghi chú ở interface
            if (sau == noi) return doan;

            var i = 0;
            return MauO.Replace(doan, mt =>
            {
                var noiDung = i++ == 0 ? sau : "";
                var the = mt.Groups[1].Value;
                // `xml:space="preserve"` để Word không nuốt khoảng trắng đầu/cuối.
                if (noiDung.Length > 0 && !the.Contains("xml:space", StringComparison.Ordinal))
                    the = the.Replace("<w:t", "<w:t xml:space=\"preserve\"", StringComparison.Ordinal);
                return the + noiDung + mt.Groups[3].Value;
            });
        });

    /// <summary>
    /// Thoát ký tự XML. BẮT BUỘC: giá trị người dùng nhập chứa `&amp;` hay `&lt;` sẽ làm hỏng
    /// XML và Word báo file lỗi — "Công ty A &amp; B" là tên thật, không phải ca hiếm.
    /// </summary>
    private static string Escape(string s) => s
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}

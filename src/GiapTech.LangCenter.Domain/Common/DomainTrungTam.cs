namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Chuẩn hoá domain của trung tâm (ADR-0008).
///
/// Đặt ở `Domain` và là NƠI DUY NHẤT: nơi GHI (site chủ gắn domain) và nơi ĐỌC (middleware tra
/// tenant) phải chuẩn hoá giống hệt nhau, nếu không thì domain gắn vào sẽ không tra ra được —
/// và hỏng theo kiểu khó lần, vì cả hai phía nhìn riêng lẻ đều "đúng".
/// </summary>
public static class DomainTrungTam
{
    /// <summary>Độ dài tối đa của tên miền theo RFC 1035.</summary>
    public const int DoDaiToiDa = 253;

    /// <summary>
    /// Về dạng lưu trong DB: chữ thường, bỏ scheme, bỏ đường dẫn, bỏ dấu chấm cuối FQDN.
    /// Rỗng/trắng ⇒ `null` (chưa gắn domain).
    ///
    /// **GIỮ NGUYÊN CỔNG.** Cắt cổng đi thì `localhost:5173` và `localhost:9999` thành một —
    /// ở local hai cổng là hai ứng dụng khác nhau. Cổng là một phần của định danh.
    ///
    /// Dấu chấm cuối (`abc.com.`) hợp lệ về DNS và trình duyệt gửi được, nhưng DB lưu dạng
    /// không chấm — không bỏ thì cùng một domain lại tra trượt.
    /// </summary>
    public static string? ChuanHoa(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return null;

        var s = domain.Trim().ToLowerInvariant();

        if (s.StartsWith("http://", StringComparison.Ordinal)) s = s[7..];
        else if (s.StartsWith("https://", StringComparison.Ordinal)) s = s[8..];

        var gach = s.IndexOf('/');
        if (gach >= 0) s = s[..gach];

        s = s.TrimEnd('.');

        return string.IsNullOrEmpty(s) ? null : s;
    }
}

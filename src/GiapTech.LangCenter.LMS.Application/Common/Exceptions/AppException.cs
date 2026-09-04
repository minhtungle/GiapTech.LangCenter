namespace GiapTech.LangCenter.LMS.Application.Common.Exceptions;

/// <summary>
/// Exception nghiệp vụ mang mã lỗi thay vì message tiếng Việt (quy tắc #3).
/// Message chỉ để đọc log phía server, KHÔNG trả về client.
/// </summary>
public class AppException(string ma, string? chiTietLog = null)
    : Exception(chiTietLog ?? ma)
{
    /// <summary>Giá trị lấy từ danh mục <see cref="Exceptions.MaLoi"/>.</summary>
    public string Ma { get; } = ma;

    /// <summary>Dữ liệu kèm theo để frontend dựng câu thông báo (vd tên trường bị lỗi).</summary>
    public Dictionary<string, object>? DuLieu { get; init; }
}

/// <summary>Không tìm thấy bản ghi — hoặc không tồn tại, hoặc thuộc tenant khác.</summary>
public class KhongTimThayException(string? chiTietLog = null)
    : AppException(Exceptions.MaLoi.KhongTimThay, chiTietLog);

/// <summary>Thiếu quyền thực hiện thao tác (FR-05).</summary>
public class KhongDuQuyenException(string chucNang, string hanhDong)
    : AppException(Exceptions.MaLoi.KhongDuQuyen, $"Thiếu quyền {hanhDong} trên {chucNang}")
{
    public string ChucNang { get; } = chucNang;
    public string HanhDong { get; } = hanhDong;
}

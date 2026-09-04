namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>Ảnh đọc về từ kho lưu trữ.</summary>
public record AnhTaiVe(Stream NoiDung, string LoaiNoiDung);

/// <summary>
/// Kho lưu trữ ảnh (logo, ảnh bìa, mã QR của trung tâm).
///
/// Trừu tượng hoá để `Application` không biết tới MinIO — cài đặt nằm ở `Infrastructure`
/// (quy tắc #10). Đổi sang S3 hay ổ đĩa cục bộ sau này chỉ cần thay lớp cài đặt.
///
/// **Khoá ảnh mang tenant ở đầu** (`{tenantId}/{loai}/{guid}.jpg`): kho lưu trữ không có
/// Global Query Filter như EF Core, nên cách ly phải nằm ngay trong đường dẫn. Không có nó
/// thì đoán được khoá của trung tâm khác là đọc được ảnh của họ.
/// </summary>
public interface ILuuTruAnh
{
    /// <summary>
    /// Tải ảnh lên, trả về **khoá** để lưu vào DB (không phải URL đầy đủ).
    ///
    /// Lưu khoá thay vì URL: đổi domain hay chuyển kho lưu trữ thì mọi hàng trong DB vẫn
    /// dùng được, không phải chạy migration sửa hàng loạt chuỗi.
    /// </summary>
    Task<string> TaiLen(Stream noiDung, string loaiNoiDung, string loai, CancellationToken ct);

    /// <summary>Đọc ảnh theo khoá. Trả null nếu không có.</summary>
    Task<AnhTaiVe?> TaiVe(string khoa, CancellationToken ct);

    /// <summary>
    /// Xoá ảnh. **Không ném** khi khoá không tồn tại: xoá một thứ đã không còn là kết quả
    /// mong muốn, ném lỗi ở đây chỉ chặn luồng thay ảnh khi ảnh cũ đã mất.
    /// </summary>
    Task Xoa(string khoa, CancellationToken ct);
}

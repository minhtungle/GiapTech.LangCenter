namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Mã hoá / giải mã bí mật lưu trong DB (ADR-0010).
///
/// ## Vì sao cần, và vì sao chỉ dùng cho MỘT việc
///
/// Mọi bí mật của hệ thống tới 30/09/2026 thuộc một trong hai loại: **biến môi trường**
/// (`JWT_SECRET`, `MINIO_SECRET_KEY` — không nằm trong DB) hoặc **băm một chiều**
/// (`password_hash`, `token_hash` — lộ ra cũng không dùng ngược được).
///
/// Mật khẩu SMTP không thuộc loại nào: phải khôi phục nguyên văn để đăng nhập máy chủ thư.
/// Đây là **bí mật đầu tiên** hệ thống giữ ở dạng giải mã ngược được.
///
/// **Đừng dùng interface này cho mật khẩu người dùng.** Mật khẩu người dùng phải BĂM, không
/// mã hoá — băm thì kẻ đọc được DB vẫn không đăng nhập được, còn mã hoá thì chỉ cần thêm
/// khoá là mở hết. Hai việc khác nhau về bản chất, dù nghe giống nhau.
/// </summary>
public interface IMaHoaBiMat
{
    /// <summary>
    /// Mã hoá. Ném <see cref="InvalidOperationException"/> nếu chưa cấu hình khoá — **không**
    /// âm thầm trả về bản rõ.
    /// </summary>
    string MaHoa(string banRo);

    /// <summary>
    /// Giải mã. Trả `null` khi bản mã hỏng hoặc bị sửa đổi, thay vì ném — nơi gọi xử lý như
    /// "chưa cấu hình" và rơi về SMTP chung, hợp lý hơn là làm sập luồng gửi email.
    /// </summary>
    string? GiaiMa(string banMa);

    /// <summary>Đã cấu hình khoá chưa — để màn thiết lập báo trước thay vì để người dùng bấm Lưu rồi mới lỗi.</summary>
    bool DaCoKhoa { get; }
}

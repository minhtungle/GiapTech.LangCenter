using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// QUAN_TRI_HE_THONG — tài khoản của **chủ sản phẩm**, đứng TRÊN mọi tenant (ADR-0009).
///
/// ## Vì sao là bảng riêng, không phải cờ trên TAI_KHOAN
///
/// Phương án cờ `la_quan_tri_he_thong` ít bảng hơn nhưng sai về an toàn: từ đó trở đi **mọi**
/// chỗ đọc `TAI_KHOAN` phải nhớ kiểm cờ, quên một chỗ là leo thang đặc quyền. Hệ thống có hàng
/// chục handler đọc bảng đó và không gì bắt người viết mới phải nhớ.
///
/// Bảng riêng thì **không có đường nào** để một tài khoản tenant trở thành chủ hệ thống — hai
/// bảng không liên quan gì nhau. An toàn đến từ cấu trúc, không đến từ việc nhớ kiểm.
///
/// ## Không kế thừa TenantEntity
///
/// Cố ý, và là entity thứ hai sau <see cref="Tenant"/> đứng ngoài Global Query Filter. Lý do
/// đã khai trong `CachLyTenantTests.NgoaiLeKhongLoc` — nơi test bắt phải viết ra.
///
/// ## Token của tài khoản này KHÔNG dùng được cho API nghiệp vụ
///
/// Không phải nhờ một lớp kiểm mới, mà nhờ cấu trúc sẵn có: token chủ **không mang claim
/// `tenant_id`**, mà `TenantMiddleware` trả 401 `TOKEN_THIEU_TENANT` cho mọi token đã xác thực
/// mà thiếu claim đó. Hàng rào có sẵn, không cần dựng thêm.
/// </summary>
public class QuanTriHeThong : BaseEntity
{
    /// <summary>
    /// Tên đăng nhập — duy nhất **toàn hệ thống**, khác hẳn `TAI_KHOAN.username` vốn chỉ duy
    /// nhất trong một tenant. Ở đây không có tenant nào để thu hẹp phạm vi.
    /// </summary>
    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    /// <summary>Tên hiển thị — để nhật ký ghi ai làm, không phải "một ai đó".</summary>
    public string HoTen { get; set; } = null!;

    /// <summary>
    /// Email nhận thông báo. Không dùng để đăng nhập, và **không có luồng quên mật khẩu**:
    /// tài khoản này quá ít và quá mạnh để mở một đường đặt lại mật khẩu qua email.
    /// Mất mật khẩu thì chủ sản phẩm đặt lại thẳng trong DB.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Còn đăng nhập được không. Vô hiệu hoá thay vì xoá — xoá thì nhật ký mất người làm.
    /// </summary>
    public bool HoatDong { get; set; } = true;

    /// <summary>
    /// Buộc đổi mật khẩu ở lần đăng nhập đầu, như tài khoản tenant. Tài khoản chủ đầu tiên
    /// sinh ra từ biến môi trường lúc triển khai nên mật khẩu ban đầu đã đi qua tay người
    /// vận hành và file cấu hình.
    /// </summary>
    public bool PhaiDoiMatKhau { get; set; } = true;

    public DateTimeOffset? LanDangNhapCuoi { get; set; }
}

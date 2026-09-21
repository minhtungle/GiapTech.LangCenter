using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// TAI_KHOAN — thông tin ĐĂNG NHẬP (FR-04).
///
/// Tách khỏi <see cref="NguoiDung"/> (07/09/2026) vì hai vòng đời khác nhau: người rời trung
/// tâm thì tài khoản bị vô hiệu hoá, nhưng tên họ vẫn phải hiện đúng trong bảng điểm danh và
/// sổ học phí của những năm trước.
///
/// Username chỉ duy nhất TRONG PHẠM VI tenant: hai trung tâm đều có thể có tài khoản "admin"
/// — ràng buộc UNIQUE(tenant_id, username), không phải UNIQUE(username).
/// </summary>
public class TaiKhoan : TenantEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Người sở hữu tài khoản. **Nullable** — tài khoản kỹ thuật (tích hợp, seed) không gắn
    /// con người nào, và xoá người dùng để lại tài khoản mồ côi chứ không xoá kèm.
    /// </summary>
    public Guid? NguoiDungId { get; set; }
    public NguoiDung? NguoiDung { get; set; }

    /// <summary>
    /// Bắt buộc đổi mật khẩu trước khi vào hệ thống (FR-01).
    /// Tài khoản admin mặc định khởi tạo với cờ này bật.
    /// </summary>
    public bool PhaiDoiMatKhau { get; set; }

    /// <summary>Còn đăng nhập được không. Không liên quan tới việc người đó còn làm hay không.</summary>
    public TrangThaiNguoiDung TrangThai { get; set; } = TrangThaiNguoiDung.HoatDong;

    /// <summary>
    /// Phiên đăng nhập ĐANG hiệu lực — mỗi tài khoản chỉ một người dùng cùng lúc (20/09/2026).
    ///
    /// Giá trị là `jti` của access token phát ra ở lần đăng nhập gần nhất. Đăng nhập mới ghi đè
    /// giá trị này ⇒ token của phiên cũ mang `jti` khác ⇒ bị chặn ở
    /// <c>PhienDuyNhatMiddleware</c>.
    ///
    /// **Dùng lại `jti` có sẵn thay vì thêm claim mới** (quy tắc #1): thêm claim là thay đổi
    /// phá vỡ tương thích với mọi token đang lưu hành — người đang mở app sẽ bị đá ra ngay khi
    /// triển khai. `jti` đã nằm trong mọi token từ trước, kể cả token phát trước thay đổi này.
    ///
    /// `null` = chưa từng đăng nhập, hoặc đã đăng xuất. Middleware coi `null` là **cho qua**,
    /// không phải chặn: token còn hạn mà cột rỗng chỉ có thể là token phát trước 20/09/2026, và
    /// đá hàng loạt người đang dùng là cái giá không đáng cho một thay đổi không khẩn cấp.
    /// </summary>
    public Guid? PhienHienTai { get; set; }

    public Tenant Tenant { get; set; } = null!;

    /// <summary>Nhóm quyền gán cho TÀI KHOẢN, không phải cho người — quyền là chuyện đăng nhập.</summary>
    public ICollection<NguoiDungQuyen> NguoiDungQuyens { get; set; } = [];
}

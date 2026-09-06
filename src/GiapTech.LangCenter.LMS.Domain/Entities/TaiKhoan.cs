using GiapTech.LangCenter.LMS.Domain.Common;
using GiapTech.LangCenter.LMS.Domain.Enums;

namespace GiapTech.LangCenter.LMS.Domain.Entities;

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

    public Tenant Tenant { get; set; } = null!;

    /// <summary>Nhóm quyền gán cho TÀI KHOẢN, không phải cho người — quyền là chuyện đăng nhập.</summary>
    public ICollection<NguoiDungQuyen> NguoiDungQuyens { get; set; } = [];
}

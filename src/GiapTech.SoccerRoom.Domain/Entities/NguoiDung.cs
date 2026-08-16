using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// NGUOI_DUNG — tài khoản đăng nhập (FR-03).
///
/// Username chỉ duy nhất TRONG PHẠM VI tenant: hai CLB khác nhau đều có thể có tài khoản
/// tên "admin" — ràng buộc UNIQUE(tenant_id, username), không phải UNIQUE(username).
/// </summary>
public class NguoiDung : TenantEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public string? Email { get; set; }
    public string? SoDienThoai { get; set; }
    public string? DiaChi { get; set; }

    /// <summary>
    /// Bắt buộc đổi mật khẩu trước khi vào hệ thống (FR-01).
    /// Tài khoản admin mặc định (admin/123456) khởi tạo với cờ này bật.
    /// </summary>
    public bool PhaiDoiMatKhau { get; set; }

    public TrangThaiNguoiDung TrangThai { get; set; } = TrangThaiNguoiDung.HoatDong;

    /// <summary>Hồ sơ cầu thủ liên kết (0..1) — tài khoản quản lý thuần túy thì để null.</summary>
    public Guid? CauThuId { get; set; }
    public CauThu? CauThu { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<NguoiDungQuyen> NguoiDungQuyens { get; set; } = [];
}

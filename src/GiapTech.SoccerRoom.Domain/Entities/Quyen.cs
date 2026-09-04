using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Enums;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// QUYEN — nhóm quyền do Admin của từng trung tâm tự định nghĩa (FR-05).
/// Có tenant_id vì nhóm quyền của trung tâm A không áp dụng cho trung tâm B.
/// </summary>
public class Quyen : TenantEntity
{
    public string TenQuyen { get; set; } = null!;
    public string? MoTa { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public ICollection<QuyenChucNang> ChucNangs { get; set; } = [];
    public ICollection<NguoiDungQuyen> NguoiDungQuyens { get; set; } = [];
}

/// <summary>
/// QUYEN_CHUC_NANG — chi tiết quyền: một cặp (chức năng, thao tác).
///
/// Mang tenant_id dù đã có quyen_id: truy vấn trực tiếp bảng này khi kiểm tra quyền
/// là đường đi nóng nhất của hệ thống, cần được Global Query Filter bảo vệ trực tiếp
/// thay vì phụ thuộc vào việc người viết nhớ join lên QUYEN.
/// </summary>
public class QuyenChucNang : TenantEntity
{
    public Guid QuyenId { get; set; }
    public Quyen Quyen { get; set; } = null!;

    /// <summary>Giá trị lấy từ <see cref="ChucNang"/> — danh mục đóng.</summary>
    public string TenChucNang { get; set; } = null!;

    public HanhDong HanhDong { get; set; }
}

/// <summary>
/// NGUOIDUNG_QUYEN — bảng trung gian N—N.
/// Một tài khoản nhiều nhóm quyền; quyền hiệu lực = HỢP của mọi nhóm (không có deny ghi đè).
/// </summary>
public class NguoiDungQuyen : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    public Guid QuyenId { get; set; }
    public Quyen Quyen { get; set; } = null!;
}

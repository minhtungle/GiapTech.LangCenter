namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Tenant của request hiện tại, resolve từ JWT claim bởi middleware ở tầng API.
/// DbContext dùng giá trị này cho Global Query Filter — xem docs/backend/multi-tenant.md.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Tenant đang đăng nhập. Null khi chưa xác thực (màn đăng nhập, health check).</summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Chạy một đoạn code trong phạm vi tenant chỉ định — dùng cho background job và
    /// seeder, những nơi không có HTTP context nên không tự có claim tenant.
    /// </summary>
    IDisposable DatPhamVi(Guid tenantId);
}

/// <summary>Người dùng của request hiện tại.</summary>
public interface ICurrentUser
{
    /// <summary>
    /// Id NGƯỜI (`NGUOI_DUNG.id`) — thứ mọi khoá ngoại nghiệp vụ trỏ tới. Null khi tài khoản
    /// không gắn con người nào.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Id TÀI KHOẢN (`TAI_KHOAN.id`) — chỉ dùng cho thao tác trên chính tài khoản (đổi mật
    /// khẩu, tự vô hiệu hoá). Null với token phát hành trước 07/09/2026.
    /// </summary>
    Guid? TaiKhoanId { get; }

    string? Username { get; }
    bool DaXacThuc { get; }
}

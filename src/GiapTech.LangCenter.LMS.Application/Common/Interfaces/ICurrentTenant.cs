namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

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
    Guid? UserId { get; }
    string? Username { get; }
    bool DaXacThuc { get; }
}

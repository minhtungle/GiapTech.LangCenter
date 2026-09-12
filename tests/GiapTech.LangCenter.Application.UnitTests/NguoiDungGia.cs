using GiapTech.LangCenter.Application.Common.Interfaces;

namespace GiapTech.LangCenter.Application.UnitTests;

/// <summary>
/// `ICurrentUser` rỗng cho test dựng `AppDbContext` trực tiếp.
///
/// `AppDbContext` cần nó từ 12/09/2026 để gán bốn cột audit (ADR-0006). Các test ở đây kiểm
/// **schema và query filter**, không kiểm audit — nên người dùng null là đúng, và bốn cột audit
/// sẽ để trống.
/// </summary>
public sealed class NguoiDungGia(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId => userId;
    public Guid? TaiKhoanId => userId;
    public string? Username => userId is null ? null : "test";
    public bool DaXacThuc => userId is not null;
}

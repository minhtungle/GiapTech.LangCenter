using System.Security.Claims;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.Common.Models;

namespace GiapTech.LangCenter.API.Services;

/// <summary>
/// Người dùng của request hiện tại, đọc từ claim.
/// Đặt ở tầng API vì phụ thuộc <see cref="IHttpContextAccessor"/>.
/// </summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    /// <summary>
    /// Id NGƯỜI (`NGUOI_DUNG`) — `null` với token CHỦ HỆ THỐNG (ADR-0009).
    ///
    /// Token chủ đặt id tài khoản chủ vào `NameIdentifier` (nó cần một định danh), nhưng tài
    /// khoản chủ **không phải một hàng trong `NGUOI_DUNG`**. Trả id đó ra đây thì
    /// `GanTenantVaDauVetAudit` gán nó vào `CreatedById` — cột có khoá ngoại tới `NGUOI_DUNG`
    /// — và INSERT chết với "violates foreign key constraint".
    ///
    /// Không phải giả định: đã xảy ra thật khi tạo trung tâm từ site chủ trên PostgreSQL
    /// (23/09/2026). Test in-memory KHÔNG bắt được vì provider đó không ép khoá ngoại.
    ///
    /// `null` là giá trị đúng về nghĩa: lệnh này do hệ thống chạy, không do một người trong
    /// trung tâm nào — cùng ca với seeder và job nền, đúng lý do hai cột audit nullable.
    /// Ai thực hiện được ghi ở nhật ký, nơi lưu `username` dạng bản chụp.
    /// </summary>
    public Guid? UserId =>
        User?.FindFirstValue(ClaimTenant.LoaiDanhTinh) == ClaimTenant.LoaiChuHeThong
            ? null
            : Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;

    /// <summary>
    /// Null với token phát hành trước khi tách bảng (07/09/2026) — chủ ý: thà từ chối thao
    /// tác trên tài khoản còn hơn đoán nhầm sang id người.
    /// </summary>
    public Guid? TaiKhoanId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTenant.TaiKhoanId), out var id) ? id : null;

    public string? Username => User?.FindFirstValue(ClaimTypes.Name);

    public bool DaXacThuc => User?.Identity?.IsAuthenticated == true;
}

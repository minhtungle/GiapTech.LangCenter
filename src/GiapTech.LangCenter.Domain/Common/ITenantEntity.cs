namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Đánh dấu entity thuộc phạm vi một tenant (trung tâm).
///
/// Mọi entity cài interface này được tự động áp Global Query Filter theo tenant đang đăng nhập
/// và tự động gán <see cref="TenantId"/> khi thêm mới — xem docs/03-backend/multi-tenant.md.
///
/// Quy tắc bất di bất dịch #2: entity nghiệp vụ mới BẮT BUỘC cài interface này.
/// Kể cả bảng chi tiết cũng mang tenant_id riêng thay vì dựa vào join lên bảng cha —
/// để truy vấn trực tiếp bảng con vẫn được lọc.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

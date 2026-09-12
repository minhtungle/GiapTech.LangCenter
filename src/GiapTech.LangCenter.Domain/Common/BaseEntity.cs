namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Lớp cơ sở cho mọi entity: khoá chính và **bốn cột audit** (ADR-0006).
///
/// Bốn cột này trả lời câu "ai tạo/sửa bản ghi này, lúc nào" bằng **một truy vấn**, không phải
/// quét `AUDIT_LOG`. Hai cơ chế bổ sung nhau chứ không thay thế:
///
/// | | Cột ở đây | `AUDIT_LOG` (FR-16) |
/// |---|---|---|
/// | "Ai sửa **lần cuối**" | 1 truy vấn, hiện được trên UI | phải quét log |
/// | "**Toàn bộ** lịch sử sửa" | không — chỉ giữ lần cuối | có, kèm trường nào đổi |
///
/// Cả bốn cột do <c>AppDbContext.SaveChangesAsync</c> **tự gán**, không handler nào phải nhớ —
/// cùng cách đã dùng cho `TenantId`. Chỗ nào để handler tự điền là chỗ sẽ có người quên.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Người tạo bản ghi — trỏ <c>PERSON.id</c> (ADR-0006).
    ///
    /// **Nullable** vì ba lẽ, không phải vì lười ràng buộc: hàng có từ trước 12/09/2026 không
    /// truy ngược được; lệnh chạy bởi hệ thống (seeder, job nền) không có người dùng nào; và
    /// người tạo có thể đã bị xoá khỏi hệ thống.
    ///
    /// Gán MỘT LẦN lúc thêm mới. `AppDbContext` không đụng tới nó khi cập nhật — nếu không thì
    /// người sửa sẽ âm thầm trở thành "người tạo" và không có gì báo.
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// Người sửa **lần cuối** — trỏ <c>PERSON.id</c>. null = chưa ai sửa kể từ khi tạo.
    ///
    /// Chỉ giữ lần cuối, không phải lịch sử. Cần toàn bộ lịch sử thì tra `AUDIT_LOG`.
    /// </summary>
    public Guid? UpdatedById { get; set; }
}

/// <summary>Entity nghiệp vụ thuộc một tenant — mặc định cho hầu hết bảng.</summary>
public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}

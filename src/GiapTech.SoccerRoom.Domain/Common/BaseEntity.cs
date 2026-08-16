namespace GiapTech.SoccerRoom.Domain.Common;

/// <summary>Lớp cơ sở cho mọi entity: khóa chính và dấu vết thời gian.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset NgayTao { get; set; }
    public DateTimeOffset? NgayCapNhat { get; set; }
}

/// <summary>Entity nghiệp vụ thuộc một tenant — mặc định cho hầu hết bảng.</summary>
public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}

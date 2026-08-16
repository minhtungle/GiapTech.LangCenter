using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// REFRESH_TOKEN — token làm mới phiên đăng nhập.
///
/// Lưu **hash** chứ không lưu token thô: người đọc được DB (backup rò rỉ, SQL injection,
/// lập trình viên xem bảng) sẽ không mạo danh được ai. Cùng lý do với password_hash.
/// </summary>
public class RefreshToken : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    /// <summary>SHA-256 của token thô. Token thô chỉ tồn tại trong response trả về client.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset HetHan { get; set; }

    /// <summary>Thời điểm bị thu hồi (đăng xuất, hoặc đã dùng để xoay vòng). Null = còn hiệu lực.</summary>
    public DateTimeOffset? ThuHoiLuc { get; set; }

    public bool ConHieuLuc(DateTimeOffset bayGio) => ThuHoiLuc is null && bayGio < HetHan;
}

/// <summary>
/// TOKEN_DAT_LAI_MK — token đặt lại mật khẩu qua email (FR-02).
/// Cũng lưu hash, dùng một lần, có thời hạn ngắn.
/// </summary>
public class TokenDatLaiMatKhau : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    public string TokenHash { get; set; } = null!;
    public DateTimeOffset HetHan { get; set; }
    public DateTimeOffset? DaDungLuc { get; set; }

    public bool ConHieuLuc(DateTimeOffset bayGio) => DaDungLuc is null && bayGio < HetHan;
}

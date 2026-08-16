using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// TENANT — một CLB độc lập. Gốc của mọi dữ liệu nghiệp vụ.
///
/// Không kế thừa <see cref="TenantEntity"/>: bảng này ĐỊNH NGHĨA tenant chứ không thuộc về
/// tenant nào, nên không bị Global Query Filter lọc.
///
/// Thiết lập chung của CLB (FR-06) nằm luôn ở đây; tenant mới chưa cấu hình thì dùng mặc định.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// ID đội người dùng gõ khi đăng nhập (FR-01): mã 7 ký tự SINH TỰ ĐỘNG, duy nhất toàn
    /// hệ thống. Không để người dùng tự đặt vì tên dạng "FC ..." rất dễ trùng.
    /// Luôn lưu dạng hoa — xem <see cref="Common.MaDoi"/>.
    /// </summary>
    public string MaDoi { get; set; } = null!;

    public string TenDoi { get; set; } = null!;
    public string? TenVietTat { get; set; }
    public DateOnly? NgayThanhLap { get; set; }
    public string? LogoUrl { get; set; }
    public string? AnhBiaUrl { get; set; }
    public string? MoTa { get; set; }

    public ICollection<NguoiDung> NguoiDungs { get; set; } = [];
    public ICollection<CauThu> CauThus { get; set; } = [];
}

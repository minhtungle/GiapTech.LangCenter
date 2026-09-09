using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Enums;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// LIEN_KET_MXH — liên kết mạng xã hội của một người, **nhiều dòng** (FR-23).
///
/// Bảng riêng thay vì vài cột trên `NGUOI_DUNG`: thêm một mạng là thêm một cột + một migration,
/// và ai chỉ dùng Zalo thì mọi cột khác NULL. Cũng không dùng `jsonb` — mảng không mang
/// `tenant_id` nên nằm ngoài Global Query Filter (quy tắc #2), và không index được.
/// </summary>
public class LienKetMxh : TenantEntity
{
    public Guid NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; } = null!;

    /// <summary>
    /// Loại mạng. Enum chứ không chuỗi tự do: "Facebook" và "facebook" là hai loại khác nhau,
    /// và UI cần biết hiện icon nào.
    /// </summary>
    public LoaiMxh Loai { get; set; }

    /// <summary>
    /// Đường dẫn hoặc số/định danh (Zalo thường là số điện thoại, không phải URL) — nên
    /// **không** validate là URL, chỉ giới hạn độ dài.
    /// </summary>
    public string DuongDan { get; set; } = null!;

    /// <summary>Ghi chú ngắn, ví dụ "tài khoản công việc" khi một người có hai Facebook.</summary>
    public string? GhiChu { get; set; }
}

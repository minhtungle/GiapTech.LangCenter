using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// MAU_DOI_HINH — đội hình mẫu dùng lại cho nhiều trận.
///
/// CLB phong trào đá đi đá lại gần như cùng một đội hình. Không có mẫu thì mỗi trận phải
/// chọn lại từng người rồi kéo lại từng áo — việc lặp vô ích.
///
/// Áp mẫu vào trận sẽ ghi sang <see cref="DoiHinhTranDau"/> và <see cref="SoDoChienThuat"/>;
/// từ đó hai bên độc lập, sửa sơ đồ của một trận không đụng tới mẫu và ngược lại. Ràng buộc
/// chúng lại sẽ khiến sửa mẫu làm đổi lịch sử các trận đã đá (quy tắc #1).
/// </summary>
public class MauDoiHinh : TenantEntity
{
    public string Ten { get; set; } = null!;

    /// <summary>Loại sân: 5, 7, 9 hoặc 11 người. Quyết định tỷ lệ sân và bộ sơ đồ dựng sẵn.</summary>
    public int LoaiSan { get; set; } = 11;

    public string? GhiChu { get; set; }

    /// <summary>
    /// Toàn bộ nội dung mẫu dạng JSON: danh sách cầu thủ, vị trí hai hiệp, quân đối thủ.
    ///
    /// Lưu JSON vì hình dạng do UI quyết định và không truy vấn theo từng cầu thủ trong mẫu —
    /// cùng lý do với <see cref="SoDoChienThuat.SoDoJson"/>. Thêm trường mới không cần migration.
    /// </summary>
    public string NoiDungJson { get; set; } = "{}";

    public Tenant Tenant { get; set; } = null!;
}

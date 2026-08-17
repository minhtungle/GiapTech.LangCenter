using GiapTech.SoccerRoom.Domain.Common;

namespace GiapTech.SoccerRoom.Domain.Entities;

/// <summary>
/// VIDEO_TRAN — link video của một trận (FR-10 tab c).
///
/// Hệ thống **chỉ lưu link** (Youtube/Drive), không lưu file video — xem ADR-0004: một trận
/// bóng quay 90 phút là vài GB, tự host sẽ đốt hết dung lượng VPS trong một mùa giải.
///
/// Thư viện video (màn tổng hợp) đọc **thẳng từ bảng này**, không giữ bản sao riêng. Hai bản
/// sao sẽ lệch nhau ngay lần đầu ai đó sửa một bên mà quên bên kia.
/// </summary>
public class VideoTran : TenantEntity
{
    public Guid TranDauId { get; set; }
    public TranDau TranDau { get; set; } = null!;

    /// <summary>Tên hiển thị do người dùng đặt: "Hiệp 1", "Bàn thắng phút 67"…</summary>
    public string Ten { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? MoTa { get; set; }

    /// <summary>
    /// Thứ tự hiển thị trong trận. Không dùng <c>NgayTao</c> để sắp xếp: người dùng thêm
    /// video hiệp 2 trước hiệp 1 là chuyện thường, thứ tự thêm không phải thứ tự muốn xem.
    /// </summary>
    public int ThuTu { get; set; }

    public Tenant Tenant { get; set; } = null!;
}

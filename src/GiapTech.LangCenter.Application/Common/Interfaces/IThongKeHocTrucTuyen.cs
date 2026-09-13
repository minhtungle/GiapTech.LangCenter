using GiapTech.LangCenter.Application.Common.Models;

namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Số liệu học tập trực tuyến cho màn thống kê (FR-28).
///
/// **Vì sao là interface chứ không đọc thẳng `db.KhoaOnlines` trong handler CRM:** đó là gọi
/// chéo hệ thống con — CRM đọc bảng của LMS. `RanhGioiHeThongConTests` canh đúng việc này, và
/// cầu nối chéo phải khai lý do chứ không lặng lẽ thêm.
///
/// Ở đây interface là đường đúng hơn cầu nối: CRM chỉ cần **con số**, không cần biết LMS lưu
/// nó ở mấy bảng. Đổi cách LMS lưu tiến độ thì CRM không phải sửa gì.
///
/// **Không có trường tiền nào** — elearning không nối sang đơn hàng (chốt 13/09/2026).
/// </summary>
public interface IThongKeHocTrucTuyen
{
    Task<SoLieuElearningDto> Lay(CancellationToken ct);
}

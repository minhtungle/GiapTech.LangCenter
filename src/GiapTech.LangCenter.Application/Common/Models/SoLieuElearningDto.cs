namespace GiapTech.LangCenter.Application.Common.Models;

/// <summary>
/// Số liệu học tập trực tuyến — **không có trường tiền nào**, có chủ ý.
///
/// Đặt ở `Common` chứ không ở `Crm/` hay `DaoTao/`: đây là **hợp đồng giữa hai hệ thống** —
/// LMS dựng số, CRM hiển thị. Để ở một bên thì bên kia phải `using` sang, tức gọi chéo hệ
/// thống con (`RanhGioiHeThongConTests` bắt được ngay 14/09/2026).
/// </summary>
public record SoLieuElearningDto(
    int SoKhoa,
    int SoNguoiHoc,
    int SoLuotGhiDanh,
    /// <summary>Số bài đã được đánh dấu hoàn thành, trên tổng số lượt (khoá × bài).</summary>
    int SoBaiHoanThanh,
    int TongLuotCanHoc);

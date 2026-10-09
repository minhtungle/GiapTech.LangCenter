using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// LICH_SU_EMAIL — mỗi lần hệ thống gửi một email cho một khách hàng (08/10/2026).
///
/// ## Vì sao cần bảng riêng, không dùng NHAT_KY_HE_THONG
///
/// Nhật ký hệ thống ghi **thao tác** ("ai gọi lệnh gì, thành công không") và bị dọn theo chính
/// sách lưu giữ. Lịch sử email là **dữ liệu nghiệp vụ của khách hàng**: người bán mở hồ sơ
/// khách ra để biết đã nói gì với họ, và câu đó phải còn nguyên sau khi nhật ký đã dọn.
///
/// Hai thứ cũng khác nhau ở chỗ tra cứu: nhật ký tra theo người thao tác, lịch sử email tra
/// theo **khách hàng** — nên có index riêng theo `khach_hang_id`.
///
/// ## Vì sao lưu cả nội dung đã gửi
///
/// Mẫu email sửa được, và người gửi cũng sửa nội dung trước khi bấm gửi. Lưu con trỏ tới mẫu
/// thì mở lại sau ba tháng sẽ thấy nội dung HIỆN TẠI của mẫu, không phải thứ khách đã nhận —
/// sai lệch im lặng, và đúng thứ gây tranh cãi khi khách hỏi lại.
/// </summary>
public class LichSuEmail : TenantEntity
{
    /// <summary>Khách nhận thư. `Restrict` khi xoá khách: xoá khách mà mất dấu vết đã liên hệ
    /// là mất bằng chứng, không phải dọn rác.</summary>
    public Guid KhachHangId { get; set; }
    public KhachHang KhachHang { get; set; } = null!;

    /// <summary>
    /// Địa chỉ THẬT đã gửi tới, chép lại tại thời điểm gửi.
    ///
    /// Không đọc qua `KhachHang.Email` khi hiển thị: khách đổi email thì lịch sử phải vẫn nói
    /// đúng thư đó đã đi tới đâu.
    /// </summary>
    public string DenEmail { get; set; } = null!;

    public string TieuDe { get; set; } = null!;

    /// <summary>Thân thư dạng HTML ngữ nghĩa, đúng thứ đã gửi — xem ghi chú ở đầu lớp.</summary>
    public string NoiDungHtml { get; set; } = null!;

    /// <summary>
    /// Gửi được không. Lưu cả lần hỏng: người bán cần biết thư KHÔNG tới nơi, nếu không họ
    /// ngồi chờ phản hồi cho một email chưa bao giờ rời máy chủ.
    /// </summary>
    public bool ThanhCong { get; set; }

    /// <summary>Mã lỗi khi hỏng (`SMTP_KHONG_KET_NOI_DUOC`…), null khi thành công.</summary>
    public string? MaLoi { get; set; }

    /// <summary>
    /// Người bấm gửi. Nullable vì sau này job nền có thể gửi tự động (nhắc nợ, nhắc lịch) —
    /// lúc đó không có người nào đứng sau.
    /// </summary>
    public Guid? NguoiGuiId { get; set; }
    public NguoiDung? NguoiGui { get; set; }
}

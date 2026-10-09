using GiapTech.LangCenter.Domain.Common;

namespace GiapTech.LangCenter.Domain.Entities;

/// <summary>
/// Loại mẫu email (FR-31).
///
/// **Cố định trong mã, người dùng không thêm được loại mới.** Mỗi loại gắn với một chỗ gọi
/// trong mã và một bộ biến riêng — thêm loại mà không có chỗ gọi thì đó là mẫu chết, soạn
/// xong không bao giờ gửi.
/// </summary>
public enum LoaiMauEmail
{
    /// <summary>
    /// Cấp tài khoản cho người dùng mới. Gửi ngay khi người tạo tích chọn ở form Người dùng
    /// (nối thật 09/10/2026 — trước đó mẫu này có sẵn nhưng chưa nơi nào gọi).
    ///
    /// Không riêng học viên: giáo viên, trợ giảng và nhân viên được cấp tài khoản cũng nhận
    /// thư này. Tên enum giữ nguyên vì nó đã nằm trong DB của các trung tâm đang chạy.
    /// </summary>
    ChaoMungHocVien = 0,

    /// <summary>Khách điền form trên trang đích (FR-30). Gửi ngay, đã có chỗ gọi.</summary>
    TraLoiLienHe = 1,

    /// <summary>
    /// Nhắc nợ học phí. **Chưa tự gửi** — cần job nền chạy theo lịch (nợ N10).
    ///
    /// Vẫn có trong danh mục ngay từ đầu để trung tâm soạn sẵn một lần; khi job nền xong thì
    /// nó chạy mà không phải sửa gì. Thêm loại sau nghĩa là bắt họ quay lại soạn thêm.
    /// </summary>
    NhacNoHocPhi = 2,

    /// <summary>Nhắc lịch học. **Chưa tự gửi** — cần job nền, như trên.</summary>
    NhacLichHoc = 3
}

/// <summary>
/// MAU_EMAIL — nội dung email soạn sẵn của một trung tâm (FR-31).
///
/// Mỗi (tenant, loại) tối đa một mẫu — UNIQUE ở tầng DB (quy tắc #8). Chưa soạn thì hệ thống
/// dùng mẫu mặc định viết trong mã, không để trống: trống nghĩa là email không gửi được cho
/// tới khi có người vào soạn, mà người đó không biết mình cần làm việc đó.
/// </summary>
public class MauEmail : TenantEntity
{
    public LoaiMauEmail Loai { get; set; }

    /// <summary>
    /// Dòng tiêu đề. Dùng được biến như thân mail — "Nhắc học phí lớp {{tenLop}}" hữu ích
    /// hơn nhiều so với một tiêu đề cố định.
    /// </summary>
    public string TieuDe { get; set; } = null!;

    /// <summary>
    /// Thân email dạng **HTML ngữ nghĩa** do Tiptap sinh (`&lt;p&gt;`, `&lt;strong&gt;`…).
    ///
    /// KHÔNG lưu HTML đã bọc khung và inline CSS: lúc gửi backend mới bọc vào khung
    /// `&lt;table&gt;` rồi inline bằng PreMailer.Net. Lưu dạng ngữ nghĩa thì đổi khung
    /// template sau này không phải sửa lại dữ liệu cũ (ADR-0010).
    /// </summary>
    public string NoiDungHtml { get; set; } = null!;

    /// <summary>
    /// Bật/tắt. Tắt thì dùng mẫu mặc định trong mã — không phải "không gửi email".
    ///
    /// Tách khỏi việc xoá: trung tâm muốn quay về mẫu gốc mà vẫn giữ bản đã soạn để so sánh.
    /// </summary>
    public bool DangDung { get; set; } = true;
}

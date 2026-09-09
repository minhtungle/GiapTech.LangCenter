namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Ba hệ thống con mà người dùng chuyển qua lại: HRM · CRM · LMS.
///
/// **Đây là cách NHÓM chức năng phân quyền, không phải ba ứng dụng riêng.** Vẫn một API, một
/// database, một lần đăng nhập. Mục đích: người dùng có quyền ở cả ba hệ thống không phải nhìn
/// một sidebar dài gộp mọi thứ — họ chọn hệ thống đang làm việc và chỉ thấy phần của nó.
///
/// Vì sao không tách thành ba service: mỗi hệ thống hiện chỉ có 1–2 module, và dữ liệu dùng
/// chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ phải đồng bộ giữa ba database — đúng cái bẫy
/// hai nguồn sự thật đã tránh khi tách người dùng khỏi tài khoản (07/09/2026).
///
/// **Không lưu vào DB.** Hệ thống của một chức năng là thuộc tính của MÃ NGUỒN (chức năng
/// `LopHoc` thuộc LMS là điều bất biến), không phải dữ liệu người dùng sửa được. Lưu xuống DB
/// thì mỗi tenant có thể có một cách nhóm khác nhau, và sidebar hết xác định.
/// </summary>
public enum HeThong
{
    /// <summary>Quản trị nhân sự — nhân viên kinh doanh, giáo viên (góc nhìn nhân sự).</summary>
    Hrm = 1,

    /// <summary>Quan hệ khách hàng — doanh thu.</summary>
    Crm = 2,

    /// <summary>Quản lý đào tạo — lớp học, buổi học, điểm danh, học liệu, học phí.</summary>
    Lms = 3
}

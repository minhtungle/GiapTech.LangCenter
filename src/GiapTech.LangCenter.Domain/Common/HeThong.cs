namespace GiapTech.LangCenter.Domain.Common;

/// <summary>
/// Các hệ thống con mà người dùng chuyển qua lại: HRM · CRM · LMS · LDP.
///
/// **Đây là cách NHÓM chức năng phân quyền, không phải bốn ứng dụng riêng.** Vẫn một API, một
/// database, một lần đăng nhập. Mục đích: người dùng có quyền ở nhiều hệ thống không phải nhìn
/// một sidebar dài gộp mọi thứ — họ chọn hệ thống đang làm việc và chỉ thấy phần của nó.
///
/// Vì sao không tách thành nhiều service: mỗi hệ thống hiện chỉ có 1–2 module, và dữ liệu dùng
/// chung (`NGUOI_DUNG`, `TENANT`, nhóm quyền) sẽ phải đồng bộ giữa nhiều database — đúng cái bẫy
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
    Lms = 3,

    /// <summary>
    /// Trang đích công khai (FR-30) — nội dung hiển thị cho khách CHƯA đăng nhập.
    ///
    /// Khác ba hệ thống kia ở một điểm quan trọng: dữ liệu của nó **cố ý ra ngoài Internet**.
    /// Nên nội dung landing nhập RIÊNG, không đọc từ `KHOA_HOC`/`HO_SO_GIAO_VIEN` — nối thẳng
    /// dữ liệu nghiệp vụ ra trang công khai là mở một đường rò rỉ vĩnh viễn, mỗi cột thêm vào
    /// sau này đều có nguy cơ xuất hiện trên Internet mà không ai rà.
    /// </summary>
    Ldp = 4
}

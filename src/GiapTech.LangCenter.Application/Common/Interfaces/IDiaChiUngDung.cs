namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Địa chỉ gốc của giao diện người dùng, để dựng link đặt vào email (09/10/2026).
///
/// ## Vì sao là interface chứ không đọc thẳng `IConfiguration`
///
/// `Application` **không tham chiếu** `Microsoft.Extensions.Configuration` — đó là chi tiết
/// hạ tầng, và quy tắc #10 giữ tầng này sạch khỏi những thứ như vậy. Giá trị đến từ biến môi
/// trường, nên nơi đọc nó là `Infrastructure`.
///
/// ## Vì sao cần nó
///
/// Email đi ra ngoài hệ thống: người nhận không có sẵn URL nào để bấm, và không biết gõ đâu
/// vào trình duyệt. Trước 09/10/2026 không email nào của hệ thống có link — thư quên mật khẩu
/// chỉ gửi mã, thư chào mừng chỉ gửi tên đăng nhập.
/// </summary>
public interface IDiaChiUngDung
{
    /// <summary>
    /// Địa chỉ gốc (không có dấu `/` cuối), ví dụ `https://lms.trungtam.edu.vn`.
    ///
    /// `null` khi chưa cấu hình `APP_BASE_URL`. Nơi gọi phải xử lý: dựng link rỗng rồi nhét
    /// vào email là gửi cho người dùng một đường dẫn hỏng.
    /// </summary>
    string? Goc { get; }
}

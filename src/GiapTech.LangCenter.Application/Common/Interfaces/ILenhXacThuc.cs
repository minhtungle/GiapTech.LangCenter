namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Lệnh XÁC THỰC tự khai danh tính cho nhật ký (22/09/2026).
///
/// ## Vì sao cần
///
/// `NhatKyBehavior` lấy tenant và username từ JWT của người gọi. Lệnh đăng nhập thì **chưa có
/// JWT**, nên nếu không có đường khai riêng:
///
/// - Đăng nhập **thất bại** không được ghi chút nào — `GhiNhatKy` thấy không có tenant thì bỏ
///   qua. Mà đó chính là thứ cần nhất để phát hiện dò mật khẩu: đo trên DB thật ngày
///   22/09/2026 có **61 bản ghi đăng nhập, 0 thất bại**.
/// - Đăng nhập **thành công** ghi nhầm username của request trước trong cùng kết nối.
///
/// ## Vì sao là interface trên COMMAND, không phải tham số của behavior
///
/// Behavior là generic (`TRequest`), nó không biết lệnh nào mang mã trung tâm ở trường nào.
/// Để lệnh tự khai thì thêm lệnh xác thực mới chỉ cần cài interface này — không phải sửa
/// behavior, và không thể quên theo kiểu "sửa chỗ này quên chỗ kia".
///
/// Mã trung tâm là **chuỗi người dùng gõ**, chưa chắc có thật; phần tra ra `TenantId` do
/// behavior lo (xem `NhatKyBehavior`).
/// </summary>
public interface ILenhXacThuc
{
    /// <summary>Mã trung tâm người dùng gõ vào. Có thể sai, có thể không tồn tại.</summary>
    string MaTrungTamDeGhiNhatKy { get; }

    /// <summary>Tên đăng nhập người dùng gõ vào. Có thể là tài khoản không tồn tại.</summary>
    string UsernameDeGhiNhatKy { get; }
}

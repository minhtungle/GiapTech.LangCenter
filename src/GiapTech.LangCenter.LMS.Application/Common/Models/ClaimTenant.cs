namespace GiapTech.LangCenter.LMS.Application.Common.Models;

/// <summary>
/// Tên claim dùng trong JWT. Đặt hằng số ở một chỗ để nơi phát hành (đăng nhập) và nơi đọc
/// (middleware) không bao giờ lệch nhau vì gõ sai chuỗi.
/// </summary>
public static class ClaimTenant
{
    public const string TenantId = "tenant_id";
    public const string MaTrungTam = "ma_trung_tam";

    /// <summary>
    /// Tên trung tâm — đưa vào token để sidebar hiển thị được ngay khi tải trang, không phải chờ
    /// một lượt gọi API chỉ để lấy một chuỗi.
    /// </summary>
    public const string TenTrungTam = "ten_trung_tam";

    /// <summary>
    /// Id TÀI KHOẢN. Tách khỏi `NameIdentifier` (giữ nguyên là id NGƯỜI) từ 07/09/2026 khi
    /// tách hai bảng.
    ///
    /// Vì sao NameIdentifier giữ id người chứ không đổi sang id tài khoản: 12 khoá ngoại
    /// nghiệp vụ (`HocVienId`, `NguoiTaoId`, `NguoiChamId`…) trỏ tới NGUOI_DUNG, và mọi handler
    /// đang so `currentUser.UserId` với chúng. Đổi nghĩa claim cũ sẽ làm mọi so sánh đó sai
    /// **âm thầm** — không lỗi biên dịch, chỉ trả về rỗng.
    ///
    /// Token phát hành TRƯỚC thay đổi này không có claim mới; <see cref="ICurrentUser"/> trả
    /// null và ba chỗ dùng nó (đổi mật khẩu, tự vô hiệu hoá, tự xoá) từ chối bằng lỗi rõ ràng
    /// thay vì so sai. Phiên cũ hết hạn trong tối đa 60 phút.
    /// </summary>
    public const string TaiKhoanId = "tai_khoan_id";
}

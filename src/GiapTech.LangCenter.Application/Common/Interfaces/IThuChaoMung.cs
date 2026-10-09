namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>
/// Gửi thư báo thông tin đăng nhập + hồ sơ cho người vừa được cấp tài khoản
/// (09/10/2026, mẫu `ChaoMungHocVien` của FR-31).
///
/// ## Vì sao là một service riêng, không viết thẳng trong handler
///
/// Có **hai** đường tạo tài khoản, và chúng sẽ còn thêm:
/// `TaoNguoiDungCommand` (tạo người kèm tài khoản — đường chính của màn Người dùng) và
/// `TaoTaiKhoanCommand` (cấp tài khoản cho người đã có hồ sơ). Viết hai lần thì hai bản sẽ
/// trôi khỏi nhau: sửa nội dung thư ở một chỗ, chỗ kia vẫn gửi bản cũ mà không ai biết.
///
/// ## Hợp đồng: KHÔNG BAO GIỜ ném lỗi
///
/// Nơi gọi đã `SaveChanges` xong — người và tài khoản ĐÃ tồn tại. Ném lỗi ở đây trả 500 cho
/// một lệnh đã thành công, và người tạo sẽ bấm Lưu lần nữa, lần này nhận
/// `USERNAME_DA_TON_TAI` và tưởng mình làm sai. Lỗi SMTP đi vào log, không đi ra client.
/// </summary>
public interface IThuChaoMung
{
    /// <summary>
    /// Gửi thư cho chủ hồ sơ <paramref name="nguoiDungId"/>.
    ///
    /// Không gửi gì (và không báo lỗi) khi hồ sơ không có email — đó là ca thường gặp, không
    /// phải sự cố.
    /// </summary>
    /// <param name="matKhauTam">
    /// Mật khẩu dạng rõ. Hệ thống chỉ lưu hash, nên đây là **lần duy nhất** đọc được nó —
    /// phải truyền từ nơi gọi, không tra lại từ DB được.
    /// </param>
    Task GuiAsync(Guid nguoiDungId, string username, string matKhauTam, CancellationToken ct = default);
}

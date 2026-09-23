namespace GiapTech.LangCenter.Application.Common.Interfaces;

/// <summary>Băm và kiểm tra mật khẩu.</summary>
public interface IPasswordHasher
{
    string Bam(string matKhau);

    /// <summary>Kiểm tra mật khẩu. Trả về false thay vì ném exception khi hash hỏng định dạng.</summary>
    bool KiemTra(string hash, string matKhau);

    /// <summary>
    /// Băm một mật khẩu giả để **tiêu tốn đúng lượng thời gian** như một lần kiểm thật, rồi
    /// bỏ kết quả đi (22/09/2026).
    ///
    /// ## Vì sao cần
    ///
    /// `DangNhapCommand` trả **cùng một mã lỗi** cho mọi trường hợp sai, nhưng trước đây nhánh
    /// *"không tìm thấy tài khoản"* `throw` ngay mà **không chạy PBKDF2**, còn nhánh *"có tài
    /// khoản, sai mật khẩu"* thì chạy ~100k vòng băm. Chênh lệch hàng chục mili-giây đó đo
    /// được qua mạng, nên người dò biết username nào có thật — đúng thứ mà việc dùng chung mã
    /// lỗi định giấu đi.
    ///
    /// Gọi hàm này ở nhánh không tìm thấy để hai đường đi tốn thời gian tương đương.
    ///
    /// ## Vì sao không chỉ `Thread.Sleep` một khoảng cố định
    ///
    /// Thời gian băm phụ thuộc tải máy và số vòng lặp cấu hình; một hằng số ngủ sẽ lệch khi
    /// máy bận hoặc khi framework nâng số vòng, và lệch theo hướng **ngược lại** cũng lộ.
    /// Chạy đúng phép băm thật là cách duy nhất tự bám theo chi phí thật.
    /// </summary>
    void BamGia();
}

/// <summary>Thông tin đưa vào JWT.</summary>
/// <param name="NguoiDungId">
/// Id NGƯỜI — null nếu tài khoản kỹ thuật không gắn con người nào. Đây là thứ mọi khoá ngoại
/// nghiệp vụ trỏ tới.
/// </param>
/// <param name="TaiKhoanId">Id TÀI KHOẢN — dùng cho thao tác trên chính tài khoản.</param>
public record ThongTinToken(
    Guid TenantId, string MaTrungTam, string TenTrungTam,
    Guid? NguoiDungId, Guid TaiKhoanId, string Username);

/// <summary>Cặp token trả về sau đăng nhập.</summary>
public record CapToken(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset HetHan,
    /// <summary>
    /// `jti` của access token vừa phát — định danh PHIÊN (20/09/2026).
    ///
    /// Trả ra đây thay vì để handler tự giải mã lại token: giải mã lại là làm hai lần một việc,
    /// và hai chỗ dễ trôi khỏi nhau khi đổi cách sinh `jti`.
    /// </summary>
    Guid Jti);

/// <summary>
/// Thông tin đưa vào JWT của **chủ hệ thống** (ADR-0009).
///
/// Là record RIÊNG, không phải `ThongTinToken` với `TenantId` nullable. Lý do: kiểu riêng thì
/// không có chỗ nào để lỡ truyền tenant vào, còn trường nullable thì chỉ cần một handler quên
/// kiểm là token chủ mang theo tenant và đi lọt vào API nghiệp vụ.
/// </summary>
public record ThongTinTokenChu(Guid QuanTriId, string Username, string HoTen);

/// <summary>Phát hành JWT (FR-01).</summary>
public interface ITokenService
{
    CapToken PhatHanh(ThongTinToken thongTin);

    /// <summary>
    /// Phát token cho tài khoản chủ hệ thống (ADR-0009).
    ///
    /// Token này **không mang claim `tenant_id`**, nên `TenantMiddleware` trả 401
    /// `TOKEN_THIEU_TENANT` ở mọi endpoint nghiệp vụ. Đó là hàng rào chính, và nó có sẵn —
    /// không phải một lớp kiểm mới ai đó có thể quên gọi.
    /// </summary>
    CapToken PhatHanhChoChuHeThong(ThongTinTokenChu thongTin);
}

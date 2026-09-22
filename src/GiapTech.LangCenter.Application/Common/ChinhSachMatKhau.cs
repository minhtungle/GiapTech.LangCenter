using FluentValidation;

namespace GiapTech.LangCenter.Application.Common;

/// <summary>
/// **Chính sách mật khẩu dùng chung** (22/09/2026) — mục 6 của đợt rà soát bảo mật.
///
/// ## Vì sao gom về một chỗ
///
/// Quy tắc độ dài từng nằm rải ở **5 nơi**, mỗi nơi một dòng `MinimumLength(6)` chép tay: tạo
/// tài khoản, quản trị đặt lại, tạo người dùng kèm tài khoản, đặt lại qua token, tự đổi mật
/// khẩu. Năm bản sao của một quy tắc thì sớm muộn cũng lệch nhau — và lệch theo hướng nguy
/// hiểm là chỗ **quên sửa** sẽ thành cửa hậu để đặt mật khẩu yếu.
///
/// Nay một chỗ khai, năm chỗ gọi.
///
/// ## Vì sao 12 ký tự, và vì sao KHÔNG bắt ký tự đặc biệt
///
/// Theo NIST SP 800-63B (bản hiện hành): **độ dài là yếu tố quan trọng nhất**, còn luật kiểu
/// "phải có hoa, thường, số, ký tự đặc biệt" thì **phản tác dụng** — người dùng đáp ứng bằng
/// `Matkhau@123`, dễ đoán hơn hẳn một cụm từ dài, rồi dán lên màn hình vì không nhớ nổi.
///
/// 6 ký tự là quá ngắn một cách nguy hiểm khi cộng với việc trước đây **không khoá tài khoản**
/// sau nhiều lần sai (mục 4, đã vá cùng đợt). Nâng lên 12 và **không** thêm luật phức tạp.
///
/// ## Chưa có blocklist mật khẩu phổ biến
///
/// Bản rà soát đề xuất thêm blocklist (kiểu "123456789012", "matkhau12345") hoặc đối chiếu
/// HIBP. Chưa làm ở đợt này: blocklist tử tế cần một danh sách lớn đóng gói kèm, còn HIBP là
/// gọi ra dịch vụ ngoài — cả hai đều là quyết định riêng, không nên lặng lẽ gộp vào một thay
/// đổi về độ dài. Đây là nợ đã biết, ghi ở `docs/ra-soat-bao-mat-dang-nhap.md`.
/// </summary>
public static class ChinhSachMatKhau
{
    /// <summary>
    /// Độ dài tối thiểu. Công khai để **test đọc được** — test chỉ so thông báo lỗi thì không
    /// bắt được việc ai đó hạ số xuống.
    /// </summary>
    public const int DoDaiToiThieu = 12;

    /// <summary>
    /// Trần độ dài — chặn gửi vài MB vào ô mật khẩu, vì PBKDF2 băm chuỗi dài tốn CPU thật và
    /// đó là kênh gây tải rẻ tiền. 128 đủ rộng cho cả một cụm từ dài.
    /// </summary>
    public const int DoDaiToiDa = 128;

    /// <summary>
    /// Áp chính sách cho một trường mật khẩu. Dùng ở **mọi** chỗ nhận mật khẩu mới.
    ///
    /// Mã lỗi giữ nguyên `MAT_KHAU_QUA_NGAN` — đã có bản dịch ở `i18n.ts` và đang lưu hành.
    /// </summary>
    public static IRuleBuilderOptions<T, string> ApDungChinhSach<T>(
        this IRuleBuilder<T, string> rule)
        => rule.NotEmpty()
            .MinimumLength(DoDaiToiThieu).WithErrorCode("MAT_KHAU_QUA_NGAN")
            .MaximumLength(DoDaiToiDa).WithErrorCode("MAT_KHAU_QUA_DAI");
}

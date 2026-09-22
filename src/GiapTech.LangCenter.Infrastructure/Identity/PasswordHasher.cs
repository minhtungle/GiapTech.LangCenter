using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace GiapTech.LangCenter.Infrastructure.Identity;

/// <summary>
/// Băm mật khẩu bằng thuật toán chuẩn của ASP.NET Core Identity (PBKDF2, salt riêng mỗi
/// mật khẩu, số vòng lặp theo mặc định hiện hành của framework).
///
/// Dùng riêng PasswordHasher thay vì kéo cả Identity stack: Identity giả định username duy
/// nhất toàn cục, còn ở đây username chỉ duy nhất trong phạm vi tenant (hai trung tâm đều có thể
/// có tài khoản "admin"). Ghép vào sẽ phải chống lại giả định đó ở mọi bước.
/// </summary>
public class AppPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object DoiTuongGia = new();

    public string Bam(string matKhau) => _hasher.HashPassword(DoiTuongGia, matKhau);

    /// <summary>
    /// Hash của một mật khẩu bất kỳ, băm **một lần** lúc khởi tạo lớp.
    ///
    /// Băm lại mỗi lần gọi `BamGia` sẽ tốn gấp đôi (một lần `Bam` + một lần `KiemTra`) và làm
    /// nhánh "không tìm thấy" **chậm hơn** nhánh thật — vẫn là chênh lệch đo được, chỉ đảo
    /// chiều. Giữ sẵn một hash cố định rồi verify mới ra đúng chi phí của một lần kiểm thật.
    /// </summary>
    private static readonly string HashGia = new PasswordHasher<object>()
        .HashPassword(DoiTuongGia, "mat-khau-gia-de-can-bang-thoi-gian");

    /// <summary>
    /// Tiêu tốn đúng chi phí một lần kiểm mật khẩu rồi bỏ kết quả — xem `IPasswordHasher.BamGia`.
    ///
    /// Chuỗi so ở đây cố tình **không** khớp `HashGia`, nhưng điều đó không ảnh hưởng thời
    /// gian: PBKDF2 phải chạy đủ số vòng rồi mới so được kết quả.
    /// </summary>
    public void BamGia() => _hasher.VerifyHashedPassword(DoiTuongGia, HashGia, "khong-khop");

    public bool KiemTra(string hash, string matKhau)
    {
        try
        {
            var kq = _hasher.VerifyHashedPassword(DoiTuongGia, hash, matKhau);
            return kq is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // Hash hỏng định dạng (dữ liệu lỗi, migration tay). Coi như sai mật khẩu thay vì
            // để exception lọt lên thành 500 và lộ ra rằng tài khoản này có tồn tại.
            return false;
        }
    }
}

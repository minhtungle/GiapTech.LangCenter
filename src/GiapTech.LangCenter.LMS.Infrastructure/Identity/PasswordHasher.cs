using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace GiapTech.LangCenter.LMS.Infrastructure.Identity;

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

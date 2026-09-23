using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Seed;

/// <summary>
/// **Tạo tài khoản chủ hệ thống đầu tiên khi chưa có cái nào** (ADR-0009).
///
/// Cùng khuôn với <see cref="TrungTamDauTien"/>, và vì cùng một lý do: không có tài khoản chủ
/// thì site chủ hoàn toàn không vào được, mà **cố ý không có endpoint tạo tài khoản chủ** —
/// endpoint như vậy là một bề mặt tấn công đổi lấy tiện lợi mà chỉ dùng đúng một lần.
///
/// ## Mật khẩu do người triển khai đặt, KHÔNG tự sinh
///
/// Y hệt lập luận ở `TrungTamDauTien`: seed chạy trong container lúc khởi động, không có ai để
/// trả mật khẩu về, nên đường duy nhất là ghi log — mà log thường được gom về nơi tập trung,
/// ai đọc được log là đọc được mật khẩu.
///
/// Không đặt <c>CHU_HE_THONG_MAT_KHAU</c> ⇒ **không tạo gì**, và ghi một dòng nói rõ vì sao.
/// Thà không có tài khoản còn hơn có một tài khoản mà mật khẩu nằm trong log — và tài khoản
/// này mạnh hơn admin của bất kỳ trung tâm nào.
///
/// ## Chỉ chạy khi CHƯA có tài khoản chủ nào
///
/// `!AnyAsync()` biến việc này thành thao tác một lần trong đời của một cài đặt. Khởi động lại
/// lần thứ hai không làm gì — kể cả khi tài khoản đầu tiên đã bị đổi tên hay vô hiệu hoá.
///
/// **Không** dùng điều kiện "chưa có tenant nào" như `TrungTamDauTien`: hai thứ độc lập với
/// nhau, và một hệ thống đang chạy với vài trung tâm vẫn có thể cần thêm tài khoản chủ đầu
/// tiên (ví dụ nâng cấp từ bản trước ADR-0009).
/// </summary>
public class ChuHeThongDauTien(
    AppDbContext db,
    IPasswordHasher hasher,
    IConfiguration config,
    ILogger<ChuHeThongDauTien> logger)
{
    /// <summary>Biến môi trường chứa mật khẩu cho tài khoản chủ đầu tiên.</summary>
    public const string KhoaMatKhau = "CHU_HE_THONG_MAT_KHAU";

    /// <summary>Biến môi trường đặt tên đăng nhập. Bỏ trống thì dùng `chu`.</summary>
    public const string KhoaUsername = "CHU_HE_THONG_USERNAME";

    private const string UsernameMacDinh = "chu";

    public async Task ChayAsync(CancellationToken ct = default)
    {
        if (await db.QuanTriHeThongs.AnyAsync(ct))
            return;

        var matKhau = config[KhoaMatKhau];

        if (string.IsNullOrWhiteSpace(matKhau))
        {
            logger.LogInformation(
                "Chưa có tài khoản chủ hệ thống nào, nhưng {Khoa} chưa đặt nên KHÔNG tạo. "
                + "Đặt biến đó rồi khởi động lại — mật khẩu do bạn chọn, hệ thống cố ý không "
                + "tự sinh để không phải ghi nó vào log.",
                KhoaMatKhau);
            return;
        }

        var username = config[KhoaUsername];
        username = string.IsNullOrWhiteSpace(username)
            ? UsernameMacDinh
            : username.Trim().ToLowerInvariant();

        db.QuanTriHeThongs.Add(new QuanTriHeThong
        {
            Username = username,
            PasswordHash = hasher.Bam(matKhau),
            HoTen = "Chủ hệ thống",
            HoatDong = true,
            // Buộc đổi ở lần đăng nhập đầu: mật khẩu này đã đi qua file cấu hình và tay người
            // triển khai, nên nó không còn là bí mật chỉ chủ tài khoản biết.
            PhaiDoiMatKhau = true
        });

        await db.SaveChangesAsync(ct);

        // Ghi USERNAME, không ghi mật khẩu — cùng lý do với `TrungTamDauTien`.
        logger.LogWarning(
            "Đã tạo tài khoản chủ hệ thống đầu tiên: {Username}. ĐỔI MẬT KHẨU ở lần đăng nhập "
            + "đầu, rồi GỠ biến {Khoa} khỏi môi trường.",
            username, KhoaMatKhau);
    }
}

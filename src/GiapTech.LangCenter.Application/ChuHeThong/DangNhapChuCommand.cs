using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.ChuHeThong;

/// <summary>
/// Đăng nhập tài khoản **chủ hệ thống** (ADR-0009) — site quản trị của chủ sản phẩm.
///
/// Luồng xác thực thứ hai, tách hẳn khỏi `DangNhapCommand` của tenant. Đây là cái giá đã nói
/// trước trong ADR: hai chỗ băm mật khẩu, hai chỗ có thể sai. Đổi lại là **không có đường nào**
/// để tài khoản tenant trở thành chủ hệ thống.
///
/// Không có mã trung tâm trong bộ đăng nhập: tài khoản này không thuộc trung tâm nào.
/// </summary>
public record DangNhapChuCommand(string Username, string MatKhau)
    : IRequest<DangNhapChuResult>;

/// <param name="PhaiDoiMatKhau">
/// Như tài khoản tenant: mật khẩu đầu tiên đi qua tay người vận hành và file cấu hình, nên
/// phải đổi trước khi làm gì khác.
/// </param>
public record DangNhapChuResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset HetHan,
    bool PhaiDoiMatKhau,
    string HoTen);

public class DangNhapChuHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokenService)
    : IRequestHandler<DangNhapChuCommand, DangNhapChuResult>
{
    public async Task<DangNhapChuResult> Handle(
        DangNhapChuCommand request, CancellationToken ct)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        var quanTri = await db.QuanTriHeThongs
            .FirstOrDefaultAsync(q => q.Username == username, ct);

        if (quanTri is null)
        {
            // Băm giả để nhánh "không có tài khoản" tốn thời gian tương đương nhánh "sai mật
            // khẩu" — cùng lý do với `DangNhapCommand`, xem `IPasswordHasher.BamGia`.
            // Ở đây còn quan trọng hơn: chỉ có vài tài khoản chủ, nên dò ra một username hợp
            // lệ là thu hẹp không gian tấn công xuống rất nhỏ.
            hasher.BamGia();
            throw new AppException(MaLoi.DangNhapThatBai, "Không có tài khoản chủ");
        }

        if (!hasher.KiemTra(quanTri.PasswordHash, request.MatKhau))
            throw new AppException(MaLoi.DangNhapThatBai, "Sai mật khẩu chủ hệ thống");

        // Kiểm trạng thái SAU khi kiểm mật khẩu: kiểm trước thì người gõ đúng username của một
        // tài khoản đã khoá sẽ nhận mã lỗi khác với username không tồn tại — lại lộ.
        if (!quanTri.HoatDong)
            throw new AppException(MaLoi.TaiKhoanBiVoHieuHoa);

        quanTri.LanDangNhapCuoi = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var cap = tokenService.PhatHanhChoChuHeThong(
            new ThongTinTokenChu(quanTri.Id, quanTri.Username, quanTri.HoTen));

        return new DangNhapChuResult(
            cap.AccessToken, cap.RefreshToken, cap.HetHan,
            quanTri.PhaiDoiMatKhau, quanTri.HoTen);
    }
}

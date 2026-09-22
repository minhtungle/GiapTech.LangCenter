using FluentValidation;
using GiapTech.LangCenter.Application.Common;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.DoiMatKhau;

/// <summary>
/// FR-01 — người dùng tự đổi mật khẩu của CHÍNH MÌNH.
/// Đổi mật khẩu cho tài khoản khác là đặc quyền riêng của Admin (FR-03), nằm ở module quản trị.
/// </summary>
public record DoiMatKhauCommand(string MatKhauCu, string MatKhauMoi) : IRequest;

public class DoiMatKhauValidator : AbstractValidator<DoiMatKhauCommand>
{
    public DoiMatKhauValidator()
    {
        RuleFor(x => x.MatKhauCu).NotEmpty();
        RuleFor(x => x.MatKhauMoi)
            .ApDungChinhSach()
            .NotEqual(x => x.MatKhauCu).WithErrorCode("MAT_KHAU_MOI_TRUNG_CU");
    }
}

public class DoiMatKhauHandler(
    IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser)
    : IRequestHandler<DoiMatKhauCommand>
{
    public async Task Handle(DoiMatKhauCommand request, CancellationToken ct)
    {
        // TaiKhoanId chứ không phải UserId: đổi mật khẩu là thao tác trên TÀI KHOẢN. Token
        // phát hành trước khi tách bảng không có claim này → từ chối rõ ràng, thà bắt người
        // dùng đăng nhập lại còn hơn đổi mật khẩu nhầm tài khoản.
        if (currentUser.TaiKhoanId is not { } taiKhoanId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Query filter đã giới hạn theo tenant, nên không thể đổi nhầm sang user trung tâm khác.
        var taiKhoan = await db.TaiKhoans.FirstOrDefaultAsync(u => u.Id == taiKhoanId, ct)
            ?? throw new KhongTimThayException($"Không thấy tài khoản {taiKhoanId}");

        if (!hasher.KiemTra(taiKhoan.PasswordHash, request.MatKhauCu))
            throw new AppException(MaLoi.MatKhauCuKhongDung);

        taiKhoan.PasswordHash = hasher.Bam(request.MatKhauMoi);

        // Gỡ cờ bắt buộc đổi — đây là điều kiện để người dùng vào được hệ thống.
        taiKhoan.PhaiDoiMatKhau = false;

        // Thu hồi refresh token của các phiên KHÁC: đổi mật khẩu thường là phản ứng khi nghi
        // bị lộ, nên phải đá mọi phiên cũ ra thay vì để chúng tự làm mới token vô thời hạn.
        var bayGio = DateTimeOffset.UtcNow;
        var phienCu = await db.RefreshTokens
            .Where(r => r.TaiKhoanId == taiKhoanId && r.ThuHoiLuc == null)
            .ToListAsync(ct);

        foreach (var r in phienCu)
            r.ThuHoiLuc = bayGio;

        await db.SaveChangesAsync(ct);
    }
}

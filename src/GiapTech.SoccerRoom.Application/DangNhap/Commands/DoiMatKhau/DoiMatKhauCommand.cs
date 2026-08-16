using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangNhap.Commands.DoiMatKhau;

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
            .NotEmpty()
            .MinimumLength(6).WithErrorCode("MAT_KHAU_QUA_NGAN")
            .NotEqual(x => x.MatKhauCu).WithErrorCode("MAT_KHAU_MOI_TRUNG_CU");
    }
}

public class DoiMatKhauHandler(
    IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser)
    : IRequestHandler<DoiMatKhauCommand>
{
    public async Task Handle(DoiMatKhauCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Query filter đã giới hạn theo tenant, nên không thể đổi nhầm sang user CLB khác.
        var nguoiDung = await db.NguoiDungs.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KhongTimThayException($"Không thấy user {userId}");

        if (!hasher.KiemTra(nguoiDung.PasswordHash, request.MatKhauCu))
            throw new AppException(MaLoi.MatKhauCuKhongDung);

        nguoiDung.PasswordHash = hasher.Bam(request.MatKhauMoi);

        // Gỡ cờ bắt buộc đổi — đây là điều kiện để người dùng vào được hệ thống.
        nguoiDung.PhaiDoiMatKhau = false;

        // Thu hồi refresh token của các phiên KHÁC: đổi mật khẩu thường là phản ứng khi nghi
        // bị lộ, nên phải đá mọi phiên cũ ra thay vì để chúng tự làm mới token vô thời hạn.
        var bayGio = DateTimeOffset.UtcNow;
        var phienCu = await db.RefreshTokens
            .Where(r => r.NguoiDungId == userId && r.ThuHoiLuc == null)
            .ToListAsync(ct);

        foreach (var r in phienCu)
            r.ThuHoiLuc = bayGio;

        await db.SaveChangesAsync(ct);
    }
}

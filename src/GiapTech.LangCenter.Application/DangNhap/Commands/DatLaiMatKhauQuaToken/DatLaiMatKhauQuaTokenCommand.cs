using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.DangNhap.Commands.QuenMatKhau;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.DatLaiMatKhauQuaToken;

/// <summary>FR-02 — đặt lại mật khẩu bằng mã nhận qua email.</summary>
public record DatLaiMatKhauQuaTokenCommand(string Token, string MatKhauMoi) : IRequest;

public class DatLaiMatKhauQuaTokenValidator : AbstractValidator<DatLaiMatKhauQuaTokenCommand>
{
    public DatLaiMatKhauQuaTokenValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.MatKhauMoi).NotEmpty().MinimumLength(6)
            .WithErrorCode("MAT_KHAU_QUA_NGAN");
    }
}

public class DatLaiMatKhauQuaTokenHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<DatLaiMatKhauQuaTokenCommand>
{
    public async Task Handle(DatLaiMatKhauQuaTokenCommand request, CancellationToken ct)
    {
        var hash = BamToken.Bam(request.Token);
        var bayGio = DateTimeOffset.UtcNow;

        // IgnoreQueryFilters: người dùng chưa đăng nhập nên context không có tenant. An toàn
        // vì tra cứu bằng hash của chuỗi ngẫu nhiên 48 byte — không đoán được, và token đã
        // gắn sẵn với đúng một tài khoản.
        var token = await db.TokenDatLaiMatKhaus
            .IgnoreQueryFilters()
            .Include(t => t.TaiKhoan)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || !token.ConHieuLuc(bayGio))
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe);

        token.TaiKhoan.PasswordHash = hasher.Bam(request.MatKhauMoi);

        // Người dùng vừa tự đặt mật khẩu mới → không bắt đổi lại lần nữa.
        token.TaiKhoan.PhaiDoiMatKhau = false;

        // Dùng một lần.
        token.DaDungLuc = bayGio;

        // Thu hồi mọi phiên đang mở: nếu tài khoản bị chiếm, đổi mật khẩu phải đá kẻ đó ra,
        // nếu không refresh token cũ vẫn cấp access token mới vô thời hạn.
        var phienDangMo = await db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(r => r.TaiKhoanId == token.TaiKhoanId && r.ThuHoiLuc == null)
            .ToListAsync(ct);

        foreach (var r in phienDangMo)
            r.ThuHoiLuc = bayGio;

        await db.SaveChangesAsync(ct);
    }
}

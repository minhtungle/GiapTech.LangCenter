using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DangNhap.Commands.DangNhap;

/// <summary>FR-01 — đăng nhập bằng bộ ba {ID đội, username, password}.</summary>
public record DangNhapCommand(string MaDoi, string Username, string MatKhau)
    : IRequest<DangNhapResult>;

/// <param name="PhaiDoiMatKhau">
/// true → frontend chuyển hướng sang màn đổi mật khẩu trước khi vào hệ thống (FR-01).
/// </param>
public record DangNhapResult(
    string AccessToken, string RefreshToken, DateTimeOffset HetHan, bool PhaiDoiMatKhau);

public class DangNhapValidator : AbstractValidator<DangNhapCommand>
{
    public DangNhapValidator()
    {
        RuleFor(x => x.MaDoi).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MatKhau).NotEmpty();
    }
}

public class DangNhapHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokenService)
    : IRequestHandler<DangNhapCommand, DangNhapResult>
{
    public async Task<DangNhapResult> Handle(DangNhapCommand request, CancellationToken ct)
    {
        // TENANT không phải ITenantEntity nên không bị Global Query Filter chặn — cần thiết,
        // vì lúc này chưa biết tenant nào để mà lọc.
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.MaDoi == request.MaDoi, ct);

        // Sai ID đội, sai username, sai mật khẩu → CÙNG một mã lỗi. Phân biệt sẽ cho phép
        // dò xem CLB nào tồn tại và tài khoản nào có thật.
        if (tenant is null)
            throw new AppException(MaLoi.DangNhapThatBai, $"Không có tenant {request.MaDoi}");

        var nguoiDung = await db.NguoiDungs
            .IgnoreQueryFilters() // chưa có tenant trong context ở bước đăng nhập
            .FirstOrDefaultAsync(
                u => u.TenantId == tenant.Id && u.Username == request.Username, ct);

        if (nguoiDung is null)
            throw new AppException(MaLoi.DangNhapThatBai, $"Không có user {request.Username}");

        if (!hasher.KiemTra(nguoiDung.PasswordHash, request.MatKhau))
            throw new AppException(MaLoi.DangNhapThatBai, "Sai mật khẩu");

        if (nguoiDung.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException(MaLoi.TaiKhoanBiVoHieuHoa);

        var token = tokenService.PhatHanh(new ThongTinToken(
            tenant.Id, tenant.MaDoi, nguoiDung.Id, nguoiDung.Username));

        return new DangNhapResult(
            token.AccessToken, token.RefreshToken, token.HetHan, nguoiDung.PhaiDoiMatKhau);
    }
}

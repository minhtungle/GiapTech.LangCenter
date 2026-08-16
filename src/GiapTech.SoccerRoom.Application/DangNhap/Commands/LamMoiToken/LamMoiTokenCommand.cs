using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.DangNhap;
using GiapTech.SoccerRoom.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.SoccerRoom.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.SoccerRoom.Application.DangNhap.Commands.LamMoiToken;

/// <summary>FR-01 — đổi refresh token lấy cặp token mới.</summary>
public record LamMoiTokenCommand(string RefreshToken) : IRequest<DangNhapResult>;

public class LamMoiTokenValidator : AbstractValidator<LamMoiTokenCommand>
{
    public LamMoiTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LamMoiTokenHandler(
    IAppDbContext db,
    ITokenService tokenService,
    ILogger<LamMoiTokenHandler> logger)
    : IRequestHandler<LamMoiTokenCommand, DangNhapResult>
{
    public async Task<DangNhapResult> Handle(LamMoiTokenCommand request, CancellationToken ct)
    {
        var hash = BamToken.Bam(request.RefreshToken);
        var bayGio = DateTimeOffset.UtcNow;

        var token = await db.RefreshTokens
            .IgnoreQueryFilters() // chưa có access token nên context chưa có tenant
            .Include(r => r.NguoiDung)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (token is null)
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token không tồn tại");

        // Token đã thu hồi mà vẫn được dùng = dấu hiệu bị đánh cắp: kẻ tấn công dùng lại bản
        // sao cũ sau khi chủ tài khoản đã xoay vòng. Thu hồi TOÀN BỘ phiên của người dùng,
        // buộc đăng nhập lại — thà phiền một lần còn hơn để phiên bị chiếm chạy tiếp.
        if (token.ThuHoiLuc is not null)
        {
            logger.LogWarning(
                "Phát hiện tái sử dụng refresh token đã thu hồi của {NguoiDung} — thu hồi toàn bộ phiên",
                token.NguoiDungId);

            var tatCa = await db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(r => r.NguoiDungId == token.NguoiDungId && r.ThuHoiLuc == null)
                .ToListAsync(ct);

            foreach (var r in tatCa)
                r.ThuHoiLuc = bayGio;

            await db.SaveChangesAsync(ct);
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token đã bị thu hồi");
        }

        if (!token.ConHieuLuc(bayGio))
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token hết hạn");

        if (token.NguoiDung.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException(MaLoi.TaiKhoanBiVoHieuHoa);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == token.TenantId, ct)
            ?? throw new KhongTimThayException($"Tenant {token.TenantId}");

        // XOAY VÒNG: token cũ chết ngay khi cấp token mới. Nếu để dùng lại nhiều lần thì một
        // bản sao bị lộ sẽ sống đến tận ngày hết hạn.
        token.ThuHoiLuc = bayGio;

        var capMoi = tokenService.PhatHanh(new ThongTinToken(
            tenant.Id, tenant.MaDoi, tenant.TenDoi, token.NguoiDung.Id, token.NguoiDung.Username));

        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            TenantId = tenant.Id,
            NguoiDungId = token.NguoiDungId,
            TokenHash = BamToken.Bam(capMoi.RefreshToken),
            HetHan = bayGio.AddDays(TokenService_HanRefreshNgay)
        });

        await db.SaveChangesAsync(ct);

        return new DangNhapResult(
            capMoi.AccessToken, capMoi.RefreshToken, capMoi.HetHan,
            token.NguoiDung.PhaiDoiMatKhau);
    }

    /// <summary>Hạn refresh token (ngày) — dài hơn access token nhiều để người dùng không phải
    /// đăng nhập lại liên tục, nhưng vẫn hữu hạn.</summary>
    public const int TokenService_HanRefreshNgay = 30;
}

using FluentValidation;
using GiapTech.LangCenter.Application.Common;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.DangNhap.Commands.DangNhap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.DoiMatKhau;

/// <summary>
/// FR-01 — người dùng tự đổi mật khẩu của CHÍNH MÌNH.
/// Đổi mật khẩu cho tài khoản khác là đặc quyền riêng của Admin (FR-03), nằm ở module quản trị.
/// </summary>
/// <returns>
/// Cặp token MỚI cho chính phiên vừa đổi mật khẩu (ADR-0007, 22/09/2026).
///
/// Lệnh này thu hồi **mọi** refresh token của tài khoản — đúng, vì đổi mật khẩu thường là phản
/// ứng khi nghi bị lộ. Nhưng như vậy cũng giết luôn refresh token của **chính người đang đổi**,
/// và từ ADR-0007 thì đó là thứ duy nhất khôi phục được phiên sau khi tải lại trang.
///
/// Trước ADR-0007 không ai thấy: access token nằm trong `localStorage` nên người dùng vẫn ở
/// nguyên trong app cho tới khi nó hết hạn. Nay F5 là mất access token, và không còn refresh
/// token hợp lệ để đổi ⇒ **bị đá về màn đăng nhập ngay sau khi đổi mật khẩu**. Gặp đúng vậy
/// khi chạy E2E lần đầu sau ADR-0007.
///
/// Nên cấp lại một cặp mới cho phiên hiện tại: các phiên khác vẫn chết, phiên này sống tiếp.
/// </returns>
public record DoiMatKhauCommand(string MatKhauCu, string MatKhauMoi) : IRequest<DangNhapResult>;

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
    IAppDbContext db,
    IPasswordHasher hasher,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    ITokenService tokenService,
    IPhienService phienService)
    : IRequestHandler<DoiMatKhauCommand, DangNhapResult>
{
    public async Task<DangNhapResult> Handle(DoiMatKhauCommand request, CancellationToken ct)
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
        {
            r.ThuHoiLuc = bayGio;
            r.LyDo = Domain.Entities.LyDoThuHoi.DangXuatHoacDoiMatKhau;
        }

        /*
          Cấp lại cặp token cho CHÍNH phiên này — xem chú thích ở `DoiMatKhauCommand`.

          Ghi `PhienHienTai` bằng `jti` mới để `PhienDuyNhatMiddleware` chấp nhận access token
          vừa cấp; thiếu dòng này thì người vừa đổi mật khẩu bị chặn ở mọi endpoint nghiệp vụ.
        */
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == taiKhoan.TenantId, ct)
            ?? throw new KhongTimThayException($"Không thấy trung tâm {taiKhoan.TenantId}");

        var token = tokenService.PhatHanh(new ThongTinToken(
            tenant.Id, tenant.MaTrungTam, tenant.TenTrungTam,
            taiKhoan.NguoiDungId, taiKhoan.Id, taiKhoan.Username));

        taiKhoan.PhienHienTai = token.Jti;

        using var phamVi = currentTenant.DatPhamVi(tenant.Id);

        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            TenantId = tenant.Id,
            TaiKhoanId = taiKhoan.Id,
            TokenHash = QuenMatKhau.BamToken.Bam(token.RefreshToken),
            HetHan = DateTimeOffset.UtcNow.AddDays(
                LamMoiToken.LamMoiTokenHandler.TokenService_HanRefreshNgay),
        });

        await db.SaveChangesAsync(ct);

        // Xoá cache phiên để middleware đọc lại `PhienHienTai` vừa ghi, không chờ 10 giây.
        phienService.XoaCache(taiKhoan.Id);

        return new DangNhapResult(
            token.AccessToken, token.RefreshToken, token.HetHan, PhaiDoiMatKhau: false);
    }
}

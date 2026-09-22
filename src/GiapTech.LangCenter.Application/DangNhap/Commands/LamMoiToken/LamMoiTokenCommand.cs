using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.DangNhap.Commands.DangNhap;
using GiapTech.LangCenter.Application.DangNhap.Commands.QuenMatKhau;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.LamMoiToken;

/// <summary>FR-01 — đổi refresh token lấy cặp token mới.</summary>
public record LamMoiTokenCommand(string RefreshToken) : IRequest<DangNhapResult>;

public class LamMoiTokenValidator : AbstractValidator<LamMoiTokenCommand>
{
    public LamMoiTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LamMoiTokenHandler(
    IAppDbContext db,
    ITokenService tokenService,
    IPhienService phienService,
    ILogger<LamMoiTokenHandler> logger)
    : IRequestHandler<LamMoiTokenCommand, DangNhapResult>
{
    public async Task<DangNhapResult> Handle(LamMoiTokenCommand request, CancellationToken ct)
    {
        var hash = BamToken.Bam(request.RefreshToken);
        var bayGio = DateTimeOffset.UtcNow;

        var token = await db.RefreshTokens
            .IgnoreQueryFilters() // chưa có access token nên context chưa có tenant
            .Include(r => r.TaiKhoan)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (token is null)
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token không tồn tại");

        // Token đã thu hồi mà vẫn được dùng = dấu hiệu bị đánh cắp: kẻ tấn công dùng lại bản
        // sao cũ sau khi chủ tài khoản đã xoay vòng. Thu hồi TOÀN BỘ phiên của người dùng,
        // buộc đăng nhập lại — thà phiền một lần còn hơn để phiên bị chiếm chạy tiếp.
        if (token.ThuHoiLuc is not null)
        {
            /*
              Phân biệt "bị đẩy ra" với "bị đánh cắp" (ADR-0007, 22/09/2026).

              Token đã thu hồi mà `PhienHienTai` của tài khoản **khác** `jti` của nó nghĩa là
              người khác vừa đăng nhập và đẩy phiên này ra — chuyện bình thường, không phải tấn
              công. Trả đúng mã `PHIEN_DA_BI_DAY_RA` để frontend hiện câu giải thích thay vì
              đưa người dùng về màn đăng nhập trắng trơn.

              Cần từ ADR-0007 vì refresh token nay đi bằng cookie: phiên bị đẩy ra lộ diện ở
              **lời gọi làm mới** (thất bại trước), chứ không còn ở 401 của endpoint nghiệp vụ
              như bản dùng `localStorage`.

              KHÔNG nới lỏng phần chống trộm: vẫn thu hồi toàn bộ phiên như cũ, chỉ đổi mã lỗi
              trả về cho đúng sự thật.
            */
            // Đọc LÝ DO đã ghi lúc thu hồi, không suy đoán từ `PhienHienTai` — suy đoán sai ở
            // ca trộm thật (sau một lần xoay vòng hợp lệ, cột đó cũng khác `jti` của token cũ).
            var biDayRa = token.LyDo == Domain.Entities.LyDoThuHoi.BiDayRa;

            if (biDayRa)
                logger.LogInformation(
                    "Phiên cũ của {NguoiDung} thử làm mới sau khi bị đăng nhập nơi khác đẩy ra",
                    token.TaiKhoanId);
            else
                logger.LogWarning(
                    "Phát hiện tái sử dụng refresh token đã thu hồi của {NguoiDung} — thu hồi toàn bộ phiên",
                    token.TaiKhoanId);

            /*
              CHỈ thu hồi toàn bộ khi nghi BỊ ĐÁNH CẮP (ADR-0007, 22/09/2026).

              Phiên bị đẩy ra là chuyện bình thường — người kia vừa đăng nhập. Thu hồi toàn bộ
              ở ca đó sẽ giết luôn refresh token **của chính người vừa đăng nhập**: hai người
              cùng bị đá ra và không ai vào được.

              Phân biệt bằng cột `LyDo` ghi lúc thu hồi, **không** suy từ `PhienHienTai`: sau
              một lần xoay vòng hợp lệ thì cột đó cũng khác `jti` của token cũ, nên suy đoán sẽ
              coi ca trộm thật là "bị đẩy ra" ⇒ nới lỏng đúng chốt chặn quan trọng nhất.

              Lỗi này chỉ lộ từ ADR-0007: trước đây phiên cũ nhận 401 ở endpoint nghiệp vụ và
              frontend **không** gọi làm mới, nên nhánh này không chạy.
            */
            if (!biDayRa)
            {
                var tatCa = await db.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(r => r.TaiKhoanId == token.TaiKhoanId && r.ThuHoiLuc == null)
                    .ToListAsync(ct);

                foreach (var r in tatCa)
                {
                    r.ThuHoiLuc = bayGio;
                    r.LyDo = Domain.Entities.LyDoThuHoi.XoayVong;
                }

                await db.SaveChangesAsync(ct);
            }

            throw biDayRa
                ? new AppException(MaLoi.PhienDaBiDayRa, "Phiên đã bị đẩy ra bởi lần đăng nhập khác")
                : new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token đã bị thu hồi");
        }

        if (!token.ConHieuLuc(bayGio))
            throw new AppException(MaLoi.TokenDatLaiKhongHopLe, "Refresh token hết hạn");

        if (token.TaiKhoan.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException(MaLoi.TaiKhoanBiVoHieuHoa);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Id == token.TenantId, ct)
            ?? throw new KhongTimThayException($"Tenant {token.TenantId}");

        // XOAY VÒNG: token cũ chết ngay khi cấp token mới. Nếu để dùng lại nhiều lần thì một
        // bản sao bị lộ sẽ sống đến tận ngày hết hạn.
        token.ThuHoiLuc = bayGio;
        // XOAY VÒNG: dùng lại token này = nghi bị đánh cắp (xem nhánh `ThuHoiLuc is not null`).
        token.LyDo = Domain.Entities.LyDoThuHoi.XoayVong;

        var capMoi = tokenService.PhatHanh(new ThongTinToken(
            tenant.Id, tenant.MaTrungTam, tenant.TenTrungTam,
            token.TaiKhoan.NguoiDungId, token.TaiKhoan.Id, token.TaiKhoan.Username));

        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            TenantId = tenant.Id,
            TaiKhoanId = token.TaiKhoanId,
            TokenHash = BamToken.Bam(capMoi.RefreshToken),
            HetHan = bayGio.AddDays(TokenService_HanRefreshNgay)
        });

        /*
          CHUYỂN phiên sang token vừa cấp — làm mới token là **cùng một phiên đi tiếp**, không
          phải phiên mới (20/09/2026).

          Thiếu dòng này thì access token mới mang `jti` khác `PhienHienTai`, và
          `PhienDuyNhatMiddleware` đá chính người đang dùng ra — cứ mỗi lần token hết hạn
          (60 phút) là văng về màn đăng nhập. Test `Lam_moi_token_tra_ve_cap_token_moi` bắt
          được ngay khi tôi quên.

          An toàn với mục tiêu "một phiên": refresh token đã bị thu hồi khi có người đăng nhập
          nơi khác, nên phiên cũ không tới được đây để giành lại phiên.
        */
        token.TaiKhoan.PhienHienTai = capMoi.Jti;

        await db.SaveChangesAsync(ct);

        // Xoá cache để token mới dùng được NGAY, cùng lý do với handler đăng nhập.
        phienService.XoaCache(token.TaiKhoanId);

        return new DangNhapResult(
            capMoi.AccessToken, capMoi.RefreshToken, capMoi.HetHan,
            token.TaiKhoan.PhaiDoiMatKhau);
    }

    /// <summary>Hạn refresh token (ngày) — dài hơn access token nhiều để người dùng không phải
    /// đăng nhập lại liên tục, nhưng vẫn hữu hạn.</summary>
    public const int TokenService_HanRefreshNgay = 30;
}

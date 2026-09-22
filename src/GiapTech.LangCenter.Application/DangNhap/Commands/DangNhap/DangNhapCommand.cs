using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.DangNhap;

/// <summary>FR-01 — đăng nhập bằng bộ ba {mã trung tâm, username, password}.</summary>
public record DangNhapCommand(string MaTrungTam, string Username, string MatKhau)
    : IRequest<DangNhapResult>, ILenhXacThuc
{
    // Khai danh tính cho nhật ký: lệnh này chưa có JWT nên behavior không tự suy được ai đang
    // cố vào — và chính lần THẤT BẠI mới là thứ cần ghi. Xem `ILenhXacThuc`.
    string ILenhXacThuc.MaTrungTamDeGhiNhatKy => MaTrungTam;
    string ILenhXacThuc.UsernameDeGhiNhatKy => Username;
}

/// <param name="PhaiDoiMatKhau">
/// true → frontend chuyển hướng sang màn đổi mật khẩu trước khi vào hệ thống (FR-01).
/// </param>
public record DangNhapResult(
    string AccessToken, string RefreshToken, DateTimeOffset HetHan, bool PhaiDoiMatKhau);

public class DangNhapValidator : AbstractValidator<DangNhapCommand>
{
    public DangNhapValidator()
    {
        RuleFor(x => x.MaTrungTam).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MatKhau).NotEmpty();
    }
}

public class DangNhapHandler(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokenService,
    ICurrentTenant currentTenant,
    IPhienService phienService,
    IChongDoMatKhau chongDo)
    : IRequestHandler<DangNhapCommand, DangNhapResult>
{
    public async Task<DangNhapResult> Handle(DangNhapCommand request, CancellationToken ct)
    {
        // Chuẩn hoá: người dùng gõ mã bằng tay nên hoa/thường và khoảng trắng thừa là
        // chuyện thường. Không chuẩn hoá thì họ bị từ chối chỉ vì bàn phím đang ở chế độ thường.
        var maTrungTam = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        /*
          Khoá tạm sau nhiều lần sai (22/09/2026) — chặn dò mật khẩu phân tán, thứ mà rate
          limit theo IP không chặn được. Xem `IChongDoMatKhau`.

          Kiểm TRƯỚC khi chạm DB: đang bị khoá thì không có lý do gì tốn thêm truy vấn, và
          đây cũng là lúc rẻ nhất để từ chối.

          Khoá đếm theo {mã trung tâm, username} chứ không theo id tài khoản, nên đếm được cả
          khi tài khoản không tồn tại — nếu chỉ đếm tài khoản có thật thì hai nhánh hành xử
          khác nhau và tạo lại đúng kênh dò mà `BamGia()` vừa bịt.
        */
        var khoaDem = IChongDoMatKhau.TaoKhoa(maTrungTam, request.Username);

        if (chongDo.DangBiKhoa(khoaDem))
            throw new AppException(MaLoi.TaiKhoanBiKhoaTam, $"Khoá tạm: {khoaDem}");

        // TENANT không phải ITenantEntity nên không bị Global Query Filter chặn — cần thiết,
        // vì lúc này chưa biết tenant nào để mà lọc.
        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.MaTrungTam == maTrungTam, ct);

        /*
          Sai mã trung tâm, sai username, sai mật khẩu → CÙNG một mã lỗi. Phân biệt sẽ cho phép
          dò xem trung tâm nào tồn tại và tài khoản nào có thật.

          **Và phải cùng cả THỜI GIAN** (22/09/2026). Cùng mã lỗi thôi là chưa đủ: bản trước
          `throw` ngay ở hai nhánh dưới mà không chạy PBKDF2, trong khi nhánh "sai mật khẩu"
          chạy ~100k vòng băm. Chênh lệch hàng chục mili-giây ấy đo được qua mạng, nên người
          dò vẫn biết mã trung tâm nào có thật và username nào tồn tại — đúng thứ mà việc dùng
          chung mã lỗi định giấu.

          Gọi `BamGia()` để hai nhánh tốn thời gian tương đương. Đợt rà soát bảo mật 22/09/2026
          xếp đây là mục 3.
        */
        if (tenant is null)
        {
            hasher.BamGia();
            chongDo.GhiNhanSai(khoaDem);
            throw new AppException(MaLoi.DangNhapThatBai, $"Không có tenant {maTrungTam}");
        }

        var taiKhoan = await db.TaiKhoans
            .IgnoreQueryFilters() // chưa có tenant trong context ở bước đăng nhập
            .FirstOrDefaultAsync(
                u => u.TenantId == tenant.Id && u.Username == request.Username, ct);

        if (taiKhoan is null)
        {
            hasher.BamGia();
            chongDo.GhiNhanSai(khoaDem);
            throw new AppException(MaLoi.DangNhapThatBai, $"Không có user {request.Username}");
        }

        if (!hasher.KiemTra(taiKhoan.PasswordHash, request.MatKhau))
        {
            chongDo.GhiNhanSai(khoaDem);
            throw new AppException(MaLoi.DangNhapThatBai, "Sai mật khẩu");
        }

        // Đăng nhập ĐÚNG ⇒ xoá bộ đếm. Thiếu dòng này thì người gõ sai vài lần rồi gõ đúng
        // vẫn mang bộ đếm cũ sang lần sau và bị khoá oan.
        chongDo.XoaDem(khoaDem);

        if (taiKhoan.TrangThai == TrangThaiNguoiDung.VoHieuHoa)
            throw new AppException(MaLoi.TaiKhoanBiVoHieuHoa);

        // KHÔNG chặn theo TrangThaiNhanSu: người đã nghỉ mà tài khoản còn hiệu lực vẫn đăng
        // nhập được (kế toán cũ vào tra sổ, giáo viên nghỉ thai sản xem lịch). Muốn chặn thì
        // vô hiệu hoá tài khoản — đó mới là cột nói về đăng nhập.

        var token = tokenService.PhatHanh(new ThongTinToken(
            tenant.Id, tenant.MaTrungTam, tenant.TenTrungTam,
            taiKhoan.NguoiDungId, taiKhoan.Id, taiKhoan.Username));

        // Lưu HASH của refresh token, không lưu token thô — người đọc được DB sẽ không mạo
        // danh được ai (cùng lý do với password_hash).
        using var _ = currentTenant.DatPhamVi(tenant.Id);

        /*
          MỘT PHIÊN MỖI TÀI KHOẢN (20/09/2026) — yêu cầu chủ sản phẩm *"chỉ cho phép 1 người
          đăng nhập tài khoản cùng lúc"*. Chốt phương án: **đẩy phiên CŨ ra**, người vừa đăng
          nhập được vào (giống Facebook/Zalo), và có hiệu lực **ngay**.

          Hai việc phải làm cùng nhau, thiếu một là hở:

          1. Ghi `PhienHienTai` = jti mới ⇒ `PhienDuyNhatMiddleware` chặn mọi access token cũ
             ngay ở request kế tiếp. Không có bước này thì phiên cũ dùng tiếp tới 60 phút (hạn
             access token) vì JWT không tra DB.
          2. Thu hồi mọi refresh token còn sống ⇒ phiên cũ không tự làm mới để sống lại. Thiếu
             bước này thì máy cũ vẫn âm thầm gia hạn và hai người dùng song song mãi.
        */
        var bayGioUtc = DateTimeOffset.UtcNow;

        var tokenCu = await db.RefreshTokens
            .Where(t => t.TaiKhoanId == taiKhoan.Id && t.ThuHoiLuc == null)
            .ToListAsync(ct);

        // Lý do BỊ ĐẨY RA (ADR-0007): phiên cũ dùng lại token này là chuyện bình thường, không
        // phải dấu hiệu bị trộm — `LamMoiTokenCommand` đọc cờ này để không thu hồi nhầm toàn bộ.
        foreach (var t in tokenCu)
        {
            t.ThuHoiLuc = bayGioUtc;
            t.LyDo = Domain.Entities.LyDoThuHoi.BiDayRa;
        }

        taiKhoan.PhienHienTai = token.Jti;

        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            TenantId = tenant.Id,
            TaiKhoanId = taiKhoan.Id,
            TokenHash = QuenMatKhau.BamToken.Bam(token.RefreshToken),
            HetHan = DateTimeOffset.UtcNow.AddDays(
                LamMoiToken.LamMoiTokenHandler.TokenService_HanRefreshNgay)
        });

        await db.SaveChangesAsync(ct);

        // Xoá cache phiên NGAY: middleware cache `phien_hien_tai` để khỏi tra DB mỗi request,
        // không xoá thì chính người vừa đăng nhập bị chặn tới khi cache hết hạn (gặp thật khi
        // kiểm chứng 20/09 — máy vừa đăng nhập nhận 401). Xem `IPhienService`.
        phienService.XoaCache(taiKhoan.Id);

        return new DangNhapResult(
            token.AccessToken, token.RefreshToken, token.HetHan, taiKhoan.PhaiDoiMatKhau);
    }
}

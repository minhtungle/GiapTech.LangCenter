using System.Security.Cryptography;
using FluentValidation;
using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Application.DangNhap.Commands.QuenMatKhau;

/// <summary>FR-02 — yêu cầu đặt lại mật khẩu qua email.</summary>
public record QuenMatKhauCommand(string MaTrungTam, string Email) : IRequest;

public class QuenMatKhauValidator : AbstractValidator<QuenMatKhauCommand>
{
    public QuenMatKhauValidator()
    {
        RuleFor(x => x.MaTrungTam).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class QuenMatKhauHandler(
    IAppDbContext db,
    IEmailSender emailSender,
    ICurrentTenant currentTenant,
    ILogger<QuenMatKhauHandler> logger)
    : IRequestHandler<QuenMatKhauCommand>
{
    /// <summary>Thời hạn ngắn: link nằm trong hộp thư càng lâu càng nhiều cơ hội bị lợi dụng.</summary>
    public static readonly TimeSpan ThoiHan = TimeSpan.FromMinutes(30);

    public async Task Handle(QuenMatKhauCommand request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        var maTrungTam = Domain.Common.MaTrungTam.ChuanHoa(request.MaTrungTam);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.MaTrungTam == maTrungTam, ct);

        // Email nằm ở NGUOI_DUNG còn token gắn với TAI_KHOAN, nên phải đi qua quan hệ 1–1.
        // Tài khoản không gắn người nào (tài khoản kỹ thuật) không có email → không đặt lại
        // mật khẩu kiểu này được, đúng như mong đợi.
        var taiKhoan = tenant is null
            ? null
            : await db.TaiKhoans
                .IgnoreQueryFilters() // chưa đăng nhập nên context chưa có tenant
                .Include(u => u.NguoiDung)
                .FirstOrDefaultAsync(
                    u => u.TenantId == tenant.Id
                         && u.NguoiDung != null && u.NguoiDung.Email == email, ct);

        // KHÔNG ném lỗi khi không tìm thấy: phản hồi phải giống hệt nhau dù email có tồn tại
        // hay không, nếu không kẻ tấn công dò được email nào đã đăng ký ở trung tâm nào.
        if (taiKhoan is null || tenant is null)
        {
            logger.LogInformation(
                "Yêu cầu quên mật khẩu cho email không khớp ({MaTrungTam})", request.MaTrungTam);
            return;
        }

        // Token thô chỉ tồn tại ở đây và trong email; DB chỉ giữ hash.
        var tokenTho = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        using var _ = currentTenant.DatPhamVi(tenant.Id);

        // Vô hiệu hoá các token cũ chưa dùng: nhiều link còn sống cùng lúc nhân rộng bề mặt
        // tấn công mà không đem lại lợi ích gì cho người dùng.
        var tokenCu = await db.TokenDatLaiMatKhaus
            .Where(t => t.TaiKhoanId == taiKhoan.Id && t.DaDungLuc == null)
            .ToListAsync(ct);

        foreach (var t in tokenCu)
            t.DaDungLuc = DateTimeOffset.UtcNow;

        db.TokenDatLaiMatKhaus.Add(new TokenDatLaiMatKhau
        {
            TenantId = tenant.Id,
            TaiKhoanId = taiKhoan.Id,
            TokenHash = BamToken.Bam(tokenTho),
            HetHan = DateTimeOffset.UtcNow.Add(ThoiHan)
        });

        await db.SaveChangesAsync(ct);

        await emailSender.GuiAsync(
            email,
            "Đặt lại mật khẩu LangCenter",
            $"""
             <p>Bạn (hoặc ai đó) đã yêu cầu đặt lại mật khẩu cho tài khoản
             <strong>{taiKhoan.Username}</strong> tại trung tâm <strong>{tenant.TenTrungTam}</strong>.</p>
             <p>Mã đặt lại: <code>{tokenTho}</code></p>
             <p>Mã có hiệu lực trong {ThoiHan.TotalMinutes:0} phút và chỉ dùng được một lần.</p>
             <p>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</p>
             """,
            ct);
    }
}

/// <summary>Băm token bằng SHA-256 — token là chuỗi ngẫu nhiên entropy cao nên không cần salt/KDF chậm.</summary>
public static class BamToken
{
    public static string Bam(string tokenTho)
        => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(tokenTho)));
}

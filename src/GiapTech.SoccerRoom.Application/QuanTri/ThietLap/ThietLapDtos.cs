using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.ThietLap;

/// <summary>
/// FR-06 — thiết lập chung của trung tâm.
///
/// PHẢI chứa đủ mọi trường mà lệnh cập nhật ghi đè — thiếu một trường thì form sửa không
/// điền lại được, và khi lưu sẽ gửi null lên, xóa mất dữ liệu người dùng chưa từng đụng tới
/// (quy tắc #1).
/// </summary>
public record ThietLapDto(
    Guid Id, string MaTrungTam, string TenTrungTam, string? TenVietTat,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    string? DiaChi = null,
    string? LienHe = null,
    // Thông tin chuyển khoản.
    string? SoTaiKhoan = null,
    string? TenNganHang = null,
    string? ChuTaiKhoan = null,
    string? AnhQrUrl = null);

public record LayThietLapQuery : IRequest<ThietLapDto>;

public class LayThietLapHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<LayThietLapQuery, ThietLapDto>
{
    public async Task<ThietLapDto> Handle(LayThietLapQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
            throw new AppException(MaLoi.ChuaXacThuc);

        // TENANT không phải ITenantEntity nên không tự lọc — phải so Id tường minh.
        return await db.Tenants
                   .Where(t => t.Id == tid)
                   .FirstOrDefaultAsync(ct)
               is { } t
            ? new ThietLapDto(
                t.Id, t.MaTrungTam, t.TenTrungTam, t.TenVietTat,
                t.LogoUrl, t.AnhBiaUrl, t.MoTa,
                t.DiaChi, t.LienHe,
                t.SoTaiKhoan, t.TenNganHang, t.ChuTaiKhoan, t.AnhQrUrl)
            : throw new KhongTimThayException($"Tenant {tid}");
    }
}

public record CapNhatThietLapCommand(
    string TenTrungTam, string? TenVietTat,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    // Mặc định null để client cũ (chưa biết các trường này) gửi lệnh cập nhật mà KHÔNG xoá
    // mất giá trị đang có — quy tắc #1.
    string? DiaChi = null,
    string? LienHe = null,
    // Bốn trường chuyển khoản, cùng quy ước null = giữ nguyên như trên.
    string? SoTaiKhoan = null,
    string? TenNganHang = null,
    string? ChuTaiKhoan = null,
    string? AnhQrUrl = null) : IRequest;

public class CapNhatThietLapValidator : AbstractValidator<CapNhatThietLapCommand>
{
    public CapNhatThietLapValidator()
    {
        RuleFor(x => x.TenTrungTam).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenVietTat).MaximumLength(50);
        RuleFor(x => x.DiaChi).MaximumLength(200);
        RuleFor(x => x.LienHe).MaximumLength(200);
        RuleFor(x => x.SoTaiKhoan).MaximumLength(50);
        RuleFor(x => x.TenNganHang).MaximumLength(100);
        RuleFor(x => x.ChuTaiKhoan).MaximumLength(200);
    }
}

public class CapNhatThietLapHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<CapNhatThietLapCommand>
{
    public async Task Handle(CapNhatThietLapCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
            throw new AppException(MaLoi.ChuaXacThuc);

        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tid, ct)
            ?? throw new KhongTimThayException($"Tenant {tid}");

        t.TenTrungTam = request.TenTrungTam.Trim();
        t.TenVietTat = request.TenVietTat;
        t.LogoUrl = request.LogoUrl;
        t.AnhBiaUrl = request.AnhBiaUrl;
        t.MoTa = request.MoTa;

        // null = client không gửi → giữ nguyên. Chuỗi rỗng = người dùng chủ động xoá → ghi
        // null. Không phân biệt hai ca này thì mỗi lần lưu thiết lập từ màn cũ sẽ âm thầm xoá
        // địa chỉ và liên hệ — đúng lỗi đã xảy ra 16/08 với ô địa chỉ.
        if (request.DiaChi is { } dc) t.DiaChi = string.IsNullOrWhiteSpace(dc) ? null : dc.Trim();
        if (request.LienHe is { } lh) t.LienHe = string.IsNullOrWhiteSpace(lh) ? null : lh.Trim();

        if (request.SoTaiKhoan is { } stk)
            t.SoTaiKhoan = string.IsNullOrWhiteSpace(stk) ? null : stk.Trim();
        if (request.TenNganHang is { } nh)
            t.TenNganHang = string.IsNullOrWhiteSpace(nh) ? null : nh.Trim();
        if (request.ChuTaiKhoan is { } ctk)
            t.ChuTaiKhoan = string.IsNullOrWhiteSpace(ctk) ? null : ctk.Trim();

        // Ảnh QR: null = client không gửi → giữ nguyên; chuỗi rỗng = người dùng xoá ảnh.
        // Cùng cách xử lý với logo và ảnh bìa.
        if (request.AnhQrUrl is { } qr)
            t.AnhQrUrl = string.IsNullOrWhiteSpace(qr) ? null : qr;

        // MaTrungTam cố tình KHÔNG cho sửa: người dùng gõ nó mỗi lần đăng nhập, đổi sẽ khóa
        // cả trung tâm ra ngoài. Muốn đổi thì cần quy trình riêng có cảnh báo rõ.
        await db.SaveChangesAsync(ct);
    }
}

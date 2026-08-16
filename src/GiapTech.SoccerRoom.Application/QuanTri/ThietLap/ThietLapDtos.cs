using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.ThietLap;

/// <summary>FR-06 — thiết lập chung của CLB.</summary>
public record ThietLapDto(
    Guid Id, string MaDoi, string TenDoi, string? TenVietTat,
    DateOnly? NgayThanhLap, string? LogoUrl, string? AnhBiaUrl, string? MoTa);

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
                   .Select(t => new ThietLapDto(
                       t.Id, t.MaDoi, t.TenDoi, t.TenVietTat,
                       t.NgayThanhLap, t.LogoUrl, t.AnhBiaUrl, t.MoTa))
                   .FirstOrDefaultAsync(ct)
               ?? throw new KhongTimThayException($"Tenant {tid}");
    }
}

public record CapNhatThietLapCommand(
    string TenDoi, string? TenVietTat, DateOnly? NgayThanhLap,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa) : IRequest;

public class CapNhatThietLapValidator : AbstractValidator<CapNhatThietLapCommand>
{
    public CapNhatThietLapValidator()
    {
        RuleFor(x => x.TenDoi).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenVietTat).MaximumLength(50);
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

        t.TenDoi = request.TenDoi.Trim();
        t.TenVietTat = request.TenVietTat;
        t.NgayThanhLap = request.NgayThanhLap;
        t.LogoUrl = request.LogoUrl;
        t.AnhBiaUrl = request.AnhBiaUrl;
        t.MoTa = request.MoTa;

        // MaDoi cố tình KHÔNG cho sửa: người dùng gõ nó mỗi lần đăng nhập, đổi sẽ khóa cả
        // CLB ra ngoài. Muốn đổi thì cần quy trình riêng có cảnh báo rõ.
        await db.SaveChangesAsync(ct);
    }
}

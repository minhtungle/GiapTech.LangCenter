using System.Text.Json;
using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.QuanTri.ThietLap;

/// <summary>FR-06 — thiết lập chung của CLB.</summary>
public record ThietLapDto(
    Guid Id, string MaDoi, string TenDoi, string? TenVietTat,
    DateOnly? NgayThanhLap, string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    /// <summary>Bộ áo đấu — trả về mảng đã tách sẵn, frontend khỏi tự parse JSON.</summary>
    List<string> MauAo);

/// <summary>
/// Đọc/ghi cột <c>mau_ao_json</c>. Tách riêng để handler đọc và handler ghi dùng chung một
/// cách hiểu về định dạng — hai nơi tự parse sẽ lệch nhau khi định dạng đổi.
/// </summary>
internal static class MauAoJson
{
    /// <summary>JSON hỏng hoặc mã lạ bị bỏ qua, không làm sập màn thiết lập.</summary>
    public static List<string> Doc(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                       .Where(MauAo.LaMaHopLe)
                       .Distinct()
                       .ToList()
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Danh sách rỗng ghi null, không ghi "[]" — để cột trống mang đúng nghĩa "chưa khai".</summary>
    public static string? Ghi(IEnumerable<string> ds)
    {
        var loc = ds.Where(MauAo.LaMaHopLe).Distinct().ToList();
        return loc.Count == 0 ? null : JsonSerializer.Serialize(loc);
    }
}

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
                t.Id, t.MaDoi, t.TenDoi, t.TenVietTat,
                t.NgayThanhLap, t.LogoUrl, t.AnhBiaUrl, t.MoTa,
                MauAoJson.Doc(t.MauAoJson))
            : throw new KhongTimThayException($"Tenant {tid}");
    }
}

public record CapNhatThietLapCommand(
    string TenDoi, string? TenVietTat, DateOnly? NgayThanhLap,
    string? LogoUrl, string? AnhBiaUrl, string? MoTa,
    List<string>? MauAo = null) : IRequest;

public class CapNhatThietLapValidator : AbstractValidator<CapNhatThietLapCommand>
{
    public CapNhatThietLapValidator()
    {
        RuleFor(x => x.TenDoi).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenVietTat).MaximumLength(50);

        // Mã lạ bị chặn tại cổng thay vì lọc âm thầm: người dùng gửi "xanhLa" mà hệ thống im
        // lặng bỏ đi thì họ tưởng đã lưu được.
        RuleForEach(x => x.MauAo)
            .Must(Domain.Common.MauAo.LaMaHopLe)
            .WithErrorCode("MAU_AO_KHONG_HOP_LE")
            .When(x => x.MauAo is not null);
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

        // null = client cũ không gửi trường này → GIỮ NGUYÊN bộ áo đang có (quy tắc #1).
        // Danh sách rỗng thì khác: người dùng chủ động bỏ hết áo, ghi null vào cột.
        if (request.MauAo is { } ds) t.MauAoJson = MauAoJson.Ghi(ds);

        // MaDoi cố tình KHÔNG cho sửa: người dùng gõ nó mỗi lần đăng nhập, đổi sẽ khóa cả
        // CLB ra ngoài. Muốn đổi thì cần quy trình riêng có cảnh báo rõ.
        await db.SaveChangesAsync(ct);
    }
}

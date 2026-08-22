using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.Common.Anh;

/// <summary>
/// Tải ảnh cho hồ sơ cầu thủ (FR-04) và CLB (FR-06).
///
/// Gộp một chỗ thay vì mỗi module tự viết: luồng giống hệt nhau (tải lên kho, xoá ảnh cũ, ghi
/// khoá vào DB), khác mỗi cột đích. Ba bản sao là ba cơ hội quên bước xoá ảnh cũ.
/// </summary>
public enum LoaiAnh
{
    /// <summary>Ảnh đại diện cầu thủ — <c>CAU_THU.anh_dai_dien</c>.</summary>
    CauThu,

    /// <summary>Logo CLB — <c>TENANT.logo_url</c>.</summary>
    Logo,

    /// <summary>Ảnh bìa CLB — <c>TENANT.anh_bia_url</c>.</summary>
    AnhBia,

    /// <summary>Mã QR chuyển khoản quỹ — <c>TENANT.anh_qr_url</c>.</summary>
    AnhQr,
}

/// <param name="DoiTuongId">Id cầu thủ. Bỏ qua với logo/ảnh bìa vì chúng thuộc về chính CLB.</param>
public record TaiAnhLenCommand(
    LoaiAnh Loai, Guid? DoiTuongId, Stream NoiDung, string LoaiNoiDung) : IRequest<string>;

public class TaiAnhLenHandler(IAppDbContext db, ILuuTruAnh luuTru, ICurrentTenant tenant)
    : IRequestHandler<TaiAnhLenCommand, string>
{
    public async Task<string> Handle(TaiAnhLenCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Đọc khoá ảnh CŨ trước khi ghi cái mới, để xoá nó sau khi ghi thành công.
        // Không dọn thì mỗi lần đổi avatar để lại một tệp mồ côi vĩnh viễn trong MinIO.
        var khoaCu = request.Loai switch
        {
            LoaiAnh.CauThu => await LayKhoaCauThu(request.DoiTuongId, ct),
            LoaiAnh.Logo => await db.Tenants.Where(t => t.Id == tenantId)
                .Select(t => t.LogoUrl).FirstOrDefaultAsync(ct),
            LoaiAnh.AnhBia => await db.Tenants.Where(t => t.Id == tenantId)
                .Select(t => t.AnhBiaUrl).FirstOrDefaultAsync(ct),
            LoaiAnh.AnhQr => await db.Tenants.Where(t => t.Id == tenantId)
                .Select(t => t.AnhQrUrl).FirstOrDefaultAsync(ct),
            // Liệt kê ĐỦ mọi nhánh thay vì `_ =>`: nhánh mặc định làm loại ảnh mới âm thầm rơi
            // vào ảnh bìa — ghi khoá QR lên `anh_bia_url` và xoá mất ảnh bìa thật.
            _ => throw new AppException("LOAI_ANH_KHONG_HO_TRO"),
        };

        var thuMuc = request.Loai switch
        {
            LoaiAnh.CauThu => "cau-thu",
            LoaiAnh.Logo => "logo",
            LoaiAnh.AnhBia => "anh-bia",
            LoaiAnh.AnhQr => "qr-chuyen-khoan",
            _ => throw new AppException("LOAI_ANH_KHONG_HO_TRO"),
        };

        var khoaMoi = await luuTru.TaiLen(request.NoiDung, request.LoaiNoiDung, thuMuc, ct);

        // Ghi khoá mới vào DB TRƯỚC khi xoá ảnh cũ: xoá trước mà ghi DB lỗi thì mất cả hai.
        switch (request.Loai)
        {
            case LoaiAnh.CauThu:
                var cauThu = await db.CauThus
                    .FirstOrDefaultAsync(c => c.Id == request.DoiTuongId, ct)
                    ?? throw new KhongTimThayException($"CauThu {request.DoiTuongId}");
                cauThu.AnhDaiDien = khoaMoi;
                break;

            case LoaiAnh.Logo:
                var t1 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                t1.LogoUrl = khoaMoi;
                break;

            case LoaiAnh.AnhBia:
                var t2 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                t2.AnhBiaUrl = khoaMoi;
                break;

            case LoaiAnh.AnhQr:
                var t3 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                t3.AnhQrUrl = khoaMoi;
                break;

            default:
                throw new AppException("LOAI_ANH_KHONG_HO_TRO");
        }

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(khoaCu)) await luuTru.Xoa(khoaCu, ct);

        return khoaMoi;
    }

    private async Task<string?> LayKhoaCauThu(Guid? id, CancellationToken ct)
    {
        if (id is not { } cauThuId) throw new AppException("THIEU_ID_CAU_THU");

        return await db.CauThus
            .Where(c => c.Id == cauThuId)
            .Select(c => c.AnhDaiDien)
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>Gỡ ảnh — đặt cột về null và xoá tệp khỏi kho.</summary>
public record XoaAnhCommand(LoaiAnh Loai, Guid? DoiTuongId) : IRequest;

public class XoaAnhHandler(IAppDbContext db, ILuuTruAnh luuTru, ICurrentTenant tenant)
    : IRequestHandler<XoaAnhCommand>
{
    public async Task Handle(XoaAnhCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        string? khoa = null;

        switch (request.Loai)
        {
            case LoaiAnh.CauThu:
                var cauThu = await db.CauThus
                    .FirstOrDefaultAsync(c => c.Id == request.DoiTuongId, ct)
                    ?? throw new KhongTimThayException($"CauThu {request.DoiTuongId}");
                khoa = cauThu.AnhDaiDien;
                cauThu.AnhDaiDien = null;
                break;

            case LoaiAnh.Logo:
                var t1 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                khoa = t1.LogoUrl;
                t1.LogoUrl = null;
                break;

            case LoaiAnh.AnhBia:
                var t2 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                khoa = t2.AnhBiaUrl;
                t2.AnhBiaUrl = null;
                break;

            case LoaiAnh.AnhQr:
                var t3 = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                    ?? throw new KhongTimThayException($"Tenant {tenantId}");
                khoa = t3.AnhQrUrl;
                t3.AnhQrUrl = null;
                break;

            default:
                throw new AppException("LOAI_ANH_KHONG_HO_TRO");
        }

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(khoa)) await luuTru.Xoa(khoa, ct);
    }
}

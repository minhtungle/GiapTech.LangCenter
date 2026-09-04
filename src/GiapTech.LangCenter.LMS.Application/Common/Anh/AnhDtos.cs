using GiapTech.LangCenter.LMS.Application.Common.Exceptions;
using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Common.Anh;

/// <summary>
/// Tải ảnh cho trung tâm (FR-06).
///
/// Gộp một chỗ thay vì mỗi module tự viết: luồng giống hệt nhau (tải lên kho, xoá ảnh cũ, ghi
/// khoá vào DB), khác mỗi cột đích. Ba bản sao là ba cơ hội quên bước xoá ảnh cũ.
/// </summary>
public enum LoaiAnh
{
    /// <summary>Logo trung tâm — <c>TENANT.logo_url</c>.</summary>
    Logo,

    /// <summary>Ảnh bìa trung tâm — <c>TENANT.anh_bia_url</c>.</summary>
    AnhBia,

    /// <summary>Mã QR chuyển khoản — <c>TENANT.anh_qr_url</c>.</summary>
    AnhQr,
}

public record TaiAnhLenCommand(
    LoaiAnh Loai, Stream NoiDung, string LoaiNoiDung) : IRequest<string>;

public class TaiAnhLenHandler(IAppDbContext db, ILuuTruAnh luuTru, ICurrentTenant tenant)
    : IRequestHandler<TaiAnhLenCommand, string>
{
    public async Task<string> Handle(TaiAnhLenCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        // Đọc khoá ảnh CŨ trước khi ghi cái mới, để xoá nó sau khi ghi thành công.
        // Không dọn thì mỗi lần đổi ảnh để lại một tệp mồ côi vĩnh viễn trong MinIO.
        var khoaCu = request.Loai switch
        {
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
            LoaiAnh.Logo => "logo",
            LoaiAnh.AnhBia => "anh-bia",
            LoaiAnh.AnhQr => "qr-chuyen-khoan",
            _ => throw new AppException("LOAI_ANH_KHONG_HO_TRO"),
        };

        var khoaMoi = await luuTru.TaiLen(request.NoiDung, request.LoaiNoiDung, thuMuc, ct);

        var tenantHienTai = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KhongTimThayException($"Tenant {tenantId}");

        // Ghi khoá mới vào DB TRƯỚC khi xoá ảnh cũ: xoá trước mà ghi DB lỗi thì mất cả hai.
        switch (request.Loai)
        {
            case LoaiAnh.Logo:
                tenantHienTai.LogoUrl = khoaMoi;
                break;

            case LoaiAnh.AnhBia:
                tenantHienTai.AnhBiaUrl = khoaMoi;
                break;

            case LoaiAnh.AnhQr:
                tenantHienTai.AnhQrUrl = khoaMoi;
                break;

            default:
                throw new AppException("LOAI_ANH_KHONG_HO_TRO");
        }

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(khoaCu)) await luuTru.Xoa(khoaCu, ct);

        return khoaMoi;
    }
}

/// <summary>Gỡ ảnh — đặt cột về null và xoá tệp khỏi kho.</summary>
public record XoaAnhCommand(LoaiAnh Loai) : IRequest;

public class XoaAnhHandler(IAppDbContext db, ILuuTruAnh luuTru, ICurrentTenant tenant)
    : IRequestHandler<XoaAnhCommand>
{
    public async Task Handle(XoaAnhCommand request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
            throw new AppException(MaLoi.ChuaXacThuc);

        var tenantHienTai = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KhongTimThayException($"Tenant {tenantId}");

        string? khoa;

        switch (request.Loai)
        {
            case LoaiAnh.Logo:
                khoa = tenantHienTai.LogoUrl;
                tenantHienTai.LogoUrl = null;
                break;

            case LoaiAnh.AnhBia:
                khoa = tenantHienTai.AnhBiaUrl;
                tenantHienTai.AnhBiaUrl = null;
                break;

            case LoaiAnh.AnhQr:
                khoa = tenantHienTai.AnhQrUrl;
                tenantHienTai.AnhQrUrl = null;
                break;

            default:
                throw new AppException("LOAI_ANH_KHONG_HO_TRO");
        }

        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(khoa)) await luuTru.Xoa(khoa, ct);
    }
}

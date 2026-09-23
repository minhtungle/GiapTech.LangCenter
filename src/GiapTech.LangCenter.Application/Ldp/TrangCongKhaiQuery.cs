using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.Ldp;

/// <summary>
/// Nội dung trang đích công khai của tenant hiện tại (FR-30) — `null` nếu chưa xuất bản.
///
/// ## Không nhận tham số nào
///
/// Tenant do `TenantMiddleware` giải từ domain (ADR-0008) hoặc từ `/t/{mã}`, rồi gán vào
/// <see cref="ICurrentTenant"/>. Người gọi **không đưa vào được gì** — đó là điểm làm endpoint
/// này an toàn hơn mọi endpoint ẩn danh khác đang có.
///
/// ## Chốt chặn quan trọng nhất
///
/// Không có tenant ⇒ trả `null` NGAY, không chạy truy vấn nào. Vì Global Query Filter có nhánh
/// `TenantIdHienTai == null` **tắt filter hoàn toàn**: chạy tiếp với tenant rỗng sẽ gom khối
/// nội dung của **mọi trung tâm** vào một trang. Middleware đã chặn ca này, nhưng đây là
/// endpoint công khai nên đáng có hai lớp — lớp thứ hai rẻ và nó canh cả những đường gọi
/// tương lai mà middleware chưa phủ.
/// </summary>
public record TrangCongKhaiQuery : IRequest<TrangCongKhaiDto?>;

public class TrangCongKhaiHandler(IAppDbContext db, ICurrentTenant tenant)
    : IRequestHandler<TrangCongKhaiQuery, TrangCongKhaiDto?>
{
    public async Task<TrangCongKhaiDto?> Handle(TrangCongKhaiQuery request, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId) return null;

        var trang = await db.TrangDiches
            .AsNoTracking()
            .Where(t => t.DaXuatBan)
            .Select(t => new
            {
                t.Id,
                t.TieuDeSeo,
                t.MoTaSeo,
                Khois = t.Khois
                    // Khối TẮT bị loại ở đây, không gửi kèm cờ `Hien=false`: gửi kèm là để lộ
                    // nội dung trung tâm cố ý giấu, chỉ cần mở DevTools là thấy.
                    .Where(k => k.Hien)
                    .OrderBy(k => k.ThuTu)
                    .Select(k => new
                    {
                        k.Id, k.Loai, k.TieuDe, k.MoTa, CoAnh = k.KhoaAnh != null,
                        k.NhanNut, k.DuongDanNut,
                        Mucs = k.Mucs.OrderBy(m => m.ThuTu)
                            .Select(m => new
                            {
                                m.Id, m.TieuDe, m.PhuDe, m.MoTa, CoAnh = m.KhoaAnh != null,
                                m.GiaNiemYet, m.DuongDan
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (trang is null) return null;

        // Tên và logo trung tâm lấy từ TENANT. `TENANT` không phải ITenantEntity nên không có
        // Query Filter — lọc theo id tường minh.
        var tt = await db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.TenTrungTam, x.MaTrungTam, CoLogo = x.LogoUrl != null })
            .FirstOrDefaultAsync(ct);

        if (tt is null) return null;

        return new TrangCongKhaiDto(
            tt.TenTrungTam,
            // Dùng endpoint logo ẩn danh sẵn có (nhận MÃ, server tự tra khoá) thay vì trả khoá
            // ảnh: khoá mang `tenantId` ở đầu.
            tt.CoLogo ? $"/api/v1/auth/logo/{tt.MaTrungTam}" : null,
            trang.TieuDeSeo,
            trang.MoTaSeo,
            trang.Khois.Select(k => new KhoiCongKhaiDto(
                k.Loai, k.TieuDe, k.MoTa,
                k.CoAnh ? $"/api/v1/ldp/anh/khoi/{k.Id}" : null,
                k.NhanNut, k.DuongDanNut,
                k.Mucs.Select(m => new MucCongKhaiDto(
                        m.TieuDe, m.PhuDe, m.MoTa,
                        m.CoAnh ? $"/api/v1/ldp/anh/muc/{m.Id}" : null,
                        m.GiaNiemYet, m.DuongDan))
                    .ToList()))
                .ToList());
    }

}

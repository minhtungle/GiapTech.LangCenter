using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Seed;

/// <summary>
/// Cấp cho nhóm "Quản trị viên" của MỌI trung tâm đã tồn tại những chức năng vừa được thêm
/// vào <see cref="ChucNang.TatCa"/>.
///
/// **Vì sao cần:** `TenantSeeder` chỉ chạy đúng một lần lúc tạo trung tâm. Thêm module mới là
/// thêm hằng vào `ChucNang`, nhưng các trung tâm tạo TRƯỚC đó không có hàng nào trong
/// `QUYEN_CHUC_NANG` cho hằng mới — nên admin của họ nhận 403 trên toàn bộ tính năng mới.
/// Triệu chứng rất khó chẩn: đăng nhập được, mọi màn cũ chạy bình thường, chỉ màn mới hỏng,
/// nên dễ bị quy oan cho frontend.
///
/// **Idempotent, chỉ THÊM không XOÁ:** admin đã cố ý bỏ bớt một ô quyền của nhóm quản trị thì
/// lần khởi động sau không được lặng lẽ cấp lại. Chỉ những cặp (chức năng, thao tác) chưa
/// từng tồn tại mới được thêm.
///
/// Chạy một lần lúc khởi động, sau khi migration đã áp xong.
/// </summary>
public class BoKhuyetQuyenQuanTri(AppDbContext db, ILogger<BoKhuyetQuyenQuanTri> logger)
{
    public async Task ChayAsync(CancellationToken ct = default)
    {
        // IgnoreQueryFilters: đây là việc của hạ tầng, chạy khi chưa có tenant nào trong ngữ
        // cảnh nên Global Query Filter sẽ lọc sạch nếu không bỏ qua nó. Một trong số ít chỗ
        // hợp lệ để dùng — xem docs/backend/multi-tenant.md.
        var nhomQuanTri = await db.Quyens
            .IgnoreQueryFilters()
            .Where(q => q.TenQuyen == NhomQuyenMacDinh.QuanTri)
            .Select(q => new { q.Id, q.TenantId })
            .ToListAsync(ct);

        if (nhomQuanTri.Count == 0) return;

        var quyenIds = nhomQuanTri.Select(q => q.Id).ToList();

        var daCo = (await db.QuyenChucNangs
                .IgnoreQueryFilters()
                .Where(qcn => quyenIds.Contains(qcn.QuyenId))
                .Select(qcn => new { qcn.QuyenId, qcn.TenChucNang, qcn.HanhDong })
                .ToListAsync(ct))
            .Select(x => (x.QuyenId, x.TenChucNang, x.HanhDong))
            .ToHashSet();

        var themMoi = 0;

        foreach (var nhom in nhomQuanTri)
        {
            foreach (var chucNang in ChucNang.TatCa)
            {
                foreach (var hanhDong in Enum.GetValues<HanhDong>())
                {
                    if (daCo.Contains((nhom.Id, chucNang, hanhDong))) continue;

                    db.QuyenChucNangs.Add(new QuyenChucNang
                    {
                        TenantId = nhom.TenantId,
                        QuyenId = nhom.Id,
                        TenChucNang = chucNang,
                        HanhDong = hanhDong
                    });
                    themMoi++;
                }
            }
        }

        if (themMoi == 0) return;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Bổ khuyết {SoQuyen} quyền cho nhóm quản trị của {SoTrungTam} trung tâm.",
            themMoi, nhomQuanTri.Count);
    }
}

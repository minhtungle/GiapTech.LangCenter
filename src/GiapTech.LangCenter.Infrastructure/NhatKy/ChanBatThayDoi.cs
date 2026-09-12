using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GiapTech.LangCenter.Infrastructure.NhatKy;

/// <summary>
/// Chụp danh sách trường đã đổi **TRƯỚC** khi EF ghi xuống DB.
///
/// Vì sao cần interceptor thay vì đọc `ChangeTracker` sau `SaveChanges`: sau khi save, EF
/// chuyển mọi entry sang `Unchanged` và đặt `OriginalValue = CurrentValue`. Đọc lúc đó trả về
/// **rỗng** — đã kiểm chứng bằng cách chạy thật: `so_ban_ghi_anh_huong = 0`, `chi_tiet = null`
/// dù vừa sửa một trường.
///
/// Bản chụp gom vào <see cref="BoDemThayDoi"/> (scoped theo request) rồi `NhatKyBehavior` đọc
/// ra sau khi handler xong.
/// </summary>
public class ChanBatThayDoi(BoDemThayDoi boDem) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Chup(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Chup(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Cắt danh sách trường ở đây. Một lệnh sinh lịch đổi 24 buổi × 4 trường = 96 mục; ghi hết
    /// thì một dòng nhật ký nặng hơn cả dữ liệu nó mô tả.
    /// </summary>
    private const int GioiHan = 40;

    private static readonly string[] TruongNhayCam =
        ["matkhau", "password", "token", "secret", "hash"];

    private void Chup(DbContext? ctx)
    {
        if (ctx is null) return;

        foreach (var entry in ctx.ChangeTracker.Entries<BaseEntity>())
        {
            // Nhật ký không tự ghi về chính nó — nếu không thì mỗi lần ghi lại sinh thêm một
            // bản ghi nữa mô tả việc ghi, và cứ thế.
            if (entry.Entity is NhatKyHeThong) continue;

            if (entry.State is not (EntityState.Added or EntityState.Modified
                or EntityState.Deleted)) continue;

            boDem.SoBanGhi++;

            var bang = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
            var id = entry.Entity.Id.ToString();

            if (entry.State == EntityState.Modified)
            {
                foreach (var p in entry.Properties)
                {
                    if (!p.IsModified) continue;

                    var ten = p.Metadata.Name;

                    // Dấu vết thời gian đổi ở MỌI lệnh sửa nên không nói lên điều gì.
                    if (ten is nameof(BaseEntity.UpdatedAt) or nameof(BaseEntity.CreatedAt))
                        continue;

                    if (LaNhayCam(ten)) continue;
                    if (boDem.Truong.Count >= GioiHan) continue;

                    boDem.Truong.Add(new TruongDaDoi(
                        bang, id, ten,
                        DoiSangChuoi(p.OriginalValue), DoiSangChuoi(p.CurrentValue)));
                }
            }
            else if (boDem.Truong.Count < GioiHan)
            {
                // Thêm/xoá: không liệt kê từng cột. Liệt kê 20 cột của một hàng mới không giúp
                // gì hơn là biết hàng đó được tạo.
                boDem.Truong.Add(new TruongDaDoi(
                    bang, id,
                    entry.State == EntityState.Added ? "(thêm)" : "(xoá)", null, null));
            }
        }
    }

    private static bool LaNhayCam(string ten)
        => TruongNhayCam.Any(x => ten.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static string? DoiSangChuoi(object? v) => v switch
    {
        null => null,
        DateTimeOffset d => d.ToString("O"),
        _ => v.ToString() is { Length: > 200 } dai ? dai[..200] + "…" : v.ToString()
    };
}

/// <summary>
/// Gom bản chụp thay đổi trong MỘT request. Scoped: mỗi request một bộ đếm riêng.
///
/// Một lệnh có thể gọi `SaveChanges` nhiều lần (ví dụ ghi điểm danh rồi chốt buổi), nên bộ đếm
/// cộng dồn thay vì ghi đè — người đọc nhật ký cần biết tổng số dòng đã đụng.
/// </summary>
public class BoDemThayDoi
{
    public List<TruongDaDoi> Truong { get; } = [];
    public int SoBanGhi { get; set; }

    public void Xoa()
    {
        Truong.Clear();
        SoBanGhi = 0;
    }
}

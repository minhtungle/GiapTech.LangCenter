using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Enums;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GiapTech.LangCenter.LMS.Infrastructure.Identity;

/// <summary>
/// Đọc quyền hiệu lực từ QUYEN_CHUC_NANG, cache ngắn hạn trong bộ nhớ.
///
/// Cache là bắt buộc chứ không phải tối ưu sớm: mỗi request được phân quyền đều tra quyền,
/// nên không cache thì mỗi lần gọi API là thêm một vòng truy vấn join 3 bảng.
/// </summary>
public class QuyenService(AppDbContext db, IMemoryCache cache) : IQuyenService
{
    /// <summary>
    /// TTL ngắn: cân bằng giữa tải DB và độ trễ khi thu hồi quyền. Mọi đường sửa quyền đều
    /// gọi XoaCache nên TTL chỉ là lưới an toàn cho trường hợp sửa dữ liệu ngoài ứng dụng.
    /// </summary>
    private static readonly TimeSpan ThoiGianSong = TimeSpan.FromMinutes(5);

    public async Task<bool> CoQuyenAsync(
        Guid tenantId, Guid taiKhoanId, string chucNang, HanhDong hanhDong,
        CancellationToken ct = default)
    {
        var quyens = await LayQuyenHieuLucAsync(tenantId, taiKhoanId, ct);
        return quyens.Contains((chucNang, hanhDong));
    }

    private async Task<HashSet<(string, HanhDong)>> LayQuyenHieuLucAsync(
        Guid tenantId, Guid taiKhoanId, CancellationToken ct)
    {
        var khoa = Khoa(tenantId, taiKhoanId);

        if (cache.TryGetValue<HashSet<(string, HanhDong)>>(khoa, out var daCo) && daCo is not null)
            return daCo;

        // Quyền hiệu lực = HỢP của mọi nhóm quyền gán cho tài khoản (không có deny ghi đè).
        var danhSach = await db.NguoiDungQuyens
            .Where(nq => nq.TaiKhoanId == taiKhoanId)
            .SelectMany(nq => nq.Quyen.ChucNangs)
            .Select(cn => new { cn.TenChucNang, cn.HanhDong })
            .ToListAsync(ct);

        var ketQua = danhSach
            .Select(x => (x.TenChucNang, x.HanhDong))
            .ToHashSet();

        cache.Set(khoa, ketQua, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ThoiGianSong
        }.AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(
            LayTokenTenant(tenantId).Token)));

        return ketQua;
    }

    public void XoaCache(Guid tenantId, Guid taiKhoanId)
        => cache.Remove(Khoa(tenantId, taiKhoanId));

    /// <summary>
    /// Xoá toàn bộ cache quyền của tenant bằng cách huỷ token gắn với mọi entry của tenant đó.
    /// Cần thiết khi sửa nội dung một nhóm quyền: không thể biết trước những tài khoản nào
    /// đang được gán nhóm đó mà không truy vấn thêm.
    /// </summary>
    public void XoaCacheToanTenant(Guid tenantId)
    {
        if (TokenTheoTenant.TryRemove(tenantId, out var cts))
            cts.Cancel();
    }

    private static string Khoa(Guid tenantId, Guid taiKhoanId) => $"quyen:{tenantId}:{taiKhoanId}";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, CancellationTokenSource>
        TokenTheoTenant = new();

    private static CancellationTokenSource LayTokenTenant(Guid tenantId)
        => TokenTheoTenant.GetOrAdd(tenantId, _ => new CancellationTokenSource());
}

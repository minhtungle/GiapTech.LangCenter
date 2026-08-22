using GiapTech.SoccerRoom.Application.Common.Exceptions;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.DuLieuMau;

public record KetQuaDonTenantTest(int DaXoa, int ConLai);

/// <summary>
/// Xoá các tenant do test E2E sinh ra, nhận diện bằng **tiền tố tên**.
///
/// Vì sao cần: mỗi test E2E tự tạo một CLB riêng (để test này không sửa dữ liệu test kia), nên
/// mỗi lần chạy cả bộ sinh ~44 CLB mới. Chúng **hiện lên trang Cộng đồng của mọi người** — đo
/// thật 20/08: sau 5 lần chạy là 242 CLB rác trên 249, trang Cộng đồng không còn dùng được để
/// test tay. Dọn tay xong lại tích lại ngay sau lần chạy kế tiếp, nên phải dọn tự động.
///
/// **Ba giới hạn cứng, cố ý không nhận tham số nào để nới:**
///
/// 1. Chỉ xoá tenant có tên bắt đầu bằng <see cref="TienToTest"/>. Không có chế độ "xoá hết" —
///    việc đó đã có <c>SeedDuLieuMauCommand(xoaDuLieuCu: true)</c> và phải chọn tường minh.
/// 2. **Dừng hẳn** nếu có lời mời bắc giữa CLB test và CLB thật. Xoá được cũng là làm mất lời
///    mời của CLB thật (quy tắc #1) — thà báo lỗi còn hơn âm thầm mất dữ liệu.
/// 3. Controller gọi nó chỉ bật ở Development.
///
/// Tiền tố phải khớp <c>frontend/e2e/tro-giup.ts</c> (<c>E2E &lt;nhãn&gt; &lt;timestamp&gt;</c>).
/// Đổi một bên mà quên bên kia thì rác lặng lẽ tích lại — <c>DonTenantTestTests</c> canh việc đó.
/// </summary>
public record DonTenantTestCommand : IRequest<KetQuaDonTenantTest>;

public class DonTenantTestHandler(IAppDbContext db)
    : IRequestHandler<DonTenantTestCommand, KetQuaDonTenantTest>
{
    /// <summary>Khớp <c>taoClb</c> trong <c>frontend/e2e/tro-giup.ts</c>.</summary>
    public const string TienToTest = "E2E ";

    public async Task<KetQuaDonTenantTest> Handle(
        DonTenantTestCommand request, CancellationToken ct)
    {
        var idTest = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.TenDoi.StartsWith(TienToTest))
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (idTest.Count == 0)
            return new KetQuaDonTenantTest(0, await db.Tenants.IgnoreQueryFilters().CountAsync(ct));

        // LoiMoiThachDau (bảng LOI_MOI_BAT_DOI) có hai FK tới TENANT — bên gửi và bên nhận —
        // và cả hai đều RESTRICT, khác 18 bảng còn lại là CASCADE. Phải xoá tay, nhưng chỉ khi
        // CẢ HAI bên đều là tenant test.
        var loiMoi = await db.LoiMoiThachDaus.IgnoreQueryFilters()
            .Where(l => idTest.Contains(l.TenantGuiId) || idTest.Contains(l.TenantNhanId))
            .ToListAsync(ct);

        var bacSangClbThat = loiMoi
            .Count(l => idTest.Contains(l.TenantGuiId) != idTest.Contains(l.TenantNhanId));

        if (bacSangClbThat > 0)
            throw new AppException("DON_TENANT_TEST_VUONG_CLB_THAT")
            {
                DuLieu = new Dictionary<string, object> { ["soLoiMoi"] = bacSangClbThat }
            };

        db.LoiMoiThachDaus.RemoveRange(loiMoi);

        var tenants = await db.Tenants.IgnoreQueryFilters()
            .Where(t => idTest.Contains(t.Id))
            .ToListAsync(ct);
        db.Tenants.RemoveRange(tenants);

        await db.SaveChangesAsync(ct);

        return new KetQuaDonTenantTest(
            tenants.Count, await db.Tenants.IgnoreQueryFilters().CountAsync(ct));
    }
}

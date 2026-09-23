using GiapTech.LangCenter.Application.Common.Exceptions;
using GiapTech.LangCenter.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Application.ChuHeThong;

/// <summary>Tóm tắt một trung tâm cho site chủ — KHÔNG có dữ liệu nghiệp vụ bên trong.</summary>
public record TrungTamTomTatDto(
    Guid Id,
    string MaTrungTam,
    string TenTrungTam,
    string? DomainQuanTri,
    string? DomainLanding,
    DateTimeOffset NgayTao);

/// <summary>
/// Danh sách mọi trung tâm (ADR-0009).
///
/// Endpoint duy nhất trong hệ thống đọc **mọi** tenant. Không lọc theo tenant vì đó chính là
/// mục đích — và `TENANT` vốn không phải `ITenantEntity` nên không có Query Filter để bỏ qua.
/// Hàng rào nằm ở `[ChiChuHeThong]` trên controller.
/// </summary>
public record DanhSachTrungTamQuery : IRequest<IReadOnlyList<TrungTamTomTatDto>>;

public class DanhSachTrungTamHandler(IAppDbContext db)
    : IRequestHandler<DanhSachTrungTamQuery, IReadOnlyList<TrungTamTomTatDto>>
{
    public async Task<IReadOnlyList<TrungTamTomTatDto>> Handle(
        DanhSachTrungTamQuery request, CancellationToken ct)
        => await db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TrungTamTomTatDto(
                t.Id, t.MaTrungTam, t.TenTrungTam,
                t.DomainQuanTri, t.DomainLanding, t.CreatedAt))
            .ToListAsync(ct);
}

/// <summary>
/// Gắn / đổi / gỡ domain của một trung tâm (ADR-0008 + ADR-0009).
///
/// `null` = gỡ domain. Gỡ là thao tác **an toàn**: trung tâm rơi về đường mã trung tâm chứ
/// không mất quyền truy cập — đúng lý do ADR-0008 giữ hai đường cùng sống.
/// </summary>
/// <param name="TrungTamId">Do controller điền từ route, không phải từ body.</param>
public record GanDomainCommand(
    Guid TrungTamId,
    string? DomainQuanTri,
    string? DomainLanding) : IRequest;

public class GanDomainHandler(IAppDbContext db, IGiaiTenantTheoDomain giaiTenant)
    : IRequestHandler<GanDomainCommand>
{
    public async Task Handle(GanDomainCommand request, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TrungTamId, ct)
            ?? throw new AppException(MaLoi.KhongTimThay, $"Tenant {request.TrungTamId}");

        var quanTriMoi = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainQuanTri);
        var landingMoi = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainLanding);

        if (quanTriMoi is not null && quanTriMoi == landingMoi)
            throw new AppException(
                MaLoi.DuLieuKhongHopLe,
                "Domain quản trị và domain landing không được trùng nhau");

        // Xoá cache của domain CŨ lẫn MỚI.
        //
        // Cũ: không xoá thì domain vừa gỡ vẫn tra ra tenant này tới 5 phút — tức là vẫn vào
        // được sau khi đã gỡ.
        // Mới: domain vừa gắn có thể đang nằm trong cache "không khớp" (TTL 30 giây), nên
        // không xoá thì người vận hành gắn xong thử ngay lại thấy 404 và tưởng mình làm sai.
        foreach (var d in new[]
                 {
                     tenant.DomainQuanTri, tenant.DomainLanding, quanTriMoi, landingMoi
                 })
        {
            if (!string.IsNullOrEmpty(d)) giaiTenant.XoaCache(d);
        }

        tenant.DomainQuanTri = quanTriMoi;
        tenant.DomainLanding = landingMoi;

        // UNIQUE ở tầng DB là thứ chặn thật khi hai người gắn cùng một domain cùng lúc
        // (quy tắc #8). Ở đây không kiểm trùng bằng `AnyAsync` rồi ghi — đó đúng là cái bẫy
        // race condition mà `DongThoiTests` canh.
        await db.SaveChangesAsync(ct);
    }

}

/// <summary>
/// Tạo trung tâm mới từ site chủ (ADR-0009) — thay endpoint tự đăng ký ẩn danh (nợ N3).
///
/// Dùng lại `ITenantSeeder` chứ không viết logic riêng: seeder đã lo mã 7 ký tự, tài khoản
/// admin, nhóm quyền "Quản trị viên" đầy đủ, và **mật khẩu ngẫu nhiên bằng CSPRNG**. Viết lại
/// là hai đường tạo tenant trôi khỏi nhau — đúng loại lỗi khiến trước 22/09/2026 mọi trung tâm
/// mới đều có `admin`/`123456`.
///
/// Domain gắn ngay lúc tạo nếu người dùng nhập; bỏ trống cũng được và gắn sau bằng
/// <see cref="GanDomainCommand"/>. Không bắt buộc vì DNS thường chưa trỏ xong lúc tạo.
/// </summary>
public record TaoTrungTamCommand(
    string TenTrungTam,
    string? DomainQuanTri,
    string? DomainLanding) : IRequest<TrungTamVuaTaoDto>;

/// <param name="MatKhauAdmin">
/// Mật khẩu thô, **chỉ trả đúng một lần này**. Server chỉ giữ bản băm — xem `TenantMoi`.
/// </param>
public record TrungTamVuaTaoDto(
    Guid Id,
    string MaTrungTam,
    string TenTrungTam,
    string Username,
    string MatKhauAdmin);

public class TaoTrungTamHandler(
    IAppDbContext db, ITenantSeeder seeder, IGiaiTenantTheoDomain giaiTenant)
    : IRequestHandler<TaoTrungTamCommand, TrungTamVuaTaoDto>
{
    public async Task<TrungTamVuaTaoDto> Handle(TaoTrungTamCommand request, CancellationToken ct)
    {
        var ten = request.TenTrungTam?.Trim();
        if (string.IsNullOrWhiteSpace(ten))
            throw new AppException(MaLoi.DuLieuKhongHopLe, "Tên trung tâm không được để trống");

        var quanTri = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainQuanTri);
        var landing = Domain.Common.DomainTrungTam.ChuanHoa(request.DomainLanding);

        if (quanTri is not null && quanTri == landing)
            throw new AppException(
                MaLoi.DuLieuKhongHopLe,
                "Domain quản trị và domain landing không được trùng nhau");

        var moi = await seeder.TaoTenantMoiAsync(ten, matKhauAdmin: null, ct);

        if (quanTri is not null || landing is not null)
        {
            var tenant = await db.Tenants.FirstAsync(t => t.Id == moi.Tenant.Id, ct);
            tenant.DomainQuanTri = quanTri;
            tenant.DomainLanding = landing;

            // Xoá cache "domain này không khớp ai" (TTL 30 giây): không xoá thì người vận hành
            // vừa tạo xong thử ngay sẽ thấy 404 và tưởng mình nhập sai.
            if (quanTri is not null) giaiTenant.XoaCache(quanTri);
            if (landing is not null) giaiTenant.XoaCache(landing);

            await db.SaveChangesAsync(ct);
        }

        return new TrungTamVuaTaoDto(
            moi.Tenant.Id, moi.Tenant.MaTrungTam, moi.Tenant.TenTrungTam,
            ITenantSeeder.UsernameAdmin, moi.MatKhauAdmin);
    }
}

/// <summary>
/// Xoá các trung tâm do test E2E sinh ra (nợ N11).
///
/// ## Vì sao endpoint này tồn tại, và vì sao nó hẹp đến mức này
///
/// Mỗi lượt chạy E2E sinh ~40 trung tâm rác. Đã phải dọn tay ít nhất **năm lần** (12/09,
/// 17/09, và ba lần trong ngày 23/09) — repo có tới 5 script `.sql` dọn rác, dấu hiệu rõ của
/// việc chữa triệu chứng lặp lại. `globalTeardown` của Playwright gọi endpoint này.
///
/// **Bốn chốt chặn, cố ý chồng lên nhau** — đây là endpoint XOÁ HÀNG LOẠT, loại nguy hiểm nhất:
///
/// 1. Chỉ xoá trung tâm có tên bắt đầu đúng <see cref="TienToE2E"/>. Trung tâm thật không bao
///    giờ mang tên đó.
/// 2. Chỉ chạy khi <c>CHO_DON_E2E=true</c>. **Mặc định TẮT**, y như `CHO_TU_DANG_KY`. Cờ kiểm
///    ở tầng API (`TinhNang.ChoDonE2E`) chứ không ở đây — `Application` không phụ thuộc
///    `IConfiguration` (quy tắc #10), và cờ tắt thì controller trả 404 nên endpoint coi như
///    không tồn tại.
/// 3. Gác bằng `[ChiChuHeThong]` — cần token chủ hệ thống, không phải ai cũng gọi được.
/// 4. Trả về SỐ LƯỢNG đã xoá để teardown ghi log; xoá nhầm thì con số bất thường là dấu hiệu.
///
/// Không dùng `TRUNCATE` hay xoá theo ngày tạo: hai cách đó không phân biệt được trung tâm
/// thật với rác, và một lần chạy nhầm trên production là mất hết.
/// </summary>
public record DonTrungTamE2ECommand : IRequest<int>;

public class DonTrungTamE2EHandler(IDonTenantE2E don)
    : IRequestHandler<DonTrungTamE2ECommand, int>
{
    /// <summary>Tiền tố tên mà `frontend/e2e/tro-giup.ts` đặt cho mọi trung tâm test.</summary>
    public const string TienToE2E = "E2E ";

    /// <summary>
    /// Giao việc xoá cho `IDonTenantE2E` (Infrastructure) chứ không tự `RemoveRange`.
    ///
    /// Bản đầu dùng Cascade của EF và chết ngay lần chạy E2E đầu tiên:
    /// `violates foreign key constraint fk_lop_hoc_nguoi_dungs_giao_vien_chinh_id`.
    /// `LOP_HOC.giao_vien_chinh_id` là RESTRICT nên xoá `NGUOI_DUNG` trước `LOP_HOC` là chết,
    /// và EF không sắp được thứ tự cho đồ thị phụ thuộc này.
    /// </summary>
    public Task<int> Handle(DonTrungTamE2ECommand request, CancellationToken ct)
        => don.XoaAsync(TienToE2E, ct);
}

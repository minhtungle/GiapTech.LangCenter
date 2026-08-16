using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Entities;
using GiapTech.SoccerRoom.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.UnitTests.MultiTenancy;

/// <summary>
/// Kiểm chứng cách ly dữ liệu giữa các tenant — quy tắc bất di bất dịch #2.
/// Rò rỉ dữ liệu chéo CLB là lỗi nghiêm trọng nhất hệ thống này có thể mắc,
/// nên nó phải có test chứ không chỉ có tài liệu. Xem docs/backend/multi-tenant.md.
/// </summary>
public class CachLyTenantTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>ICurrentTenant giả, đổi tenant được giữa các lần truy vấn.</summary>
    private sealed class TenantGia : ICurrentTenant
    {
        public Guid? TenantId { get; set; }
        public IDisposable DatPhamVi(Guid tenantId)
        {
            var truoc = TenantId;
            TenantId = tenantId;
            return new Khoi(() => TenantId = truoc);
        }
        private sealed class Khoi(Action f) : IDisposable { public void Dispose() => f(); }
    }

    private static AppDbContext TaoContext(TenantGia tenant, string tenDb)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(tenDb)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, tenant);
    }

    // Mỗi tenant dùng một context riêng — phản ánh đúng runtime, nơi DbContext đăng ký scoped
    // nên mỗi HTTP request nhận một instance mới cùng một ICurrentTenant đã resolve sẵn.

    [Fact]
    public void Query_filter_chi_tra_ve_du_lieu_cua_tenant_dang_dang_nhap()
    {
        const string db = nameof(Query_filter_chi_tra_ve_du_lieu_cua_tenant_dang_dang_nhap);

        using (var ctxA = TaoContext(new TenantGia { TenantId = TenantA }, db))
        {
            ctxA.CauThus.Add(new CauThu { HoTen = "Cầu thủ của CLB A" });
            ctxA.SaveChanges();
        }

        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            ctxB.CauThus.Add(new CauThu { HoTen = "Cầu thủ của CLB B" });
            ctxB.SaveChanges();
        }

        // Đứng ở tenant B thì chỉ thấy dữ liệu của B.
        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            var cuaB = ctxB.CauThus.ToList();
            Assert.Single(cuaB);
            Assert.Equal("Cầu thủ của CLB B", cuaB[0].HoTen);
        }

        // Đứng ở tenant A thì chỉ thấy dữ liệu của A.
        using (var ctxA = TaoContext(new TenantGia { TenantId = TenantA }, db))
        {
            var cuaA = ctxA.CauThus.ToList();
            Assert.Single(cuaA);
            Assert.Equal("Cầu thủ của CLB A", cuaA[0].HoTen);
        }
    }

    [Fact]
    public void Khong_doc_duoc_ban_ghi_tenant_khac_ke_ca_khi_biet_dung_id()
    {
        const string db = nameof(Khong_doc_duoc_ban_ghi_tenant_khac_ke_ca_khi_biet_dung_id);

        Guid idCuaA;
        using (var ctxA = TaoContext(new TenantGia { TenantId = TenantA }, db))
        {
            var cauThuA = new CauThu { HoTen = "Bí mật của CLB A" };
            ctxA.CauThus.Add(cauThuA);
            ctxA.SaveChanges();
            idCuaA = cauThuA.Id;
        }

        // Kẻ tấn công biết đúng Id nhưng đang đăng nhập tenant B.
        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            Assert.Null(ctxB.CauThus.FirstOrDefault(x => x.Id == idCuaA));
        }
    }

    [Fact]
    public void SaveChanges_tu_gan_tenant_id_khi_them_moi()
    {
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(SaveChanges_tu_gan_tenant_id_khi_them_moi));

        // Cố tình KHÔNG gán TenantId — tầng Application không phải nhớ việc này.
        var cauThu = new CauThu { HoTen = "Không gán tenant thủ công" };
        db.CauThus.Add(cauThu);
        db.SaveChanges();

        Assert.Equal(TenantA, cauThu.TenantId);
        Assert.NotEqual(default, cauThu.NgayTao);
    }

    [Fact]
    public void Moi_entity_nghiep_vu_deu_duoc_ap_query_filter()
    {
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(Moi_entity_nghiep_vu_deu_duoc_ap_query_filter));

        var thieuFilter = db.Model.GetEntityTypes()
            .Where(e => typeof(Domain.Common.ITenantEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(
            thieuFilter.Count == 0,
            $"Entity thiếu Global Query Filter: {string.Join(", ", thieuFilter)}. " +
            "Mọi ITenantEntity phải được lọc tự động — xem docs/backend/multi-tenant.md.");
    }

    [Fact]
    public void Bang_con_cung_mang_tenant_id_de_truy_van_truc_tiep_van_duoc_loc()
    {
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(Bang_con_cung_mang_tenant_id_de_truy_van_truc_tiep_van_duoc_loc));

        // Quyết định thiết kế: 7 bảng chi tiết mang tenant_id riêng thay vì join lên bảng cha.
        string[] bangCon =
        [
            nameof(VoteMvp), nameof(DoiHinhTranDau), nameof(DongGopQuy),
            nameof(DanhGiaCauThu), nameof(SoDoChienThuat),
            nameof(QuyenChucNang), nameof(NguoiDungQuyen)
        ];

        foreach (var ten in bangCon)
        {
            var entity = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == ten);

            Assert.True(
                entity.FindProperty(nameof(Domain.Common.ITenantEntity.TenantId)) is not null,
                $"{ten} thiếu cột tenant_id — truy vấn trực tiếp bảng này sẽ không được lọc.");

            Assert.True(
                entity.GetQueryFilter() is not null,
                $"{ten} thiếu Global Query Filter.");
        }
    }
}

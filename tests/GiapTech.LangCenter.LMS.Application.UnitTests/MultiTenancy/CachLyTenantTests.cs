using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Domain.Entities;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.UnitTests.MultiTenancy;

/// <summary>
/// Kiểm chứng cách ly dữ liệu giữa các tenant — quy tắc bất di bất dịch #2.
/// Rò rỉ dữ liệu chéo trung tâm là lỗi nghiêm trọng nhất hệ thống này có thể mắc,
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
            ctxA.Quyens.Add(new Quyen { TenQuyen = "Nhóm quyền của trung tâm A" });
            ctxA.SaveChanges();
        }

        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            ctxB.Quyens.Add(new Quyen { TenQuyen = "Nhóm quyền của trung tâm B" });
            ctxB.SaveChanges();
        }

        // Đứng ở tenant B thì chỉ thấy dữ liệu của B.
        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            var cuaB = ctxB.Quyens.ToList();
            Assert.Single(cuaB);
            Assert.Equal("Nhóm quyền của trung tâm B", cuaB[0].TenQuyen);
        }

        // Đứng ở tenant A thì chỉ thấy dữ liệu của A.
        using (var ctxA = TaoContext(new TenantGia { TenantId = TenantA }, db))
        {
            var cuaA = ctxA.Quyens.ToList();
            Assert.Single(cuaA);
            Assert.Equal("Nhóm quyền của trung tâm A", cuaA[0].TenQuyen);
        }
    }

    [Fact]
    public void Khong_doc_duoc_ban_ghi_tenant_khac_ke_ca_khi_biet_dung_id()
    {
        const string db = nameof(Khong_doc_duoc_ban_ghi_tenant_khac_ke_ca_khi_biet_dung_id);

        Guid idCuaA;
        using (var ctxA = TaoContext(new TenantGia { TenantId = TenantA }, db))
        {
            var quyenA = new Quyen { TenQuyen = "Bí mật của trung tâm A" };
            ctxA.Quyens.Add(quyenA);
            ctxA.SaveChanges();
            idCuaA = quyenA.Id;
        }

        // Kẻ tấn công biết đúng Id nhưng đang đăng nhập tenant B.
        using (var ctxB = TaoContext(new TenantGia { TenantId = TenantB }, db))
        {
            Assert.Null(ctxB.Quyens.FirstOrDefault(x => x.Id == idCuaA));
        }
    }

    [Fact]
    public void SaveChanges_tu_gan_tenant_id_khi_them_moi()
    {
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(SaveChanges_tu_gan_tenant_id_khi_them_moi));

        // Cố tình KHÔNG gán TenantId — tầng Application không phải nhớ việc này.
        var quyen = new Quyen { TenQuyen = "Không gán tenant thủ công" };
        db.Quyens.Add(quyen);
        db.SaveChanges();

        Assert.Equal(TenantA, quyen.TenantId);
        Assert.NotEqual(default, quyen.NgayTao);
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

    /// <summary>
    /// Danh sách entity CỐ Ý không có Global Query Filter, kèm lý do.
    ///
    /// Mỗi tên ở đây là một chỗ Query Filter KHÔNG bảo vệ, tức là chỗ phải tự lọc bằng tay và
    /// dễ rò rỉ dữ liệu chéo trung tâm nhất. Danh sách phải ngắn và mỗi mục phải giải thích được.
    /// </summary>
    private static readonly Dictionary<string, string> NgoaiLeKhongLoc = new()
    {
        [nameof(Tenant)] = "Bảng ĐỊNH NGHĨA tenant, không thuộc tenant nào.",
        [nameof(RefreshToken)] = "Tra theo token trước khi biết tenant nào — xem XacThucNangCao.",
        [nameof(TokenDatLaiMatKhau)] = "Quên mật khẩu: chưa đăng nhập nên chưa có tenant.",
    };

    [Fact]
    public void Khong_co_entity_nao_am_tham_thoat_khoi_query_filter()
    {
        // Chiều ngược của test trên. Test kia hỏi "ITenantEntity có bị lọc không"; test này hỏi
        // "cái KHÔNG bị lọc có phải ngoại lệ có chủ ý không".
        //
        // Cần nó vì thêm một entity không kế thừa TenantEntity là đủ để nó nằm ngoài mọi lớp
        // bảo vệ tự động, mà không test nào kêu. Ai thêm entity mới sẽ phải dừng lại ở đây và
        // viết ra lý do — hoặc nhận ra là mình quên kế thừa TenantEntity.
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(Khong_co_entity_nao_am_tham_thoat_khoi_query_filter));

        var khongLoc = db.Model.GetEntityTypes()
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .Where(ten => !NgoaiLeKhongLoc.ContainsKey(ten))
            .ToList();

        Assert.True(
            khongLoc.Count == 0,
            $"Entity không có Query Filter mà chưa khai lý do: {string.Join(", ", khongLoc)}. " +
            "Nếu nó là dữ liệu của một trung tâm → cho kế thừa TenantEntity. Nếu cố ý nằm ngoài → " +
            "thêm vào NgoaiLeKhongLoc kèm lý do, và viết test canh việc tự lọc bằng tay.");
    }

    [Fact]
    public void Bang_con_cung_mang_tenant_id_de_truy_van_truc_tiep_van_duoc_loc()
    {
        var tenant = new TenantGia { TenantId = TenantA };
        var db = TaoContext(tenant, nameof(Bang_con_cung_mang_tenant_id_de_truy_van_truc_tiep_van_duoc_loc));

        // Quyết định thiết kế: bảng chi tiết mang tenant_id riêng thay vì join lên bảng cha.
        string[] bangCon =
        [
            nameof(QuyenChucNang), nameof(NguoiDungQuyen),
            nameof(LopHocHocVien), nameof(LopHocTroGiang)
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

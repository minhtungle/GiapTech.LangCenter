using System.Linq.Expressions;
using System.Reflection;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Domain.Common;
using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.Infrastructure.Persistence;

/// <summary>
/// DbContext của ứng dụng. Chịu trách nhiệm cách ly dữ liệu giữa các tenant
/// — xem docs/backend/multi-tenant.md.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<HoSoGiaoVien> HoSoGiaoViens => Set<HoSoGiaoVien>();
    public DbSet<HoSoHocVien> HoSoHocViens => Set<HoSoHocVien>();
    public DbSet<HoSoNhanVien> HoSoNhanViens => Set<HoSoNhanVien>();
    public DbSet<PhongBan> PhongBans => Set<PhongBan>();

    public DbSet<Quyen> Quyens => Set<Quyen>();
    public DbSet<QuyenChucNang> QuyenChucNangs => Set<QuyenChucNang>();
    public DbSet<NguoiDungQuyen> NguoiDungQuyens => Set<NguoiDungQuyen>();

    public DbSet<LopHoc> LopHocs => Set<LopHoc>();
    public DbSet<LopHocHocVien> LopHocHocViens => Set<LopHocHocVien>();
    public DbSet<LopHocTroGiang> LopHocTroGiangs => Set<LopHocTroGiang>();

    public DbSet<BuoiHoc> BuoiHocs => Set<BuoiHoc>();
    public DbSet<DiemDanh> DiemDanhs => Set<DiemDanh>();
    public DbSet<NhanXetBuoiHoc> NhanXetBuoiHocs => Set<NhanXetBuoiHoc>();

    public DbSet<TepDinhKem> TepDinhKems => Set<TepDinhKem>();
    public DbSet<BaiTap> BaiTaps => Set<BaiTap>();
    public DbSet<BaiNop> BaiNops => Set<BaiNop>();
    public DbSet<BaiKiemTra> BaiKiemTras => Set<BaiKiemTra>();
    public DbSet<BaiLam> BaiLams => Set<BaiLam>();
    public DbSet<TaiLieu> TaiLieus => Set<TaiLieu>();
    public DbSet<TaiLieuLopHoc> TaiLieuLopHocs => Set<TaiLieuLopHoc>();

    public DbSet<KhoanThuHocPhi> KhoanThuHocPhis => Set<KhoanThuHocPhi>();

    // CRM (FR-17 → FR-19)
    public DbSet<KhachHang> KhachHangs => Set<KhachHang>();
    public DbSet<KhoaHoc> KhoaHocs => Set<KhoaHoc>();
    public DbSet<SanPham> SanPhams => Set<SanPham>();
    public DbSet<DangKyKhoaHoc> DangKyKhoaHocs => Set<DangKyKhoaHoc>();
    public DbSet<LichSuChamSoc> LichSuChamSocs => Set<LichSuChamSoc>();
    public DbSet<ThuTienDangKy> ThuTienDangKys => Set<ThuTienDangKy>();
    public DbSet<YeuCauXepLop> YeuCauXepLops => Set<YeuCauXepLop>();

    public DbSet<NhatKyHeThong> NhatKyHeThongs => Set<NhatKyHeThong>();


    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TokenDatLaiMatKhau> TokenDatLaiMatKhaus => Set<TokenDatLaiMatKhau>();

    /// <summary>
    /// Tenant của context này, đọc bởi Global Query Filter.
    ///
    /// PHẢI là property của chính DbContext, KHÔNG được để filter trỏ thẳng vào object
    /// ICurrentTenant bên ngoài. Lý do: EF cache model và dùng chung cho mọi context có cùng
    /// options. Với tham chiếu ra object ngoài, model bị "nướng cứng" vào instance đầu tiên,
    /// nên context của tenant B đọc tenant của A và THẤY DỮ LIỆU CỦA A — lỗi im lặng, không
    /// exception, chỉ trả về dữ liệu sai. Còn khi filter trỏ vào DbContext, EF nhận ra và
    /// thay bằng instance đang chạy ở mỗi truy vấn.
    ///
    /// Đã kiểm chứng cả hai chiều: đổi về Expression.Constant(currentTenant) làm
    /// CachLyTenantTests đỏ 2 test; đổi lại thì xanh.
    /// </summary>
    public Guid? TenantIdHienTai => currentTenant.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ChuanHoaThoiGianVeUtc(modelBuilder);
        ApDungQueryFilterTheoTenant(modelBuilder);
    }

    /// <summary>
    /// Chuẩn hoá mọi <see cref="DateTimeOffset"/> về UTC khi ghi xuống DB.
    ///
    /// PostgreSQL `timestamptz` CHỈ nhận offset 0; client gửi `+07:00` sẽ làm Npgsql ném
    /// ArgumentException và cả request hỏng với lỗi 500 khó hiểu. Chuyển đổi tập trung ở đây
    /// thay vì bắt từng handler nhớ gọi `.ToUniversalTime()` — quên một chỗ là lỗi lại xuất hiện.
    ///
    /// Giá trị thời điểm không đổi, chỉ đổi cách biểu diễn; client tự hiển thị theo giờ địa phương.
    /// </summary>
    private static void ChuanHoaThoiGianVeUtc(ModelBuilder modelBuilder)
    {
        var boChuyenDoi = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
            .ValueConverter<DateTimeOffset, DateTimeOffset>(
                v => v.ToUniversalTime(),
                v => v.ToUniversalTime());

        var boChuyenDoiNullable = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
            .ValueConverter<DateTimeOffset?, DateTimeOffset?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? v.Value.ToUniversalTime() : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(boChuyenDoi);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(boChuyenDoiNullable);
            }
        }
    }

    /// <summary>
    /// TẦNG PHÒNG VỆ 1 — áp Global Query Filter cho MỌI entity cài <see cref="ITenantEntity"/>.
    ///
    /// Duyệt toàn bộ model bằng reflection thay vì khai báo thủ công từng entity: entity mới
    /// được bảo vệ TỰ ĐỘNG. Khai báo tay thì chỉ cần một lần quên là rò rỉ dữ liệu chéo trung tâm.
    ///
    /// Filter đọc currentTenant.TenantId qua closure nên đánh giá lại mỗi truy vấn,
    /// không bị "đóng băng" giá trị lúc dựng model.
    /// </summary>
    private void ApDungQueryFilterTheoTenant(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // e => this.TenantIdHienTai == null || e.TenantId == this.TenantIdHienTai
            //
            // Đọc qua property của CHÍNH context (xem chú thích ở TenantIdHienTai) — không
            // dùng Expression.Constant(currentTenant), vì model được cache và dùng chung nên
            // object bị "nướng cứng" vào context đầu tiên.
            //
            // Nhánh "TenantId == null" cho phép hạ tầng (migration, seeder cấp hệ thống) chạy
            // khi chưa có tenant. Middleware bắt buộc mọi endpoint nghiệp vụ phải có tenant,
            // nên nhánh này không mở đường cho request thường đọc chéo trung tâm.
            var thamSo = Expression.Parameter(entityType.ClrType, "e");

            var tenantHienTai = Expression.Property(
                Expression.Constant(this), nameof(TenantIdHienTai));

            var khongCoTenant = Expression.Equal(
                tenantHienTai, Expression.Constant(null, typeof(Guid?)));

            var khopTenant = Expression.Equal(
                Expression.Convert(
                    Expression.Property(thamSo, nameof(ITenantEntity.TenantId)), typeof(Guid?)),
                tenantHienTai);

            var than = Expression.OrElse(khongCoTenant, khopTenant);

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(than, thamSo));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GanTenantVaDauVetThoiGian();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        GanTenantVaDauVetThoiGian();
        return base.SaveChanges();
    }

    /// <summary>
    /// TẦNG PHÒNG VỆ 2 — tự gán tenant_id khi thêm mới.
    ///
    /// Để tầng Application tự gán thì sớm muộn cũng có chỗ quên; gán tập trung ở đây khiến
    /// việc quên trở nên bất khả thi.
    /// </summary>
    private void GanTenantVaDauVetThoiGian()
    {
        var bayGio = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.NgayTao = bayGio;
                    if (entry.Entity is ITenantEntity moi && moi.TenantId == Guid.Empty
                                                          && currentTenant.TenantId is { } tid)
                    {
                        moi.TenantId = tid;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.NgayCapNhat = bayGio;
                    // Không cho đổi tenant của bản ghi đã tồn tại — đó là chuyển dữ liệu sang trung tâm khác.
                    if (entry.Entity is ITenantEntity)
                        entry.Property(nameof(ITenantEntity.TenantId)).IsModified = false;
                    break;
            }
        }
    }
}

using System.Linq.Expressions;
using System.Reflection;
using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Domain.Common;
using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Infrastructure.Persistence;

/// <summary>
/// DbContext của ứng dụng. Chịu trách nhiệm cách ly dữ liệu giữa các tenant
/// — xem docs/backend/multi-tenant.md.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<CauThu> CauThus => Set<CauThu>();

    public DbSet<Quyen> Quyens => Set<Quyen>();
    public DbSet<QuyenChucNang> QuyenChucNangs => Set<QuyenChucNang>();
    public DbSet<NguoiDungQuyen> NguoiDungQuyens => Set<NguoiDungQuyen>();

    public DbSet<DoiThu> DoiThus => Set<DoiThu>();
    public DbSet<LoiMoiDoiThu> LoiMoiDoiThus => Set<LoiMoiDoiThu>();
    public DbSet<TranDau> TranDaus => Set<TranDau>();
    public DbSet<DoiHinhTranDau> DoiHinhTranDaus => Set<DoiHinhTranDau>();
    public DbSet<SoDoChienThuat> SoDoChienThuats => Set<SoDoChienThuat>();
    public DbSet<MauDoiHinh> MauDoiHinhs => Set<MauDoiHinh>();
    public DbSet<VideoTran> VideoTrans => Set<VideoTran>();
    public DbSet<LoiMoiThamGia> LoiMoiThamGias => Set<LoiMoiThamGia>();
    public DbSet<PhanHoiThamGia> PhanHoiThamGias => Set<PhanHoiThamGia>();
    public DbSet<DanhGiaCauThu> DanhGiaCauThus => Set<DanhGiaCauThu>();
    public DbSet<VoteMvp> VoteMvps => Set<VoteMvp>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TokenDatLaiMatKhau> TokenDatLaiMatKhaus => Set<TokenDatLaiMatKhau>();

    public DbSet<Quy> Quys => Set<Quy>();
    public DbSet<DongGopQuy> DongGopQuys => Set<DongGopQuy>();
    public DbSet<KhoanChi> KhoanChis => Set<KhoanChi>();

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
    /// được bảo vệ TỰ ĐỘNG. Khai báo tay thì chỉ cần một lần quên là rò rỉ dữ liệu chéo CLB.
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
            // nên nhánh này không mở đường cho request thường đọc chéo CLB.
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
                    // Không cho đổi tenant của bản ghi đã tồn tại — đó là chuyển dữ liệu sang CLB khác.
                    if (entry.Entity is ITenantEntity)
                        entry.Property(nameof(ITenantEntity.TenantId)).IsModified = false;
                    break;
            }
        }
    }
}

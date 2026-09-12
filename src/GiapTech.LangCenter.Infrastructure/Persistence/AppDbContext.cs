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
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser)
    : DbContext(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<TaiKhoan> TaiKhoans => Set<TaiKhoan>();
    public DbSet<HoSoGiaoVien> HoSoGiaoViens => Set<HoSoGiaoVien>();
    public DbSet<HoSoHocVien> HoSoHocViens => Set<HoSoHocVien>();
    public DbSet<HoSoNhanVien> HoSoNhanViens => Set<HoSoNhanVien>();
    public DbSet<PhongBan> PhongBans => Set<PhongBan>();
    public DbSet<ChucVu> ChucVus => Set<ChucVu>();
    public DbSet<LienKetMxh> LienKetMxhs => Set<LienKetMxh>();

    public DbSet<Quyen> Quyens => Set<Quyen>();
    public DbSet<QuyenChucNang> QuyenChucNangs => Set<QuyenChucNang>();
    public DbSet<NguoiDungQuyen> NguoiDungQuyens => Set<NguoiDungQuyen>();

    public DbSet<LopHoc> LopHocs => Set<LopHoc>();
    public DbSet<LopHocHocVien> LopHocHocViens => Set<LopHocHocVien>();
    public DbSet<LopHocTroGiang> LopHocTroGiangs => Set<LopHocTroGiang>();
    public DbSet<LopHocKhoaHoc> LopHocKhoaHocs => Set<LopHocKhoaHoc>();

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
        ApDungKhoaNgoaiChoCotAudit(modelBuilder);
        ApDungQueryFilterTheoTenant(modelBuilder);
    }

    /// <summary>
    /// Nối `CreatedById` / `UpdatedById` về `NGUOI_DUNG` bằng **khoá ngoại thật** cho MỌI entity.
    ///
    /// Bản đầu (12/09/2026) chỉ khai hai cột `Guid?` trong <see cref="BaseEntity"/> và để chú
    /// thích nói rằng chúng "trỏ `PERSON.id`" — nhưng **không gì ép điều đó**. Một cột `uuid`
    /// không ràng buộc thì chứa được GUID rác, hoặc trỏ người đã bị xoá, và câu hỏi "ai tạo bản
    /// ghi này" trả về một id không join được. Dấu vết audit sai còn tệ hơn không có dấu vết:
    /// người đọc tin vào nó.
    ///
    /// `SET NULL` khi người bị xoá — cùng hành vi với `NguoiTaoId` cũ mà hai cột này thay thế.
    /// Không dùng `CASCADE`: xoá một người không được kéo theo mọi bản ghi họ từng tạo.
    ///
    /// `NGUOI_DUNG` **tự tham chiếu** — người tạo ra một người cũng là một người. Không bỏ qua
    /// nó (ai tạo tài khoản này là câu hỏi chính đáng), nhưng phải khai hai quan hệ TÁCH RỜI:
    /// để mặc định thì EF thấy hai navigation cùng trỏ `NguoiDung` và tưởng chúng là hai đầu
    /// của MỘT quan hệ 1-1, rồi ném "dependent side could not be determined".
    /// Seeder tạo người đầu tiên khi chưa có ai để trỏ tới — `CreatedById` null, ca hợp lệ.
    /// </summary>
    private static void ApDungKhoaNgoaiChoCotAudit(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) continue;

            // `HasOne(typeof(NguoiDung))` KHÔNG dùng được ở đây: nó không nói rõ quan hệ này
            // đi qua property nào, nên EF tạo cột bóng `CreatedById1` bên cạnh cột thật —
            // 108 cột rác trong snapshot, phát hiện khi sinh migration lần đầu (13/09/2026).
            // `HasOne` theo TÊN NAVIGATION thì EF ghép đúng vào `CreatedById` sẵn có.
            var e = modelBuilder.Entity(entityType.ClrType);

            e.HasOne(nameof(BaseEntity.CreatedBy))
                .WithMany()
                .HasForeignKey(nameof(BaseEntity.CreatedById))
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(nameof(BaseEntity.UpdatedBy))
                .WithMany()
                .HasForeignKey(nameof(BaseEntity.UpdatedById))
                .OnDelete(DeleteBehavior.SetNull);
        }
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
        GanTenantVaDauVetAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        GanTenantVaDauVetAudit();
        return base.SaveChanges();
    }

    /// <summary>
    /// TẦNG PHÒNG VỆ 2 — tự gán tenant_id và **bốn cột audit** khi ghi (ADR-0006).
    ///
    /// Để tầng Application tự gán thì sớm muộn cũng có chỗ quên; gán tập trung ở đây khiến
    /// việc quên trở nên bất khả thi.
    /// </summary>
    private void GanTenantVaDauVetAudit()
    {
        var bayGio = DateTimeOffset.UtcNow;

        // `UserId` (PERSON) chứ không `TaiKhoanId`: đây là khoá ngoại nghiệp vụ trỏ con người.
        // null khi lệnh chạy bởi hệ thống — seeder, job nền, hoặc endpoint ẩn danh như đăng ký
        // trung tâm. Đó là lý do hai cột này nullable.
        var nguoiHienTai = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = bayGio;
                    entry.Entity.CreatedById ??= nguoiHienTai;

                    if (entry.Entity is ITenantEntity moi && moi.TenantId == Guid.Empty
                                                          && currentTenant.TenantId is { } tid)
                    {
                        moi.TenantId = tid;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = bayGio;
                    entry.Entity.UpdatedById = nguoiHienTai;

                    // KHÔNG đụng `CreatedById` khi cập nhật: người sửa sẽ âm thầm trở thành
                    // "người tạo" và không có gì báo vì cả hai đều là Guid hợp lệ.
                    entry.Property(nameof(BaseEntity.CreatedById)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;

                    // Không cho đổi tenant của bản ghi đã tồn tại — đó là chuyển dữ liệu sang trung tâm khác.
                    if (entry.Entity is ITenantEntity)
                        entry.Property(nameof(ITenantEntity.TenantId)).IsModified = false;
                    break;
            }
        }
    }
}

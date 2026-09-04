using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Configurations;

// Cấu hình EF đặt ở Infrastructure, KHÔNG rải attribute trong Domain (quy tắc #10).
// Tên bảng/cột theo quy ước UPPER_SNAKE_CASE / lower_snake_case — xem
// docs/database/quy-uoc-migration.md. Ánh xạ tên cột do UseSnakeCaseNamingConvention lo,
// ở đây chỉ đặt tên bảng, ràng buộc và index.

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("TENANT");
        b.Property(x => x.MaTrungTam)
            .HasMaxLength(Domain.Common.MaTrungTam.DoDai).IsFixedLength().IsRequired();
        b.Property(x => x.TenTrungTam).HasMaxLength(200).IsRequired();
        b.Property(x => x.TenVietTat).HasMaxLength(50);
        b.Property(x => x.DiaChi).HasMaxLength(200);
        b.Property(x => x.LienHe).HasMaxLength(200);
        b.Property(x => x.MoTa).HasMaxLength(1000);
        b.Property(x => x.SoTaiKhoan).HasMaxLength(50);
        b.Property(x => x.TenNganHang).HasMaxLength(100);
        b.Property(x => x.ChuTaiKhoan).HasMaxLength(200);

        // Mã trung tâm người dùng gõ khi đăng nhập — phải duy nhất toàn hệ thống.
        b.HasIndex(x => x.MaTrungTam).IsUnique();
    }
}

public class NguoiDungConfig : IEntityTypeConfiguration<NguoiDung>
{
    public void Configure(EntityTypeBuilder<NguoiDung> b)
    {
        b.ToTable("NGUOI_DUNG");
        b.Property(x => x.Username).HasMaxLength(100).IsRequired();
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.SoDienThoai).HasMaxLength(20);

        // Username duy nhất TRONG tenant — hai trung tâm đều có thể có tài khoản "admin".
        b.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();

        b.HasOne(x => x.Tenant).WithMany(t => t.NguoiDungs)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuyenConfig : IEntityTypeConfiguration<Quyen>
{
    public void Configure(EntityTypeBuilder<Quyen> b)
    {
        b.ToTable("QUYEN");
        b.Property(x => x.TenQuyen).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.TenQuyen }).IsUnique();

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuyenChucNangConfig : IEntityTypeConfiguration<QuyenChucNang>
{
    public void Configure(EntityTypeBuilder<QuyenChucNang> b)
    {
        b.ToTable("QUYEN_CHUC_NANG");
        b.Property(x => x.TenChucNang).HasMaxLength(100).IsRequired();

        // Không lặp cùng một (nhóm quyền, chức năng, thao tác).
        b.HasIndex(x => new { x.QuyenId, x.TenChucNang, x.HanhDong }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.Quyen).WithMany(q => q.ChucNangs)
            .HasForeignKey(x => x.QuyenId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NguoiDungQuyenConfig : IEntityTypeConfiguration<NguoiDungQuyen>
{
    public void Configure(EntityTypeBuilder<NguoiDungQuyen> b)
    {
        b.ToTable("NGUOIDUNG_QUYEN");
        b.HasIndex(x => new { x.NguoiDungId, x.QuyenId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.NguoiDung).WithMany(n => n.NguoiDungQuyens)
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Quyen).WithMany(q => q.NguoiDungQuyens)
            .HasForeignKey(x => x.QuyenId).OnDelete(DeleteBehavior.Cascade);
    }
}

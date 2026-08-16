using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Configurations;

// Cấu hình EF đặt ở Infrastructure, KHÔNG rải attribute trong Domain (quy tắc #9).
// Tên bảng/cột theo quy ước UPPER_SNAKE_CASE / lower_snake_case — xem
// docs/database/quy-uoc-migration.md. Ánh xạ tên cột do UseSnakeCaseNamingConvention lo,
// ở đây chỉ đặt tên bảng, ràng buộc và index.

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("TENANT");
        b.Property(x => x.MaDoi).HasMaxLength(50).IsRequired();
        b.Property(x => x.TenDoi).HasMaxLength(200).IsRequired();
        b.Property(x => x.TenVietTat).HasMaxLength(50);

        // ID đội người dùng gõ khi đăng nhập — phải duy nhất toàn hệ thống.
        b.HasIndex(x => x.MaDoi).IsUnique();
    }
}

public class CauThuConfig : IEntityTypeConfiguration<CauThu>
{
    public void Configure(EntityTypeBuilder<CauThu> b)
    {
        b.ToTable("CAU_THU");
        b.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.Tenant).WithMany(t => t.CauThus)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
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

        // Username duy nhất TRONG tenant — hai CLB đều có thể có tài khoản "admin".
        b.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();

        b.HasOne(x => x.Tenant).WithMany(t => t.NguoiDungs)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

        // Xóa tài khoản không xóa hồ sơ cầu thủ — hai thực thể độc lập (FR-03, FR-04).
        b.HasOne(x => x.CauThu).WithMany()
            .HasForeignKey(x => x.CauThuId).OnDelete(DeleteBehavior.SetNull);
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

public class DoiThuConfig : IEntityTypeConfiguration<DoiThu>
{
    public void Configure(EntityTypeBuilder<DoiThu> b)
    {
        b.ToTable("DOI_THU");
        b.Property(x => x.TenDoi).HasMaxLength(200).IsRequired();
        b.Property(x => x.LienHe).HasMaxLength(200);
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoiMoiDoiThuConfig : IEntityTypeConfiguration<LoiMoiDoiThu>
{
    public void Configure(EntityTypeBuilder<LoiMoiDoiThu> b)
    {
        b.ToTable("LOI_MOI_DOI_THU");
        b.HasIndex(x => new { x.TenantId, x.TrangThai });

        b.HasOne(x => x.DoiThu).WithMany(d => d.LoiMois)
            .HasForeignKey(x => x.DoiThuId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TranDauConfig : IEntityTypeConfiguration<TranDau>
{
    public void Configure(EntityTypeBuilder<TranDau> b)
    {
        b.ToTable("TRAN_DAU");
        b.Property(x => x.LinkVideo).HasMaxLength(500);

        // Bộ lọc chính của FR-07/FR-12 là theo tenant + thời gian.
        b.HasIndex(x => new { x.TenantId, x.ThoiGian });

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

        // Xóa đối thủ không xóa lịch sử trận đã đấu.
        b.HasOne(x => x.DoiThu).WithMany(d => d.TranDaus)
            .HasForeignKey(x => x.DoiThuId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DoiHinhTranDauConfig : IEntityTypeConfiguration<DoiHinhTranDau>
{
    public void Configure(EntityTypeBuilder<DoiHinhTranDau> b)
    {
        b.ToTable("DOIHINH_TRANDAU");
        b.Property(x => x.ViTri).HasMaxLength(50);

        // Một cầu thủ chỉ xuất hiện một lần trong đội hình của một trận.
        b.HasIndex(x => new { x.TranDauId, x.CauThuId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.TranDau).WithMany(t => t.DoiHinhs)
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.CauThu).WithMany(c => c.DoiHinhs)
            .HasForeignKey(x => x.CauThuId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SoDoChienThuatConfig : IEntityTypeConfiguration<SoDoChienThuat>
{
    public void Configure(EntityTypeBuilder<SoDoChienThuat> b)
    {
        b.ToTable("SODO_CHIENTHUAT");
        b.Property(x => x.SoDoJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => x.TenantId);

        // Quan hệ 1—1 với trận đấu.
        b.HasOne(x => x.TranDau).WithOne(t => t.SoDoChienThuat)
            .HasForeignKey<SoDoChienThuat>(x => x.TranDauId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DanhGiaCauThuConfig : IEntityTypeConfiguration<DanhGiaCauThu>
{
    public void Configure(EntityTypeBuilder<DanhGiaCauThu> b)
    {
        b.ToTable("DANHGIA_CAUTHU");
        b.Property(x => x.ChiSoKyNang).HasColumnType("jsonb");

        // Mỗi cầu thủ một bản đánh giá trong một trận.
        b.HasIndex(x => new { x.TranDauId, x.CauThuId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.TranDau).WithMany(t => t.DanhGias)
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.CauThu).WithMany(c => c.DanhGias)
            .HasForeignKey(x => x.CauThuId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VoteMvpConfig : IEntityTypeConfiguration<VoteMvp>
{
    public void Configure(EntityTypeBuilder<VoteMvp> b)
    {
        b.ToTable("VOTE_MVP");

        // QUY TẮC BẤT DI BẤT DỊCH #7 — mỗi người tối đa 1 tim/trận.
        // Ràng buộc phải nằm ở DB: chặn ở UI hay ở handler đều không cứu được trường hợp
        // hai request đồng thời cùng vượt qua bước kiểm tra rồi cùng ghi.
        b.HasIndex(x => new { x.TranDauId, x.NguoiVoteId })
            .IsUnique()
            .HasDatabaseName("UQ_VOTE_MVP_tran_dau_nguoi_vote");

        // Đếm phiếu cho bảng xếp hạng MVP (FR-14).
        b.HasIndex(x => new { x.TenantId, x.CauThuDuocVoteId });

        b.HasOne(x => x.TranDau).WithMany(t => t.Votes)
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuyConfig : IEntityTypeConfiguration<Quy>
{
    public void Configure(EntityTypeBuilder<Quy> b)
    {
        b.ToTable("QUY");
        b.Property(x => x.TenQuy).HasMaxLength(200).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.TrangThai });

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DongGopQuyConfig : IEntityTypeConfiguration<DongGopQuy>
{
    public void Configure(EntityTypeBuilder<DongGopQuy> b)
    {
        b.ToTable("DONGGOP_QUY");

        // Tiền: precision cố định, không dùng floating point.
        b.Property(x => x.SoTienCanDong).HasPrecision(18, 2);
        b.Property(x => x.SoTienDaDong).HasPrecision(18, 2);

        // Một cầu thủ chỉ có một khoản đóng trong mỗi đợt quỹ.
        b.HasIndex(x => new { x.QuyId, x.CauThuId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.Quy).WithMany(q => q.DongGops)
            .HasForeignKey(x => x.QuyId).OnDelete(DeleteBehavior.Cascade);

        // Xóa cầu thủ không xóa lịch sử tài chính — dữ liệu tiền phải giữ vết.
        b.HasOne(x => x.CauThu).WithMany(c => c.DongGops)
            .HasForeignKey(x => x.CauThuId).OnDelete(DeleteBehavior.Restrict);
    }
}

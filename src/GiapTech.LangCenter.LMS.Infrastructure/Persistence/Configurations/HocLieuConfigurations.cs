using GiapTech.LangCenter.LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Configurations;

public class TepDinhKemConfig : IEntityTypeConfiguration<TepDinhKem>
{
    public void Configure(EntityTypeBuilder<TepDinhKem> b)
    {
        // Đúng MỘT cột FK khác null. Ép ở tầng DB chứ không chỉ validate ở handler: một
        // handler mới quên kiểm là sinh ra hàng đính kèm không thuộc về ai, và job dọn rác
        // sẽ coi tệp đó là còn dùng nên không bao giờ xoá.
        const string dungMotChu =
            "(CASE WHEN bai_tap_id IS NOT NULL THEN 1 ELSE 0 END"
            + " + CASE WHEN bai_nop_id IS NOT NULL THEN 1 ELSE 0 END"
            + " + CASE WHEN bai_kiem_tra_id IS NOT NULL THEN 1 ELSE 0 END"
            + " + CASE WHEN bai_lam_id IS NOT NULL THEN 1 ELSE 0 END"
            + " + CASE WHEN tai_lieu_id IS NOT NULL THEN 1 ELSE 0 END) = 1";

        b.ToTable("TEP_DINH_KEM",
            t => t.HasCheckConstraint("ck_tep_dinh_kem_dung_mot_chu", dungMotChu));

        b.Property(x => x.KhoaLuuTru).HasMaxLength(500).IsRequired();
        b.Property(x => x.TenGoc).HasMaxLength(200).IsRequired();
        b.Property(x => x.LoaiNoiDung).HasMaxLength(150).IsRequired();

        b.HasIndex(x => x.TenantId);
        // Job dọn tệp mồ côi đối chiếu kho MinIO với cột này.
        b.HasIndex(x => x.KhoaLuuTru);

        // Cascade: xoá bài tập thì hàng đính kèm chết theo. Tệp trong MinIO KHÔNG tự mất —
        // handler phải xoá tệp trước, và cần job quét định kỳ cho phần sót lại.
        b.HasOne(x => x.BaiTap).WithMany(t => t.Teps)
            .HasForeignKey(x => x.BaiTapId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BaiNop).WithMany(t => t.Teps)
            .HasForeignKey(x => x.BaiNopId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BaiKiemTra).WithMany(t => t.Teps)
            .HasForeignKey(x => x.BaiKiemTraId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BaiLam).WithMany(t => t.Teps)
            .HasForeignKey(x => x.BaiLamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TaiLieu).WithMany(t => t.Teps)
            .HasForeignKey(x => x.TaiLieuId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.NguoiTaiLen).WithMany()
            .HasForeignKey(x => x.NguoiTaiLenId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BaiTapConfig : IEntityTypeConfiguration<BaiTap>
{
    public void Configure(EntityTypeBuilder<BaiTap> b)
    {
        b.ToTable("BAI_TAP");
        b.Property(x => x.TieuDe).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(2000);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.BuoiHocId);

        // Cascade khác điểm danh: bài tập được giao TRONG buổi, buổi mất thì nó vô nghĩa. Mà
        // buổi đã điểm danh vốn đã bị Restrict chặn từ trước, nên Cascade ở đây chỉ áp cho
        // buổi chưa diễn ra.
        b.HasOne(x => x.BuoiHoc).WithMany()
            .HasForeignKey(x => x.BuoiHocId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.NguoiTao).WithMany()
            .HasForeignKey(x => x.NguoiTaoId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BaiNopConfig : IEntityTypeConfiguration<BaiNop>
{
    public void Configure(EntityTypeBuilder<BaiNop> b)
    {
        b.ToTable("BAI_NOP");
        b.Property(x => x.NoiDung).HasMaxLength(4000);
        b.Property(x => x.NhanXet).HasMaxLength(2000);
        b.Property(x => x.Diem).HasPrecision(5, 2);

        // Nộp nhiều lần: khoá gồm cả LanNop. Bài mới nhất = LanNop lớn nhất.
        b.HasIndex(x => new { x.BaiTapId, x.HocVienId, x.LanNop }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.BaiTap).WithMany(t => t.BaiNops)
            .HasForeignKey(x => x.BaiTapId).OnDelete(DeleteBehavior.Cascade);

        // Restrict: bài đã chấm là dữ liệu học tập, không mất theo tài khoản.
        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NguoiCham).WithMany()
            .HasForeignKey(x => x.NguoiChamId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BaiKiemTraConfig : IEntityTypeConfiguration<BaiKiemTra>
{
    public void Configure(EntityTypeBuilder<BaiKiemTra> b)
    {
        b.ToTable("BAI_KIEM_TRA");
        b.Property(x => x.TieuDe).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(2000);
        b.Property(x => x.ThangDiem).HasPrecision(5, 2);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.LopHocId);

        // Restrict: kết quả kiểm tra là dữ liệu học tập, không biến mất theo lớp.
        b.HasOne(x => x.LopHoc).WithMany()
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NguoiTao).WithMany()
            .HasForeignKey(x => x.NguoiTaoId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BaiLamConfig : IEntityTypeConfiguration<BaiLam>
{
    public void Configure(EntityTypeBuilder<BaiLam> b)
    {
        b.ToTable("BAI_LAM");
        b.Property(x => x.NhanXet).HasMaxLength(2000);
        b.Property(x => x.Diem).HasPrecision(5, 2);

        // Đúng MỘT bài làm cho mỗi học viên — khác bài tập, không cho nộp lại nhiều lần.
        b.HasIndex(x => new { x.BaiKiemTraId, x.HocVienId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.BaiKiemTra).WithMany(t => t.BaiLams)
            .HasForeignKey(x => x.BaiKiemTraId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NguoiCham).WithMany()
            .HasForeignKey(x => x.NguoiChamId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TaiLieuConfig : IEntityTypeConfiguration<TaiLieu>
{
    public void Configure(EntityTypeBuilder<TaiLieu> b)
    {
        b.ToTable("TAI_LIEU");
        b.Property(x => x.TieuDe).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(2000);
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.NguoiTaiLen).WithMany()
            .HasForeignKey(x => x.NguoiTaiLenId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TaiLieuLopHocConfig : IEntityTypeConfiguration<TaiLieuLopHoc>
{
    public void Configure(EntityTypeBuilder<TaiLieuLopHoc> b)
    {
        b.ToTable("TAI_LIEU_LOP_HOC");
        b.HasIndex(x => new { x.TaiLieuId, x.LopHocId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.TaiLieu).WithMany(t => t.LopHocs)
            .HasForeignKey(x => x.TaiLieuId).OnDelete(DeleteBehavior.Cascade);

        // Cascade: gỡ liên kết khi lớp mất, KHÔNG xoá tài liệu (nó có thể gắn nhiều lớp).
        b.HasOne(x => x.LopHoc).WithMany()
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Cascade);
    }
}

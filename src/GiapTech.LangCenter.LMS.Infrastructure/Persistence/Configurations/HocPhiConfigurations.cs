using GiapTech.LangCenter.LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Configurations;

public class KhoanThuHocPhiConfig : IEntityTypeConfiguration<KhoanThuHocPhi>
{
    public void Configure(EntityTypeBuilder<KhoanThuHocPhi> b)
    {
        b.ToTable("KHOAN_THU_HOC_PHI", t => t.HasCheckConstraint(
            "ck_khoan_thu_so_tien_duong", "so_tien > 0"));

        // numeric tường minh cho tiền — double là cấm tuyệt đối.
        b.Property(x => x.SoTien).HasPrecision(18, 2);
        b.Property(x => x.SoPhieu).HasMaxLength(100);
        b.Property(x => x.GhiChu).HasMaxLength(500);

        b.HasIndex(x => x.TenantId);
        // Tính công nợ luôn lọc theo cặp này.
        b.HasIndex(x => new { x.LopHocId, x.HocVienId });

        // Restrict cả hai: dữ liệu tiền không được biến mất theo tài khoản hay theo lớp.
        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LopHoc).WithMany()
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NguoiThu).WithMany()
            .HasForeignKey(x => x.NguoiThuId).OnDelete(DeleteBehavior.SetNull);
    }
}

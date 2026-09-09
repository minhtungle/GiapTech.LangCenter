using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

public class BuoiHocConfig : IEntityTypeConfiguration<BuoiHoc>
{
    public void Configure(EntityTypeBuilder<BuoiHoc> b)
    {
        b.ToTable("BUOI_HOC");
        b.Property(x => x.PhongHoc).HasMaxLength(200);
        b.Property(x => x.LinkHoc).HasMaxLength(500);
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        // Không kèm tenant_id: lop_hoc_id đã FK về LOP_HOC vốn đã thuộc một tenant, nên thêm
        // tenant_id vào index chỉ làm nó rộng hơn mà không loại thêm hàng nào. Nhất quán với
        // UNIQUE(quyen_id, ten_chuc_nang, hanh_dong).
        b.HasIndex(x => new { x.LopHocId, x.ThuTu }).IsUnique();

        // Kiểm trùng lịch giáo viên chạy trên hai cột này.
        b.HasIndex(x => new { x.GiaoVienId, x.BatDau });
        b.HasIndex(x => new { x.TenantId, x.BatDau });

        // Restrict: xoá lớp đã có buổi học phải bị chặn — buổi học là gốc của điểm danh, và
        // Cascade ở đây sẽ cuốn sạch dữ liệu chuyên cần không phục hồi được (quy tắc #1).
        b.HasOne(x => x.LopHoc).WithMany()
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.GiaoVien).WithMany()
            .HasForeignKey(x => x.GiaoVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DiemDanhConfig : IEntityTypeConfiguration<DiemDanh>
{
    public void Configure(EntityTypeBuilder<DiemDanh> b)
    {
        b.Property(x => x.NhanXet).HasMaxLength(2000);
        b.ToTable("DIEM_DANH");
        b.Property(x => x.LyDoVang).HasMaxLength(500);

        // Mỗi học viên đúng một bản ghi điểm danh cho mỗi buổi — chống bấm hai lần và chống
        // hai request song song cùng ghi (quy tắc #8).
        b.HasIndex(x => new { x.BuoiHocId, x.HocVienId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        // Restrict: điểm danh là bằng chứng chuyên cần, không được biến mất vì ai đó xoá buổi.
        b.HasOne(x => x.BuoiHoc).WithMany(bh => bh.DiemDanhs)
            .HasForeignKey(x => x.BuoiHocId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);

        // SetNull chứ không Restrict: đây chỉ là dấu vết "ai xác nhận". Restrict sẽ khoá cứng
        // mọi tài khoản giáo viên vĩnh viễn — nghỉ việc cũng không xoá được tài khoản.
        b.HasOne(x => x.NguoiXacNhan).WithMany()
            .HasForeignKey(x => x.NguoiXacNhanId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class NhanXetBuoiHocConfig : IEntityTypeConfiguration<NhanXetBuoiHoc>
{
    public void Configure(EntityTypeBuilder<NhanXetBuoiHoc> b)
    {
        b.ToTable("NHAN_XET_BUOI_HOC");

        b.Property(x => x.NoiDung).HasMaxLength(2000).IsRequired();

        // Mỗi học viên một nhận xét cho mỗi buổi. Gửi lần hai là SỬA, không tạo thêm dòng —
        // nhiều nhận xét cho cùng một buổi thì không biết cái nào là ý kiến cuối.
        b.HasIndex(x => new { x.BuoiHocId, x.HocVienId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        // Restrict: nhận xét là ý kiến đã phát biểu, xoá buổi không được cuốn nó đi.
        b.HasOne(x => x.BuoiHoc).WithMany()
            .HasForeignKey(x => x.BuoiHocId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

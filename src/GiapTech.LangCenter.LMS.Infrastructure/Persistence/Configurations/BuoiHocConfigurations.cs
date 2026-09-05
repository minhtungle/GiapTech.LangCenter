using GiapTech.LangCenter.LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence.Configurations;

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

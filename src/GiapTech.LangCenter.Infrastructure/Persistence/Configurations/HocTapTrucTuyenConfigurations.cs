using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

public class KhoaOnlineConfig : IEntityTypeConfiguration<KhoaOnline>
{
    public void Configure(EntityTypeBuilder<KhoaOnline> b)
    {
        b.ToTable("KHOA_ONLINE");

        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(2000);

        b.HasIndex(x => x.TenantId);
    }
}

public class BaiHocOnlineConfig : IEntityTypeConfiguration<BaiHocOnline>
{
    public void Configure(EntityTypeBuilder<BaiHocOnline> b)
    {
        b.ToTable("BAI_HOC_ONLINE");

        b.Property(x => x.TieuDe).HasMaxLength(300).IsRequired();
        // Nội dung bài học là markdown dài — không giới hạn như các cột chuỗi khác.
        b.Property(x => x.NoiDung).HasColumnType("text");

        b.HasIndex(x => x.TenantId);
        // Truy vấn nóng nhất: liệt kê bài của một khoá theo thứ tự.
        b.HasIndex(x => new { x.KhoaOnlineId, x.ThuTu });

        // Cascade: xoá khoá thì bài chết theo — bài học không tồn tại độc lập ngoài khoá.
        b.HasOne(x => x.KhoaOnline).WithMany(k => k.BaiHocs)
            .HasForeignKey(x => x.KhoaOnlineId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GhiDanhKhoaOnlineConfig : IEntityTypeConfiguration<GhiDanhKhoaOnline>
{
    public void Configure(EntityTypeBuilder<GhiDanhKhoaOnline> b)
    {
        b.ToTable("GHI_DANH_KHOA_ONLINE");

        b.Property(x => x.GhiChu).HasMaxLength(500);

        b.HasIndex(x => x.TenantId);

        // Quy tắc #8 — "một người ghi danh một khoá đúng MỘT lần" phải là UNIQUE ở tầng DB,
        // không phải `if` trong handler. Cấp quyền hai lần cho cùng người là thao tác TAY dễ
        // xảy ra nhất ở màn này: hai người điều phối cùng xử lý một đơn.
        b.HasIndex(x => new { x.KhoaOnlineId, x.HocVienId }).IsUnique();

        // Truy vấn "khoá của tôi" — vào mọi lần học viên mở màn học tập.
        b.HasIndex(x => x.HocVienId);

        b.HasOne(x => x.KhoaOnline).WithMany(k => k.GhiDanhs)
            .HasForeignKey(x => x.KhoaOnlineId).OnDelete(DeleteBehavior.Cascade);

        // RESTRICT chứ không Cascade: xoá một người mà kéo theo lịch sử học của họ là mất dấu
        // vết. Người nghỉ học thì đổi trạng thái tài khoản, không xoá (quy tắc #1).
        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TienDoBaiHocConfig : IEntityTypeConfiguration<TienDoBaiHoc>
{
    public void Configure(EntityTypeBuilder<TienDoBaiHoc> b)
    {
        b.ToTable("TIEN_DO_BAI_HOC");

        b.HasIndex(x => x.TenantId);

        // Một người đánh dấu một bài đúng MỘT lần. Bấm hai lần vì mạng chậm là ca thường gặp
        // hơn cả — `AnyAsync` rồi `Add` sẽ để lọt khi hai request chạy song song (quy tắc #8).
        b.HasIndex(x => new { x.BaiHocOnlineId, x.HocVienId }).IsUnique();

        // Truy vấn "tôi học tới đâu trong khoá này".
        b.HasIndex(x => x.HocVienId);

        b.HasOne(x => x.BaiHocOnline).WithMany()
            .HasForeignKey(x => x.BaiHocOnlineId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

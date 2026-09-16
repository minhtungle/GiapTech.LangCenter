using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

/// <summary>Cấu hình EF cho FR-29 — đánh giá chất lượng theo tiêu chí (16/09/2026).</summary>
public class TieuChiDanhGiaConfig : IEntityTypeConfiguration<TieuChiDanhGia>
{
    public void Configure(EntityTypeBuilder<TieuChiDanhGia> b)
    {
        b.ToTable("TIEU_CHI_DANH_GIA");

        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(500);

        b.HasIndex(x => x.TenantId);

        // Trùng tên TRONG CÙNG NHÓM là lỗi: hai tiêu chí "Nhiệt tình" trên một phiếu thì người
        // chấm không biết chấm cái nào. Khác nhóm thì được — "Thái độ" có nghĩa riêng ở mỗi bên.
        // UNIQUE ở tầng DB, không chỉ `if` trong handler (quy tắc #8).
        b.HasIndex(x => new { x.TenantId, x.Nhom, x.Ten }).IsUnique();
    }
}

public class DiemTieuChiConfig : IEntityTypeConfiguration<DiemTieuChi>
{
    public void Configure(EntityTypeBuilder<DiemTieuChi> b)
    {
        // "Đúng một cột chủ khác null" — cùng khuôn `TEP_DINH_KEM`. Thêm loại phiếu thứ ba mà
        // quên sửa dòng này là mọi hàng dùng cột mới bị chặn ở tầng DB (lỗi lúc chạy).
        const string dungMotChu =
            "(CASE WHEN nhan_xet_buoi_hoc_id IS NOT NULL THEN 1 ELSE 0 END"
            + " + CASE WHEN phieu_danh_gia_nhan_vien_id IS NOT NULL THEN 1 ELSE 0 END) = 1";

        b.ToTable("DIEM_TIEU_CHI", t =>
        {
            t.HasCheckConstraint("ck_diem_tieu_chi_dung_mot_chu", dungMotChu);
            // Thang 5 ép ở tầng DB: validator có thể bị bỏ qua nếu sau này có đường ghi khác
            // (script, import), mà điểm 0 hay 99 sẽ làm mọi số trung bình vô nghĩa.
            t.HasCheckConstraint("ck_diem_tieu_chi_thang_5", "diem BETWEEN 1 AND 5");
        });

        b.HasIndex(x => x.TenantId);

        // Một tiêu chí chỉ có MỘT điểm trong một phiếu — chấm lại là sửa, không thêm dòng.
        b.HasIndex(x => new { x.NhanXetBuoiHocId, x.TieuChiId })
            .IsUnique()
            .HasFilter("nhan_xet_buoi_hoc_id IS NOT NULL");
        b.HasIndex(x => new { x.PhieuDanhGiaNhanVienId, x.TieuChiId })
            .IsUnique()
            .HasFilter("phieu_danh_gia_nhan_vien_id IS NOT NULL");

        b.HasOne(x => x.TieuChi).WithMany(t => t.Diems)
            // KHÔNG Cascade: xoá tiêu chí đang có điểm sẽ làm mọi kỳ đã chấm đổi số một cách im
            // lặng (quy tắc #1). Muốn bỏ tiêu chí thì đặt `DangDung = false`.
            .HasForeignKey(x => x.TieuChiId).OnDelete(DeleteBehavior.Restrict);

        // Cascade theo PHIẾU thì đúng: xoá phiếu là bỏ cả lần chấm đó, điểm lẻ không còn nghĩa.
        b.HasOne(x => x.NhanXetBuoiHoc).WithMany(n => n.DiemTieuChis)
            .HasForeignKey(x => x.NhanXetBuoiHocId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PhieuDanhGiaNhanVien).WithMany(p => p.Diems)
            .HasForeignKey(x => x.PhieuDanhGiaNhanVienId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhieuDanhGiaNhanVienConfig : IEntityTypeConfiguration<PhieuDanhGiaNhanVien>
{
    public void Configure(EntityTypeBuilder<PhieuDanhGiaNhanVien> b)
    {
        b.ToTable("PHIEU_DANH_GIA_NHAN_VIEN");

        // `yyyy-MM` — 7 ký tự. Để rộng hơn thì "2026-9" và "2026-09" cùng lọt và thành hai kỳ.
        b.Property(x => x.Ky).HasMaxLength(7).IsRequired();
        b.Property(x => x.NhanXet).HasMaxLength(2000);

        b.HasIndex(x => x.TenantId);

        // Một phiếu cho mỗi (nhân viên, kỳ) — quy tắc #8.
        b.HasIndex(x => new { x.TenantId, x.NhanVienId, x.Ky }).IsUnique();

        // Restrict: xoá hồ sơ nhân sự không được âm thầm xoá lịch sử đánh giá của họ.
        b.HasOne(x => x.NhanVien).WithMany()
            .HasForeignKey(x => x.NhanVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

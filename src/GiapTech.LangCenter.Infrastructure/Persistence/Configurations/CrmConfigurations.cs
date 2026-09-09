using GiapTech.LangCenter.Domain.Entities;
using GiapTech.LangCenter.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

public class KhachHangConfig : IEntityTypeConfiguration<KhachHang>
{
    public void Configure(EntityTypeBuilder<KhachHang> b)
    {
        b.ToTable("KHACH_HANG");

        b.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.SoDienThoai).HasMaxLength(30);
        b.Property(x => x.LinkFacebook).HasMaxLength(500);
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.HoTen);

        // "Chỉ một" phải là UNIQUE ở tầng DB (quy tắc #8) — hai người bán nhập cùng một khách
        // là chuyện thường ngày, và `AnyAsync` rồi `Add` thì hai request song song đều lọt.
        //
        // Lọc `IS NOT NULL AND <> ''`: khách chỉ để lại Facebook thì không có số điện thoại, mà
        // UNIQUE thường sẽ chặn người thứ hai không có số. Partial index giải đúng chỗ đó.
        //
        // Kèm `TenantId`: UNIQUE toàn cục sẽ chặn hai trung tâm cùng có một khách — sai, họ là
        // hai doanh nghiệp độc lập.
        b.HasIndex(x => new { x.TenantId, x.SoDienThoai })
            .IsUnique()
            .HasFilter("so_dien_thoai IS NOT NULL AND so_dien_thoai <> ''");

        // SetNull: xoá hồ sơ học viên không được cuốn theo dữ liệu khách hàng (và đơn hàng của
        // họ). Khác Restrict ở chỗ đây chỉ là mối nối "cùng một người", không phải phụ thuộc.
        b.HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class KhoaHocConfig : IEntityTypeConfiguration<KhoaHoc>
{
    public void Configure(EntityTypeBuilder<KhoaHoc> b)
    {
        b.ToTable("KHOA_HOC", t => t.HasCheckConstraint(
            "ck_khoa_hoc_gia_khong_am", "gia_tien >= 0"));

        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        // numeric tường minh cho tiền — double là cấm tuyệt đối.
        b.Property(x => x.GiaTien).HasPrecision(18, 2);

        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Ten }).IsUnique();
    }
}

public class SanPhamConfig : IEntityTypeConfiguration<SanPham>
{
    public void Configure(EntityTypeBuilder<SanPham> b)
    {
        b.ToTable("SAN_PHAM", t => t.HasCheckConstraint(
            "ck_san_pham_gia_khong_am", "gia_tien >= 0"));

        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.GhiChu).HasMaxLength(1000);
        b.Property(x => x.DonViTinh).HasMaxLength(50);
        b.Property(x => x.GiaTien).HasPrecision(18, 2);

        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Ten }).IsUnique();
    }
}

public class DangKyKhoaHocConfig : IEntityTypeConfiguration<DangKyKhoaHoc>
{
    public void Configure(EntityTypeBuilder<DangKyKhoaHoc> b)
    {
        b.ToTable("DANG_KY_KHOA_HOC", t =>
        {
            t.HasCheckConstraint("ck_dang_ky_so_tien_khong_am", "so_tien >= 0");
            t.HasCheckConstraint("ck_dang_ky_gia_goc_khong_am", "gia_goc >= 0");
            // Tỷ giá 0 làm doanh thu quy đổi thành 0 một cách âm thầm; số âm thì vô nghĩa.
            t.HasCheckConstraint("ck_dang_ky_ty_gia_duong", "ty_gia_ve_vnd > 0");
            t.HasCheckConstraint("ck_dang_ky_so_luong_duong", "so_luong > 0");

            // ĐÚNG MỘT trong hai khoá ngoại có giá trị. Ràng buộc ở tầng DB chứ không chỉ
            // validate ở handler: một dòng có cả hai (hoặc không có cái nào) là dữ liệu vô
            // nghĩa mà mọi báo cáo phải tự đoán cách xử lý. Cùng khuôn `TEP_DINH_KEM`.
            t.HasCheckConstraint(
                "ck_dang_ky_dung_mot_loai",
                "(khoa_hoc_id IS NOT NULL AND san_pham_id IS NULL) OR "
                + "(khoa_hoc_id IS NULL AND san_pham_id IS NOT NULL)");
        });

        b.Property(x => x.GiaGoc).HasPrecision(18, 2);
        b.Property(x => x.SoTien).HasPrecision(18, 2);
        // Tỷ giá cần nhiều số thập phân hơn tiền: 1 VND = 0.0000377 EUR.
        b.Property(x => x.TyGiaVeVnd).HasPrecision(18, 6);
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.KhachHangId);
        // Báo cáo doanh thu luôn lọc theo khoảng ngày.
        b.HasIndex(x => x.NgayDangKy);

        // KHÔNG có UNIQUE(KhachHangId, KhoaHocId): một khách đăng ký LẠI cùng một khoá là hợp
        // lệ (học lại, gia hạn) — chặn ở đây sẽ không bán được lần thứ hai.

        // Restrict cả hai: dữ liệu tiền không được biến mất theo khách hay theo khoá, và đơn
        // hàng cũ phải giữ được tên khoá đã bán.
        b.HasOne(x => x.KhachHang).WithMany(x => x.DangKys)
            .HasForeignKey(x => x.KhachHangId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.KhoaHoc).WithMany(x => x.DangKys)
            .HasForeignKey(x => x.KhoaHocId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SanPham).WithMany(x => x.DonHangs)
            .HasForeignKey(x => x.SanPhamId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LichSuChamSocConfig : IEntityTypeConfiguration<LichSuChamSoc>
{
    public void Configure(EntityTypeBuilder<LichSuChamSoc> b)
    {
        b.ToTable("LICH_SU_CHAM_SOC");

        b.Property(x => x.NoiDung).HasMaxLength(2000).IsRequired();

        b.HasIndex(x => x.TenantId);
        // Tab Lịch sử chăm sóc luôn lọc theo khách và sắp theo thời điểm giảm dần; trạng thái
        // hiện tại của khách cũng đọc từ dòng mới nhất theo đúng cặp cột này.
        b.HasIndex(x => new { x.KhachHangId, x.ThoiDiem });

        // Cascade: lịch sử chăm sóc thuộc HẲN về khách, không có nghĩa độc lập. Khác đăng ký
        // (Restrict — dữ liệu tiền) nên xoá khách vẫn bị chặn nếu họ đã mua.
        b.HasOne(x => x.KhachHang).WithMany(x => x.LichSuChamSocs)
            .HasForeignKey(x => x.KhachHangId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.NguoiPhuTrach).WithMany()
            .HasForeignKey(x => x.NguoiPhuTrachId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ThuTienDangKyConfig : IEntityTypeConfiguration<ThuTienDangKy>
{
    public void Configure(EntityTypeBuilder<ThuTienDangKy> b)
    {
        b.ToTable("THU_TIEN_DANG_KY", t => t.HasCheckConstraint(
            "ck_thu_tien_dang_ky_duong", "so_tien > 0"));

        // numeric tường minh cho tiền — double là cấm tuyệt đối.
        b.Property(x => x.SoTien).HasPrecision(18, 2);
        b.Property(x => x.GhiChu).HasMaxLength(500);

        b.HasIndex(x => x.TenantId);
        // Tính "còn thiếu" luôn cộng theo đăng ký.
        b.HasIndex(x => x.DangKyId);

        // Restrict: dữ liệu tiền không được biến mất theo đăng ký — muốn xoá đăng ký thì phải
        // xoá các lần thu trước, một cách có ý thức.
        b.HasOne(x => x.DangKy).WithMany(x => x.CacLanThu)
            .HasForeignKey(x => x.DangKyId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.NguoiThu).WithMany()
            .HasForeignKey(x => x.NguoiThuId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class YeuCauXepLopConfig : IEntityTypeConfiguration<YeuCauXepLop>
{
    public void Configure(EntityTypeBuilder<YeuCauXepLop> b)
    {
        b.ToTable("YEU_CAU_XEP_LOP");

        b.Property(x => x.GhiChu).HasMaxLength(500);
        b.Property(x => x.LyDoTuChoi).HasMaxLength(500);

        b.HasIndex(x => x.TenantId);
        // Danh sách chờ luôn lọc theo trạng thái.
        b.HasIndex(x => x.TrangThai);

        // Một đơn gửi được NHIỀU lần (09/09/2026): bị từ chối thì người bán bổ sung thông tin
        // rồi gửi lại, và lịch sử mua hàng phải hiện đủ số lần gửi kèm trạng thái từng lần.
        //
        // `UNIQUE(dang_ky_id, lan_gui)` chặn hai dòng cùng số thứ tự — bấm gửi hai lần song song
        // thì cả hai đọc `MAX(lan_gui)` được cùng một giá trị và đều muốn ghi "lần 2".
        b.HasIndex(x => new { x.DangKyId, x.LanGui }).IsUnique();

        // Và chỉ ĐÚNG MỘT lần đang chờ trên mỗi đơn — partial unique index (quy tắc #8).
        // Không có nó thì danh sách chờ có hai dòng cùng học viên, xếp lớp hai lần.
        b.HasIndex(x => x.DangKyId)
            .IsUnique()
            .HasFilter($"trang_thai = {(int)TrangThaiYeuCauXepLop.DangCho}")
            .HasDatabaseName("ux_yeu_cau_xep_lop_dang_ky_dang_cho");

        // Restrict: yêu cầu là vết bàn giao giữa hai bộ phận, xoá đơn hàng không được cuốn nó đi.
        b.HasOne(x => x.DangKy).WithMany(x => x.CacYeuCauXepLop)
            .HasForeignKey(x => x.DangKyId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);

        // Lớp bị xoá thì yêu cầu quay về trạng thái chờ được — nên SetNull, không Restrict.
        b.HasOne(x => x.LopHoc).WithMany()
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.NguoiGui).WithMany()
            .HasForeignKey(x => x.NguoiGuiId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.NguoiDuyet).WithMany()
            .HasForeignKey(x => x.NguoiDuyetId).OnDelete(DeleteBehavior.SetNull);
    }
}

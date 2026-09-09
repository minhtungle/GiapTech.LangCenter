using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

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
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.AnhBiaUrl).HasMaxLength(500);
        b.Property(x => x.AnhQrUrl).HasMaxLength(500);
        b.Property(x => x.MuiGio).HasMaxLength(64).IsRequired();

        // Mã trung tâm người dùng gõ khi đăng nhập — phải duy nhất toàn hệ thống.
        b.HasIndex(x => x.MaTrungTam).IsUnique();
    }
}

public class NguoiDungConfig : IEntityTypeConfiguration<NguoiDung>
{
    public void Configure(EntityTypeBuilder<NguoiDung> b)
    {
        b.ToTable("NGUOI_DUNG");
        b.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.SoDienThoai).HasMaxLength(20);
        b.Property(x => x.DiaChi).HasMaxLength(500);
        // URL ảnh là KHOÁ MinIO (`{tenantId}/{loai}/{guid}{ext}`), luôn dưới 150 ký tự — 500
        // là dư thoải mái. Không khai thì cột thành `text` không giới hạn.
        b.Property(x => x.AnhDaiDienUrl).HasMaxLength(500);

        // Lọc "chọn giáo viên" / "chọn học viên" chạy trên cột này ở mọi màn nghiệp vụ.
        b.HasIndex(x => new { x.TenantId, x.LoaiNguoiDung });

        b.HasOne(x => x.Tenant).WithMany(t => t.NguoiDungs)
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

        // Đếm sĩ số từng phòng ban và liệt kê người trong phòng đều chạy trên cột này.
        b.HasIndex(x => x.PhongBanId);

        // SetNull: xoá phòng ban KHÔNG được cuốn người theo. Handler vẫn chặn xoá phòng còn
        // người (`PHONG_BAN_CON_NGUOI`) — SetNull ở đây là lưới an toàn cho đường xoá khác.
        b.HasOne(x => x.PhongBan).WithMany(p => p.NhanSus)
            .HasForeignKey(x => x.PhongBanId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Cơ cấu tổ chức dạng cây (FR-22).</summary>
public class PhongBanConfig : IEntityTypeConfiguration<PhongBan>
{
    public void Configure(EntityTypeBuilder<PhongBan> b)
    {
        b.ToTable("PHONG_BAN");
        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(1000);

        b.HasIndex(x => x.TenantId);
        // Dựng cây: truy vấn "các con của phòng X" chạy trên cột này.
        b.HasIndex(x => x.PhongBanChaId);

        // Trùng tên trong CÙNG MỘT CHA thì người dùng chọn sai phòng (quy tắc #8). Khác cha thì
        // cho trùng: "Bộ môn Anh" dưới hai chi nhánh là hợp lệ.
        //
        // Lưu ý PostgreSQL: NULL không bằng NULL, nên index này KHÔNG chặn hai phòng gốc trùng
        // tên (`phong_ban_cha_id` cùng NULL). Đó là lý do có thêm index partial bên dưới.
        b.HasIndex(x => new { x.TenantId, x.PhongBanChaId, x.Ten }).IsUnique();

        // Phòng ban GỐC (cha = NULL) — partial unique index cho đúng trường hợp NULL ở trên.
        b.HasIndex(x => new { x.TenantId, x.Ten })
            .IsUnique()
            .HasFilter("phong_ban_cha_id IS NULL")
            .HasDatabaseName("ux_phong_ban_goc_ten");

        // Restrict: xoá phòng cha còn phòng con phải bị chặn, buộc người dùng dọn cây từ dưới
        // lên. Cascade sẽ âm thầm xoá cả nhánh — mất cả cơ cấu vì một cú bấm.
        b.HasOne(x => x.PhongBanCha).WithMany(x => x.PhongBanCons)
            .HasForeignKey(x => x.PhongBanChaId).OnDelete(DeleteBehavior.Restrict);

        // SetNull: người quản lý nghỉ việc thì phòng ban vẫn còn, chỉ trống chỗ quản lý.
        b.HasOne(x => x.NguoiQuanLy).WithMany()
            .HasForeignKey(x => x.NguoiQuanLyId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TaiKhoanConfig : IEntityTypeConfiguration<TaiKhoan>
{
    public void Configure(EntityTypeBuilder<TaiKhoan> b)
    {
        b.ToTable("TAI_KHOAN");
        b.Property(x => x.Username).HasMaxLength(100).IsRequired();
        // Hash của PasswordHasher (Identity v3) dài 84 ký tự base64; 200 để còn chỗ nếu
        // Identity đổi định dạng, mà vẫn không phải `text` vô hạn.
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

        // Username duy nhất TRONG tenant — hai trung tâm đều có thể có tài khoản "admin".
        b.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();

        // Một người tối đa một tài khoản: hai tài khoản cùng người thì không biết quyền nào
        // thắng. Filter cho phép nhiều hàng NULL (tài khoản kỹ thuật không gắn ai).
        b.HasIndex(x => x.NguoiDungId).IsUnique()
            .HasFilter("nguoi_dung_id IS NOT NULL");

        // SetNull chứ không Cascade: xoá người để lại tài khoản mồ côi — còn dấu vết ai từng
        // đăng nhập. Cascade sẽ xoá luôn cả lịch sử phiên.
        b.HasOne(x => x.NguoiDung).WithOne(n => n.TaiKhoan)
            .HasForeignKey<TaiKhoan>(x => x.NguoiDungId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Ba bảng hồ sơ theo vai trò, cùng khuôn: 1–1 với NGUOI_DUNG, Cascade (hồ sơ là một phần của
/// người, không có nghĩa khi đứng riêng), UNIQUE trên nguoi_dung_id.
/// </summary>
public class HoSoGiaoVienConfig : IEntityTypeConfiguration<HoSoGiaoVien>
{
    public void Configure(EntityTypeBuilder<HoSoGiaoVien> b)
    {
        b.ToTable("HO_SO_GIAO_VIEN");
        b.Property(x => x.BangCap).HasMaxLength(300);
        b.Property(x => x.ChuyenMon).HasMaxLength(300);

        b.HasIndex(x => x.NguoiDungId).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.NguoiDung).WithOne(n => n.HoSoGiaoVien)
            .HasForeignKey<HoSoGiaoVien>(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HoSoHocVienConfig : IEntityTypeConfiguration<HoSoHocVien>
{
    public void Configure(EntityTypeBuilder<HoSoHocVien> b)
    {
        b.ToTable("HO_SO_HOC_VIEN");
        b.Property(x => x.TruongLop).HasMaxLength(300);
        b.Property(x => x.TenPhuHuynh).HasMaxLength(200);
        b.Property(x => x.SoDienThoaiPhuHuynh).HasMaxLength(20);

        b.HasIndex(x => x.NguoiDungId).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.NguoiDung).WithOne(n => n.HoSoHocVien)
            .HasForeignKey<HoSoHocVien>(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HoSoNhanVienConfig : IEntityTypeConfiguration<HoSoNhanVien>
{
    public void Configure(EntityTypeBuilder<HoSoNhanVien> b)
    {
        b.ToTable("HO_SO_NHAN_VIEN");
        b.Property(x => x.ChucVu).HasMaxLength(200);

        b.HasIndex(x => x.NguoiDungId).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.NguoiDung).WithOne(n => n.HoSoNhanVien)
            .HasForeignKey<HoSoNhanVien>(x => x.NguoiDungId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NhatKyHeThongConfig : IEntityTypeConfiguration<NhatKyHeThong>
{
    public void Configure(EntityTypeBuilder<NhatKyHeThong> b)
    {
        b.ToTable("NHAT_KY_HE_THONG");

        b.Property(x => x.TenLenh).HasMaxLength(200).IsRequired();
        b.Property(x => x.ChucNang).HasMaxLength(100);
        b.Property(x => x.Username).HasMaxLength(100);
        b.Property(x => x.HoTen).HasMaxLength(200);
        b.Property(x => x.MaLoi).HasMaxLength(100);
        b.Property(x => x.DiaChiIp).HasMaxLength(64);

        // Truy vấn chính của màn nhật ký: mới nhất trước, trong phạm vi tenant.
        b.HasIndex(x => new { x.TenantId, x.NgayTao });
        // Lọc theo module và theo người — hai bộ lọc hay dùng nhất.
        b.HasIndex(x => new { x.TenantId, x.ChucNang });
        b.HasIndex(x => new { x.TenantId, x.NguoiDungId });

        // SetNull chứ không Restrict: nhật ký phải sống lâu hơn người dùng, và Restrict sẽ
        // khoá cứng mọi tài khoản vĩnh viễn vì ai cũng có vết trong nhật ký.
        // Tên và username đã lưu bản chụp nên mất khoá ngoại vẫn đọc được.
        b.HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class QuyenConfig : IEntityTypeConfiguration<Quyen>
{
    public void Configure(EntityTypeBuilder<Quyen> b)
    {
        b.ToTable("QUYEN");
        b.Property(x => x.TenQuyen).HasMaxLength(200).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(500);
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
        b.HasIndex(x => new { x.TaiKhoanId, x.QuyenId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.TaiKhoan).WithMany(n => n.NguoiDungQuyens)
            .HasForeignKey(x => x.TaiKhoanId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Quyen).WithMany(q => q.NguoiDungQuyens)
            .HasForeignKey(x => x.QuyenId).OnDelete(DeleteBehavior.Cascade);
    }
}

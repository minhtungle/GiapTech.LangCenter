using GiapTech.LangCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.LangCenter.Infrastructure.Persistence.Configurations;

public class LopHocConfig : IEntityTypeConfiguration<LopHoc>
{
    public void Configure(EntityTypeBuilder<LopHoc> b)
    {
        b.ToTable("LOP_HOC");
        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.PhongHoc).HasMaxLength(200);
        b.Property(x => x.LinkHoc).HasMaxLength(500);
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        // Tiền: numeric tường minh, KHÔNG để EF tự chọn. double cho tiền là cấm tuyệt đối.
        b.Property(x => x.HocPhi).HasPrecision(18, 2);

        // Tên lớp duy nhất trong trung tâm — nhưng KHÔNG tính lớp nháp.
        //
        // Nếu tính cả nháp thì: admin A tạo nháp "IELTS 6.5" rồi bỏ ngang, ba tháng sau admin B
        // tạo lớp thật cùng tên và nhận lỗi trùng — trong khi danh sách lớp không hiện nháp nên
        // B không thấy lớp nào trùng cả. Không tự chẩn được.
        b.HasIndex(x => new { x.TenantId, x.Ten })
            .IsUnique()
            .HasFilter("trang_thai <> 0");

        b.HasIndex(x => new { x.TenantId, x.TrangThai });

        // Xoá tài khoản giáo viên đang dạy phải bị CHẶN, buộc admin bàn giao lớp trước.
        // SetNull thì lớp mất giáo viên âm thầm; Cascade thì xoá 1 tài khoản kéo sập cả lớp.
        b.HasOne(x => x.GiaoVienChinh).WithMany()
            .HasForeignKey(x => x.GiaoVienChinhId).OnDelete(DeleteBehavior.Restrict);

        // Chỉ là dấu vết nguồn gốc — xoá lớp gốc không được kéo theo bản sao đang chạy.
        b.HasOne(x => x.NhanBanTuLop).WithMany()
            .HasForeignKey(x => x.NhanBanTuLopId).OnDelete(DeleteBehavior.SetNull);

    }
}

public class LopHocHocVienConfig : IEntityTypeConfiguration<LopHocHocVien>
{
    public void Configure(EntityTypeBuilder<LopHocHocVien> b)
    {
        b.ToTable("LOP_HOC_HOC_VIEN");
        b.Property(x => x.GhiChu).HasMaxLength(500);
        b.Property(x => x.HocPhiApDung).HasPrecision(18, 2);

        // Một học viên MỘT bản ghi trong một lớp. Rời lớp thì đổi trạng thái, không xoá hàng —
        // xoá là điểm danh và học phí của họ thành dữ liệu treo không giải thích được.
        b.HasIndex(x => new { x.LopHocId, x.HocVienId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.LopHoc).WithMany(l => l.HocViens)
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Cascade);

        // Restrict: xoá tài khoản học viên đang trong lớp sẽ làm học phí/điểm danh treo.
        b.HasOne(x => x.HocVien).WithMany()
            .HasForeignKey(x => x.HocVienId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LopHocTroGiangConfig : IEntityTypeConfiguration<LopHocTroGiang>
{
    public void Configure(EntityTypeBuilder<LopHocTroGiang> b)
    {
        b.ToTable("LOP_HOC_TRO_GIANG");
        b.Property(x => x.GhiChu).HasMaxLength(500);

        b.HasIndex(x => new { x.LopHocId, x.TroGiangId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.LopHoc).WithMany(l => l.TroGiangs)
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Cascade);

        // Cascade được: trợ giảng bị xoá thì gỡ khỏi lớp là hợp lý, không có dữ liệu con treo
        // theo (khác học viên — họ có điểm danh và học phí).
        b.HasOne(x => x.TroGiang).WithMany()
            .HasForeignKey(x => x.TroGiangId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LopHocKhoaHocConfig : IEntityTypeConfiguration<LopHocKhoaHoc>
{
    public void Configure(EntityTypeBuilder<LopHocKhoaHoc> b)
    {
        b.ToTable("LOP_HOC_KHOA_HOC");

        // Quy tắc #8: "một khoá chỉ gán một lần vào một lớp" là UNIQUE ở tầng DB, không phải
        // `if` trong handler — hai request song song đều thấy "chưa có" và đều ghi.
        b.HasIndex(x => new { x.LopHocId, x.KhoaHocId }).IsUnique();
        b.HasIndex(x => x.TenantId);

        b.HasOne(x => x.LopHoc).WithMany(l => l.KhoaHocs)
            .HasForeignKey(x => x.LopHocId).OnDelete(DeleteBehavior.Cascade);

        // RESTRICT, KHÔNG Cascade: xoá một khoá học đang được lớp dạy sẽ âm thầm bỏ liên kết,
        // và lớp mất căn cứ để đối chiếu đơn CRM lúc duyệt học viên. Bắt người xoá phải gỡ
        // khỏi lớp trước — cùng lý lẽ với `KHOA_HOC` đã bán thì ngừng bán chứ không xoá (FR-19).
        b.HasOne(x => x.KhoaHoc).WithMany()
            .HasForeignKey(x => x.KhoaHocId).OnDelete(DeleteBehavior.Restrict);
    }
}

using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GiapTech.SoccerRoom.Infrastructure.Persistence.Configurations;

// Cấu hình EF đặt ở Infrastructure, KHÔNG rải attribute trong Domain (quy tắc #10).
// Tên bảng/cột theo quy ước UPPER_SNAKE_CASE / lower_snake_case — xem
// docs/database/quy-uoc-migration.md. Ánh xạ tên cột do UseSnakeCaseNamingConvention lo,
// ở đây chỉ đặt tên bảng, ràng buộc và index.

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("TENANT");
        b.Property(x => x.MaDoi).HasMaxLength(Domain.Common.MaDoi.DoDai).IsFixedLength().IsRequired();
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
        b.Property(x => x.ViTriSoTruong).HasMaxLength(8);
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

        // Một đối thủ cho mỗi (tenant, mã đội hệ thống).
        //
        // Rà soát 20/08 tìm ra: 5 request chấp nhận lời mời link ĐỒNG THỜI tạo 5 bản ghi đối thủ
        // cùng trỏ về một CLB, kèm 5 trận. Handler kiểm `FirstOrDefaultAsync` rồi `Add` — hai
        // request song song đều thấy "chưa có" và đều ghi.
        //
        // Chặn ở tầng DB, không chỉ ở tầng ứng dụng: cùng lý do với UNIQUE vote MVP (quy tắc #8).
        // Filter `IS NOT NULL` vì đối thủ tên gõ tay (mã null) thì trùng bao nhiêu cũng được —
        // "FC Sông Hàn" của tôi và của bạn là hai đội khác nhau.
        b.HasIndex(x => new { x.TenantId, x.MaDoiHeThong })
            .IsUnique()
            .HasFilter("ma_doi_he_thong IS NOT NULL")
            .HasDatabaseName("UQ_DOI_THU_tenant_ma_doi_he_thong");
        b.Property(x => x.TenDoi).HasMaxLength(200).IsRequired();
        b.Property(x => x.LienHe).HasMaxLength(200);
        b.Property(x => x.MaDoiHeThong).HasMaxLength(Domain.Common.MaDoi.DoDai).IsFixedLength();
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

public class VideoTranConfig : IEntityTypeConfiguration<VideoTran>
{
    public void Configure(EntityTypeBuilder<VideoTran> b)
    {
        b.ToTable("VIDEO_TRAN");
        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.MoTa).HasMaxLength(1000);
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.TranDauId);

        // Xóa trận thì xóa luôn video của nó (FR-11) — link mồ côi không dùng được vào việc gì.
        b.HasOne(x => x.TranDau).WithMany(t => t.Videos)
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoiMoiThamGiaConfig : IEntityTypeConfiguration<LoiMoiThamGia>
{
    public void Configure(EntityTypeBuilder<LoiMoiThamGia> b)
    {
        b.ToTable("LOI_MOI_THAM_GIA");
        b.Property(x => x.LoiNhan).HasMaxLength(1000);
        b.HasIndex(x => x.TenantId);

        // Mỗi trận tối đa MỘT lời mời: gửi hai lần thì cầu thủ thấy hai thẻ giống hệt và
        // không biết trả lời cái nào mới tính.
        b.HasIndex(x => x.TranDauId).IsUnique();

        b.HasOne(x => x.TranDau).WithMany()
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoiMoiThachDauConfig : IEntityTypeConfiguration<LoiMoiThachDau>
{
    public void Configure(EntityTypeBuilder<LoiMoiThachDau> b)
    {
        b.ToTable("LOI_MOI_BAT_DOI");
        b.Property(x => x.LoiNhan).HasMaxLength(1000);
        b.Property(x => x.PhanHoi).HasMaxLength(1000);
        b.Property(x => x.DiaDiem).HasMaxLength(200);

        // Index cho CẢ HAI chiều: hòm thư đọc "lời mời tôi nhận" và "lời mời tôi gửi", hai
        // truy vấn khác nhau. Chỉ index một chiều thì chiều kia quét toàn bảng.
        b.HasIndex(x => x.TenantNhanId);
        b.HasIndex(x => x.TenantGuiId);

        // Một lời mời ĐANG CHỜ cho mỗi cặp CLB — ràng buộc này vốn chỉ được kiểm ở tầng ứng
        // dụng (`AnyAsync` rồi `Add`), nên 5 request đồng thời tạo 5 lời mời (rà soát 20/08).
        //
        // Filter theo `trang_thai = 0` (ChoPhanHoi): đá xong rồi mời lại lần sau là hợp lệ, nên
        // chỉ chặn lời mời đang treo.
        b.HasIndex(x => new { x.TenantGuiId, x.TenantNhanId })
            .IsUnique()
            .HasFilter("trang_thai = 0")
            .HasDatabaseName("UQ_LOI_MOI_BAT_DOI_dang_cho");

        // KHÔNG Cascade: xoá một CLB không được xoá lời mời khỏi hòm thư của CLB kia — đó là
        // dữ liệu của họ, không phải của bên bị xoá (quy tắc #1). Restrict buộc phải xử lý
        // tường minh nếu sau này có chức năng xoá CLB.
        b.HasOne(x => x.TenantGui).WithMany()
            .HasForeignKey(x => x.TenantGuiId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TenantNhan).WithMany()
            .HasForeignKey(x => x.TenantNhanId).OnDelete(DeleteBehavior.Restrict);

        // Không đặt FK tới TRAN_DAU: trận nằm trong tenant của từng bên, mà bảng này xuyên
        // tenant — FK sẽ mở đường join từ tenant này sang trận của tenant kia.
    }
}

public class LoiMoiLinkConfig : IEntityTypeConfiguration<LoiMoiLink>
{
    public void Configure(EntityTypeBuilder<LoiMoiLink> b)
    {
        b.ToTable("LOI_MOI_LINK");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.LoiNhan).HasMaxLength(1000);
        b.Property(x => x.PhanHoi).HasMaxLength(1000);
        b.Property(x => x.DiaDiem).HasMaxLength(200);

        b.HasIndex(x => x.TenantId);

        // UNIQUE trên hash: hai lời mời cùng token là không thể xảy ra với 32 byte ngẫu nhiên,
        // nhưng nếu xảy ra (lỗi sinh token) thì phải nổ ở tầng DB chứ không âm thầm cho một
        // token mở được hai lời mời.
        b.HasIndex(x => x.TokenHash).IsUnique();

        // Một link ĐANG CHỜ cho mỗi đối thủ. Cùng lý do với hai ràng buộc trên: 5 request tạo
        // link đồng thời cho ra 5 token khác nhau, và người nhận nhận được 5 link cho một trận.
        //
        // Filter loại cả link đã thu hồi: thu hồi rồi thì gửi lại được.
        b.HasIndex(x => x.DoiThuId)
            .IsUnique()
            .HasFilter("trang_thai = 0 AND thu_hoi_luc IS NULL")
            .HasDatabaseName("UQ_LOI_MOI_LINK_dang_cho");

        b.HasOne(x => x.DoiThu).WithMany()
            .HasForeignKey(x => x.DoiThuId).OnDelete(DeleteBehavior.Cascade);

        // KHÔNG Cascade từ trận: lời mời đã gửi ra ngoài, người nhận có thể đang mở link. Xoá
        // trận thì lời mời chuyển sang "trận không còn", không biến mất.
        b.HasOne(x => x.TranDau).WithMany()
            .HasForeignKey(x => x.TranDauId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);

        // Không FK tới TENANT cho TenantNhanId: cùng lý do với DoiThu.MaDoiHeThong — FK cho phép
        // join xuyên tenant, và CLB kia xoá tài khoản không được kéo theo lời mời của ta.
    }
}

public class PhanHoiThamGiaConfig : IEntityTypeConfiguration<PhanHoiThamGia>
{
    public void Configure(EntityTypeBuilder<PhanHoiThamGia> b)
    {
        b.ToTable("PHAN_HOI_THAM_GIA");
        b.Property(x => x.GhiChu).HasMaxLength(500);
        b.HasIndex(x => x.TenantId);

        // Mỗi cầu thủ một phản hồi cho mỗi lời mời — chặn ở tầng DB, cùng lý do với vote MVP:
        // hai request đồng thời vẫn lọt qua kiểm tra ở tầng ứng dụng.
        b.HasIndex(x => new { x.LoiMoiId, x.CauThuId }).IsUnique();

        b.HasOne(x => x.LoiMoi).WithMany(l => l.PhanHois)
            .HasForeignKey(x => x.LoiMoiId).OnDelete(DeleteBehavior.Cascade);

        // Xoá hồ sơ cầu thủ thì xoá luôn phản hồi của họ: phản hồi không mang giá trị thống kê
        // (khác đánh giá và đóng quỹ), giữ lại chỉ thành hàng mồ côi không hiển thị được tên.
        b.HasOne(x => x.CauThu).WithMany()
            .HasForeignKey(x => x.CauThuId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MauDoiHinhConfig : IEntityTypeConfiguration<MauDoiHinh>
{
    public void Configure(EntityTypeBuilder<MauDoiHinh> b)
    {
        b.ToTable("MAU_DOI_HINH");
        b.Property(x => x.Ten).HasMaxLength(200).IsRequired();
        b.Property(x => x.NoiDungJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => x.TenantId);

        // Tên mẫu duy nhất TRONG tenant — hai CLB đều có thể có mẫu "Đội hình mạnh nhất".
        b.HasIndex(x => new { x.TenantId, x.Ten }).IsUnique();

        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
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

public class KhoanChiConfig : IEntityTypeConfiguration<KhoanChi>
{
    public void Configure(EntityTypeBuilder<KhoanChi> b)
    {
        b.ToTable("KHOAN_CHI");
        b.Property(x => x.NoiDung).HasMaxLength(300).IsRequired();
        b.Property(x => x.NguoiChi).HasMaxLength(200);
        b.Property(x => x.GhiChu).HasMaxLength(1000);

        // Tiền: precision cố định, không dùng floating point.
        b.Property(x => x.SoTien).HasPrecision(18, 2);

        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => x.NgayChi);

        // Xoá đợt quỹ KHÔNG xoá khoản chi — tiền đã tiêu là sự thật kế toán, giữ lại dưới
        // dạng chi chung của CLB. SetNull thay vì Cascade.
        b.HasOne(x => x.Quy).WithMany()
            .HasForeignKey(x => x.QuyId).OnDelete(DeleteBehavior.SetNull);

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

using GiapTech.LangCenter.LMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.LangCenter.LMS.Application.Common.Interfaces;

/// <summary>
/// Cổng truy cập dữ liệu cho tầng Application.
///
/// Khai báo ở đây, cài đặt ở Infrastructure — handler không phụ thuộc DbContext cụ thể
/// nên unit test thay được bằng in-memory provider.
///
/// Lưu ý: <see cref="DbSet{TEntity}"/> đến từ package EF Core, nhưng Application chỉ dùng
/// nó như một abstraction truy vấn (IQueryable) — không tham chiếu Npgsql hay bất kỳ
/// provider nào. Test luật phụ thuộc canh đúng ranh giới này.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<NguoiDung> NguoiDungs { get; }
    DbSet<TaiKhoan> TaiKhoans { get; }
    DbSet<HoSoGiaoVien> HoSoGiaoViens { get; }
    DbSet<HoSoHocVien> HoSoHocViens { get; }
    DbSet<HoSoNhanVien> HoSoNhanViens { get; }

    DbSet<Quyen> Quyens { get; }
    DbSet<QuyenChucNang> QuyenChucNangs { get; }
    DbSet<NguoiDungQuyen> NguoiDungQuyens { get; }

    DbSet<LopHoc> LopHocs { get; }
    DbSet<LopHocHocVien> LopHocHocViens { get; }
    DbSet<LopHocTroGiang> LopHocTroGiangs { get; }

    DbSet<BuoiHoc> BuoiHocs { get; }
    DbSet<DiemDanh> DiemDanhs { get; }
    DbSet<NhanXetBuoiHoc> NhanXetBuoiHocs { get; }

    DbSet<TepDinhKem> TepDinhKems { get; }
    DbSet<BaiTap> BaiTaps { get; }
    DbSet<BaiNop> BaiNops { get; }
    DbSet<BaiKiemTra> BaiKiemTras { get; }
    DbSet<BaiLam> BaiLams { get; }
    DbSet<TaiLieu> TaiLieus { get; }
    DbSet<TaiLieuLopHoc> TaiLieuLopHocs { get; }

    DbSet<KhoanThuHocPhi> KhoanThuHocPhis { get; }

    // CRM (FR-17 → FR-19)
    DbSet<KhachHang> KhachHangs { get; }
    DbSet<KhoaHoc> KhoaHocs { get; }
    DbSet<DangKyKhoaHoc> DangKyKhoaHocs { get; }
    DbSet<LichSuChamSoc> LichSuChamSocs { get; }
    DbSet<ThuTienDangKy> ThuTienDangKys { get; }

    DbSet<NhatKyHeThong> NhatKyHeThongs { get; }

    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TokenDatLaiMatKhau> TokenDatLaiMatKhaus { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

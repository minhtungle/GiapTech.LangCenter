using GiapTech.SoccerRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GiapTech.SoccerRoom.Application.Common.Interfaces;

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
    DbSet<CauThu> CauThus { get; }

    DbSet<Quyen> Quyens { get; }
    DbSet<QuyenChucNang> QuyenChucNangs { get; }
    DbSet<NguoiDungQuyen> NguoiDungQuyens { get; }

    DbSet<DoiThu> DoiThus { get; }
    DbSet<LoiMoiDoiThu> LoiMoiDoiThus { get; }
    DbSet<TranDau> TranDaus { get; }
    DbSet<DoiHinhTranDau> DoiHinhTranDaus { get; }
    DbSet<SoDoChienThuat> SoDoChienThuats { get; }
    DbSet<MauDoiHinh> MauDoiHinhs { get; }
    DbSet<VideoTran> VideoTrans { get; }
    DbSet<LoiMoiThamGia> LoiMoiThamGias { get; }

    /// <summary>
    /// Lời mời thách đấu giữa hai CLB. KHÔNG có Global Query Filter — nó thuộc về hai tenant
    /// cùng lúc. Mọi truy vấn phải tự lọc `TenantGuiId == x || TenantNhanId == x`.
    /// </summary>
    DbSet<LoiMoiThachDau> LoiMoiThachDaus { get; }
    DbSet<PhanHoiThamGia> PhanHoiThamGias { get; }
    DbSet<DanhGiaCauThu> DanhGiaCauThus { get; }
    DbSet<VoteMvp> VoteMvps { get; }

    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TokenDatLaiMatKhau> TokenDatLaiMatKhaus { get; }

    DbSet<Quy> Quys { get; }
    DbSet<DongGopQuy> DongGopQuys { get; }
    DbSet<KhoanChi> KhoanChis { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

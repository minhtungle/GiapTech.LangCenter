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

    DbSet<Quyen> Quyens { get; }
    DbSet<QuyenChucNang> QuyenChucNangs { get; }
    DbSet<NguoiDungQuyen> NguoiDungQuyens { get; }

    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TokenDatLaiMatKhau> TokenDatLaiMatKhaus { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

using GiapTech.LangCenter.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiapTech.LangCenter.Infrastructure.Persistence;

/// <summary>
/// Factory cho `dotnet ef migrations` — công cụ CLI cần dựng được DbContext lúc design-time,
/// khi chưa có DI container nào chạy.
///
/// Connection string ở đây chỉ dùng để sinh migration (EF cần biết provider là PostgreSQL),
/// không phải cấu hình chạy thật — runtime lấy từ biến môi trường, xem docs/07-ha-tang/bien-moi-truong.md.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Ưu tiên biến môi trường để `dotnet ef database update` chạy được lên DB thật;
        // chuỗi mặc định chỉ đủ cho `migrations add` (EF chỉ cần biết provider là PostgreSQL).
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=langcenter_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new TenantRong(), new NguoiDungRong());
    }

    /// <summary>
    /// Không có người dùng lúc design-time. EF chỉ đọc schema, không ghi bản ghi nào — nên bốn
    /// cột audit (ADR-0006) không được gán và điều đó đúng.
    /// </summary>
    private sealed class NguoiDungRong : ICurrentUser
    {
        public Guid? UserId => null;
        public Guid? TaiKhoanId => null;
        public string? Username => null;
        public bool DaXacThuc => false;
    }

    /// <summary>Không có tenant lúc design-time — Global Query Filter không ảnh hưởng schema.</summary>
    private sealed class TenantRong : ICurrentTenant
    {
        public Guid? TenantId => null;
        public IDisposable DatPhamVi(Guid tenantId) => new KhongLam();
        private sealed class KhongLam : IDisposable { public void Dispose() { } }
    }
}

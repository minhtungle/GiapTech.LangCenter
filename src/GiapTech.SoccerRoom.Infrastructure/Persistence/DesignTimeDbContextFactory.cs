using GiapTech.SoccerRoom.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiapTech.SoccerRoom.Infrastructure.Persistence;

/// <summary>
/// Factory cho `dotnet ef migrations` — công cụ CLI cần dựng được DbContext lúc design-time,
/// khi chưa có DI container nào chạy.
///
/// Connection string ở đây chỉ dùng để sinh migration (EF cần biết provider là PostgreSQL),
/// không phải cấu hình chạy thật — runtime lấy từ biến môi trường, xem docs/ha-tang/bien-moi-truong.md.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=soccerroom_design;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new TenantRong());
    }

    /// <summary>Không có tenant lúc design-time — Global Query Filter không ảnh hưởng schema.</summary>
    private sealed class TenantRong : ICurrentTenant
    {
        public Guid? TenantId => null;
        public IDisposable DatPhamVi(Guid tenantId) => new KhongLam();
        private sealed class KhongLam : IDisposable { public void Dispose() { } }
    }
}

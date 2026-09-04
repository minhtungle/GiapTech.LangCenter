using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiapTech.LangCenter.LMS.Infrastructure.Persistence;

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
        // Ưu tiên biến môi trường để `dotnet ef database update` chạy được lên DB thật;
        // chuỗi mặc định chỉ đủ cho `migrations add` (EF chỉ cần biết provider là PostgreSQL).
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Database=langcenter-lms_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
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

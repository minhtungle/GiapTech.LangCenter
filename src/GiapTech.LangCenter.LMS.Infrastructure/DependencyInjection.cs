using GiapTech.LangCenter.LMS.Application.Common.Interfaces;
using GiapTech.LangCenter.LMS.Infrastructure.Identity;
using GiapTech.LangCenter.LMS.Infrastructure.LuuTru;
using GiapTech.LangCenter.LMS.Infrastructure.MultiTenancy;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence;
using GiapTech.LangCenter.LMS.Infrastructure.Persistence.Seed;
using GiapTech.LangCenter.LMS.Infrastructure.ThongBao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.LMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection ThemInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Scoped: mỗi HTTP request một tenant. Đăng ký cả hai cửa (concrete + interface)
        // trỏ về CÙNG một instance — middleware cần lớp cụ thể để gán giá trị, còn
        // DbContext và handler chỉ thấy interface.
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());

        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                // EnableRetryOnFailure: thử lại các lỗi kết nối nhất thời thay vì để chúng nổi
                // lên thành 500.
                //
                // Không có nó thì mỗi lần PostgreSQL restart (deploy, failover, `docker compose
                // up`), API vẫn giữ pool trỏ tới tiến trình cũ và **request đầu tiên sau đó chết
                // hẳn** — người dùng thấy "Đã có lỗi xảy ra", các request sau tự lành. Đã gặp
                // thật 20/08 khi dựng lại cụm.
                //
                // Đánh đổi: execution strategy không cho phép transaction do người dùng TỰ mở
                // (`BeginTransaction`) vì nó không thể phát lại cả khối. Hiện không chỗ nào trong
                // `src/` tự mở transaction — đã rà. Nếu sau này cần, dùng
                // `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` bọc quanh, đừng bỏ cờ này.
                .UseNpgsql(configuration.GetConnectionString("Default"),
                    npgsql => npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(2),
                        errorCodesToAdd: null))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddMemoryCache();
        services.AddScoped<IQuyenService, QuyenService>();
        services.AddScoped<IPhamViLopHoc, PhamViLopHoc>();
        services.AddSingleton<IPasswordHasher, AppPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ITenantSeeder, TenantSeeder>();
        services.AddScoped<Persistence.Seed.BoKhuyetQuyenQuanTri>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Scoped chứ không Singleton: MinioLuuTruAnh phụ thuộc ICurrentTenant (theo request)
        // để cách ly ảnh giữa các trung tâm. Singleton sẽ giữ tenant của request ĐẦU TIÊN cho mọi
        // request sau — đúng kiểu rò rỉ chéo mà quy tắc #2 cấm.
        services.AddScoped<ILuuTruAnh, MinioLuuTruAnh>();
        services.AddScoped<ILuuTruTep, MinioLuuTruTep>();

        return services;
    }
}

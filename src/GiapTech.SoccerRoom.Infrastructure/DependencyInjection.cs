using GiapTech.SoccerRoom.Application.Common.Interfaces;
using GiapTech.SoccerRoom.Infrastructure.Identity;
using GiapTech.SoccerRoom.Infrastructure.LuuTru;
using GiapTech.SoccerRoom.Infrastructure.MultiTenancy;
using GiapTech.SoccerRoom.Infrastructure.Persistence;
using GiapTech.SoccerRoom.Infrastructure.Persistence.Seed;
using GiapTech.SoccerRoom.Infrastructure.ThongBao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.Infrastructure;

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
                .UseNpgsql(configuration.GetConnectionString("Default"))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddMemoryCache();
        services.AddScoped<IQuyenService, QuyenService>();
        services.AddSingleton<IPasswordHasher, AppPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ITenantSeeder, TenantSeeder>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Scoped chứ không Singleton: MinioLuuTruAnh phụ thuộc ICurrentTenant (theo request)
        // để cách ly ảnh giữa các CLB. Singleton sẽ giữ tenant của request ĐẦU TIÊN cho mọi
        // request sau — đúng kiểu rò rỉ chéo mà quy tắc #2 cấm.
        services.AddScoped<ILuuTruAnh, MinioLuuTruAnh>();

        return services;
    }
}

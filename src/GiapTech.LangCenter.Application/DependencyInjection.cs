using FluentValidation;
using GiapTech.LangCenter.Application.Common.Behaviors;
using GiapTech.LangCenter.Application.Common.Interfaces;
using GiapTech.LangCenter.Application.QuanTri.Email;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.LangCenter.Application;

public static class DependencyInjection
{
    public static IServiceCollection ThemApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyReference).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // SAU ValidationBehavior: chỉ ghi nhật ký cho lệnh đã hợp lệ. Request sai định dạng
        // là lỗi client, không phải thao tác nghiệp vụ.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(NhatKyBehavior<,>));

        // Mẫu email (FR-31). Đăng ký ở đây chứ không ở `Infrastructure`: nó chỉ đọc DbSet qua
        // `IAppDbContext`, không chạm hạ tầng ngoài nào.
        services.AddScoped<IMauEmail, DungMauEmail>();

        // Thư chào mừng (09/10/2026). Cùng lý do đặt ở đây: chỉ đọc DbSet và hai interface
        // đã đăng ký, không chạm hạ tầng ngoài nào.
        services.AddScoped<IThuChaoMung, ThuChaoMung>();

        return services;
    }
}

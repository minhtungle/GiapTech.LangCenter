using FluentValidation;
using GiapTech.LangCenter.Application.Common.Behaviors;
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

        return services;
    }
}

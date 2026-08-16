using FluentValidation;
using GiapTech.SoccerRoom.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GiapTech.SoccerRoom.Application;

public static class DependencyInjection
{
    public static IServiceCollection ThemApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyReference).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}

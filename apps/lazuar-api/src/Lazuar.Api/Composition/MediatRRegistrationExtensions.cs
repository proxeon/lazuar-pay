namespace Lazuar.Api.Composition;

/// <summary>
/// Registers MediatR handlers from the host assembly plus each module Application/Infrastructure assembly.
/// CRM has no Application layer assembly — only Infrastructure is registered for that module.
/// Application assembly markers resolve via transitive ProjectReferences from Infrastructure
/// (host must not add direct *Application.csproj refs — Phase 17.5).
/// </summary>
public static class MediatRRegistrationExtensions
{
    public static IServiceCollection AddLazuarMediatR(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.One.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Application.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Application.DependencyInjection).Assembly);

            cfg.RegisterServicesFromAssembly(typeof(Modules.One.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Messaging.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Payments.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.CRM.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Ops.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Billing.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Lhdn.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Commerce.Infrastructure.DependencyInjection).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(Modules.Communications.Infrastructure.DependencyInjection).Assembly);
        });

        return services;
    }
}

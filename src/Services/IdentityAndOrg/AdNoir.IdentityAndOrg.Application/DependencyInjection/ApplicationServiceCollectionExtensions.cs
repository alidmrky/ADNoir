using AdNoir.IdentityAndOrg.Application.Identity;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Application.OrgUnits;
using AdNoir.IdentityAndOrg.Application.Tenants;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AdNoir.IdentityAndOrg.Application.DependencyInjection;

/// <summary>Registers the service's application layer.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and their validators.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddIdentityAndOrgApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IOrgUnitService, OrgUnitService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRoleGrantPolicy, RoleGrantPolicy>();

        // Scanned rather than listed one by one: a validator that exists but was never
        // registered silently validates nothing, and that is invisible in review.
        services.AddValidatorsFromAssemblyContaining<ITenantService>(ServiceLifetime.Scoped);

        return services;
    }
}

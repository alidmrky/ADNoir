using AdNoir.Notification.Application.Emails;
using Microsoft.Extensions.DependencyInjection;

namespace AdNoir.Notification.Application.DependencyInjection;

/// <summary>Registers Notification application services independently of persistence.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers interface-based application services.</summary>
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IEmailQueryService, EmailQueryService>();
        return services;
    }
}

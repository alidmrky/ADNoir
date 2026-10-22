using AdNoir.BuildingBlocks.Services;
using AdNoir.Sample.Application.SampleItems;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AdNoir.Sample.Application.DependencyInjection;

/// <summary>Registers the Sample service's application layer.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Registers application services and their validators.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSampleApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ISampleItemService, SampleItemService>();

        // Scanned rather than listed one by one: a validator that exists but was never
        // registered silently validates nothing, and that is invisible in review.
        services.AddValidatorsFromAssemblyContaining<ISampleItemService>(ServiceLifetime.Scoped);

        return services;
    }
}

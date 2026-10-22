using AdNoir.Sample.Domain.SampleItems;
using AdNoir.Sample.Application.SampleItems;
using AdNoir.Sample.Infrastructure.Persistence;
using System.Reflection;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.IdentityAndOrg.Application.Tenants;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.IdentityAndOrg.Infrastructure.Persistence;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// The assemblies under architectural test, resolved through a type rather than by name so a
/// rename cannot silently reduce a rule to testing nothing. Host assemblies are loaded by
/// name and checked against the source project inventory.
/// </summary>
public static class AdNoirAssemblies
{
    /// <summary>The gateway assembly.</summary>
    public static Assembly Gateway => typeof(AdNoir.Gateway.Configuration.GatewayOptions).Assembly;

    /// <summary>The shared contracts assembly.</summary>
    public static Assembly SharedContracts => typeof(BaseResponse).Assembly;

    /// <summary>The domain abstractions assembly.</summary>
    public static Assembly DomainAbstractions => typeof(IEntity).Assembly;

    /// <summary>The building blocks assembly.</summary>
    public static Assembly BuildingBlocks => typeof(ITenantContext).Assembly;

    /// <summary>The MongoDB building blocks assembly.</summary>
    public static Assembly BuildingBlocksMongo => typeof(MongoRepository<>).Assembly;

    /// <summary>The IdentityAndOrg domain assembly.</summary>
    public static Assembly IdentityAndOrgDomain => typeof(Tenant).Assembly;

    /// <summary>The IdentityAndOrg application assembly.</summary>
    public static Assembly IdentityAndOrgApplication => typeof(ITenantService).Assembly;

    /// <summary>The IdentityAndOrg infrastructure assembly.</summary>
    public static Assembly IdentityAndOrgInfrastructure => typeof(MongoTenantRepository).Assembly;

    /// <summary>The Sample domain assembly.</summary>
    public static Assembly SampleDomain => typeof(SampleItem).Assembly;

    /// <summary>The Sample application assembly.</summary>
    public static Assembly SampleApplication => typeof(ISampleItemService).Assembly;

    /// <summary>The Sample infrastructure assembly.</summary>
    public static Assembly SampleInfrastructure => typeof(MongoSampleItemRepository).Assembly;

    /// <summary>Every AdNoir assembly currently in the solution.</summary>
    /// <returns>The assemblies.</returns>
    /// <remarks>
    /// Adding a service here is what brings it under every existing rule. Forgetting to do so
    /// leaves the rules green while they check nothing about the new code - the
    /// "passes by having nothing to look at" failure mode.
    /// </remarks>
    public static IReadOnlyList<Assembly> All() =>
    [
        typeof(AdNoir.Notification.Domain.Emails.EmailDelivery).Assembly,
        typeof(AdNoir.Notification.Application.Emails.EmailQueryService).Assembly,
        typeof(AdNoir.Notification.Infrastructure.Configuration.NotificationOptions).Assembly,
        Gateway,
        SharedContracts,
        DomainAbstractions,
        BuildingBlocks,
        BuildingBlocksMongo,
        IdentityAndOrgDomain,
        IdentityAndOrgApplication,
        IdentityAndOrgInfrastructure,
        SampleDomain,
        SampleApplication,
        SampleInfrastructure,
        Assembly.Load("AdNoir.IdentityAndOrg.Api"),
        Assembly.Load("AdNoir.Sample.Api"),
        Assembly.Load("AdNoir.Notification.Api"),
        Assembly.Load("AdNoir.DbMigrator"),
    ];

    /// <summary>
    /// The service Application assemblies, which must never see a database driver.
    /// </summary>
    /// <returns>The assemblies.</returns>
    public static IReadOnlyList<Assembly> ApplicationLayers() =>
    [
        IdentityAndOrgApplication,
        SampleApplication,
        typeof(AdNoir.Notification.Application.Emails.EmailQueryService).Assembly,
    ];

    /// <summary>Assembly name prefixes that identify AdNoir's own code.</summary>
    public const string RootNamespace = "AdNoir";
}

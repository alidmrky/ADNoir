using System.Reflection;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// Plan I.2: MassTransit stops at the Infrastructure layer.
/// </summary>
/// <remarks>
/// Application code publishes through the thin <c>IIntegrationEventPublisher</c> in
/// Shared.Contracts and never sees the library. Infrastructure, being the adapter layer, uses
/// MassTransit directly - consumers, retry filters, the test harness. Wrapping the library
/// everywhere is an anti-pattern: it hides exactly the features the library was chosen for.
/// </remarks>
public sealed class MessagingBoundaryTests
{
    [Fact]
    public void Domain_and_Application_assemblies_do_not_reference_MassTransit()
    {
        List<string> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            string name = assembly.GetName().Name ?? string.Empty;

            if (!IsDomainOrApplication(name))
            {
                continue;
            }

            IEnumerable<string> massTransitReferences = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(referenced => referenced.StartsWith("MassTransit", StringComparison.OrdinalIgnoreCase));

            violations.AddRange(massTransitReferences.Select(referenced => $"{name} references {referenced}"));
        }

        violations.ShouldBeEmpty(
            "MassTransit must not be visible from Domain or Application. " +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void SharedContracts_owns_the_publishing_interface()
    {
        Type? publisher = AdNoirAssemblies.SharedContracts
            .GetTypes()
            .FirstOrDefault(type => type.Name == "IIntegrationEventPublisher");

        publisher.ShouldNotBeNull(
            "IIntegrationEventPublisher lives in Shared.Contracts so Application can publish " +
            "without taking a dependency on a messaging package.");
    }

    [Fact]
    public void No_type_is_named_IEventBus_or_uses_an_abbreviated_house_prefix()
    {
        // Plan I.2 settles the naming: "bus" is ambiguous once domain events exist alongside
        // integration events, and abbreviated prefixes like IADEventBus are ruled out by the
        // .NET naming guidelines.
        List<string> offenders = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            offenders.AddRange(assembly
                .GetTypes()
                .Where(type => type.Name is "IEventBus" or "EventBus" ||
                               type.Name.StartsWith("IAD", StringComparison.Ordinal))
                .Select(type => $"{assembly.GetName().Name}: {type.Name}"));
        }

        offenders.ShouldBeEmpty(
            $"use IIntegrationEventPublisher / IAdNoirEventBus instead." +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
    }

    private static bool IsDomainOrApplication(string assemblyName) =>
        assemblyName.EndsWith(".Domain", StringComparison.Ordinal) ||
        assemblyName.EndsWith(".Domain.Abstractions", StringComparison.Ordinal) ||
        assemblyName.EndsWith(".Application", StringComparison.Ordinal);
}

using System.Reflection;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// The layer dependency rules, enforced by machine rather than by review.
/// </summary>
/// <remarks>
/// Four shared projects will periodically feel like too many and someone will want to move
/// "just this one thing" into BuildingBlocks. These tests are
/// what turns that instinct into a failing build instead of a Mongo reference in Application.
/// </remarks>
public sealed class LayerDependencyTests
{
    [Fact]
    public void SharedContracts_references_no_other_AdNoir_project()
    {
        IReadOnlyList<string> referenced = AdNoirReferences(AdNoirAssemblies.SharedContracts);

        referenced.ShouldBeEmpty(
            "AdNoir.Shared.Contracts must reference nothing. Every other rule " +
            "in the dependency graph rests on this one, so a single reference here quietly " +
            $"weakens all of them. Found: {string.Join(", ", referenced)}");
    }

    [Fact]
    public void DomainAbstractions_references_no_other_AdNoir_project()
    {
        IReadOnlyList<string> referenced = AdNoirReferences(AdNoirAssemblies.DomainAbstractions);

        referenced.ShouldBeEmpty(
            "AdNoir.Domain.Abstractions must reference nothing. It is what keeps " +
            $"service Domain projects free of infrastructure. Found: {string.Join(", ", referenced)}");
    }

    [Fact]
    public void DomainAbstractions_has_no_infrastructure_packages()
    {
        IReadOnlyList<string> forbidden = ForbiddenInfrastructureReferences(AdNoirAssemblies.DomainAbstractions);

        forbidden.ShouldBeEmpty(
            "AdNoir.Domain.Abstractions must be pure BCL. Domain code that can see MongoDB, " +
            "MassTransit or ASP.NET Core will eventually use them. " +
            $"Found: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void BuildingBlocks_does_not_reference_MongoDB()
    {
        IReadOnlyList<string> mongoReferences = AdNoirAssemblies.BuildingBlocks
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("MongoDB", StringComparison.OrdinalIgnoreCase))
            .ToList();

        mongoReferences.ShouldBeEmpty(
            "AdNoir.BuildingBlocks must have no MongoDB dependency. This is the " +
            "rule that makes \"the Application layer is independent of the database technology\" " +
            "machine-enforceable: Application references BuildingBlocks but never BuildingBlocks.Mongo. " +
            "If BuildingBlocks itself pulls in the driver, that guarantee is gone and nothing else " +
            $"will notice. Found: {string.Join(", ", mongoReferences)}");
    }

    [Fact]
    public void BuildingBlocks_references_only_the_two_contract_projects()
    {
        IReadOnlyList<string> referenced = AdNoirReferences(AdNoirAssemblies.BuildingBlocks);

        referenced.ShouldBe(
            ["AdNoir.Domain.Abstractions", "AdNoir.Shared.Contracts"],
            ignoreOrder: true,
            "AdNoir.BuildingBlocks may reference only Shared.Contracts and Domain.Abstractions " +
            ".");
    }

    [Fact]
    public void BuildingBlocksMongo_references_BuildingBlocks()
    {
        IReadOnlyList<string> referenced = AdNoirReferences(AdNoirAssemblies.BuildingBlocksMongo);

        referenced.ShouldContain(
            "AdNoir.BuildingBlocks",
            "AdNoir.BuildingBlocks.Mongo builds on BuildingBlocks.");
    }

    [Fact]
    public void Service_Application_layers_cannot_see_the_Mongo_building_blocks()
    {
        // Application must not see BuildingBlocks.Mongo: that separation is what makes
        // "the Application layer is independent of the database technology" enforceable.
        List<string> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.ApplicationLayers())
        {
            IEnumerable<string> forbidden = assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => name == "AdNoir.BuildingBlocks.Mongo" ||
                               name.StartsWith("MongoDB", StringComparison.OrdinalIgnoreCase));

            violations.AddRange(forbidden.Select(name => $"{assembly.GetName().Name} references {name}"));
        }

        violations.ShouldBeEmpty(
            "an Application layer must be independent of the database technology. " +
            "This is enforced by the missing project reference, and checked here so that adding one " +
            $"back fails the build rather than passing review.{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Service_Domain_layers_reference_only_DomainAbstractions()
    {
        List<string> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            string name = assembly.GetName().Name ?? string.Empty;

            if (!name.EndsWith(".Domain", StringComparison.Ordinal))
            {
                continue;
            }

            IReadOnlyList<string> referenced = AdNoirReferences(assembly);

            IEnumerable<string> unexpected =
                referenced.Where(reference => reference != "AdNoir.Domain.Abstractions");

            violations.AddRange(unexpected.Select(reference => $"{name} references {reference}"));

            IEnumerable<string> infrastructure = ForbiddenInfrastructureReferences(assembly);
            violations.AddRange(infrastructure.Select(reference => $"{name} references {reference}"));
        }

        violations.ShouldBeEmpty(
            "a service Domain project may reference only AdNoir.Domain.Abstractions, and no " +
            "infrastructure package. Domain rules that can see MongoDB will " +
            $"eventually use it.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void No_AdNoir_assembly_references_a_service_Api_project()
    {
        // Composition points one way. An Infrastructure or Application project that can see an
        // Api project is a cycle waiting for its second edge.
        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            IReadOnlyList<string> apiReferences = AdNoirReferences(assembly)
                .Where(name => name.EndsWith(".Api", StringComparison.Ordinal))
                .ToList();

            apiReferences.ShouldBeEmpty(
                $"{assembly.GetName().Name} references an Api project: {string.Join(", ", apiReferences)}");
        }
    }

    private static List<string> AdNoirReferences(Assembly assembly) =>
        [.. assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(AdNoirAssemblies.RootNamespace + ".", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    private static List<string> ForbiddenInfrastructureReferences(Assembly assembly)
    {
        string[] forbiddenPrefixes =
        [
            "MongoDB",
            "MassTransit",
            "Microsoft.AspNetCore",
            "Microsoft.Extensions.Caching",
            "StackExchange.Redis",
            "Serilog",
            "OpenTelemetry",
            "FluentValidation",
        ];

        return
        [
            .. assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => forbiddenPrefixes.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        ];
    }
}

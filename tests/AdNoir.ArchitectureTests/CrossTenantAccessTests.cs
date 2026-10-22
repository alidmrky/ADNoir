using System.Reflection;
using AdNoir.ArchitectureTests.Il;
using AdNoir.ArchitectureTests.Samples;
using AdNoir.Domain.Abstractions.Repositories;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// Crossing tenant boundaries is allowed, but never quietly.
/// </summary>
/// <remarks>
/// Every method that calls into <see cref="IUnsafeCrossTenantQuery{TEntity}"/> must carry
/// <see cref="AllowsCrossTenantAccessAttribute"/> with a written reason. The point is not
/// bureaucracy: it puts each leak point in the code next to its justification, where a
/// reviewer will actually see it.
/// </remarks>
public sealed class CrossTenantAccessTests
{
    private const string UnsafeQueryTypeName =
        "AdNoir.Domain.Abstractions.Repositories.IUnsafeCrossTenantQuery`1";

    private const string AllowsAttributeName =
        "AdNoir.Domain.Abstractions.Repositories.AllowsCrossTenantAccessAttribute";

    [Fact]
    public void Rule_flags_an_unjustified_cross_tenant_call()
    {
        IReadOnlyList<IlViolation> violations = IlScanner.FindCallsInto(
            typeof(RuleProbes).Assembly,
            UnsafeQueryTypeName,
            AllowsAttributeName);

        violations.ShouldContain(
            violation => violation.TypeName.Contains(
                nameof(RuleProbes.UnjustifiedCrossTenantProbe),
                StringComparison.Ordinal),
            "a cross-tenant call without the attribute must be reported");
        violations.ShouldContain(violation => violation.TypeName.Contains(
            nameof(RuleProbes.UnjustifiedAsyncCrossTenantProbe), StringComparison.Ordinal));
    }

    [Fact]
    public void Rule_accepts_a_justified_cross_tenant_call()
    {
        IReadOnlyList<IlViolation> violations = IlScanner.FindCallsInto(
            typeof(RuleProbes).Assembly,
            UnsafeQueryTypeName,
            AllowsAttributeName);

        violations.ShouldNotContain(
            violation => violation.TypeName.Contains(
                nameof(RuleProbes.JustifiedCrossTenantProbe),
                StringComparison.Ordinal),
            "a call carrying [AllowsCrossTenantAccess] is allowed");
        violations.ShouldNotContain(violation => violation.TypeName.Contains(
            nameof(RuleProbes.JustifiedAsyncCrossTenantProbe), StringComparison.Ordinal));
    }

    [Fact]
    public void Production_code_never_crosses_tenants_without_a_justification()
    {
        List<IlViolation> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            violations.AddRange(IlScanner.FindCallsInto(assembly, UnsafeQueryTypeName, AllowsAttributeName));
        }

        violations.ShouldBeEmpty(
            "every method using IUnsafeCrossTenantQuery must be marked " +
            "[AllowsCrossTenantAccess(\"reason\")]. Without the attribute a " +
            "tenant-boundary crossing looks like ordinary data access in review." +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }
}

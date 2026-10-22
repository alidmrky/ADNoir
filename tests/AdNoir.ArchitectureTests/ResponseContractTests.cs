using System.Reflection;
using AdNoir.ArchitectureTests.Rules;
using AdNoir.ArchitectureTests.Samples;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// The response contract and the DTO rule.
/// </summary>
/// <remarks>
/// Each rule is checked twice: once against the probe samples, to prove the rule detects a
/// violation at all, and once against the real assemblies. The probe half matters because
/// without it these tests could pass by having nothing to look at.
/// </remarks>
public sealed class ResponseContractTests
{
    [Fact]
    public void Rule_flags_a_service_that_breaks_the_contract()
    {
        IReadOnlyList<MethodInfo> methods =
            ResponseContractRule.ContractMethodsOf(typeof(RuleProbes.BadProbeService));

        List<string> violations = [.. methods.SelectMany(ResponseContractRule.ContractViolations)];

        violations.ShouldNotBeEmpty("the probe service breaks the contract on purpose");

        violations.ShouldContain(
            violation => violation.Contains("NotWrappedAsync", StringComparison.Ordinal),
            "returning a bare DTO must be reported");

        violations.ShouldContain(
            violation => violation.Contains("NotAsync", StringComparison.Ordinal),
            "a synchronous method must be reported");

        violations.ShouldContain(
            violation => violation.Contains("NoCancellationTokenAsync", StringComparison.Ordinal),
            "a missing CancellationToken must be reported");
    }

    [Fact]
    public void Rule_accepts_a_service_that_follows_the_contract()
    {
        IReadOnlyList<MethodInfo> methods =
            ResponseContractRule.ContractMethodsOf(typeof(RuleProbes.GoodProbeService));

        methods.ShouldNotBeEmpty("the probe service must expose methods for the check to be meaningful");

        List<string> violations = [.. methods.SelectMany(ResponseContractRule.ContractViolations)];

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Rule_flags_a_service_that_returns_a_domain_entity()
    {
        IReadOnlyList<MethodInfo> methods =
            ResponseContractRule.ContractMethodsOf(typeof(RuleProbes.BadProbeService));

        List<string> violations = [.. methods.SelectMany(ResponseContractRule.PayloadViolations)];

        violations.ShouldContain(
            violation => violation.Contains("LeaksEntityAsync", StringComparison.Ordinal),
            "returning an IEntity from a service must be reported");
    }

    [Fact]
    public void Every_application_service_follows_the_response_contract()
    {
        List<string> violations = [];

        foreach (Assembly assembly in AdNoirAssemblies.All())
        {
            foreach (Type serviceType in ResponseContractRule.ServiceTypesIn(assembly))
            {
                foreach (MethodInfo method in ResponseContractRule.ContractMethodsOf(serviceType))
                {
                    violations.AddRange(ResponseContractRule.ContractViolations(method));
                    violations.AddRange(ResponseContractRule.PayloadViolations(method));
                }
            }
        }

        violations.ShouldBeEmpty(
            "every IApplicationService method must return Task<BaseResponse> or " +
            "Task<BaseResponse<T>>, be asynchronous, take a CancellationToken last, and never " +
            $"return a domain entity.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }
}

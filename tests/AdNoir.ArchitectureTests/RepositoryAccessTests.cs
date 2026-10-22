using AdNoir.ArchitectureTests.Rules;
using AdNoir.ArchitectureTests.Samples;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

public sealed class RepositoryAccessTests
{
    [Fact]
    public void Services_use_the_shared_repository_for_collection_data_access()
    {
        string[] serviceLayers = [".Domain", ".Application", ".Infrastructure", ".Api"];
        var assemblies = AdNoirAssemblies.All().Where(assembly =>
            serviceLayers.Any(suffix => assembly.GetName().Name!.EndsWith(suffix, StringComparison.Ordinal)));
        assemblies.SelectMany(RepositoryAccessRule.Check).ShouldBeEmpty();
    }

    [Fact]
    public void Rule_detects_sync_async_and_delegate_bypasses_but_allows_repository_and_index_access()
    {
        var violations = RepositoryAccessRule.Check(typeof(RepositoryAccessProbes).Assembly);
        violations.ShouldContain(item => item.MethodName == "DirectRead");
        violations.ShouldContain(item => item.TypeName.Contains("<AsyncWrite>"));
        violations.ShouldContain(item => item.MethodName == "DelegateWrite");
        violations.ShouldNotContain(item => item.TypeName.Contains(nameof(AllowedRepositoryProbe)));
        violations.ShouldNotContain(item => item.MethodName == "SharedRead" || item.MethodName == "Indexes");
    }
}

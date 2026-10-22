using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

public sealed class AssemblyInventoryTests
{
    [Fact]
    public void Every_source_project_is_registered_once()
    {
        var root = FindRoot();
        var expected = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Select(path => XDocument.Load(path).Descendants("AssemblyName").FirstOrDefault()?.Value ?? Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal).ToArray();
        var actual = AdNoirAssemblies.All().Select(assembly => assembly.GetName().Name!).Order(StringComparer.Ordinal).ToArray();
        Matches(expected, actual).ShouldBeTrue($"Expected: {string.Join(", ", expected)}; registered: {string.Join(", ", actual)}");
        AdNoirAssemblies.ApplicationLayers().Select(assembly => assembly.GetName().Name!).Order(StringComparer.Ordinal)
            .ToArray().ShouldBe(expected.Where(name => name.EndsWith(".Application", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public void Inventory_rejects_missing_extra_and_duplicate_entries()
    {
        string[] expected = ["AdNoir.One", "AdNoir.Two"];
        Matches(expected, ["AdNoir.Two", "AdNoir.One"]).ShouldBeTrue();
        Matches(expected, ["AdNoir.One"]).ShouldBeFalse();
        Matches(expected, ["AdNoir.One", "AdNoir.Two", "AdNoir.Other"]).ShouldBeFalse();
        Matches(expected, ["AdNoir.One", "AdNoir.Two", "AdNoir.Two"]).ShouldBeFalse();
    }

    private static bool Matches(IEnumerable<string> expected, IEnumerable<string> actual) =>
        expected.Order(StringComparer.Ordinal).SequenceEqual(actual.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AdNoir.sln")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new InvalidOperationException("Run architecture checks from a source checkout; project inventory must not be skipped.");
    }
}

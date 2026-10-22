using AdNoir.BuildingBlocks.Configuration;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Configuration;

/// <summary>
/// The <c>.env</c> reader. With no container runtime on this machine, this is
/// the only thing that turns the file into configuration, so its parsing rules matter.
/// </summary>
public sealed class DotEnvConfigurationProviderTests
{
    [Fact]
    public void Double_underscores_become_configuration_sections()
    {
        Dictionary<string, string?> data = Parse("AdNoir__Mongo__ConnectionString=mongodb://localhost:27017");

        data["AdNoir:Mongo:ConnectionString"].ShouldBe("mongodb://localhost:27017");
    }

    [Fact]
    public void Comments_and_blank_lines_are_ignored()
    {
        Dictionary<string, string?> data = Parse(
            "# a comment",
            string.Empty,
            "   ",
            "AdNoir__Redis__InstanceName=adnoir");

        data.Count.ShouldBe(1);
        data["AdNoir:Redis:InstanceName"].ShouldBe("adnoir");
    }

    [Fact]
    public void An_export_prefix_is_accepted()
    {
        Dictionary<string, string?> data = Parse("export AdNoir__Jwt__Audience=adnoir");

        data["AdNoir:Jwt:Audience"].ShouldBe("adnoir");
    }

    [Fact]
    public void Surrounding_quotes_are_stripped()
    {
        Dictionary<string, string?> data = Parse(
            "A=\"double quoted\"",
            "B='single quoted'");

        data["A"].ShouldBe("double quoted");
        data["B"].ShouldBe("single quoted");
    }

    [Fact]
    public void Escapes_are_resolved_inside_double_quotes_only()
    {
        Dictionary<string, string?> data = Parse(
            "A=\"line1\\nline2\"",
            "B='line1\\nline2'");

        data["A"].ShouldBe("line1\nline2");
        data["B"].ShouldBe("line1\\nline2");
    }

    [Fact]
    public void A_hash_inside_a_value_is_kept()
    {
        // Inline comments are deliberately unsupported: treating everything after # as a
        // comment would silently truncate passwords, and the resulting authentication failure
        // points nowhere near the .env file.
        Dictionary<string, string?> data = Parse("AdNoir__RabbitMq__Password=p@ss#word1");

        data["AdNoir:RabbitMq:Password"].ShouldBe("p@ss#word1");
    }

    [Fact]
    public void An_equals_sign_inside_a_value_is_kept()
    {
        Dictionary<string, string?> data = Parse("A=base64value==");

        data["A"].ShouldBe("base64value==");
    }

    [Fact]
    public void An_empty_value_is_preserved_rather_than_dropped()
    {
        // An empty Redis connection string is meaningful: it selects the in-memory fallback.
        Dictionary<string, string?> data = Parse("AdNoir__Redis__ConnectionString=");

        data.ShouldContainKey("AdNoir:Redis:ConnectionString");
        data["AdNoir:Redis:ConnectionString"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Lines_without_a_key_are_skipped()
    {
        Dictionary<string, string?> data = Parse("=novalue", "novalueatall", "A=1");

        data.Count.ShouldBe(1);
        data["A"].ShouldBe("1");
    }

    private static Dictionary<string, string?> Parse(params string[] lines) =>
        DotEnvConfigurationProvider.Parse(lines);
}

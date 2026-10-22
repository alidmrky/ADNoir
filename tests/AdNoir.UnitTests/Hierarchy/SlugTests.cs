using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Hierarchy;

/// <summary>
/// Slug derivation. A slug is persisted inside every HierarchyPath, so changing these rules
/// invalidates stored data - which is the reason the rules are pinned here.
/// </summary>
public sealed class SlugTests
{
    [Theory]
    [InlineData("Ankara", "ankara")]
    [InlineData("Head Office", "head-office")]
    [InlineData("  Head   Office  ", "head-office")]
    [InlineData("Branch #7", "branch-7")]
    [InlineData("a/b/c", "a-b-c")]
    [InlineData("Already-A-Slug", "already-a-slug")]
    public void Text_becomes_a_lower_case_dashed_segment(string input, string expected) =>
        Slug.From(input).ShouldBe(expected);

    [Theory]
    [InlineData("Çankaya Şubesi", "cankaya-subesi")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("Işık", "isik")]
    [InlineData("Gaziantep Güneydoğu", "gaziantep-guneydogu")]
    [InlineData("ÖÇŞİĞÜ", "ocsigu")]
    public void Turkish_characters_map_to_ascii_rather_than_vanishing(string input, string expected)
    {
        // Diacritic stripping alone would turn dotless 'ı' into nothing and 'İ' into "i" plus a
        // combining dot, so the explicit map is what keeps these readable.
        Slug.From(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Café")]
    [InlineData("Köln")]
    public void Other_accented_latin_text_keeps_its_base_letters(string input) =>
        Slug.From(input).ShouldNotBeEmpty();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("!!!")]
    public void Nothing_usable_produces_an_empty_slug(string? input) =>
        Slug.From(input).ShouldBeEmpty();

    [Fact]
    public void Separator_runs_collapse_and_never_lead_or_trail()
    {
        string slug = Slug.From("  ---Head???Office!!!  ");

        slug.ShouldBe("head-office");
        slug.ShouldNotStartWith("-");
        slug.ShouldNotEndWith("-");
    }

    [Fact]
    public void A_long_name_is_truncated_without_a_trailing_dash()
    {
        string slug = Slug.From(new string('a', Slug.MaxLength + 50));

        slug.Length.ShouldBe(Slug.MaxLength);
        slug.ShouldNotEndWith("-");
    }

    [Fact]
    public void Truncation_cannot_leave_a_dash_at_the_boundary()
    {
        // The character landing exactly on the limit is the separator, so trimming must run
        // after the cut rather than before it.
        string slug = Slug.From(new string('a', Slug.MaxLength) + " tail");

        slug.ShouldBe(new string('a', Slug.MaxLength));
    }

    [Fact]
    public void Everything_From_produces_is_accepted_by_IsValid()
    {
        foreach (string input in new[] { "Çankaya Şubesi", "Branch #7", "a/b/c", "  Head   Office  " })
        {
            Slug.IsValid(Slug.From(input)).ShouldBeTrue($"'{input}' produced an invalid slug");
        }
    }

    [Theory]
    [InlineData("ankara", true)]
    [InlineData("branch-7", true)]
    [InlineData("a", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("double--dash", false)]
    [InlineData("UpperCase", false)]
    [InlineData("has space", false)]
    [InlineData("türkçe", false)]
    [InlineData("under_score", false)]
    public void IsValid_accepts_only_lower_case_alphanumerics_with_single_dashes(string? slug, bool expected) =>
        Slug.IsValid(slug).ShouldBe(expected);

    [Fact]
    public void IsValid_rejects_anything_over_the_length_limit() =>
        Slug.IsValid(new string('a', Slug.MaxLength + 1)).ShouldBeFalse();

    [Fact]
    public void A_collision_suffix_is_appended() =>
        Slug.WithSuffix("ankara", 2).ShouldBe("ankara-2");

    [Fact]
    public void A_collision_suffix_keeps_the_result_within_the_limit()
    {
        string suffixed = Slug.WithSuffix(new string('a', Slug.MaxLength), 12);

        suffixed.Length.ShouldBeLessThanOrEqualTo(Slug.MaxLength);
        suffixed.ShouldEndWith("-12");
        Slug.IsValid(suffixed).ShouldBeTrue();
    }

    [Fact]
    public void A_collision_suffix_does_not_produce_a_double_dash()
    {
        // Cutting the head can land on a dash, which would make the result invalid.
        string head = new string('a', Slug.MaxLength - 3) + "-bbb";

        Slug.IsValid(Slug.WithSuffix(head, 2)).ShouldBeTrue();
    }

    [Fact]
    public void A_suffix_attempt_below_two_is_rejected() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Slug.WithSuffix("ankara", 1));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_suffix_needs_a_slug_to_work_from(string? slug) =>
        Should.Throw<ArgumentException>(() => Slug.WithSuffix(slug!, 2));
}

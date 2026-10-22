using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Hierarchy;

/// <summary>
/// Hierarchy path construction. The trailing separator is what stops a prefix comparison from
/// matching /dealer-1 against /dealer-10, and the length limit is what keeps the Mongo index
/// usable, so both are pinned here.
/// </summary>
public sealed class HierarchyPathsTests
{
    [Fact]
    public void A_root_child_hangs_directly_off_the_root() =>
        HierarchyPaths.Build(HierarchyPaths.Root, "dealer-1").ShouldBe("/dealer-1/");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_parent_path_is_treated_as_the_root(string? parent) =>
        HierarchyPaths.Build(parent, "dealer-1").ShouldBe("/dealer-1/");

    [Fact]
    public void A_child_path_extends_its_parent() =>
        HierarchyPaths.Build("/dealer-1/", "unit-a").ShouldBe("/dealer-1/unit-a/");

    [Fact]
    public void A_parent_path_missing_its_separators_is_normalised_first() =>
        HierarchyPaths.Build("dealer-1", "unit-a").ShouldBe("/dealer-1/unit-a/");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Building_without_a_slug_is_rejected(string? slug) =>
        Should.Throw<ArgumentException>(() => HierarchyPaths.Build("/dealer-1/", slug!));

    [Fact]
    public void Every_built_path_starts_and_ends_with_the_separator()
    {
        string path = HierarchyPaths.Build(HierarchyPaths.Build("/", "a"), "b");

        path[0].ShouldBe(HierarchyPaths.Separator);
        path[^1].ShouldBe(HierarchyPaths.Separator);
    }

    [Fact]
    public void A_sibling_prefix_cannot_be_mistaken_for_a_descendant()
    {
        // The reason the trailing separator exists: without it "/dealer-1" is a prefix of
        // "/dealer-10", and a subtree rewrite would silently capture an unrelated branch.
        string one = HierarchyPaths.Build(null, "dealer-1");
        string ten = HierarchyPaths.Build(null, "dealer-10");

        ten.StartsWith(one, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    [InlineData("/", "/")]
    [InlineData("a", "/a/")]
    [InlineData("/a", "/a/")]
    [InlineData("a/", "/a/")]
    [InlineData("/a/", "/a/")]
    [InlineData("  /a/b  ", "/a/b/")]
    public void Normalize_guarantees_both_separators(string? input, string expected) =>
        HierarchyPaths.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void An_empty_path_does_not_count_as_within_the_limit(string? path) =>
        HierarchyPaths.IsWithinLimit(path!).ShouldBeFalse();

    [Fact]
    public void A_path_at_the_limit_is_accepted() =>
        HierarchyPaths.IsWithinLimit(new string('a', HierarchyPaths.MaxLength)).ShouldBeTrue();

    [Fact]
    public void A_path_over_the_limit_is_rejected()
    {
        // Exceeding Mongo's index key limit does not error: the path simply stops being
        // indexed and subtree listings quietly become collection scans.
        HierarchyPaths.IsWithinLimit(new string('a', HierarchyPaths.MaxLength + 1)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    public void The_root_has_no_segments(string? path) =>
        HierarchyPaths.Segments(path).ShouldBeEmpty();

    [Fact]
    public void Segments_are_returned_outermost_first() =>
        HierarchyPaths.Segments("/dealer-1/unit-a/station-5/")
            .ShouldBe(["dealer-1", "unit-a", "station-5"]);

    [Theory]
    [InlineData("/dealer-1/unit-a/station-5/", "/dealer-1/unit-a/")]
    [InlineData("/dealer-1/unit-a/", "/dealer-1/")]
    [InlineData("/dealer-1/", "/")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    public void ParentOf_drops_the_last_segment(string? path, string expected) =>
        HierarchyPaths.ParentOf(path).ShouldBe(expected);

    [Fact]
    public void Rebase_moves_a_descendant_under_a_new_ancestor_path() =>
        HierarchyPaths.Rebase("/old/unit-a/station-5/", "/old/", "/new/")
            .ShouldBe("/new/unit-a/station-5/");

    [Fact]
    public void Rebase_handles_a_re_slug_as_well_as_a_move() =>
        HierarchyPaths.Rebase("/dealer-1/unit-a/", "/dealer-1/", "/dealer-one/")
            .ShouldBe("/dealer-one/unit-a/");

    [Fact]
    public void Rebase_leaves_the_depth_below_the_ancestor_untouched()
    {
        string rebased = HierarchyPaths.Rebase("/a/b/c/d/", "/a/", "/x/y/");

        rebased.ShouldBe("/x/y/b/c/d/");
        HierarchyPaths.Segments(rebased).Count.ShouldBe(5);
    }

    [Fact]
    public void Rebasing_a_path_that_is_not_a_descendant_is_rejected()
    {
        // Silently rewriting an unrelated node would corrupt a different branch of the tree.
        Should.Throw<ArgumentException>(() =>
            HierarchyPaths.Rebase("/other/unit-a/", "/old/", "/new/"));
    }

    [Theory]
    [InlineData(null, "/old/", "/new/")]
    [InlineData("/old/a/", null, "/new/")]
    [InlineData("/old/a/", "/old/", null)]
    public void Rebase_requires_all_three_paths(string? descendant, string? oldAncestor, string? newAncestor) =>
        Should.Throw<ArgumentException>(() =>
            HierarchyPaths.Rebase(descendant!, oldAncestor!, newAncestor!));
}

using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.OrgUnits;

/// <summary>
/// Hierarchy move, re-slug and cycle rules. A fault here corrupts the tree without raising
/// anything: paths and ancestors drift apart, and subtree queries start returning the wrong
/// set of records.
/// </summary>
public sealed class OrgUnitTreeTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // A fixed four-level tree: root / branch / unit / desk.
    private static readonly Guid RootId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid BranchId = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid UnitId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly Guid DeskId = Guid.Parse("00000000-0000-0000-0000-0000000000d4");
    private static readonly Guid NewParentId = Guid.Parse("00000000-0000-0000-0000-0000000000e5");

    private static OrgUnit Node(Guid id, string slug, string path, Guid[] ancestors, long version = 3) =>
        new()
        {
            Id = id,
            TenantId = TenantId,
            Name = slug,
            Slug = slug,
            HierarchyPath = path,
            Ancestors = ancestors,
            Version = version,
        };

    // ------------------------------------------------------------------ cycles

    [Fact]
    public void A_node_cannot_become_its_own_parent()
    {
        OrgUnit node = Node(BranchId, "branch", "/root/branch/", [RootId]);

        OrgUnitTree.WouldCreateCycle(node, node).ShouldBeTrue();
    }

    [Fact]
    public void A_node_cannot_move_into_its_own_subtree()
    {
        OrgUnit branch = Node(BranchId, "branch", "/root/branch/", [RootId]);
        OrgUnit descendant = Node(DeskId, "desk", "/root/branch/unit/desk/", [RootId, BranchId, UnitId]);

        OrgUnitTree.WouldCreateCycle(branch, descendant).ShouldBeTrue();
    }

    [Fact]
    public void A_node_can_move_under_an_unrelated_parent()
    {
        OrgUnit branch = Node(BranchId, "branch", "/root/branch/", [RootId]);
        OrgUnit elsewhere = Node(NewParentId, "other", "/root/other/", [RootId]);

        OrgUnitTree.WouldCreateCycle(branch, elsewhere).ShouldBeFalse();
    }

    [Fact]
    public void A_node_can_move_under_its_own_ancestor()
    {
        // Promoting a node towards the root is a legitimate move, not a cycle.
        OrgUnit unit = Node(UnitId, "unit", "/root/branch/unit/", [RootId, BranchId]);
        OrgUnit root = Node(RootId, "root", "/root/", []);

        OrgUnitTree.WouldCreateCycle(unit, root).ShouldBeFalse();
    }

    // ------------------------------------------------------------- positioning

    [Fact]
    public void A_node_without_a_parent_sits_at_the_root_with_no_ancestors()
    {
        (string path, IReadOnlyList<Guid> ancestors) = OrgUnitTree.PositionUnder(null, "root");

        path.ShouldBe("/root/");
        ancestors.ShouldBeEmpty();
    }

    [Fact]
    public void A_child_inherits_its_parents_chain_plus_the_parent_itself()
    {
        OrgUnit branch = Node(BranchId, "branch", "/root/branch/", [RootId]);

        (string path, IReadOnlyList<Guid> ancestors) = OrgUnitTree.PositionUnder(branch, "unit");

        path.ShouldBe("/root/branch/unit/");
        ancestors.ShouldBe([RootId, BranchId]);
    }

    [Fact]
    public void Positioning_without_a_slug_is_rejected() =>
        Should.Throw<ArgumentException>(() => OrgUnitTree.PositionUnder(null, "  "));

    // --------------------------------------------------------------- rewriting

    [Fact]
    public void Moving_a_node_rebases_every_descendant_path_and_chain()
    {
        OrgUnit unit = Node(UnitId, "unit", "/root/branch/unit/", [RootId, BranchId]);
        OrgUnit desk = Node(DeskId, "desk", "/root/branch/unit/desk/", [RootId, BranchId, UnitId]);

        IReadOnlyList<OrgUnitRewrite> rewrites = OrgUnitTree.ComputeSubtreeRewrites(
            BranchId,
            oldPath: "/root/branch/",
            newPath: "/root/other/branch/",
            newNodeAncestors: [RootId, NewParentId],
            descendants: [unit, desk]);

        rewrites.Count.ShouldBe(2);

        OrgUnitRewrite unitRewrite = rewrites.Single(r => r.Id == UnitId);
        unitRewrite.HierarchyPath.ShouldBe("/root/other/branch/unit/");
        unitRewrite.Ancestors.ShouldBe([RootId, NewParentId, BranchId]);

        OrgUnitRewrite deskRewrite = rewrites.Single(r => r.Id == DeskId);
        deskRewrite.HierarchyPath.ShouldBe("/root/other/branch/unit/desk/");
        deskRewrite.Ancestors.ShouldBe([RootId, NewParentId, BranchId, UnitId]);
    }

    [Fact]
    public void A_re_slug_rewrites_the_subtree_exactly_as_a_move_does()
    {
        // The trigger that is easy to miss: the node stays where it is, but its own segment
        // changed, so every descendant path below it is now wrong.
        OrgUnit unit = Node(UnitId, "unit", "/root/branch/unit/", [RootId, BranchId]);

        IReadOnlyList<OrgUnitRewrite> rewrites = OrgUnitTree.ComputeSubtreeRewrites(
            BranchId,
            oldPath: "/root/branch/",
            newPath: "/root/branch-renamed/",
            newNodeAncestors: [RootId],
            descendants: [unit]);

        rewrites.Single().HierarchyPath.ShouldBe("/root/branch-renamed/unit/");
        rewrites.Single().Ancestors.ShouldBe([RootId, BranchId]);
    }

    [Fact]
    public void The_moved_node_is_not_rewritten_as_its_own_descendant()
    {
        OrgUnit branch = Node(BranchId, "branch", "/root/branch/", [RootId]);

        OrgUnitTree.ComputeSubtreeRewrites(
            BranchId, "/root/branch/", "/root/other/branch/", [RootId, NewParentId], [branch])
            .ShouldBeEmpty();
    }

    [Fact]
    public void A_node_that_is_not_a_descendant_is_skipped_rather_than_rewritten()
    {
        // Rewriting it would move an unrelated branch. Skipping keeps the caller's bug local.
        OrgUnit unrelated = Node(UnitId, "unit", "/root/elsewhere/unit/", [RootId, NewParentId]);

        OrgUnitTree.ComputeSubtreeRewrites(
            BranchId, "/root/branch/", "/root/other/branch/", [RootId], [unrelated])
            .ShouldBeEmpty();
    }

    [Fact]
    public void Each_rewrite_carries_the_version_it_was_computed_from()
    {
        // The bulk update filters on this, so a concurrent edit is still detected instead of
        // being overwritten by the move.
        OrgUnit unit = Node(UnitId, "unit", "/root/branch/unit/", [RootId, BranchId], version: 42);

        OrgUnitTree.ComputeSubtreeRewrites(
            BranchId, "/root/branch/", "/root/other/branch/", [RootId], [unit])
            .Single().ExpectedVersion.ShouldBe(42);
    }

    [Fact]
    public void An_empty_subtree_produces_no_rewrites() =>
        OrgUnitTree.ComputeSubtreeRewrites(
            BranchId, "/root/branch/", "/root/other/branch/", [RootId], []).ShouldBeEmpty();

    [Fact]
    public void Rewriting_requires_both_paths() =>
        Should.Throw<ArgumentException>(() =>
            OrgUnitTree.ComputeSubtreeRewrites(BranchId, "  ", "/new/", [], []));

    // ------------------------------------------------------------ length guard

    [Fact]
    public void The_longest_resulting_path_accounts_for_the_moved_node_itself() =>
        OrgUnitTree.LongestResultingPath([], "/root/branch/").ShouldBe("/root/branch/".Length);

    [Fact]
    public void The_longest_resulting_path_finds_the_deepest_descendant()
    {
        OrgUnitRewrite shallow = new(UnitId, "/a/b/", [], 1);
        OrgUnitRewrite deep = new(DeskId, "/a/b/c/d/e/", [], 1);

        OrgUnitTree.LongestResultingPath([shallow, deep], "/a/").ShouldBe("/a/b/c/d/e/".Length);
    }

    [Fact]
    public void A_move_that_would_overflow_the_index_key_is_detectable_before_it_is_applied()
    {
        // This is the point of computing it up front: the alternative is discovering the
        // overflow half-way through the bulk write, with the tree already partly rewritten.
        string tooDeep = "/" + new string('a', HierarchyPaths.MaxLength) + "/";
        OrgUnitRewrite rewrite = new(DeskId, tooDeep, [], 1);

        int longest = OrgUnitTree.LongestResultingPath([rewrite], "/root/");

        HierarchyPaths.IsWithinLimit(new string('x', longest)).ShouldBeFalse();
    }
}

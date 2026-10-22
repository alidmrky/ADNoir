using AdNoir.IdentityAndOrg.Domain.Hierarchy;

namespace AdNoir.IdentityAndOrg.Domain.OrgUnits;

/// <summary>
/// The new position of one node after its ancestor moved or was re-slugged.
/// </summary>
/// <param name="Id">The node being rewritten.</param>
/// <param name="HierarchyPath">Its new path.</param>
/// <param name="Ancestors">Its new ancestor chain.</param>
/// <param name="ExpectedVersion">
/// The version the node carried when the rewrite was computed, so the bulk update can keep
/// the optimistic concurrency check instead of dropping it for convenience.
/// </param>
public sealed record OrgUnitRewrite(
    Guid Id,
    string HierarchyPath,
    IReadOnlyList<Guid> Ancestors,
    long ExpectedVersion);

/// <summary>
/// Pure hierarchy rules. No database, no async, no infrastructure - the
/// whole point is that the tree logic can be reasoned about on its own.
/// </summary>
public static class OrgUnitTree
{
    /// <summary>
    /// Whether moving <paramref name="node"/> under <paramref name="newParent"/> would create
    /// a cycle.
    /// </summary>
    /// <param name="node">The node being moved.</param>
    /// <param name="newParent">The proposed parent.</param>
    /// <returns><see langword="true"/> when the move is not allowed.</returns>
    /// <remarks>
    /// Two cases: a node cannot be its own parent, and it cannot move into its own subtree -
    /// which is exactly "the proposed parent has this node among its ancestors". Both are
    /// answered from data already on the nodes, with no extra query.
    /// </remarks>
    public static bool WouldCreateCycle(OrgUnit node, OrgUnit newParent)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(newParent);

        return newParent.Id == node.Id || newParent.Ancestors.Contains(node.Id);
    }

    /// <summary>Computes a node's path and ancestors under a given parent.</summary>
    /// <param name="parent">The parent, or null for a root node.</param>
    /// <param name="slug">The node's slug.</param>
    /// <returns>The path and ancestor chain the node should carry.</returns>
    public static (string Path, IReadOnlyList<Guid> Ancestors) PositionUnder(OrgUnit? parent, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return parent is null
            ? (HierarchyPaths.Build(HierarchyPaths.Root, slug), [])
            : (HierarchyPaths.Build(parent.HierarchyPath, slug), [.. parent.Ancestors, parent.Id]);
    }

    /// <summary>
    /// Computes the rewrites every descendant needs after a node's own path and ancestors
    /// changed.
    /// </summary>
    /// <param name="movedNodeId">The node that moved or was re-slugged.</param>
    /// <param name="oldPath">The node's path before the change.</param>
    /// <param name="newPath">The node's path after the change.</param>
    /// <param name="newNodeAncestors">The node's ancestor chain after the change.</param>
    /// <param name="descendants">
    /// Every node beneath it, found with <c>{ Ancestors: movedNodeId }</c>.
    /// </param>
    /// <returns>One rewrite per descendant.</returns>
    /// <remarks>
    /// <para>
    /// This runs for <strong>two</strong> triggers, and the second is easily missed:
    /// moving a node, and <em>renaming</em> it in a way that changes its slug.
    /// A slug change invalidates the <c>HierarchyPath</c> of the entire subtree just as a move
    /// does, so the same rewrite has to happen.
    /// </para>
    /// <para>
    /// Ancestors are rebuilt rather than string-replaced: everything up to and including the
    /// moved node is taken from its new chain, and the portion below it is kept as-is. Paths
    /// are rebased by prefix, which is safe because both paths end with the separator.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<OrgUnitRewrite> ComputeSubtreeRewrites(
        Guid movedNodeId,
        string oldPath,
        string newPath,
        IReadOnlyList<Guid> newNodeAncestors,
        IEnumerable<OrgUnit> descendants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        ArgumentNullException.ThrowIfNull(newNodeAncestors);
        ArgumentNullException.ThrowIfNull(descendants);

        List<OrgUnitRewrite> rewrites = [];

        foreach (OrgUnit descendant in descendants)
        {
            if (descendant.Id == movedNodeId)
            {
                continue;
            }

            int index = IndexOf(descendant.Ancestors, movedNodeId);

            if (index < 0)
            {
                // Not actually a descendant. Skipping is safer than guessing; the caller
                // passing an unrelated node is a bug we should not paper over by rewriting it.
                continue;
            }

            List<Guid> ancestors = [.. newNodeAncestors, movedNodeId];

            for (int i = index + 1; i < descendant.Ancestors.Count; i++)
            {
                ancestors.Add(descendant.Ancestors[i]);
            }

            rewrites.Add(new OrgUnitRewrite(
                descendant.Id,
                HierarchyPaths.Rebase(descendant.HierarchyPath, oldPath, newPath),
                ancestors,
                descendant.Version));
        }

        return rewrites;
    }

    /// <summary>
    /// The longest path any descendant would end up with after a rewrite, used to reject a
    /// move before it is applied rather than half-way through.
    /// </summary>
    /// <param name="rewrites">The computed rewrites.</param>
    /// <param name="nodePath">The moved node's own new path.</param>
    /// <returns>The longest resulting path length.</returns>
    public static int LongestResultingPath(IReadOnlyList<OrgUnitRewrite> rewrites, string nodePath)
    {
        ArgumentNullException.ThrowIfNull(rewrites);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodePath);

        int longest = nodePath.Length;

        foreach (OrgUnitRewrite rewrite in rewrites)
        {
            if (rewrite.HierarchyPath.Length > longest)
            {
                longest = rewrite.HierarchyPath.Length;
            }
        }

        return longest;
    }

    private static int IndexOf(IReadOnlyList<Guid> ancestors, Guid value)
    {
        for (int i = 0; i < ancestors.Count; i++)
        {
            if (ancestors[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}

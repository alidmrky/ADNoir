using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.OrgUnits.Dtos;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.OrgUnits;

/// <summary>Default <see cref="IOrgUnitService"/>.</summary>
/// <remarks>
/// <para>
/// Structural changes - move and re-slug - go through <see cref="RepositionAsync"/>, which
/// updates the node and its whole subtree inside one transaction. A partially updated tree
/// must never be left behind, because half the descendants would then carry a path that no
/// longer resolves and nothing would report it.
/// </para>
/// <para>
/// Subtree rewrites are computed in memory: the subtree is read, the new positions are
/// calculated by <see cref="OrgUnitTree"/>, and one bulk write applies them. For an
/// organisation tree - hundreds of nodes, not millions - this is the honest trade for keeping
/// each node's version in the update filter, which a server-side pipeline update would give up.
/// </para>
/// </remarks>
public sealed class OrgUnitService(
    IOrgUnitRepository repository,
    ITenantContext tenantContext,
    ITransactionalAuditRecorder auditRecorder,
    IUnitOfWork unitOfWork) : IOrgUnitService
{
    private const int MaxSlugAttempts = 50;

    private readonly IOrgUnitRepository _repository = repository;
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public async Task<BaseResponse<OrgUnitDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        OrgUnit? unit = await _repository.FindByIdAsync(id, cancellationToken);

        return unit is null
            ? BaseResponse<OrgUnitDto>.NotFound(MessageCodes.OrgUnit.NotFound, ("orgUnitId", id))
            : BaseResponse<OrgUnitDto>.Success(ToDto(unit));
    }

    /// <inheritdoc />
    public async Task<PagedResponse<OrgUnitDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        PagedResult<OrgUnit> page = await _repository.ListPagedAsync(
            pageNumber,
            pageSize,
            predicate: null,
            orderBy: unit => unit.HierarchyPath,
            descending: false,
            cancellationToken);

        return PagedResponse<OrgUnitDto>.Create(
            [.. page.Items.Select(ToDto)],
            pageNumber,
            pageSize,
            page.TotalCount);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<IReadOnlyList<OrgUnitDto>>> ListChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OrgUnit> children = await _repository.ListChildrenAsync(parentId, cancellationToken);

        return BaseResponse<IReadOnlyList<OrgUnitDto>>.Success([.. children.Select(ToDto)]);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<IReadOnlyList<OrgUnitDto>>> ListSubtreeAsync(
        Guid rootId,
        CancellationToken cancellationToken = default)
    {
        OrgUnit? root = await _repository.FindByIdAsync(rootId, cancellationToken);

        if (root is null)
        {
            return BaseResponse<IReadOnlyList<OrgUnitDto>>.NotFound(
                MessageCodes.OrgUnit.NotFound,
                ("orgUnitId", rootId));
        }

        IReadOnlyList<OrgUnit> subtree = await _repository.ListSubtreeAsync(rootId, cancellationToken);

        return BaseResponse<IReadOnlyList<OrgUnitDto>>.Success(
            [.. subtree.OrderBy(unit => unit.HierarchyPath, StringComparer.Ordinal).Select(ToDto)]);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<OrgUnitDto>> CreateAsync(
        CreateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        OrgUnit? parent = null;

        if (request.ParentId is Guid parentId)
        {
            if (!_tenantContext.HierarchyEnabled)
            {
                return BaseResponse<OrgUnitDto>.BusinessRuleViolation(MessageCodes.Tenant.HierarchyDisabled);
            }

            parent = await _repository.FindByIdAsync(parentId, cancellationToken);

            if (parent is null)
            {
                return BaseResponse<OrgUnitDto>.NotFound(
                    MessageCodes.OrgUnit.ParentNotFound,
                    ("parentId", parentId));
            }
        }

        BaseResponse<string> slugResult = await ResolveSlugAsync(
            request.Slug,
            request.Name,
            request.ParentId,
            excludingId: null,
            cancellationToken);

        if (!slugResult.IsSuccess)
        {
            return BaseResponse<OrgUnitDto>.Propagate(slugResult);
        }

        string slug = slugResult.Data!;

        OrgUnit unit = parent is null
            ? OrgUnit.CreateRoot(_tenantContext.TenantId, request.Name.Trim(), slug, request.Code)
            : OrgUnit.CreateChild(parent, request.Name.Trim(), slug, request.Code);

        if (!HierarchyPaths.IsWithinLimit(unit.HierarchyPath))
        {
            return BaseResponse<OrgUnitDto>.BusinessRuleViolation(
                MessageCodes.Tenant.HierarchyPathTooLong,
                ("maxLength", HierarchyPaths.MaxLength));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        await _repository.InsertAsync(unit, cancellationToken);
        await auditRecorder.RecordAsync(unit.TenantId, unit, ChangeKind.Created,
            OrgUnitAuditFields.Diff(null, unit), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<OrgUnitDto>.Created(ToDto(unit));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<OrgUnitDto>> RenameAsync(
        Guid id,
        RenameOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        OrgUnit? unit = await _repository.FindByIdAsync(id, cancellationToken);

        if (unit is null)
        {
            return BaseResponse<OrgUnitDto>.NotFound(MessageCodes.OrgUnit.NotFound, ("orgUnitId", id));
        }

        if (unit.Version != request.ExpectedVersion)
        {
            return BaseResponse<OrgUnitDto>.ConcurrencyConflict();
        }

        var auditBefore = OrgUnitAuditFields.Capture(unit);
        unit.Name = request.Name.Trim();
        unit.Code = request.Code;

        string? requestedSlug = string.IsNullOrWhiteSpace(request.Slug) ? null : request.Slug.Trim();

        // A name change alone leaves the path untouched. Only a slug change reshapes the
        // subtree, and it has to be asked for explicitly.
        if (requestedSlug is null || string.Equals(requestedSlug, unit.Slug, StringComparison.Ordinal))
        {
            await _unitOfWork.BeginAsync(cancellationToken);
            await _repository.UpdateAsync(unit, cancellationToken);
            await auditRecorder.RecordAsync(unit.TenantId, unit, ChangeKind.Updated,
                OrgUnitAuditFields.Diff(auditBefore, unit), cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return BaseResponse<OrgUnitDto>.Success(ToDto(unit));
        }

        if (!Slug.IsValid(requestedSlug))
        {
            return BaseResponse<OrgUnitDto>.ValidationFailed(
                [ResponseMessage.ForField(MessageCodes.Validation.InvalidFormat, nameof(request.Slug))]);
        }

        if (await _repository.SlugExistsUnderParentAsync(unit.ParentId, requestedSlug, id, cancellationToken))
        {
            return BaseResponse<OrgUnitDto>.Conflict(
                MessageCodes.OrgUnit.SlugConflict,
                ("slug", requestedSlug));
        }

        OrgUnit? parent = unit.ParentId is Guid parentId
            ? await _repository.FindByIdAsync(parentId, cancellationToken)
            : null;

        return await RepositionAsync(unit, parent, requestedSlug, auditBefore, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<OrgUnitDto>> MoveAsync(
        Guid id,
        MoveOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tenantContext.HierarchyEnabled)
        {
            return BaseResponse<OrgUnitDto>.BusinessRuleViolation(MessageCodes.Tenant.HierarchyDisabled);
        }

        OrgUnit? unit = await _repository.FindByIdAsync(id, cancellationToken);

        if (unit is null)
        {
            return BaseResponse<OrgUnitDto>.NotFound(MessageCodes.OrgUnit.NotFound, ("orgUnitId", id));
        }

        if (unit.Version != request.ExpectedVersion)
        {
            return BaseResponse<OrgUnitDto>.ConcurrencyConflict();
        }

        OrgUnit? newParent = null;

        if (request.NewParentId is Guid newParentId)
        {
            newParent = await _repository.FindByIdAsync(newParentId, cancellationToken);

            if (newParent is null)
            {
                return BaseResponse<OrgUnitDto>.NotFound(
                    MessageCodes.OrgUnit.ParentNotFound,
                    ("parentId", newParentId));
            }

            if (OrgUnitTree.WouldCreateCycle(unit, newParent))
            {
                return BaseResponse<OrgUnitDto>.BusinessRuleViolation(
                    MessageCodes.OrgUnit.CycleDetected,
                    ("orgUnitId", id),
                    ("targetParentId", newParentId));
            }
        }

        if (unit.ParentId == request.NewParentId)
        {
            // Already where it is being moved to. Doing nothing beats writing a no-op that
            // bumps the version and invalidates every caller's cached copy.
            return BaseResponse<OrgUnitDto>.Success(ToDto(unit));
        }

        if (await _repository.SlugExistsUnderParentAsync(
                request.NewParentId,
                unit.Slug,
                id,
                cancellationToken))
        {
            return BaseResponse<OrgUnitDto>.Conflict(MessageCodes.OrgUnit.SlugConflict, ("slug", unit.Slug));
        }

        return await RepositionAsync(unit, newParent, unit.Slug, OrgUnitAuditFields.Capture(unit), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OrgUnit? unit = await _repository.FindByIdAsync(id, cancellationToken);

        if (unit is null)
        {
            return BaseResponse.NotFound(MessageCodes.OrgUnit.NotFound, ("orgUnitId", id));
        }

        IReadOnlyList<OrgUnit> children = await _repository.ListChildrenAsync(id, cancellationToken);

        if (children.Count > 0)
        {
            // Cascading would soft-delete an arbitrary number of units from a single request,
            // and the caller would find out how many only afterwards. Refusing is recoverable.
            return BaseResponse.BusinessRuleViolation(
                MessageCodes.OrgUnit.HasChildren,
                ("childCount", children.Count));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        await _repository.DeleteAsync(unit, cancellationToken);
        await auditRecorder.RecordAsync(unit.TenantId, unit, ChangeKind.Deleted,
            [new FieldChange(nameof(OrgUnit.IsDeleted), "False", "True")], cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse.NoContent();
    }

    /// <summary>
    /// Applies a new position to a node and rewrites its whole subtree, in one transaction.
    /// </summary>
    /// <remarks>
    /// Both structural triggers land here: moving a node and changing its slug. Keeping them
    /// on one path is what stops the second from being implemented as "just an update" and
    /// leaving the descendants behind.
    /// </remarks>
    private async Task<BaseResponse<OrgUnitDto>> RepositionAsync(
        OrgUnit unit,
        OrgUnit? newParent,
        string slug,
        Dictionary<string, string?> auditBefore,
        CancellationToken cancellationToken)
    {
        string oldPath = unit.HierarchyPath;

        (string newPath, IReadOnlyList<Guid> newAncestors) = OrgUnitTree.PositionUnder(newParent, slug);

        IReadOnlyList<OrgUnit> subtree = await _repository.ListSubtreeAsync(unit.Id, cancellationToken);

        IReadOnlyList<OrgUnitRewrite> rewrites = OrgUnitTree.ComputeSubtreeRewrites(
            unit.Id,
            oldPath,
            newPath,
            newAncestors,
            subtree);

        int longest = OrgUnitTree.LongestResultingPath(rewrites, newPath);

        if (longest > HierarchyPaths.MaxLength)
        {
            // Checked before anything is written: a rejected move leaves the tree untouched,
            // rather than failing part-way through the subtree.
            return BaseResponse<OrgUnitDto>.BusinessRuleViolation(
                MessageCodes.Tenant.HierarchyPathTooLong,
                ("maxLength", HierarchyPaths.MaxLength));
        }

        unit.Slug = slug;
        unit.ParentId = newParent?.Id;
        unit.HierarchyPath = newPath;
        unit.Ancestors = newAncestors;

        await _unitOfWork.BeginAsync(cancellationToken);
        await _repository.UpdateAsync(unit, cancellationToken);

        if (rewrites.Count > 0)
        {
            await _repository.ApplySubtreeRewritesAsync(rewrites, cancellationToken);
            Dictionary<Guid, OrgUnit> descendants = subtree.ToDictionary(descendant => descendant.Id);
            foreach (OrgUnitRewrite rewrite in rewrites)
            {
                OrgUnit descendant = descendants[rewrite.Id];
                var descendantBefore = OrgUnitAuditFields.Capture(descendant);
                descendant.HierarchyPath = rewrite.HierarchyPath;
                descendant.Ancestors = rewrite.Ancestors;
                await auditRecorder.RecordAsync(descendant.TenantId, descendant, ChangeKind.Updated,
                    OrgUnitAuditFields.Diff(descendantBefore, descendant), cancellationToken);
            }
        }

        await auditRecorder.RecordAsync(unit.TenantId, unit, ChangeKind.Updated,
            OrgUnitAuditFields.Diff(auditBefore, unit), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<OrgUnitDto>.Success(ToDto(unit));
    }

    private async Task<BaseResponse<string>> ResolveSlugAsync(
        string? requestedSlug,
        string name,
        Guid? parentId,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        bool wasSupplied = !string.IsNullOrWhiteSpace(requestedSlug);
        string slug = wasSupplied ? requestedSlug!.Trim() : Slug.From(name);

        if (slug.Length == 0 || !Slug.IsValid(slug))
        {
            return BaseResponse<string>.ValidationFailed(
                [ResponseMessage.ForField(MessageCodes.Validation.InvalidFormat, "slug")]);
        }

        if (wasSupplied)
        {
            return await _repository.SlugExistsUnderParentAsync(parentId, slug, excludingId, cancellationToken)
                ? BaseResponse<string>.Conflict(MessageCodes.OrgUnit.SlugConflict, ("slug", slug))
                : BaseResponse<string>.Success(slug);
        }

        // Derived from the name, so a collision is resolved rather than reported: the caller
        // never asked for this exact string.
        if (!await _repository.SlugExistsUnderParentAsync(parentId, slug, excludingId, cancellationToken))
        {
            return BaseResponse<string>.Success(slug);
        }

        for (int attempt = 2; attempt <= MaxSlugAttempts; attempt++)
        {
            string candidate = Slug.WithSuffix(slug, attempt);

            if (!await _repository.SlugExistsUnderParentAsync(parentId, candidate, excludingId, cancellationToken))
            {
                return BaseResponse<string>.Success(candidate);
            }
        }

        return BaseResponse<string>.Conflict(MessageCodes.OrgUnit.SlugConflict, ("slug", slug));
    }

    private static OrgUnitDto ToDto(OrgUnit unit) =>
        new(
            unit.Id,
            unit.Name,
            unit.Slug,
            unit.Code,
            unit.ParentId,
            unit.HierarchyPath,
            unit.Ancestors,
            unit.Depth,
            unit.IsActive,
            unit.Version);
}

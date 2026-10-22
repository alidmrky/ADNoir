using AdNoir.IdentityAndOrg.Application.Identity;
using AdNoir.BuildingBlocks.Auditing;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Tenants.Dtos;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Tenants;

/// <summary>Default <see cref="ITenantService"/>.</summary>
/// <remarks>
/// No try/catch anywhere: expected outcomes come back as failure responses, and anything
/// unexpected rises to the single global handler. Nothing here knows that the
/// store is MongoDB - this project cannot even see the driver.
/// </remarks>
public sealed class TenantService(
    ITenantRepository repository,
    ITransactionalAuditRecorder auditRecorder,
    IUnitOfWork unitOfWork) : ITenantService
{
    private const int MaxSlugAttempts = 50;

    private readonly ITenantRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public async Task<BaseResponse<TenantDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _repository.FindByIdAsync(id, cancellationToken);

        return tenant is null
            ? BaseResponse<TenantDto>.NotFound(MessageCodes.Tenant.NotFound, ("tenantId", id))
            : BaseResponse<TenantDto>.Success(ToDto(tenant));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<TenantDto>> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await _repository.FindBySlugAsync(slug, cancellationToken);

        return tenant is null
            ? BaseResponse<TenantDto>.NotFound(MessageCodes.Tenant.NotFound, ("tenantId", slug))
            : BaseResponse<TenantDto>.Success(ToDto(tenant));
    }

    /// <inheritdoc />
    public async Task<PagedResponse<TenantDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        PagedResult<Tenant> page = await _repository.ListPagedAsync(
            pageNumber,
            pageSize,
            predicate: null,
            orderBy: tenant => tenant.Name,
            descending: false,
            cancellationToken);

        return PagedResponse<TenantDto>.Create(
            [.. page.Items.Select(ToDto)],
            pageNumber,
            pageSize,
            page.TotalCount);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<TenantDto>> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool slugWasSupplied = !string.IsNullOrWhiteSpace(request.Slug);
        string slug = slugWasSupplied ? request.Slug!.Trim() : Slug.From(request.Name);

        if (slug.Length == 0)
        {
            return BaseResponse<TenantDto>.ValidationFailed(
                [ResponseMessage.ForField(MessageCodes.Validation.InvalidFormat, nameof(request.Slug))]);
        }

        if (slugWasSupplied)
        {
            // An explicit slug is a decision, so a collision is an error rather than something
            // to silently work around by renaming what the caller asked for.
            if (await _repository.SlugExistsAsync(slug, excludingId: null, cancellationToken))
            {
                return BaseResponse<TenantDto>.Conflict(MessageCodes.Tenant.SlugConflict, ("slug", slug));
            }
        }
        else
        {
            string? available = await FindAvailableSlugAsync(slug, cancellationToken);

            if (available is null)
            {
                return BaseResponse<TenantDto>.Conflict(MessageCodes.Tenant.SlugConflict, ("slug", slug));
            }

            slug = available;
        }

        Tenant tenant = Tenant.Create(
            request.Name.Trim(),
            slug,
            request.HierarchyEnabled,
            request.TimeZoneId);

        await _unitOfWork.BeginAsync(cancellationToken);
        await _repository.InsertAsync(tenant, cancellationToken);
        await auditRecorder.RecordAsync(tenant.Id, tenant, ChangeKind.Created,
            IdentityAuditFields.Diff(null, IdentityAuditFields.Capture(tenant)), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<TenantDto>.Created(ToDto(tenant));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<TenantDto>> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Tenant? tenant = await _repository.FindByIdAsync(id, cancellationToken);

        if (tenant is null)
        {
            return BaseResponse<TenantDto>.NotFound(MessageCodes.Tenant.NotFound, ("tenantId", id));
        }

        if (tenant.Version != request.ExpectedVersion)
        {
            // Caught here as well as in the repository filter. Failing before the write keeps
            // the response identical whether the conflict is noticed early or late.
            return BaseResponse<TenantDto>.ConcurrencyConflict();
        }

        var auditBefore = IdentityAuditFields.Capture(tenant);
        tenant.Name = request.Name.Trim();
        tenant.TimeZoneId = request.TimeZoneId;
        tenant.Status = request.Status;

        await _unitOfWork.BeginAsync(cancellationToken);
        await _repository.UpdateAsync(tenant, cancellationToken);
        await auditRecorder.RecordAsync(tenant.Id, tenant, ChangeKind.Updated,
            IdentityAuditFields.Diff(auditBefore, IdentityAuditFields.Capture(tenant)), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<TenantDto>.Success(ToDto(tenant));
    }

    /// <summary>
    /// Finds a free slug by appending a numeric suffix, giving up after a bounded number of
    /// attempts rather than looping forever against a pathological data set.
    /// </summary>
    private async Task<string?> FindAvailableSlugAsync(string baseSlug, CancellationToken cancellationToken)
    {
        if (!await _repository.SlugExistsAsync(baseSlug, excludingId: null, cancellationToken))
        {
            return baseSlug;
        }

        for (int attempt = 2; attempt <= MaxSlugAttempts; attempt++)
        {
            string candidate = Slug.WithSuffix(baseSlug, attempt);

            if (!await _repository.SlugExistsAsync(candidate, excludingId: null, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status,
            tenant.HierarchyEnabled,
            tenant.TimeZoneId,
            tenant.PermissionVersion,
            tenant.CreatedAtUtc,
            tenant.UpdatedAtUtc,
            tenant.Version);
}

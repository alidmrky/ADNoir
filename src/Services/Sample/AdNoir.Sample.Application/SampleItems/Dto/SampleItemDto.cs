using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Sample.Application.SampleItems.Dto;

/// <summary>Data transferred to callers representing a <c>SampleItem</c>.</summary>
/// <remarks>
/// Returning the domain entity is forbidden: it would expose persistence fields
/// and couple the API contract to the storage schema.
/// </remarks>
public sealed record SampleItemDto(
    Guid Id,
    Guid TenantId,
    string Title,
    string? Description,
    DateTime CreatedAtUtc,
    Guid? CreatedBy);

/// <summary>Payload for creating a new sample item.</summary>
public sealed record CreateSampleItemRequest(
    string Title,
    string? Description);

using AdNoir.BuildingBlocks.Services;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.ArchitectureTests.Samples;

/// <summary>
/// Deliberately correct and deliberately broken samples used to prove the architecture rules
/// actually detect something.
/// </summary>
/// <remarks>
/// <para>
/// A rule written in a form that can never fail stays green forever while appearing to
/// protect something. A rule that has never been observed to fail is indistinguishable from
/// a rule that does not work.
/// </para>
/// <para>
/// Each probe below is scanned by a test that asserts the rule flags the bad sample and
/// leaves the good one alone. Nothing here is production code, and the real assemblies are
/// checked separately.
/// </para>
/// </remarks>
internal static class RuleProbes
{
    /// <summary>A domain entity, used to prove the DTO leakage rule notices one.</summary>
    internal sealed class ProbeEntity : IEntity
    {
        public Guid Id { get; set; }

        public string Secret { get; set; } = string.Empty;
    }

    /// <summary>A DTO, which is what a service is supposed to return.</summary>
    internal sealed record ProbeDto(Guid Id, string Name);

    /// <summary>A service that follows the response contract in every respect.</summary>
    internal sealed class GoodProbeService : IApplicationService
    {
        public Task<BaseResponse<ProbeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(BaseResponse<ProbeDto>.Success(new ProbeDto(id, "probe")));

        public Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(BaseResponse.NoContent());
    }

    /// <summary>A service that breaks the contract in three separate ways.</summary>
    internal sealed class BadProbeService : IApplicationService
    {
        /// <summary>Returns a bare DTO instead of the envelope.</summary>
        public Task<ProbeDto> NotWrappedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProbeDto(Guid.Empty, "nope"));

        /// <summary>Synchronous, so a caller cannot cancel it.</summary>
        public BaseResponse NotAsync() => BaseResponse.Success();

        /// <summary>Asynchronous but takes no cancellation token.</summary>
        public Task<BaseResponse<ProbeDto>> NoCancellationTokenAsync(Guid id) =>
            Task.FromResult(BaseResponse<ProbeDto>.Success(new ProbeDto(id, "probe")));

        /// <summary>Returns a domain entity, which leaks the storage shape into the API.</summary>
        public Task<BaseResponse<ProbeEntity>> LeaksEntityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BaseResponse<ProbeEntity>.Success(new ProbeEntity()));
    }

    /// <summary>Throws a runtime-owned exception type, which is forbidden.</summary>
    internal static class ForbiddenThrowProbe
    {
        public static void ThrowsNullReference() => throw new NullReferenceException("probe");

        public static void ThrowsIndexOutOfRange() => throw new IndexOutOfRangeException("probe");
    }

    /// <summary>Crosses tenants without the required justification attribute.</summary>
    internal sealed class UnjustifiedCrossTenantProbe(IUnsafeCrossTenantQuery<ProbeEntity> query)
    {
        private readonly IUnsafeCrossTenantQuery<ProbeEntity> _query = query;

        public Task<IReadOnlyList<ProbeEntity>> ListEverythingAsync() => _query.ListAcrossTenantsAsync();
    }

    /// <summary>Crosses tenants with the justification the rule requires.</summary>
    internal sealed class JustifiedCrossTenantProbe(IUnsafeCrossTenantQuery<ProbeEntity> query)
    {
        private readonly IUnsafeCrossTenantQuery<ProbeEntity> _query = query;

        [AllowsCrossTenantAccess("Probe for the architecture test that verifies this attribute is honoured.")]
        public Task<IReadOnlyList<ProbeEntity>> ListEverythingAsync() => _query.ListAcrossTenantsAsync();
    }
    internal sealed class UnjustifiedAsyncCrossTenantProbe(IUnsafeCrossTenantQuery<ProbeEntity> query)
    {
        public async Task<IReadOnlyList<ProbeEntity>> ListEverythingAsync()
        {
            await Task.Yield();
            return await query.ListAcrossTenantsAsync();
        }
    }

    internal sealed class JustifiedAsyncCrossTenantProbe(IUnsafeCrossTenantQuery<ProbeEntity> query)
    {
        [AllowsCrossTenantAccess("Async probe verifies source method authorization without skipping state machines.")]
        public async Task<IReadOnlyList<ProbeEntity>> ListEverythingAsync()
        {
            await Task.Yield();
            return await query.ListAcrossTenantsAsync();
        }
    }
}

using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Fills <see cref="ITenantContext"/> from the access token.
/// </summary>
/// <remarks>
/// <para>
/// An authenticated request without a usable <c>tenant_id</c> claim is rejected outright.
/// Letting it through would leave the tenant unresolved, and the first repository call would
/// then throw from somewhere deep in the stack - a confusing failure a long way from its
/// cause. Failing here says exactly what is wrong.
/// </para>
/// <para>
/// Anonymous requests pass through untouched: sign-in and token refresh have no tenant yet
/// by definition.
/// </para>
/// </remarks>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    /// <summary>Resolves the tenant for this request.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="tenantContext">The scoped tenant context to populate.</param>
    /// <param name="messageResolver">Resolver used when writing the rejection envelope.</param>
    /// <returns>A task representing the request pipeline continuation.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IMessageResolver messageResolver)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(messageResolver);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        string? tenantClaim = context.User.FindFirst(AdNoirClaimTypes.TenantId)?.Value;

        if (!Guid.TryParse(tenantClaim, out Guid tenantId) || tenantId == Guid.Empty)
        {
            await WriteTenantNotResolvedAsync(context, messageResolver);
            return;
        }

        bool hierarchyEnabled = bool.TryParse(
            context.User.FindFirst(AdNoirClaimTypes.HierarchyEnabled)?.Value,
            out bool parsed) && parsed;

        tenantContext.SetTenant(tenantId, hierarchyEnabled);

        await _next(context);
    }

    private static async Task WriteTenantNotResolvedAsync(HttpContext context, IMessageResolver messageResolver)
    {
        BaseResponse response = BaseResponse.Failure(
            ResponseStatus.Unauthorized,
            MessageCodes.Tenant.NotResolved);

        BaseResponse enriched = ResponseEnricher.Enrich(response, context, messageResolver);

        context.Response.StatusCode = ResponseStatusHttpMap.ToHttpStatusCode(enriched.Status);

        await context.Response.WriteAsJsonAsync(
            enriched,
            AdNoirJsonOptions.Default,
            contentType: "application/json",
            context.RequestAborted);
    }
}

/// <summary>Registration helper for <see cref="TenantResolutionMiddleware"/>.</summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// Adds tenant resolution. Must run after authentication, since it reads the token's
    /// claims, and before authorization, which needs the tenant to load the permission matrix.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseAdNoirTenantResolution(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}

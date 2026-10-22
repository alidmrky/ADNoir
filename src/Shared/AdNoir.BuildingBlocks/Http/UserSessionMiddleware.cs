using System.Globalization;
using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>Rejects stale sessions and mandatory-password business access before authorization.</summary>
public sealed class UserSessionMiddleware(RequestDelegate next)
{
    /// <summary>Checks every authenticated non-anonymous request against live Identity state.</summary>
    public async Task InvokeAsync(HttpContext context, IUserSessionValidator validator, IMessageResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.User.Identity?.IsAuthenticated != true ||
            context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }
        string? stamp = context.User.FindFirst(AdNoirClaimTypes.SecurityStamp)?.Value;
        UserSessionState state = UserSessionState.Invalid;
        if (Guid.TryParse(context.User.FindFirst(AdNoirClaimTypes.Subject)?.Value, out Guid userId) &&
            Guid.TryParse(context.User.FindFirst(AdNoirClaimTypes.TenantId)?.Value, out Guid tenantId) &&
            long.TryParse(context.User.FindFirst(AdNoirClaimTypes.PermissionVersion)?.Value, CultureInfo.InvariantCulture, out long version) &&
            !string.IsNullOrWhiteSpace(stamp))
        {
            state = await validator.ValidateAsync(tenantId, userId, stamp, version, context.RequestAborted);
        }
        if (state == UserSessionState.Valid || (state == UserSessionState.PasswordChangeRequired &&
            context.GetEndpoint()?.Metadata.GetMetadata<AllowPasswordChangeRequiredAttribute>() is not null))
        {
            await next(context);
            return;
        }
        bool passwordRequired = state == UserSessionState.PasswordChangeRequired;
        BaseResponse response = BaseResponse.Failure(passwordRequired ? ResponseStatus.Forbidden : ResponseStatus.Unauthorized,
            passwordRequired ? MessageCodes.Identity.PasswordChangeRequired : MessageCodes.Auth.TokenInvalid);
        context.Response.StatusCode = passwordRequired ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(ResponseEnricher.Enrich(response, context, resolver),
            AdNoirJsonOptions.Default, context.RequestAborted);
    }
}

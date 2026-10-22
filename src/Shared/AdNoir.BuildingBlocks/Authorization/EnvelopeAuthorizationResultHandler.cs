using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// Writes authorization failures in the response envelope.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core's default handler answers a rejected request with a bare 401 or 403 and no
/// body at all. Every response must arrive in the envelope, so
/// this replaces the default and produces a <see cref="BaseResponse"/> with a catalog code
/// the client can act on.
/// </para>
/// <para>
/// It also separates two outcomes the default cannot tell apart: "you lack this permission"
/// (403) and "we could not verify your permissions because Identity is unreachable" (502).
/// Collapsing the second into a 403 would tell the user they have no access when in fact the
/// system does not know - and would hide a real outage behind a permissions message.
/// </para>
/// </remarks>
public sealed class EnvelopeAuthorizationResultHandler(IMessageResolver messageResolver)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly IMessageResolver _messageResolver = messageResolver;

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        BaseResponse response = BuildResponse(authorizeResult);
        BaseResponse enriched = ResponseEnricher.Enrich(response, context, _messageResolver);

        context.Response.StatusCode = ResponseStatusHttpMap.ToHttpStatusCode(enriched.Status);

        await context.Response.WriteAsJsonAsync(
            enriched,
            AdNoirJsonOptions.Default,
            contentType: "application/json",
            context.RequestAborted);
    }

    private static BaseResponse BuildResponse(PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            return BaseResponse.Unauthorized(MessageCodes.Common.Unauthorized);
        }

        string? reason = authorizeResult.AuthorizationFailure?.FailureReasons
            .Select(failureReason => failureReason.Message)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return reason switch
        {
            PermissionAuthorizationHandler.MatrixUnavailableReason => BaseResponse.Failure(
                ResponseStatus.ExternalServiceError,
                MessageCodes.Auth.PermissionMatrixUnavailable),

            MessageCodes.Tenant.NotResolved => BaseResponse.Failure(
                ResponseStatus.Unauthorized,
                MessageCodes.Tenant.NotResolved),

            _ => BaseResponse.Forbidden(MessageCodes.Auth.PermissionDenied),
        };
    }
}

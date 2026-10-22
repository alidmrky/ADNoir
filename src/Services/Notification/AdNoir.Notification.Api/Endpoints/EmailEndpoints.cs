using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Http;
using AdNoir.Notification.Application.Emails;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Permissions;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Api.Endpoints;

/// <summary>Read-only tenant notification history endpoints.</summary>
public static class EmailEndpoints
{
    /// <summary>Maps permission-protected delivery metadata and aggregate statistics.</summary>
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder emails = app.MapGroup("/api/v1/notifications/emails").RequireAuthorization()
            .RequirePermission(PermissionCatalog.Notifications.Read).WithTags("Email history");
        emails.MapGet("/", async (string? status, IEmailQueryService service, CancellationToken cancellationToken) =>
        {
            EmailDeliveryStatus? filter = null;
            if (status is not null)
            {
                if (!Enum.TryParse(status, ignoreCase: true, out EmailDeliveryStatus parsed) || !Enum.IsDefined(parsed))
                {
                    return BaseResponse.Failure(ResponseStatus.ValidationFailed, MessageCodes.Common.ValidationFailed).ToHttpResult();
                }
                filter = parsed;
            }
            return (await service.ListAsync(filter, cancellationToken)).ToHttpResult();
        });
        emails.MapGet("/{id:guid}", async (Guid id, IEmailQueryService service, CancellationToken cancellationToken) =>
            (await service.FindAsync(id, cancellationToken)).ToHttpResult());
        emails.MapGet("/statistics", async (IEmailQueryService service, CancellationToken cancellationToken) =>
            (await service.StatisticsAsync(cancellationToken)).ToHttpResult());
        return app;
    }
}

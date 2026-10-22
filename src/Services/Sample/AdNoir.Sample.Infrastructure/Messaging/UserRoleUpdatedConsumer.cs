using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Sample.Domain.SampleItems;
using AdNoir.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace AdNoir.Sample.Infrastructure.Messaging;

/// <summary>
/// Handles <see cref="UserRoleUpdatedEvent"/> published by the IdentityAndOrg service
/// (pattern demonstration).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotency rule (AGENTS.md §8):</strong> every consumer must be
/// idempotent. The same message can arrive more than once (at-least-once delivery). Relying
/// solely on <c>InboxState</c> deduplication is not enough because the deduplication window
/// is finite; the consumer logic itself must be safe to run multiple times.
/// </para>
/// <para>
/// MassTransit is used directly here — <see cref="ConsumeContext{T}"/>,
/// <see cref="IConsumer{T}"/>, retry policies and topology are all MassTransit API. The
/// Infrastructure layer is the adapter and should not hide the library behind another
/// abstraction.
/// </para>
/// </remarks>
public sealed class UserRoleUpdatedConsumer(
    ISampleItemRepository repository,
    ILogger<UserRoleUpdatedConsumer> logger) : IConsumer<UserRoleUpdatedEvent>
{
    private readonly ISampleItemRepository _repository = repository;
    private readonly ILogger<UserRoleUpdatedConsumer> _logger = logger;

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UserRoleUpdatedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        UserRoleUpdatedEvent message = context.Message;

        string? tenantIdStr = message.TenantId?.ToString();
        string? messageIdStr = context.MessageId?.ToString();
        _logger.LogReceivedUserRoleUpdatedEvent(message.UserId, tenantIdStr, messageIdStr);

        // Idempotency: this consumer has no side effects in the sample service other than
        // logging. In a real consumer, check a processed-event record before applying changes,
        // then write the record in the same transaction to prevent double application.
        //
        // The InboxState deduplication in the outbox covers the common case, but
        // its window is finite, so defensive logic here is the belt-and-suspenders layer.

        await Task.CompletedTask;
    }
}

/// <summary>Static LoggerMessage definitions for <see cref="UserRoleUpdatedConsumer"/> (CA1873).</summary>
internal static partial class UserRoleUpdatedConsumerLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Received UserRoleUpdatedEvent for user {UserId} in tenant {TenantId} (MessageId={MessageId}).")]
    public static partial void LogReceivedUserRoleUpdatedEvent(this ILogger logger, Guid userId, string? tenantId, string? messageId);
}

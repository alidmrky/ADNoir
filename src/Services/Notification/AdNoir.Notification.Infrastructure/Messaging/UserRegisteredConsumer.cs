using System.Net.Mail;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using AdNoir.Notification.Infrastructure.Persistence;
using AdNoir.Notification.Infrastructure.Templates;
using AdNoir.Shared.Contracts.Events;
using MassTransit;

namespace AdNoir.Notification.Infrastructure.Messaging;

/// <summary>Creates one durable welcome intent. SMTP is never invoked inside a broker transaction.</summary>
public sealed class UserRegisteredConsumer(ITenantContext tenant, MongoEmailStore store, NotificationOptions options)
    : IConsumer<UserRegisteredEvent>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        UserRegisteredEvent message = context.Message;
        if (message.TenantId is not Guid tenantId || tenantId == Guid.Empty || message.EventId == Guid.Empty ||
            message.DisplayName is null || message.DisplayName.Length > 200 || message.Email is null || message.Email.Length > 254 ||
            !MailAddress.TryCreate(message.Email, out MailAddress? address) || address.Address != message.Email ||
            message.CorrelationId?.Length > 128)
        {
            throw new ArgumentException("Invalid registration notification event.");
        }

        tenant.SetTenant(tenantId, false);
        options.Tenants.TryGetValue(tenantId.ToString(), out TenantEmailOptions? policy);
        bool enabled = options.Enabled && policy?.Enabled == true;
        string culture = message.Culture?.StartsWith("tr", StringComparison.OrdinalIgnoreCase) == true ? "tr" : "en";
        WelcomeTemplate template = WelcomeTemplates.Resolve(policy, culture);
        string? correlationId = message.CorrelationId ?? context.Headers.Get<string>("adnoir-correlation-id");
        if (correlationId?.Length > 128)
        {
            correlationId = null;
        }
        DateTime now = DateTime.UtcNow;
        await store.EnqueueAsync(new EmailDelivery
        {
            TenantId = tenantId,
            SourceEventId = message.EventId,
            CorrelationId = enabled ? correlationId : null,
            Culture = culture,
            TemplateVersion = template.Version,
            Recipient = enabled ? message.Email : null,
            Subject = enabled ? template.Subject : null,
            Body = enabled ? template.Body.Replace("{displayName}", message.DisplayName, StringComparison.Ordinal) : null,
            Status = enabled ? EmailDeliveryStatus.Pending : EmailDeliveryStatus.Skipped,
            OutcomeCode = enabled ? null : "tenant_disabled",
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
            CompletedAtUtc = enabled ? null : now,
            RedactAtUtc = enabled ? null : now,
            IsRedacted = !enabled,
        }, context.CancellationToken);
    }
}

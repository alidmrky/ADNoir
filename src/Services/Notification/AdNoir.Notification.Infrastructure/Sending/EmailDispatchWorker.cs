using System.Diagnostics;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Notification.Application.Emails;
using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using AdNoir.Notification.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdNoir.Notification.Infrastructure.Sending;

/// <summary>Bounded per-tenant polling with CAS leases. No external send occurs in a Mongo transaction.</summary>
public sealed partial class EmailDispatchWorker(IServiceScopeFactory scopes, NotificationOptions options,
    INotificationProvider sender, ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.PollSeconds));
        do
        {
            foreach (var pair in options.Tenants)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await ProcessTenantAsync(Guid.Parse(pair.Key), pair.Value, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    // Exceptions may contain recipients, SMTP diagnostics or rendered content.
                    // Log only a stable operational code. Persisted leases recover uncertain work.
                    LogCycleFailed(logger, pair.Key, exception.GetType().Name);
                }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessTenantAsync(Guid tenantId, TenantEmailOptions policy, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId, false);
        MongoEmailStore store = scope.ServiceProvider.GetRequiredService<MongoEmailStore>();
        await store.MaintainAsync(DateTime.UtcNow, options.RetentionDays, cancellationToken);
        if (!options.Enabled)
        {
            return;
        }

        EmailDelivery? delivery = await store.ClaimAsync(DateTime.UtcNow, options.LeaseSeconds, policy.Enabled, options.RetentionDays, cancellationToken);
        if (delivery is null)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        SendOutcome result = await sender.SendAsync(delivery, cancellationToken);
        DateTime now = DateTime.UtcNow;
        EmailAttempt attempt = delivery.Attempts[^1];
        attempt.CompletedAtUtc = now;
        attempt.DurationMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        attempt.Outcome = result.Code;
        attempt.SmtpStatusCode = result.SmtpStatusCode;
        delivery.Status = result.Status;
        delivery.OutcomeCode = result.Code;
        delivery.LeaseUntilUtc = null;
        delivery.LeaseId = null;
        if (delivery.Status == EmailDeliveryStatus.RetryScheduled && delivery.Attempts.Count >= options.MaxAttempts)
        {
            delivery.Status = EmailDeliveryStatus.Failed;
            delivery.OutcomeCode = "retry_budget_exhausted";
        }
        if (delivery.Status == EmailDeliveryStatus.RetryScheduled)
        {
            delivery.NextAttemptAtUtc = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, delivery.Attempts.Count - 1)) + Random.Shared.Next(1, 10));
        }
        else
        {
            delivery.CompletedAtUtc = now;
            delivery.RedactAtUtc = now.AddDays(options.RetentionDays);
        }
        // Persist even if host shutdown cancelled the SMTP call; bounded independent deadline.
        using CancellationTokenSource completion = new(TimeSpan.FromSeconds(10));
        bool saved = await store.SaveAsync(delivery, completion.Token);
        if (!saved)
        {
            LogCompletionLost(logger, delivery.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification worker cycle failed for tenant {TenantId} ({ExceptionType}); inspect database and provider connectivity.")]
    private static partial void LogCycleFailed(ILogger logger, string tenantId, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification completion CAS failed for {DeliveryId}; delivery requires outcome reconciliation.")]
    private static partial void LogCompletionLost(ILogger logger, Guid deliveryId);
}

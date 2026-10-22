using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
namespace AdNoir.Notification.Application.Emails.Abstractions;

/// <summary>External delivery boundary; each implementation owns its protocol and safe error classification.</summary>
public interface INotificationProvider
{
    /// <summary>Attempts one persisted email without writing provider secrets into its history.</summary>
    Task<SendOutcome> SendAsync(EmailDelivery delivery, CancellationToken cancellationToken);
}

using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AdNoir.Notification.Infrastructure.Sending;

/// <summary>SMTP adapter with explicit final acceptance, a deadline and normal certificate validation.</summary>
public sealed class SmtpEmailSender(NotificationOptions options) : INotificationProvider
{
    /// <inheritdoc />
    public async Task<SendOutcome> SendAsync(EmailDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrWhiteSpace(delivery.Recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(delivery.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(delivery.Body);
        SmtpOptions smtp = options.Smtp;
        using MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        message.To.Add(MailboxAddress.Parse(delivery.Recipient!));
        message.Subject = delivery.Subject;
        message.Body = new TextPart("plain") { Text = delivery.Body };
        // Stable correlation header, not a guarantee of provider-side deduplication.
        message.MessageId = $"{delivery.Id:N}@notifications.adnoir.invalid";
        using SmtpClient client = new();
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(smtp.TimeoutSeconds));
        bool sendingStarted = false;
        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port,
                smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, deadline.Token);
            if (!string.IsNullOrWhiteSpace(smtp.Username))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(smtp.Password);
                await client.AuthenticateAsync(smtp.Username, smtp.Password, deadline.Token);
            }
            sendingStarted = true;
            // SendAsync waits for the final SMTP response. Do not persist its arbitrary text.
            await client.SendAsync(message, deadline.Token);
            return new(EmailDeliveryStatus.Accepted, "smtp_accepted");
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode >= 400 && (int)exception.StatusCode < 500)
        {
            return new(EmailDeliveryStatus.RetryScheduled, "smtp_temporary_rejection", (int)exception.StatusCode);
        }
        catch (SmtpCommandException exception) when ((int)exception.StatusCode >= 500 && (int)exception.StatusCode < 600)
        {
            return new(EmailDeliveryStatus.Failed, "smtp_permanent_rejection", (int)exception.StatusCode);
        }
        catch (AuthenticationException)
        {
            return new(EmailDeliveryStatus.Failed, "smtp_authentication_failed");
        }
        catch (SslHandshakeException)
        {
            return new(EmailDeliveryStatus.Failed, "smtp_tls_failed");
        }
        catch (Exception exception) when (exception is IOException or SmtpProtocolException or OperationCanceledException or System.Net.Sockets.SocketException)
        {
            return sendingStarted
                ? new(EmailDeliveryStatus.OutcomeUnknown, "smtp_transport_unknown")
                : new(EmailDeliveryStatus.RetryScheduled, "smtp_connection_failed");
        }
    }
}

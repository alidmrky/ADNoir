using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Notification.Domain.Emails;
using System.Net;
using System.Net.Mail;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Mongo.DependencyInjection;
using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.Notification.Application.Emails;
using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Infrastructure.Configuration;
using AdNoir.Notification.Infrastructure.Messaging;
using AdNoir.Notification.Infrastructure.Persistence;
using AdNoir.Notification.Infrastructure.Sending;
using AdNoir.Notification.Infrastructure.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdNoir.Notification.Infrastructure.DependencyInjection;

/// <summary>Composition and fail-fast validation for the optional notification service.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers persistence, broker consumption and tenant-scoped dispatch.</summary>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        NotificationOptions options = configuration.GetSection(NotificationOptions.SectionName).Get<NotificationOptions>() ?? new();
        if (options.MaxAttempts is < 1 or > 10 || options.RetentionDays is < 1 or > 365 ||
            options.PollSeconds is < 1 or > 60 || options.Smtp.TimeoutSeconds is < 1 or > 60 ||
            options.LeaseSeconds < options.Smtp.TimeoutSeconds + 30 || options.LeaseSeconds > 600)
        {
            throw new InvalidOperationException("Notification retry, retention, polling or lease bounds are invalid.");
        }

        foreach (var pair in options.Tenants)
        {
            if (!Guid.TryParseExact(pair.Key, "D", out Guid tenantId) || tenantId == Guid.Empty)
            {
                throw new InvalidOperationException("Notification tenant keys must be non-empty canonical GUIDs.");
            }

            foreach (string culture in pair.Value.Templates.Keys)
            {
                if (culture is not ("en" or "tr"))
                {
                    throw new InvalidOperationException("Unsupported notification template culture.");
                }
            }

            WelcomeTemplates.Resolve(pair.Value, "en");
            WelcomeTemplates.Resolve(pair.Value, "tr");
        }
        options.Tenants = options.Tenants.ToDictionary(pair => Guid.Parse(pair.Key).ToString(), pair => pair.Value, StringComparer.Ordinal);
        if (options.Enabled)
        {
            SmtpOptions smtp = options.Smtp;
            bool loopback = smtp.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                (IPAddress.TryParse(smtp.Host, out IPAddress? address) && IPAddress.IsLoopback(address));
            if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
                !MailAddress.TryCreate(smtp.From, out MailAddress? from) || from.Address != smtp.From ||
                (!smtp.EnableSsl && !(environment.IsDevelopment() && loopback)) ||
                (!string.IsNullOrWhiteSpace(smtp.Username) && string.IsNullOrWhiteSpace(smtp.Password)))
            {
                throw new InvalidOperationException("Notification requires a sender, SMTP host/port and STARTTLS; only Development loopback permits plaintext.");
            }
        }
        InfrastructureMode.ResolveTransport(environment.IsProduction(), configuration[$"{RabbitMqOptions.SectionName}:Host"]);
        services.AddSingleton(options);
        services.AddAdNoirMongo(configuration);
        NotificationClassMaps.Register();
        services.AddSingleton<EmailQueueGuard>();
        services.AddScoped<MongoEmailStore>();
        services.AddScoped<MongoEmailRepository>();
        services.AddScoped<IRepository<EmailDelivery>>(provider => provider.GetRequiredService<MongoEmailRepository>());
        services.AddScoped<IEmailQueries>(provider => provider.GetRequiredService<MongoEmailRepository>());

        services.AddSingleton<INotificationProvider, SmtpEmailSender>();
        services.AddHealthChecks().AddCheck<NotificationReadiness>("notification-storage");
        services.AddHostedService<EmailDispatchWorker>();
        services.AddNotificationMassTransit(configuration, options);
        return services;
    }

    /// <summary>Registers indexes for explicit migrator execution only.</summary>
    public static IServiceCollection AddNotificationIndexProviders(this IServiceCollection services)
    {
        services.AddSingleton<IMongoIndexProvider, EmailIndexProvider>();
        return services;
    }
}

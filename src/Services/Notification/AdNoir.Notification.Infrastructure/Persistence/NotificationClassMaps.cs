using AdNoir.Notification.Domain.Emails;
using MongoDB.Bson.Serialization;
namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Mongo mappings, kept outside the domain model.</summary>
public static class NotificationClassMaps
{
    /// <summary>Registers domain mappings before accessing collections.</summary>
    public static void Register()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(EmailDelivery)))
        {
            BsonClassMap.RegisterClassMap<EmailDelivery>(map => map.AutoMap());
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(EmailAttempt)))
        {
            BsonClassMap.RegisterClassMap<EmailAttempt>(map => map.AutoMap());
        }
    }

}

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace AdNoir.BuildingBlocks.Mongo.Conventions;

/// <summary>
/// The single convention pack for every collection.
/// </summary>
/// <remarks>
/// Registered once per process, before any class map is built. Registering conventions after
/// a type has already been serialized has no effect on that type, which produces documents
/// that disagree with the rest of the database and is tedious to notice.
/// </remarks>
public static class AdNoirMongoConventions
{
    private const string PackName = "AdNoir";

    private static readonly Lock RegistrationLock = new();
    private static bool _registered;

    /// <summary>Registers the conventions. Safe to call more than once.</summary>
    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (_registered)
            {
                return;
            }

            // GUIDs are stored as binary subtype 4, not as strings: 16 bytes instead of 36,
            // and index-friendly. Combined with Guid.CreateVersion7() the keys are also
            // time-ordered, so inserts append rather than scatter across the index.
            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

            // Every DateTime round-trips as UTC. Without this a local-kind value is written
            // as-is and read back as UTC, quietly shifting by the machine's offset.
            BsonSerializer.TryRegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));

            ConventionPack pack =
            [
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(ignoreExtraElements: true),
                new IgnoreIfNullConvention(ignoreIfNull: true),
            ];

            ConventionRegistry.Register(PackName, pack, _ => true);

            _registered = true;
        }
    }
}

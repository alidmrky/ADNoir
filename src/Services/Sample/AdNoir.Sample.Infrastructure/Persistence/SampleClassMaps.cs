using AdNoir.Sample.Domain.SampleItems;
using MongoDB.Bson.Serialization;

namespace AdNoir.Sample.Infrastructure.Persistence;

/// <summary>
/// BsonClassMap registrations for Sample service entities (no [Bson*]
/// attributes in Domain — all mapping lives here in Infrastructure).
/// </summary>
public static class SampleClassMaps
{
    private static bool _registered;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Registers the class maps. Idempotent — safe to call multiple times
    /// (e.g. from both the API and the DbMigrator).
    /// </summary>
    public static void Register()
    {
        lock (_lock)
        {
            if (_registered)
            {
                return;
            }

            BsonClassMap.RegisterClassMap<SampleItem>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });

            _registered = true;
        }
    }
}

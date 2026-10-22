using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using MongoDB.Bson.Serialization;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>
/// Maps this service's entities to MongoDB documents.
/// </summary>
/// <remarks>
/// The mapping lives here rather than as <c>[Bson*]</c> attributes on the entities, because
/// the Domain project is not allowed to know that MongoDB exists. Element
/// names, enum representation and null handling come from the shared convention pack; only
/// what the conventions cannot express is stated here.
/// </remarks>
public static class IdentityAndOrgClassMaps
{
    private static readonly Lock RegistrationLock = new();
    private static bool _registered;

    /// <summary>Registers the class maps. Safe to call more than once.</summary>
    public static void Register()
    {
        lock (RegistrationLock)
        {
            if (_registered)
            {
                return;
            }

            RegisterTenant();
            RegisterOrgUnit();
            RegisterUser();
            RegisterRole();
            RegisterRefreshToken();

            _registered = true;
        }
    }

    private static void RegisterTenant()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Tenant)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<Tenant>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);

            // Id is not mapped here. It is declared on the Entity base class, which gets its
            // own class map, and the default NamedIdMemberConvention already promotes a member
            // called "Id" to the document _id. Calling MapIdMember on this type throws,
            // because it only accepts members declared directly on it.

            // Computed from Status; persisting it would let the document contradict itself
            // after a status change made outside the entity.
            map.UnmapMember(tenant => tenant.IsUsable);
        });
    }

    private static void RegisterOrgUnit()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(OrgUnit)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<OrgUnit>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);

            // Id and Ancestors are declared on base classes and belong to those classes' own
            // maps, so they are not touched here: a class map only exposes the members its own
            // type declares, and reaching for an inherited one returns null.
            // Ancestors serializes through the driver's default for IReadOnlyList<T>, an
            // ImpliedImplementationInterfaceSerializer over List<T>, which stores it as a BSON
            // array - exactly what the { Ancestors: <guid> } multikey index needs.

            // Both derived from stored fields; persisting them would let a document disagree
            // with itself after ParentId or Ancestors changed.
            map.UnmapMember(unit => unit.IsRoot);
            map.UnmapMember(unit => unit.Depth);
        });
    }

    private static void RegisterUser()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(User)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<User>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);

            // Derived from RoleAssignments. Storing them would let a document claim a role or
            // a scope that its assignments no longer contain.
            map.UnmapMember(user => user.RoleCodes);
            map.UnmapMember(user => user.EffectiveReadScope);
            map.UnmapMember(user => user.EffectiveWriteScope);
        });

        if (!BsonClassMap.IsClassMapRegistered(typeof(RoleAssignment)))
        {
            BsonClassMap.RegisterClassMap<RoleAssignment>(map =>
            {
                map.AutoMap();
                map.SetIgnoreExtraElements(true);
            });
        }
    }

    private static void RegisterRole()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Role)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<Role>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterRefreshToken()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(RefreshToken)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<RefreshToken>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });
    }
}

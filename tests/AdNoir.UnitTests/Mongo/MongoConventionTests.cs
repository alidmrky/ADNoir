using AdNoir.BuildingBlocks.Mongo.Auditing;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Mongo;

/// <summary>
/// Pure functions from the Mongo building blocks. No database is involved, so these run
/// everywhere - which matters here, where no MongoDB is installed.
/// </summary>
public sealed class MongoConventionTests
{
    private sealed class OrgUnit;

    private sealed class Category;

    private sealed class Address;

    private sealed class Box;

    [Fact]
    public void Collection_names_are_camel_case_and_pluralised()
    {
        MongoCollectionNaming.For(typeof(OrgUnit)).ShouldBe("orgUnits");
        MongoCollectionNaming.For(typeof(Category)).ShouldBe("categories");
        MongoCollectionNaming.For(typeof(Address)).ShouldBe("addresses");
        MongoCollectionNaming.For(typeof(Box)).ShouldBe("boxes");
    }

    [Fact]
    public void Sensitive_field_names_are_recognised_case_insensitively_and_by_substring()
    {
        AuditFieldMasker masker = new(Options.Create(new AuditTrailOptions()));

        masker.IsSensitive("Password").ShouldBeTrue();
        masker.IsSensitive("passwordHash").ShouldBeTrue();
        masker.IsSensitive("RefreshToken").ShouldBeTrue();
        masker.IsSensitive("securityStamp").ShouldBeTrue();
        masker.IsSensitive("displayName").ShouldBeFalse();
    }

    [Fact]
    public void A_sensitive_change_keeps_the_fact_and_drops_the_values()
    {
        AuditFieldMasker masker = new(Options.Create(new AuditTrailOptions()));

        AuditFieldChange masked = masker.Mask(new AuditFieldChange("PasswordHash", "old-hash", "new-hash"));

        masked.Field.ShouldBe("PasswordHash");
        masked.Before.ShouldBe(AuditTrailOptions.MaskedValue);
        masked.After.ShouldBe(AuditTrailOptions.MaskedValue);
    }

    [Fact]
    public void A_null_side_of_a_sensitive_change_stays_null()
    {
        // "was not set, now is" is information worth keeping; masking null into *** would
        // invent a previous value that never existed.
        AuditFieldMasker masker = new(Options.Create(new AuditTrailOptions()));

        AuditFieldChange masked = masker.Mask(new AuditFieldChange("Token", null, "new-token"));

        masked.Before.ShouldBeNull();
        masked.After.ShouldBe(AuditTrailOptions.MaskedValue);
    }

    [Fact]
    public void A_non_sensitive_change_is_left_alone()
    {
        AuditFieldMasker masker = new(Options.Create(new AuditTrailOptions()));

        AuditFieldChange change = new("DisplayName", "Ada", "Ada L.");

        masker.Mask(change).ShouldBe(change);
    }
}

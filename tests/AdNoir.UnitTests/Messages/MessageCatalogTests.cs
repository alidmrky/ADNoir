using System.Globalization;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Messages;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Messages;

/// <summary>Placeholder handling and culture fallback for the message catalog.</summary>
public sealed class MessageCatalogTests
{
    private static readonly IMessageCatalog Catalog =
        new EmbeddedJsonMessageCatalog(Options.Create(new LocalizationOptions()));

    private static readonly IMessageResolver Resolver = new MessageResolver(Catalog);

    [Fact]
    public void Placeholders_are_extracted_by_name()
    {
        IReadOnlyList<string> names = MessagePlaceholders.Extract("{field} must be at most {maxLength} characters.");

        names.ShouldBe(["field", "maxLength"]);
    }

    [Fact]
    public void Placeholders_are_substituted_by_name()
    {
        string text = MessagePlaceholders.Format(
            "{field} must be at most {maxLength} characters.",
            new Dictionary<string, object?> { ["field"] = "Name", ["maxLength"] = 64 });

        text.ShouldBe("Name must be at most 64 characters.");
    }

    [Fact]
    public void An_unmatched_placeholder_is_left_visible()
    {
        // Blanking it would produce a sentence that reads as if nothing were missing.
        string text = MessagePlaceholders.Format(
            "{field} is required.",
            new Dictionary<string, object?> { ["other"] = "x" });

        text.ShouldBe("{field} is required.");
    }

    [Fact]
    public void A_known_code_resolves_in_english()
    {
        string text = Resolver.Resolve(MessageCodes.Common.NotFound, culture: new CultureInfo("en"));

        text.ShouldBe("The requested record was not found.");
    }

    [Fact]
    public void A_known_code_resolves_in_turkish()
    {
        string text = Resolver.Resolve(MessageCodes.Common.NotFound, culture: new CultureInfo("tr"));

        text.ShouldBe("İstenen kayıt bulunamadı.");
    }

    [Fact]
    public void A_regional_culture_falls_back_to_its_parent()
    {
        // Accept-Language: tr-TR is far more common than tr. Without the parent step every
        // Turkish caller would quietly get English while the catalog looked complete.
        string text = Resolver.Resolve(MessageCodes.Common.NotFound, culture: new CultureInfo("tr-TR"));

        text.ShouldBe("İstenen kayıt bulunamadı.");
    }

    [Fact]
    public void An_unsupported_culture_falls_back_to_the_default()
    {
        string text = Resolver.Resolve(MessageCodes.Common.NotFound, culture: new CultureInfo("fr-FR"));

        text.ShouldBe("The requested record was not found.");
    }

    [Fact]
    public void An_unknown_code_is_returned_with_its_arguments_rather_than_blank()
    {
        string text = Resolver.Resolve(
            "module.thing.does_not_exist",
            new Dictionary<string, object?> { ["id"] = 7 });

        text.ShouldContain("module.thing.does_not_exist");
        text.ShouldContain("id=7");
    }

    [Fact]
    public void Arguments_are_substituted_into_the_resolved_text()
    {
        string text = Resolver.Resolve(
            MessageCodes.Auth.PermissionDenied,
            new Dictionary<string, object?> { ["permission"] = "user:write" },
            new CultureInfo("en"));

        text.ShouldBe("You do not have the user:write permission.");
    }

    [Fact]
    public void CanResolve_reports_per_culture_without_falling_back()
    {
        Resolver.CanResolve(MessageCodes.Common.NotFound, "en").ShouldBeTrue();
        Resolver.CanResolve(MessageCodes.Common.NotFound, "tr").ShouldBeTrue();
        Resolver.CanResolve("nope.not.here", "en").ShouldBeFalse();
    }

    [Fact]
    public void A_missing_default_culture_is_rejected_at_construction()
    {
        LocalizationOptions options = new() { DefaultCulture = "de", SupportedCultures = ["en", "tr"] };

        Should.Throw<InvalidOperationException>(() =>
            new EmbeddedJsonMessageCatalog(Options.Create(options)));
    }

    [Fact]
    public void A_culture_without_a_resource_file_is_rejected_at_construction()
    {
        // Failing at startup beats discovering the gap when a user sees a raw key.
        LocalizationOptions options = new() { DefaultCulture = "en", SupportedCultures = ["en", "de"] };

        Should.Throw<InvalidOperationException>(() =>
            new EmbeddedJsonMessageCatalog(Options.Create(options)));
    }
}

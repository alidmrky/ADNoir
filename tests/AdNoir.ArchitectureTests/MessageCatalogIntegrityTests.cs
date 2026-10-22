using System.Reflection;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Messages;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AdNoir.ArchitectureTests;

/// <summary>
/// Integrity checks for the message catalog, the lifeblood of the message system. The reason
/// is specific: a code with no catalog entry shows the user a raw key like
/// <c>identity.user.not_found</c>, and that is normally discovered in production.
/// </summary>
/// <remarks>
/// One further check is not implemented here: matching placeholder names against the
/// arguments each call site actually passes needs a Roslyn analyzer, because the argument
/// keys are only known statically when they are literals and reflection cannot see them.
/// The cross-culture consistency check below covers the part that is verifiable today.
/// </remarks>
public sealed class MessageCatalogIntegrityTests
{
    private static readonly LocalizationOptions Options = new();

    private static readonly IMessageCatalog Catalog =
        new EmbeddedJsonMessageCatalog(Microsoft.Extensions.Options.Options.Create(Options));

    [Fact]
    public void Catalog_declares_at_least_one_code()
    {
        // Guards the guard: if reflection stopped finding constants, every check below would
        // pass over an empty set and report success.
        DeclaredCodes().ShouldNotBeEmpty("MessageCodes must declare constants for the rules to mean anything");
    }

    [Fact]
    public void Every_declared_code_has_an_entry_in_every_culture()
    {
        List<string> missing = [];

        foreach (string code in DeclaredCodes())
        {
            foreach (string culture in Options.SupportedCultures)
            {
                if (!Catalog.TryGet(culture, code, out _))
                {
                    missing.Add($"{code} is missing from messages.{culture}.json");
                }
            }
        }

        missing.ShouldBeEmpty(
            "a code without a catalog entry reaches the user as a raw key. Add the entry to every " +
            $"culture file before using the code.{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void No_culture_carries_an_orphaned_entry()
    {
        HashSet<string> declared = [.. DeclaredCodes()];
        List<string> orphans = [];

        foreach (string culture in Options.SupportedCultures)
        {
            foreach (string key in Catalog.Entries(culture).Keys)
            {
                if (!declared.Contains(key))
                {
                    orphans.Add($"messages.{culture}.json has '{key}', which no MessageCodes constant declares");
                }
            }
        }

        orphans.ShouldBeEmpty(
            "an orphaned entry is dead weight at best and a renamed code at worst - the old text " +
            $"stays while the new code resolves to nothing.{Environment.NewLine}" +
            string.Join(Environment.NewLine, orphans));
    }

    [Fact]
    public void Cultures_agree_on_placeholder_names()
    {
        string reference = Options.DefaultCulture;
        List<string> mismatches = [];

        foreach (string code in DeclaredCodes())
        {
            if (!Catalog.TryGet(reference, code, out string? referenceText) || referenceText is null)
            {
                continue;
            }

            IReadOnlyList<string> expected = MessagePlaceholders.Extract(referenceText);

            foreach (string culture in Options.SupportedCultures)
            {
                if (string.Equals(culture, reference, StringComparison.OrdinalIgnoreCase) ||
                    !Catalog.TryGet(culture, code, out string? text) ||
                    text is null)
                {
                    continue;
                }

                IReadOnlyList<string> actual = MessagePlaceholders.Extract(text);

                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    mismatches.Add(
                        $"{code}: {reference} uses [{string.Join(", ", expected)}] but " +
                        $"{culture} uses [{string.Join(", ", actual)}]");
                }
            }
        }

        mismatches.ShouldBeEmpty(
            "translations must use the same placeholder names. A renamed placeholder is not " +
            "substituted, so the user sees a literal {field} in one language and the value in " +
            $"another.{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
    }

    [Fact]
    public void Every_declared_code_is_lower_snake_hierarchical()
    {
        List<string> malformed =
        [
            .. DeclaredCodes().Where(code =>
                !code.Contains('.', StringComparison.Ordinal) ||
                code.Any(character => char.IsUpper(character)) ||
                code.Contains(' ', StringComparison.Ordinal))
        ];

        malformed.ShouldBeEmpty(
            "codes are hierarchical lower-case strings in {module}.{entity}.{state} form " +
            $".{Environment.NewLine}{string.Join(Environment.NewLine, malformed)}");
    }

    /// <summary>Reads every constant declared by <see cref="MessageCodes"/> and its nested classes.</summary>
    private static IReadOnlyList<string> DeclaredCodes()
    {
        List<string> codes = [];
        Collect(typeof(MessageCodes), codes);
        codes.Sort(StringComparer.Ordinal);

        return codes;
    }

    private static void Collect(Type type, List<string> codes)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field is { IsLiteral: true, IsInitOnly: false } &&
                field.GetRawConstantValue() is string value)
            {
                codes.Add(value);
            }
        }

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
        {
            Collect(nested, codes);
        }
    }
}

using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// JSON settings for the response envelope: null payloads are omitted, enums are
/// written as strings, property names are camelCase.
/// </summary>
/// <remarks>
/// A <c>System.Text.Json</c> source generator is not wired up here. A generator needs a
/// serializer context per concrete payload type, so it becomes worthwhile once services
/// define their DTOs. Nothing about these settings blocks it - a context can be added later
/// without changing call sites.
/// </remarks>
public static class AdNoirJsonOptions
{
    /// <summary>Shared serializer options for writing responses.</summary>
    public static readonly JsonSerializerOptions Default = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA),
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        // populateMissingResolver: true installs the reflection-based resolver. Without it
        // MakeReadOnly throws, and because this is a static field the failure surfaces as a
        // TypeInitializationException from whichever code path happens to serialize first -
        // including the global exception handler, which then cannot report anything at all.
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace AquariumSynth.Dsl;

public sealed record ZynInstrument(
    string Name,
    string Author,
    string Comments,
    IReadOnlyList<ZynKitItem> KitItems)
{
    public IReadOnlyList<ReferenceFeature> Features()
    {
        var enabled = KitItems.Where(item => item.Enabled).ToList();
        var engineCounts = enabled
            .SelectMany(item => item.Engines)
            .GroupBy(engine => engine)
            .ToDictionary(group => group.Key, group => group.Count());
        var layered = enabled.Count > 1 || enabled.Any(item => item.Engines.Count > 1);

        var features = new List<ReferenceFeature>
        {
            new("kit_item_count", KitItems.Count.ToString(), "Declared Zyn instrument kit items."),
            new("enabled_kit_items", enabled.Count.ToString(), "Enabled Zyn instrument kit items."),
            new("layered_instrument", layered ? "yes" : "no", "True when multiple kit items or multiple engines are active."),
        };

        foreach (var engine in new[] { ZynEngine.AddSynth, ZynEngine.SubSynth, ZynEngine.PadSynth })
        {
            features.Add(new($"engine_{EngineName(engine)}", engineCounts.GetValueOrDefault(engine).ToString()));
        }

        features.Add(new("envelope_count", enabled.Sum(item => item.EnvelopeCount).ToString()));
        features.Add(new("free_envelope_count", enabled.Sum(item => item.FreeEnvelopeCount).ToString()));
        features.Add(new("lfo_count", enabled.Sum(item => item.LfoCount).ToString()));
        features.Add(new("filter_count", enabled.Sum(item => item.FilterCount).ToString()));
        features.Add(new("formant_filter_count", enabled.Sum(item => item.FormantFilterCount).ToString()));
        features.Add(new("effect_count", enabled.Sum(item => item.EffectCount).ToString()));
        return features;
    }

    public ReferencePatch ToReferencePatch(string id, ReferenceSource source) =>
        new(id, "zynaddsubfx", Name, source, Features(), Parameters: []);

    private static string EngineName(ZynEngine engine) => engine switch
    {
        ZynEngine.AddSynth => "add",
        ZynEngine.SubSynth => "sub",
        ZynEngine.PadSynth => "pad",
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, null)
    };
}

public sealed record ZynKitItem(
    int Id,
    string Name,
    bool Enabled,
    IReadOnlyList<ZynEngine> Engines,
    int EnvelopeCount,
    int FreeEnvelopeCount,
    int LfoCount,
    int FilterCount,
    int FormantFilterCount,
    int EffectCount);

public enum ZynEngine
{
    AddSynth,
    SubSynth,
    PadSynth
}

public static class ZynInstrumentReader
{
    public static ZynInstrument ParseFile(string path) =>
        Parse(File.ReadAllBytes(path));

    public static ZynInstrument Parse(ReadOnlySpan<byte> bytes)
    {
        using var xml = OpenXmlStream(bytes);
        var document = XDocument.Load(xml, LoadOptions.None);
        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var info = instrument.Element("INFO");
        var kitItems = instrument
            .Descendants("INSTRUMENT_KIT_ITEM")
            .Select(ParseKitItem)
            .ToList();

        return new ZynInstrument(
            StringValue(info, "name", "unnamed"),
            StringValue(info, "author", ""),
            StringValue(info, "comments", ""),
            kitItems);
    }

    public static ReferenceSource SourceForBytes(string uri, string license, ReadOnlySpan<byte> bytes, string notes = "") =>
        new("zyn-xiz", uri, license, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), notes);

    private static ZynKitItem ParseKitItem(XElement item)
    {
        var engines = new List<ZynEngine>();
        if (EngineEnabled(item, "add_enabled", "ADD_SYNTH_PARAMETERS")) engines.Add(ZynEngine.AddSynth);
        if (EngineEnabled(item, "sub_enabled", "SUB_SYNTH_PARAMETERS")) engines.Add(ZynEngine.SubSynth);
        if (EngineEnabled(item, "pad_enabled", "PAD_SYNTH_PARAMETERS")) engines.Add(ZynEngine.PadSynth);

        return new ZynKitItem(
            Id: IntAttribute(item, "id"),
            Name: StringValue(item, "name", ""),
            Enabled: BoolParam(item, "enabled"),
            Engines: engines,
            EnvelopeCount: CountElements(item, name => name.EndsWith("_ENVELOPE", StringComparison.OrdinalIgnoreCase)),
            FreeEnvelopeCount: item.Descendants()
                .Count(element => element.Name.LocalName.EndsWith("_ENVELOPE", StringComparison.OrdinalIgnoreCase) &&
                                  BoolParam(element, "free_mode")),
            LfoCount: CountElements(item, name => name.EndsWith("_LFO", StringComparison.OrdinalIgnoreCase)),
            FilterCount: CountElements(item, name => name.Contains("FILTER", StringComparison.OrdinalIgnoreCase)),
            FormantFilterCount: CountElements(item, name => name.Contains("FORMANT", StringComparison.OrdinalIgnoreCase)),
            EffectCount: CountElements(item, name => name.Contains("EFFECT", StringComparison.OrdinalIgnoreCase)));
    }

    private static MemoryStream OpenXmlStream(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
        {
            using var compressed = new MemoryStream(bytes.ToArray());
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            decompressed.Position = 0;
            return decompressed;
        }

        return new MemoryStream(bytes.ToArray());
    }

    private static int CountElements(XElement root, Func<string, bool> predicate) =>
        root.Descendants().Count(element => predicate(element.Name.LocalName));

    private static bool EngineEnabled(XElement item, string flagName, string sectionName) =>
        BoolParamValue(item, flagName) ?? item.Element(sectionName) is not null;

    private static string StringValue(XElement? root, string name, string fallback) =>
        root?.Elements("string")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Value ?? fallback;

    private static bool BoolParam(XElement root, string name) =>
        BoolParamValue(root, name) ?? false;

    private static bool? BoolParamValue(XElement root, string name)
    {
        var value = root.Elements("par_bool")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        if (value is null) return null;
        return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    private static int IntAttribute(XElement element, string name) =>
        int.TryParse(AttributeValue(element, name), out var value) ? value : 0;

    private static string? AttributeValue(XElement element, string name) =>
        element.Attribute(name)?.Value;
}

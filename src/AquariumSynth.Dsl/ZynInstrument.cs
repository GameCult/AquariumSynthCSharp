using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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

public sealed record ZynInstrumentSurveyItem(
    string Path,
    string Name,
    int ComplexityScore,
    IReadOnlyList<ReferenceFeature> Features);

public sealed record ZynPadRebuild(
    string InstrumentName,
    int KitItemId,
    string Script,
    IReadOnlyList<ReferenceFeature> MatchedFeatures,
    IReadOnlyList<ReferenceFeature> MissingFeatures);

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
        var document = ParseDocument(bytes);
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

    public static ZynPadRebuild RebuildFirstPadAsAquariumScript(
        string path,
        float tableRootFrequencyHz,
        float playbackFrequencyHz = 261.6256f) =>
        RebuildFirstPadAsAquariumScript(File.ReadAllBytes(path), tableRootFrequencyHz, playbackFrequencyHz);

    public static ZynPadRebuild RebuildFirstPadAsAquariumScript(
        ReadOnlySpan<byte> bytes,
        float tableRootFrequencyHz,
        float playbackFrequencyHz = 261.6256f)
    {
        if (tableRootFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(tableRootFrequencyHz));
        if (playbackFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(playbackFrequencyHz));

        var document = ParseDocument(bytes);
        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var name = StringValue(instrument.Element("INFO"), "name", "unnamed");
        var item = instrument
            .Descendants("INSTRUMENT_KIT_ITEM")
            .FirstOrDefault(IsEnabledPadItem)
            ?? throw new ArgumentException("Zyn instrument XML has no enabled PAD kit item");
        var pad = item.Element("PAD_SYNTH_PARAMETERS")
            ?? throw new ArgumentException("Enabled PAD kit item is missing PAD_SYNTH_PARAMETERS");
        var amplitude = pad.Element("AMPLITUDE_PARAMETERS");
        var envelope = amplitude?.Element("AMPLITUDE_ENVELOPE");
        var harmonics = pad.Element("OSCIL")?.Element("HARMONICS")?.Elements("HARMONIC")
            .Select(ParsePadHarmonic)
            .Where(partial => partial.Gain > 0)
            .OrderBy(partial => partial.Ratio)
            .Take(32)
            .ToList() ?? [];
        if (harmonics.Count == 0)
        {
            harmonics.Add(new HarmonicPartial(1, 0.16f));
        }

        var volume = IntParam(amplitude, "volume", 90);
        var layerGain = Math.Clamp(volume / 500f, 0.02f, 0.4f);
        var attack = EnvelopeTime(envelope, "A_dt", 0.28f);
        var decay = EnvelopeTime(envelope, "D_dt", 0.42f);
        var release = EnvelopeTime(envelope, "R_dt", 1.2f);
        var safeName = SafeIdentifier(string.IsNullOrWhiteSpace(name) ? $"pad_{IntAttribute(item, "id")}" : name);
        var partialText = string.Join(",", harmonics.Select(partial => $"{F(partial.Ratio)}:{F(partial.Gain)}"));

        var matched = new List<ReferenceFeature>
        {
            new("engine_pad", "1", "Translated first enabled PAD kit item."),
            new("pad_oscillator_harmonics", harmonics.Count.ToString(CultureInfo.InvariantCulture), "Mapped OSCIL/HARMONICS magnitudes into Aquarium spectrum partials."),
            new("pad_table_root", F(tableRootFrequencyHz), "Root frequency comes from the Zyn oracle generated sample basefreq."),
            new("pad_volume", volume.ToString(CultureInfo.InvariantCulture), "Mapped PAD amplitude volume into Aquarium layer gain.")
        };
        var missing = new List<ReferenceFeature>();
        if (IntParam(pad, "bandwidth", 0) > 0)
        {
            missing.Add(new("pad_bandwidth_profile", IntParam(pad, "bandwidth", 0).ToString(CultureInfo.InvariantCulture), "Aquarium spread is kept at zero until Zyn bandwidth/profile semantics are calibrated."));
        }
        if (pad.Element("HARMONIC_PROFILE") is { } profile)
        {
            missing.Add(new("pad_harmonic_profile", IntParam(profile, "base_type", 0).ToString(CultureInfo.InvariantCulture), "Zyn harmonic profile shaping is not yet lowered; explicit OSCIL harmonics are used as the first spectral rung."));
        }
        if (pad.Element("FILTER_PARAMETERS") is not null)
        {
            missing.Add(new("pad_filter_parameters", "present", "Filter envelope/LFO lowering remains separate pressure."));
        }

        var script = new StringBuilder();
        script.AppendLine("# Generated from a ZynAddSubFX PAD fixture for parity pressure.");
        script.AppendLine("# Scope: first enabled PAD kit item, OSCIL harmonic magnitudes, basic volume/envelope.");
        script.AppendLine("patch");
        script.AppendLine("    gain=1");
        script.AppendLine("    soft_clip=false");
        script.AppendLine();
        script.AppendLine("defaults");
        script.AppendLine("    wave=sine");
        script.AppendLine("    lpf=1");
        script.AppendLine("    hpf=0");
        script.AppendLine();
        script.AppendLine($"layer name={safeName} engine=pad gain={F(layerGain)} env=rl rates={F(attack)},{F(decay)},1,{F(release)} levels=1,1,1,0 curves=lin,lin,lin,lin gate=1.5");
        script.AppendLine($"spectrum layer={safeName} root={F(tableRootFrequencyHz)} freq={F(playbackFrequencyHz)} spread=0 partials={partialText}");

        return new ZynPadRebuild(name, IntAttribute(item, "id"), script.ToString(), matched, missing);
    }

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
            FilterCount: CountElements(item, name => name.Equals("FILTER", StringComparison.OrdinalIgnoreCase)),
            FormantFilterCount: CountElements(item, name => name.Equals("FORMANT_FILTER", StringComparison.OrdinalIgnoreCase)),
            EffectCount: CountElements(item, name => name.Contains("EFFECT", StringComparison.OrdinalIgnoreCase)));
    }

    private static string XmlText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
        {
            using var compressed = new MemoryStream(bytes.ToArray());
            using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            var decompressed = new MemoryStream();
            gzip.CopyTo(decompressed);
            return System.Text.Encoding.UTF8.GetString(decompressed.ToArray());
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static XDocument ParseDocument(ReadOnlySpan<byte> bytes) =>
        XDocument.Parse(XmlText(bytes).TrimStart(), LoadOptions.None);

    private static int CountElements(XElement root, Func<string, bool> predicate) =>
        root.Descendants().Count(element => predicate(element.Name.LocalName));

    private static bool EngineEnabled(XElement item, string flagName, string sectionName) =>
        BoolParamValue(item, flagName) ?? item.Element(sectionName) is not null;

    private static bool IsEnabledPadItem(XElement item) =>
        BoolParam(item, "enabled") && EngineEnabled(item, "pad_enabled", "PAD_SYNTH_PARAMETERS");

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

    private static HarmonicPartial ParsePadHarmonic(XElement harmonic)
    {
        var id = IntAttribute(harmonic, "id");
        var ratio = Math.Max(1, id);
        var magnitude = IntParam(harmonic, "mag", 0);
        return new HarmonicPartial(ratio, 0.16f * Math.Clamp(magnitude / 127f, 0, 1));
    }

    private static int IntParam(XElement? root, string name, int fallback)
    {
        var value = root?.Elements("par")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static float EnvelopeTime(XElement? envelope, string name, float fallback)
    {
        var value = IntParam(envelope, name, -1);
        return value < 0 ? fallback : Math.Clamp(value / 127f, 0.02f, 1.4f);
    }

    private static string SafeIdentifier(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var safe = string.Join("_", new string(chars).Split('_', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safe) ? "pad" : safe;
    }

    private static string F(float value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}

public static class ZynInstrumentSurvey
{
    public static IReadOnlyList<ZynInstrumentSurveyItem> RankDirectory(string root, int take = 25)
    {
        return Directory.GetFiles(root, "*.xiz", SearchOption.AllDirectories)
            .Select(file =>
            {
                var instrument = ZynInstrumentReader.ParseFile(file);
                var features = instrument.Features();
                return new ZynInstrumentSurveyItem(
                    Path.GetRelativePath(root, file),
                    instrument.Name,
                    ComplexityScore(features),
                    features);
            })
            .OrderByDescending(item => item.ComplexityScore)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    public static int ComplexityScore(IReadOnlyList<ReferenceFeature> features)
    {
        var byName = features.ToDictionary(feature => feature.Name, feature => feature.Value, StringComparer.OrdinalIgnoreCase);
        var layered = byName.GetValueOrDefault("layered_instrument") == "yes" ? 8 : 0;
        return Int("enabled_kit_items") * 4 +
               layered +
               Int("engine_add") * 4 +
               Int("engine_sub") * 4 +
               Int("engine_pad") * 7 +
               Int("envelope_count") +
               Int("free_envelope_count") * 4 +
               Int("lfo_count") * 2 +
               Int("filter_count") * 2 +
               Int("formant_filter_count") * 8 +
               Int("effect_count") * 3;

        int Int(string name) => int.TryParse(byName.GetValueOrDefault(name), out var value) ? value : 0;
    }
}

using System.IO.Compression;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AquaSynth.Dsl;

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

public enum ZynCoverageStatus
{
    Handled,
    Counted,
    Unknown
}

public sealed record ZynCoverageBucket(
    string Area,
    string Key,
    ZynCoverageStatus Status,
    int Count,
    IReadOnlyList<string> Examples,
    string Notes);

public sealed record ZynCoverageReport(
    int FileCount,
    int ParsedFileCount,
    int EnabledKitItemCount,
    int LayeredInstrumentCount,
    IReadOnlyList<ZynCoverageBucket> Buckets)
{
    public IReadOnlyList<ZynCoverageBucket> UnknownBuckets =>
        Buckets.Where(bucket => bucket.Status == ZynCoverageStatus.Unknown).ToList();

    public IReadOnlyList<ZynCoverageBucket> CountedBuckets =>
        Buckets.Where(bucket => bucket.Status == ZynCoverageStatus.Counted).ToList();
}

public sealed record ZynPadRebuild(
    string InstrumentName,
    int KitItemId,
    string Script,
    IReadOnlyList<ReferenceFeature> MatchedFeatures,
    IReadOnlyList<ReferenceFeature> MissingFeatures);

public sealed record ZynFormantMotionRebuild(
    string InstrumentName,
    int KitItemId,
    string Script,
    IReadOnlyList<ReferenceFeature> MatchedFeatures,
    IReadOnlyList<ReferenceFeature> MissingFeatures);

public sealed record ZynPadTableRoot(float Sample0RootFrequencyHz, float? SelectedRootFrequencyHz = null);

public enum ZynEngine
{
    AddSynth,
    SubSynth,
    PadSynth
}

public static class ZynInstrumentReader
{
    private const float ZynPadOutputGain = 1.6f;

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

    public static ZynPadRebuild RebuildFirstPadAsAquaSynthScript(
        string path,
        float tableRootFrequencyHz,
        float playbackFrequencyHz = 261.6256f) =>
        RebuildFirstPadAsAquaSynthScript(File.ReadAllBytes(path), tableRootFrequencyHz, playbackFrequencyHz);

    public static ZynPadRebuild RebuildEnabledPadsAsAquaSynthScript(
        string path,
        IReadOnlyDictionary<int, float> tableRootFrequencyHzByKitItem,
        float playbackFrequencyHz = 261.6256f) =>
        RebuildEnabledPadsAsAquaSynthScript(File.ReadAllBytes(path), tableRootFrequencyHzByKitItem, playbackFrequencyHz);

    public static ZynPadRebuild RebuildEnabledPadsAsAquaSynthScript(
        string path,
        IReadOnlyDictionary<int, ZynPadTableRoot> tableRootFrequencyHzByKitItem,
        float playbackFrequencyHz = 261.6256f) =>
        RebuildEnabledPadsAsAquaSynthScript(File.ReadAllBytes(path), tableRootFrequencyHzByKitItem, playbackFrequencyHz);

    public static ZynFormantMotionRebuild RebuildFirstFormantMotionAsAquaSynthScript(
        string path,
        float playbackFrequencyHz = 110.0f) =>
        RebuildFirstFormantMotionAsAquaSynthScript(File.ReadAllBytes(path), playbackFrequencyHz);

    public static ZynFormantMotionRebuild RebuildFirstFormantMotionAsAquaSynthScript(
        ReadOnlySpan<byte> bytes,
        float playbackFrequencyHz = 110.0f)
    {
        if (playbackFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(playbackFrequencyHz));

        var document = ParseDocument(bytes);
        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var name = StringValue(instrument.Element("INFO"), "name", "unnamed");
        var item = instrument
            .Descendants("INSTRUMENT_KIT_ITEM")
            .Where(item => BoolParam(item, "enabled"))
            .FirstOrDefault(item => item.Descendants("FORMANT_FILTER").Any())
            ?? throw new ArgumentException("Zyn instrument XML has no enabled formant filter kit item");

        var formantFilter = item
            .Descendants("FORMANT_FILTER")
            .FirstOrDefault(filter => filter.Ancestors("FILTER_PARAMETERS")
                .Elements("FILTER_LFO")
                .Any(lfo => IntParam(lfo, "intensity", 0) > 0))
            ?? item.Descendants("FORMANT_FILTER").First();
        var filterLfo = formantFilter
            .Ancestors("FILTER_PARAMETERS")
            .Elements("FILTER_LFO")
            .FirstOrDefault(lfo => IntParam(lfo, "intensity", 0) > 0);

        var frameCount = Math.Clamp(IntParam(formantFilter, "sequence_size", 1), 1, 8);
        var formantCount = Math.Clamp(IntParam(formantFilter, "num_formants", 3), 1, 6);
        var sequence = formantFilter
            .Elements("SEQUENCE_POS")
            .OrderBy(element => IntAttribute(element, "id"))
            .Select(element => IntParam(element, "vowel_id", 0))
            .Take(frameCount)
            .ToArray();
        if (sequence.Length == 0)
        {
            sequence = Enumerable.Range(0, frameCount).ToArray();
        }

        var frames = sequence
            .Select(vowelId => ParseZynVowelFrame(formantFilter, vowelId, formantCount))
            .Where(frame => frame.Count > 0)
            .ToList();
        if (frames.Count == 0)
        {
            throw new ArgumentException("Zyn formant filter has no vowel frame formants");
        }

        var safeName = SafeIdentifier(name);
        var vowelText = string.Join("|", frames.Select(frame => string.Join(",", frame.Select(formant => $"{F(formant.FrequencyHz)}:{F(formant.BandwidthHz)}:{F(formant.Gain)}"))));
        var vowelRate = filterLfo is null ? 0.5f : Math.Clamp(RealParam(filterLfo, "freq", 0.5f), 0.01f, 20.0f);
        var engines = EnabledEnginesForReader(item).ToArray();
        var engine = engines.Contains(ZynEngine.AddSynth) ? "add" :
            engines.Contains(ZynEngine.SubSynth) ? "sub" :
            engines.Contains(ZynEngine.PadSynth) ? "pad" : "formant";

        var script = new StringBuilder();
        script.AppendLine("# Generated from a ZynAddSubFX formant-motion fixture for parity pressure.");
        script.AppendLine("# Scope: first enabled kit item with a FORMANT_FILTER vowel sequence.");
        script.AppendLine("patch gain=0.45 soft_clip=true");
        script.AppendLine($"layer name={safeName} engine={engine} gain=0.25");
        script.AppendLine($"voice layer={safeName} wave=saw freq={F(playbackFrequencyHz)} gain=0.18 gate=1.8 env=rl rates=0.04,0.18,0.9,0.3 levels=1,0.9,0.75,0 formant_mix=0.7 vowel_hz={F(vowelRate)} vowels={vowelText}");

        return new ZynFormantMotionRebuild(
            name,
            IntAttribute(item, "id"),
            script.ToString(),
            [
                new("formant_filter_count", item.Descendants("FORMANT_FILTER").Count().ToString(CultureInfo.InvariantCulture), "Read Zyn FORMANT_FILTER blocks from the enabled kit item."),
                new("formant_sequence", string.Join(",", sequence), "Lowered the Zyn vowel sequence into AquaSynth vowel frames."),
                new("formant_frames", frames.Count.ToString(CultureInfo.InvariantCulture), "Each frame owns one static formant bank; Faust blends between frames at control/audio expression time."),
                new("formant_frame_rate_hz", F(vowelRate), filterLfo is null ? "No active FILTER_LFO was found; the workout uses a slow audible scan." : "Seeded from the active Zyn FILTER_LFO frequency.")
            ],
            [
                new("zyn_formant_position_driver", "filter frequency to vowel position", "Zyn derives vowel position from the formant filter cutoff path; AquaSynth currently states an explicit frame rate."),
                new("zyn_formant_smoothing", "formant_slowness/vowel_clearness", "AquaSynth blends adjacent frames with a simple cyclic triangular window."),
                new("zyn_add_voice_detail", "full ADD voice oscillator/FM stack", "This workout keeps the source deliberately plain so the formant-motion authority is the thing under test.")
            ]);
    }

    public static ZynPadRebuild RebuildFirstPadAsAquaSynthScript(
        ReadOnlySpan<byte> bytes,
        float tableRootFrequencyHz,
        float playbackFrequencyHz = 261.6256f)
    {
        if (tableRootFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(tableRootFrequencyHz));
        if (playbackFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(playbackFrequencyHz));

        var document = ParseDocument(bytes);
        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var item = instrument
            .Descendants("INSTRUMENT_KIT_ITEM")
            .FirstOrDefault(IsEnabledPadItem)
            ?? throw new ArgumentException("Zyn instrument XML has no enabled PAD kit item");
        return RebuildPadItemsAsAquaSynthScript(
            document,
            [item],
            new Dictionary<int, ZynPadTableRoot> { [IntAttribute(item, "id")] = new(tableRootFrequencyHz) },
            playbackFrequencyHz);
    }

    public static ZynPadRebuild RebuildEnabledPadsAsAquaSynthScript(
        ReadOnlySpan<byte> bytes,
        IReadOnlyDictionary<int, float> tableRootFrequencyHzByKitItem,
        float playbackFrequencyHz = 261.6256f)
    {
        return RebuildEnabledPadsAsAquaSynthScript(
            bytes,
            tableRootFrequencyHzByKitItem.ToDictionary(
                pair => pair.Key,
                pair => new ZynPadTableRoot(pair.Value)),
            playbackFrequencyHz);
    }

    public static ZynPadRebuild RebuildEnabledPadsAsAquaSynthScript(
        ReadOnlySpan<byte> bytes,
        IReadOnlyDictionary<int, ZynPadTableRoot> tableRootFrequencyHzByKitItem,
        float playbackFrequencyHz = 261.6256f)
    {
        if (tableRootFrequencyHzByKitItem.Count == 0) throw new ArgumentException("At least one PAD table root is required.", nameof(tableRootFrequencyHzByKitItem));
        if (playbackFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(playbackFrequencyHz));

        var document = ParseDocument(bytes);
        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var items = instrument
            .Descendants("INSTRUMENT_KIT_ITEM")
            .Where(IsEnabledPadItem)
            .ToList();
        if (items.Count == 0)
        {
            throw new ArgumentException("Zyn instrument XML has no enabled PAD kit item");
        }

        return RebuildPadItemsAsAquaSynthScript(document, items, tableRootFrequencyHzByKitItem, playbackFrequencyHz);
    }

    private static ZynPadRebuild RebuildPadItemsAsAquaSynthScript(
        XDocument document,
        IReadOnlyList<XElement> items,
        IReadOnlyDictionary<int, ZynPadTableRoot> tableRootFrequencyHzByKitItem,
        float playbackFrequencyHz)
    {
        if (playbackFrequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(playbackFrequencyHz));

        var instrument = document.Descendants("INSTRUMENT").FirstOrDefault()
            ?? throw new ArgumentException("Zyn instrument XML is missing INSTRUMENT");
        var name = StringValue(instrument.Element("INFO"), "name", "unnamed");
        var safeBaseName = SafeIdentifier(name);

        var script = new StringBuilder();
        script.AppendLine("# Generated from a ZynAddSubFX PAD fixture for parity pressure.");
        script.AppendLine("# Scope: enabled PAD kit items, OSCIL source shaping, PAD table profile, basic volume/envelope.");
        script.AppendLine("patch");
        script.AppendLine($"    gain={F(ZynPadOutputGain)}");
        script.AppendLine("    soft_clip=false");
        script.AppendLine();
        script.AppendLine("defaults");
        script.AppendLine("    wave=sine");
        script.AppendLine("    lpf=1");
        script.AppendLine("    hpf=0");
        script.AppendLine();

        var matched = new List<ReferenceFeature>
        {
            new("engine_pad", items.Count.ToString(CultureInfo.InvariantCulture), "Translated enabled PAD kit item layers."),
            new("pad_output_gain", F(ZynPadOutputGain), "Calibrated generated Zyn PAD note output against the reference renderer.")
        };
        var missing = new List<ReferenceFeature>();

        foreach (var item in items)
        {
            var kitId = IntAttribute(item, "id");
            if (!tableRootFrequencyHzByKitItem.TryGetValue(kitId, out var tableRoot))
            {
                throw new ArgumentException($"Missing PAD table root for kit item {kitId}.", nameof(tableRootFrequencyHzByKitItem));
            }

            var tableRootFrequencyHz = ZynPadRootForAquaSynth(item, tableRoot);
            if (tableRootFrequencyHz <= 0)
            {
                throw new ArgumentException($"Invalid PAD table root for kit item {kitId}.", nameof(tableRootFrequencyHzByKitItem));
            }

            AppendPadItemScript(
                item,
                name,
                safeBaseName,
                items.Count > 1,
                tableRootFrequencyHz,
                playbackFrequencyHz,
                script,
                matched,
                missing);
        }

        return new ZynPadRebuild(name, items.Count == 1 ? IntAttribute(items[0], "id") : -1, script.ToString(), matched, missing);
    }

    private static float ZynPadRootForAquaSynth(XElement item, ZynPadTableRoot tableRoot)
    {
        var oscillator = item
            .Element("PAD_SYNTH_PARAMETERS")
            ?.Element("OSCIL");
        if (IntParam(oscillator, "base_function", 0) == 7 &&
            tableRoot.SelectedRootFrequencyHz is > 0)
        {
            return tableRoot.SelectedRootFrequencyHz.Value;
        }

        return tableRoot.Sample0RootFrequencyHz;
    }

    private static void AppendPadItemScript(
        XElement item,
        string instrumentName,
        string safeBaseName,
        bool includeKitSuffix,
        float tableRootFrequencyHz,
        float playbackFrequencyHz,
        StringBuilder script,
        List<ReferenceFeature> matched,
        List<ReferenceFeature> missing)
    {
        var pad = item.Element("PAD_SYNTH_PARAMETERS")
            ?? throw new ArgumentException("Enabled PAD kit item is missing PAD_SYNTH_PARAMETERS");
        var kitId = IntAttribute(item, "id");
        var amplitude = pad.Element("AMPLITUDE_PARAMETERS");
        var envelope = amplitude?.Element("AMPLITUDE_ENVELOPE");
        var oscillator = pad.Element("OSCIL");
        var harmonics = ParsePadOscillatorHarmonics(oscillator, tableRootFrequencyHz, maxHarmonics: 64).ToList();
        if (harmonics.Count == 0)
        {
            harmonics.Add(new HarmonicPartial(1, 0.16f));
        }

        var volume = IntParam(amplitude, "volume", 90);
        var layerGain = Math.Clamp(
            MathF.Pow(0.1f, 3.0f * (1.0f - volume / 96.0f)),
            0.001f,
            3.0f);
        var frequency = pad.Element("FREQUENCY_PARAMETERS");
        var noteFrequencyHz = ZynPadPlaybackFrequency(frequency, playbackFrequencyHz);
        var attack = EnvelopeTime(envelope, "A_dt", 0.28f);
        var decay = EnvelopeTime(envelope, "D_dt", 0.42f);
        var release = EnvelopeTime(envelope, "R_dt", 1.2f);
        var mode = ZynPadMode(IntParam(pad, "mode", 0));
        var bandwidth = IntParam(pad, "bandwidth", 500);
        var bandwidthScale = IntParam(pad, "bandwidth_scale", 0);
        var profile = ZynProfileText(pad.Element("HARMONIC_PROFILE"));
        var position = ZynPositionText(pad.Element("HARMONIC_POSITION"));
        var filter = pad.Element("FILTER_PARAMETERS");
        var lowPass = ZynPadLowPass(filter, playbackFrequencyHz);
        var lowPassQ = ZynPadLowPassQ(filter);
        var lowPassOrder = ZynPadLowPassOrder(filter);
        var highPass = ZynPadHighPass(filter, playbackFrequencyHz);
        var highPassOrder = ZynPadHighPassOrder(filter);
        var bandPass = ZynPadBandPass(filter, playbackFrequencyHz);
        var bandPassQ = ZynPadFilterQ(filter);
        var bandPassOrder = ZynPadBandPassOrder(filter);
        var notch = ZynPadNotch(filter, playbackFrequencyHz);
        var notchQ = ZynPadFilterQ(filter);
        var notchOrder = ZynPadNotchOrder(filter);
        var lowPassEnvelope = ZynPadLowPassEnvelope(filter, lowPass);
        var highPassEnvelope = ZynPadHighPassEnvelope(filter, highPass);
        var safeName = includeKitSuffix ? $"{safeBaseName}_{kitId}" : safeBaseName;
        var partialText = string.Join(",", harmonics.Select(partial => $"{F(partial.Ratio)}:{F(partial.Gain)}"));

        var featurePrefix = includeKitSuffix ? $"pad_kit_{kitId}_" : "pad_";
        matched.Add(new($"{featurePrefix}oscillator_harmonics", harmonics.Count.ToString(CultureInfo.InvariantCulture), "Mapped OSCIL/HARMONICS magnitudes into AquaSynth spectrum partials."));
        matched.Add(new($"{featurePrefix}oscillator_base_function", IntParam(oscillator, "base_function", 0).ToString(CultureInfo.InvariantCulture), "Expanded Zyn OscilGen base function into harmonic partials before PAD synthesis."));
        if (IntParam(oscillator, "base_function_modulation", 0) != 0)
        {
            matched.Add(new($"{featurePrefix}oscillator_base_function_modulation", IntParam(oscillator, "base_function_modulation", 0).ToString(CultureInfo.InvariantCulture), "Applied Zyn OscilGen base-function phase modulation before PAD synthesis."));
        }
        if (IntParam(oscillator, "adaptive_harmonics", 0) != 0)
        {
            matched.Add(new($"{featurePrefix}oscillator_adaptive_harmonics", IntParam(oscillator, "adaptive_harmonics", 0).ToString(CultureInfo.InvariantCulture), "Applied Zyn adaptive harmonic remapping at the PAD table root frequency."));
        }
        matched.Add(new($"{featurePrefix}oscillator_spectrum_adjust", $"{IntParam(oscillator, "spectrum_adjust_type", 0)}:{IntParam(oscillator, "spectrum_adjust_par", 64)}", "Applied Zyn OscilGen spectrum adjustment to generated harmonic partials."));
        matched.Add(new($"{featurePrefix}table_root", F(tableRootFrequencyHz), "Root frequency comes from the Zyn oracle generated sample basefreq."));
        matched.Add(new($"{featurePrefix}note_frequency", F(noteFrequencyHz), "Mapped Zyn PAD coarse/fine detune into AquaSynth table playback frequency."));
        matched.Add(new($"{featurePrefix}volume", volume.ToString(CultureInfo.InvariantCulture), "Mapped PAD amplitude volume into AquaSynth layer gain."));
        matched.Add(new($"{featurePrefix}bandwidth", bandwidth.ToString(CultureInfo.InvariantCulture), "Mapped Zyn PAD bandwidth into AquaSynth spectral table generation."));
        matched.Add(new($"{featurePrefix}bandwidth_scale", bandwidthScale.ToString(CultureInfo.InvariantCulture), "Mapped Zyn PAD bandwidth scaling exponent selection."));
        matched.Add(new($"{featurePrefix}harmonic_profile", profile, "Mapped Zyn PAD harmonic profile shape into AquaSynth spectral table generation."));
        matched.Add(new($"{featurePrefix}harmonic_position", position, "Mapped Zyn PAD harmonic position warp into AquaSynth spectral table generation."));
        if (lowPass < 1)
        {
            matched.Add(new($"{featurePrefix}filter_lpf", F(lowPass), "Mapped static Zyn PAD global low-pass frequency into AquaSynth layer filtering."));
            matched.Add(new($"{featurePrefix}filter_lpf_q", F(lowPassQ), "Mapped Zyn low-pass Q into AquaSynth filter damping."));
            matched.Add(new($"{featurePrefix}filter_lpf_order", lowPassOrder.ToString(CultureInfo.InvariantCulture), "Mapped Zyn low-pass family and stage count into AquaSynth low-pass order."));
        }
        if (highPass > 0)
        {
            matched.Add(new($"{featurePrefix}filter_hpf", F(highPass), "Mapped static Zyn PAD global high-pass frequency into AquaSynth layer filtering."));
            matched.Add(new($"{featurePrefix}filter_hpf_order", highPassOrder.ToString(CultureInfo.InvariantCulture), "Mapped Zyn high-pass family and stage count into AquaSynth high-pass order."));
        }
        if (bandPass > 0)
        {
            matched.Add(new($"{featurePrefix}filter_bpf", F(bandPass), "Mapped static Zyn PAD global band-pass center into AquaSynth layer filtering."));
            matched.Add(new($"{featurePrefix}filter_bpf_q", F(bandPassQ), "Mapped Zyn band-pass Q into AquaSynth filter bandwidth."));
            matched.Add(new($"{featurePrefix}filter_bpf_order", bandPassOrder.ToString(CultureInfo.InvariantCulture), "Mapped Zyn band-pass family and stage count into AquaSynth band-pass order."));
        }
        if (notch > 0)
        {
            matched.Add(new($"{featurePrefix}filter_notch", F(notch), "Mapped static Zyn PAD global notch center into AquaSynth layer filtering."));
            matched.Add(new($"{featurePrefix}filter_notch_q", F(notchQ), "Mapped Zyn notch Q into AquaSynth notch width."));
            matched.Add(new($"{featurePrefix}filter_notch_order", notchOrder.ToString(CultureInfo.InvariantCulture), "Mapped Zyn notch family and stage count into AquaSynth notch order."));
        }
        if (lowPassEnvelope is not null)
        {
            matched.Add(new($"{featurePrefix}filter_lpf_envelope", "ratelevel", "Mapped Zyn PAD filter envelope octave offsets into AquaSynth low-pass cutoff motion."));
        }
        if (highPassEnvelope is not null)
        {
            matched.Add(new($"{featurePrefix}filter_hpf_envelope", "ratelevel", "Mapped Zyn PAD filter envelope octave offsets into AquaSynth high-pass cutoff motion."));
        }
        if (filter?.Element("FILTER_LFO") is { } lfo && IntParam(lfo, "intensity", 0) != 0)
        {
            missing.Add(new($"{featurePrefix}filter_lfo", "present", "Filter LFO lowering remains separate pressure."));
        }

        var filterEnvelopeText = lowPassEnvelope is null
            ? ""
            : $" lpf_env=rl lpf_start={F(lowPassEnvelope.StartLevel)} lpf_rates={F(lowPassEnvelope.Rate1Seconds)},{F(lowPassEnvelope.Rate2Seconds)},{F(lowPassEnvelope.Rate3Seconds)},{F(lowPassEnvelope.Rate4Seconds)} lpf_levels={F(lowPassEnvelope.Level1)},{F(lowPassEnvelope.Level2)},{F(lowPassEnvelope.Level3)},{F(lowPassEnvelope.Level4)} lpf_curves=lin,lin,lin,lin";
        var highPassEnvelopeText = highPassEnvelope is null
            ? ""
            : $" hpf_env=rl hpf_start={F(highPassEnvelope.StartLevel)} hpf_rates={F(highPassEnvelope.Rate1Seconds)},{F(highPassEnvelope.Rate2Seconds)},{F(highPassEnvelope.Rate3Seconds)},{F(highPassEnvelope.Rate4Seconds)} hpf_levels={F(highPassEnvelope.Level1)},{F(highPassEnvelope.Level2)},{F(highPassEnvelope.Level3)},{F(highPassEnvelope.Level4)} hpf_curves=lin,lin,lin,lin";
        var filterQText = lowPass < 1 ? $" lpf_q={F(lowPassQ)}" : "";
        var highPassText = highPass > 0 ? $" hpf={F(highPass)} hpf_order={highPassOrder}" : "";
        var bandPassText = bandPass > 0 ? $" bpf={F(bandPass)} bpf_q={F(bandPassQ)} bpf_order={bandPassOrder}" : "";
        var notchText = notch > 0 ? $" notch={F(notch)} notch_q={F(notchQ)} notch_order={notchOrder}" : "";
        script.AppendLine($"layer name={safeName} engine=pad gain={F(layerGain)} lpf={F(lowPass)}{filterQText} lpf_order={lowPassOrder}{highPassText}{bandPassText}{notchText}{filterEnvelopeText}{highPassEnvelopeText} env=rl rates={F(attack)},{F(decay)},1,{F(release)} levels=1,1,1,0 curves=lin,lin,lin,lin gate=1.5");
        script.AppendLine($"spectrum layer={safeName} root={F(tableRootFrequencyHz)} freq={F(noteFrequencyHz)} spread=0 pad_mode={mode} pad_bandwidth={bandwidth} pad_bwscale={bandwidthScale} pad_profile={profile} pad_position={position} partials={partialText}");
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

    internal static XDocument ParseDocumentForSurvey(ReadOnlySpan<byte> bytes) =>
        ParseDocument(bytes);

    private static int CountElements(XElement root, Func<string, bool> predicate) =>
        root.Descendants().Count(element => predicate(element.Name.LocalName));

    private static bool EngineEnabled(XElement item, string flagName, string sectionName) =>
        BoolParamValue(item, flagName) ?? item.Element(sectionName) is not null;

    private static bool IsEnabledPadItem(XElement item) =>
        BoolParam(item, "enabled") && EngineEnabled(item, "pad_enabled", "PAD_SYNTH_PARAMETERS");

    private static IEnumerable<ZynEngine> EnabledEnginesForReader(XElement item)
    {
        if (EngineEnabled(item, "add_enabled", "ADD_SYNTH_PARAMETERS")) yield return ZynEngine.AddSynth;
        if (EngineEnabled(item, "sub_enabled", "SUB_SYNTH_PARAMETERS")) yield return ZynEngine.SubSynth;
        if (EngineEnabled(item, "pad_enabled", "PAD_SYNTH_PARAMETERS")) yield return ZynEngine.PadSynth;
    }

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

    private static bool? NullableBoolParamValue(XElement? root, string name) =>
        root is null ? null : BoolParamValue(root, name);

    private static int IntAttribute(XElement element, string name) =>
        int.TryParse(AttributeValue(element, name), out var value) ? value : 0;

    private static string? AttributeValue(XElement element, string name) =>
        element.Attribute(name)?.Value;

    private static IReadOnlyList<HarmonicPartial> ParsePadOscillatorHarmonics(XElement? oscillator, float tableRootFrequencyHz, int maxHarmonics)
    {
        var harmonicMagnitudes = oscillator?.Element("HARMONICS")?.Elements("HARMONIC")
            .Select(ParsePadHarmonicMagnitude)
            .Where(partial => partial.Gain != 0)
            .OrderBy(partial => partial.Ratio)
            .ToArray() ?? [];
        if (harmonicMagnitudes.Length == 0)
        {
            return [];
        }

        if (!NeedsOscilGenWaveformExpansion(oscillator, harmonicMagnitudes))
        {
            return oscillator?.Element("HARMONICS")?.Elements("HARMONIC")
                .Select(ParsePadDirectHarmonic)
                .Where(partial => partial.Gain > 0)
                .OrderBy(partial => partial.Ratio)
                .Take(Math.Min(maxHarmonics, 32))
                .ToArray() ?? [];
        }

        var freqs = ZynOscilGenFrequencies(oscillator, harmonicMagnitudes, tableRootFrequencyHz, maxHarmonics);
        var gains = freqs.Select(value => value.Magnitude).ToArray();
        ApplyZynSpectrumAdjust(
            gains,
            IntParam(oscillator, "spectrum_adjust_type", 0),
            IntParam(oscillator, "spectrum_adjust_par", 64));

        var max = gains.DefaultIfEmpty(0).Max(Math.Abs);
        if (max <= 0) return [];
        return gains
            .Select((gain, index) => new { gain, index })
            .Where(item => item.index > 0 && Math.Abs(item.gain) > max * 0.0001)
            .Select(item => new HarmonicPartial(item.index, (float)(0.16 * Math.Abs(item.gain) / max)))
            .Take(Math.Min(maxHarmonics, 32))
            .ToArray();
    }

    private static bool NeedsOscilGenWaveformExpansion(XElement? oscillator, IReadOnlyList<HarmonicPartial> harmonicMagnitudes) =>
        IntParam(oscillator, "wave_shaping_function", 0) != 0 ||
        IntParam(oscillator, "filter_type", 0) != 0 ||
        IntParam(oscillator, "modulation", 0) != 0 ||
        IntParam(oscillator, "base_function_modulation", 0) != 0 ||
        IntParam(oscillator, "spectrum_adjust_type", 0) != 0 ||
        (IntParam(oscillator, "base_function", 0) != 0 && harmonicMagnitudes.Count > 1) ||
        IntParam(oscillator, "harmonic_shift", 0) != 0 ||
        IntParam(oscillator, "adaptive_harmonics", 0) != 0;

    private static Complex[] ZynOscilGenFrequencies(
        XElement? oscillator,
        IReadOnlyList<HarmonicPartial> harmonicMagnitudes,
        float tableRootFrequencyHz,
        int maxHarmonics)
    {
        var freqs = Dft(ZynOscilGenBaseSamples(oscillator, harmonicMagnitudes), maxHarmonics);
        if (NullableBoolParamValue(oscillator, "harmonic_shift_first") ?? false)
        {
            ApplyZynHarmonicShift(freqs, IntParam(oscillator, "harmonic_shift", 0));
        }

        var filterBeforeWaveShaping = IntParam(oscillator, "filter_before_wave_shaping", 0) != 0;
        if (filterBeforeWaveShaping)
        {
            ApplyZynOscilFilter(freqs, oscillator);
            freqs = ApplyZynWaveShape(freqs, oscillator, maxHarmonics);
        }
        else
        {
            freqs = ApplyZynWaveShape(freqs, oscillator, maxHarmonics);
            ApplyZynOscilFilter(freqs, oscillator);
        }

        freqs = ApplyZynOscilModulation(freqs, oscillator, maxHarmonics);
        if (!(NullableBoolParamValue(oscillator, "harmonic_shift_first") ?? false))
        {
            ApplyZynHarmonicShift(freqs, IntParam(oscillator, "harmonic_shift", 0));
        }

        ApplyZynAdaptiveHarmonics(freqs, oscillator, tableRootFrequencyHz);
        return freqs;
    }

    private static double[] ZynOscilGenBaseSamples(XElement? oscillator, IReadOnlyList<HarmonicPartial> harmonicMagnitudes)
    {
        const int sampleCount = 4096;
        var samples = new double[sampleCount];
        var baseFunction = IntParam(oscillator, "base_function", 0);
        var parameter = IntParam(oscillator, "base_function_par", 64);
        var parameterValue = parameter == 64 ? 0.5 : (parameter + 0.5) / 128.0;

        foreach (var harmonic in harmonicMagnitudes)
        {
            var harmonicNumber = Math.Max(1, (int)Math.Round(harmonic.Ratio));
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (double)sampleCount * harmonicNumber;
                var phase = ZynBaseFunctionPhase(oscillator, t);
                var sample = baseFunction == 0
                    ? -Math.Sin(Math.Tau * phase)
                    : ZynBaseFunction(baseFunction, phase, parameterValue);
                samples[i] += sample * harmonic.Gain;
            }
        }

        NormalizePeak(samples);
        return samples;
    }

    private static double ZynBaseFunctionPhase(XElement? oscillator, double phase)
    {
        var type = IntParam(oscillator, "base_function_modulation", 0);
        if (type == 0)
        {
            return phase - Math.Floor(phase);
        }

        var p1 = IntParam(oscillator, "base_function_modulation_par1", 64) / 127.0;
        var p2 = IntParam(oscillator, "base_function_modulation_par2", 64) / 127.0;
        var p3 = IntParam(oscillator, "base_function_modulation_par3", 32) / 127.0;
        phase = type switch
        {
            1 => phase * ZynBaseModulationRevMultiplier(p3) +
                 Math.Sin((phase + p2) * Math.Tau) * ((Math.Pow(2.0, p1 * 5.0) - 1.0) / 10.0),
            2 => phase +
                 Math.Sin((phase * (1.0 + Math.Floor(Math.Pow(2.0, p3 * 5.0) - 1.0)) + p2) * Math.Tau) *
                 ((Math.Pow(2.0, p1 * 5.0) - 1.0) / 10.0),
            3 => phase +
                 Math.Pow((1.0 - Math.Cos((phase + p2) * Math.Tau)) * 0.5,
                     0.01 + (Math.Pow(2.0, p3 * 16.0) - 1.0) / 10.0) *
                 ((Math.Pow(2.0, p1 * 7.0) - 1.0) / 10.0),
            4 => phase * Math.Pow(2.0,
                     IntParam(oscillator, "base_function_modulation_par1", 64) / 32.0 +
                     IntParam(oscillator, "base_function_modulation_par2", 64) / 2048.0) + p3,
            _ => phase
        };

        return phase - Math.Floor(phase);
    }

    private static double ZynBaseModulationRevMultiplier(double parameter)
    {
        var multiplier = Math.Floor(Math.Pow(2.0, parameter * 5.0) - 1.0);
        return multiplier < 0.9999 ? -1.0 : multiplier;
    }

    private static Complex[] Dft(IReadOnlyList<double> samples, int maxHarmonics)
    {
        var freqs = new Complex[maxHarmonics + 1];
        for (var harmonic = 1; harmonic <= maxHarmonics; harmonic++)
        {
            var sum = Complex.Zero;
            for (var i = 0; i < samples.Count; i++)
            {
                var angle = -Math.Tau * harmonic * i / samples.Count;
                sum += samples[i] * Complex.FromPolarCoordinates(1.0, angle);
            }
            freqs[harmonic] = sum / samples.Count;
        }
        return freqs;
    }

    private static double[] IDft(Complex[] freqs, int sampleCount)
    {
        var samples = new double[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sum = Complex.Zero;
            for (var harmonic = 1; harmonic < freqs.Length; harmonic++)
            {
                var angle = Math.Tau * harmonic * i / sampleCount;
                sum += freqs[harmonic] * Complex.FromPolarCoordinates(1.0, angle);
            }
            samples[i] = 2.0 * sum.Real;
        }
        return samples;
    }

    private static void ApplyZynOscilFilter(Complex[] freqs, XElement? oscillator)
    {
        var type = IntParam(oscillator, "filter_type", 0);
        if (type == 0) return;
        var par = 1.0 - IntParam(oscillator, "filter_par1", 64) / 128.0;
        var par2 = IntParam(oscillator, "filter_par2", 64) / 127.0;
        for (var harmonic = 1; harmonic < freqs.Length; harmonic++)
        {
            freqs[harmonic] *= ZynOscilFilterGain(type, harmonic, par, par2);
        }
        NormalizeComplexPeak(freqs);
    }

    private static double ZynOscilFilterGain(int type, int harmonic, double par, double par2)
    {
        var i = Math.Max(1, harmonic);
        return type switch
        {
            1 => ZynOscilLowPass(i, par, par2),
            2 => Math.Pow(1.0 - Math.Pow(1.0 - par * par, i + 1), par2 * 2.0 + 0.1),
            6 => (i + 1 > Math.Pow(2.0, (1.0 - par) * 10.0) ? 0.0 : 1.0) * par2 + (1.0 - par2),
            7 => (i + 1 > Math.Pow(2.0, (1.0 - par) * 7.0) ? 1.0 : 0.0) * par2 + (1.0 - par2),
            13 => i == (int)Math.Pow(2.0, (1.0 - par) * 7.2) ? Math.Pow(2.0, par2 * par2 * 8.0) : 1.0,
            _ => 1.0
        };
    }

    private static double ZynOscilLowPass(int i, double par, double par2)
    {
        var gain = Math.Pow(1.0 - par * par * par * 0.99, i);
        var tmp = Math.Pow(par2, 4.0) * 0.5 + 0.0001;
        return gain < tmp ? Math.Pow(gain, 10.0) / Math.Pow(tmp, 9.0) : gain;
    }

    private static Complex[] ApplyZynWaveShape(Complex[] freqs, XElement? oscillator, int maxHarmonics)
    {
        var type = IntParam(oscillator, "wave_shaping_function", 0);
        if (type == 0) return freqs;
        var samples = IDft(freqs, 4096);
        NormalizePeak(samples);
        var drive = IntParam(oscillator, "wave_shaping", 64) / 127.0;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = ZynWaveShapeSample(samples[i], type, drive);
        }
        return Dft(samples, maxHarmonics);
    }

    private static double ZynWaveShapeSample(double sample, int type, double drive)
    {
        return type switch
        {
            1 => Math.Atan(sample * (Math.Pow(10.0, drive * drive * 3.0) - 1.0 + 0.001)) / Math.Atan(Math.Pow(10.0, drive * drive * 3.0) - 1.0 + 0.001),
            4 => ZynSineWaveShape(sample, drive),
            7 => Math.Clamp(sample, -Math.Pow(2.0, -drive * drive * 8.0), Math.Pow(2.0, -drive * drive * 8.0)) / Math.Pow(2.0, -drive * drive * 8.0),
            11 => sample * (Math.Pow(5.0, drive * drive) - 0.5) * 0.9999 - Math.Floor(0.5 + sample * (Math.Pow(5.0, drive * drive) - 0.5) * 0.9999),
            15 => sample * (drive * drive * 35.0 + 1.0) / Math.Pow(1.0 + Math.Pow(Math.Abs(sample * (drive * drive * 35.0 + 1.0)), 2.5), 1.0 / 2.5),
            _ => sample
        };
    }

    private static double ZynSineWaveShape(double sample, double drive)
    {
        var ws = drive * drive * drive * 32.0 + 0.0001;
        var divisor = ws < 1.57 ? Math.Sin(ws) : 1.0;
        return Math.Sin(sample * ws) / divisor;
    }

    private static Complex[] ApplyZynOscilModulation(Complex[] freqs, XElement? oscillator, int maxHarmonics)
    {
        var type = IntParam(oscillator, "modulation", 0);
        if (type == 0) return freqs;
        var samples = IDft(freqs, 4096);
        NormalizePeak(samples);
        var output = new double[samples.Length];
        var par1 = IntParam(oscillator, "modulation_par1", 64) / 127.0;
        var par2 = 0.5 - IntParam(oscillator, "modulation_par2", 64) / 127.0;
        var par3 = IntParam(oscillator, "modulation_par3", 32) / 127.0;
        (par1, par3) = type switch
        {
            1 => ((Math.Pow(2.0, par1 * 7.0) - 1.0) / 100.0, Math.Floor(Math.Pow(2.0, par3 * 5.0) - 1.0) < 0.9999 ? -1.0 : Math.Floor(Math.Pow(2.0, par3 * 5.0) - 1.0)),
            2 => ((Math.Pow(2.0, par1 * 7.0) - 1.0) / 100.0, 1.0 + Math.Floor(Math.Pow(2.0, par3 * 5.0) - 1.0)),
            3 => ((Math.Pow(2.0, par1 * 9.0) - 1.0) / 100.0, 0.01 + (Math.Pow(2.0, par3 * 16.0) - 1.0) / 10.0),
            _ => (par1, par3)
        };
        for (var i = 0; i < output.Length; i++)
        {
            var t = i / (double)output.Length;
            t = type switch
            {
                1 => t * par3 + Math.Sin((t + par2) * Math.Tau) * par1,
                2 => t + Math.Sin((t * par3 + par2) * Math.Tau) * par1,
                3 => t + Math.Pow((1.0 - Math.Cos((t + par2) * Math.Tau)) * 0.5, par3) * par1,
                _ => t
            };
            output[i] = InterpolateWrap(samples, t * samples.Length);
        }
        return Dft(output, maxHarmonics);
    }

    private static double InterpolateWrap(IReadOnlyList<double> samples, double position)
    {
        position -= Math.Floor(position / samples.Count) * samples.Count;
        var index = (int)Math.Floor(position);
        var frac = position - index;
        return samples[index] * (1.0 - frac) + samples[(index + 1) % samples.Count] * frac;
    }

    private static void ApplyZynHarmonicShift(Complex[] freqs, int shift)
    {
        if (shift == 0) return;
        var copy = freqs.ToArray();
        Array.Clear(freqs);
        for (var i = 1; i < copy.Length; i++)
        {
            var target = i - shift;
            if (target > 0 && target < freqs.Length)
            {
                freqs[target] = copy[i];
            }
        }
    }

    private static void ApplyZynAdaptiveHarmonics(Complex[] freqs, XElement? oscillator, float baseFrequencyHz)
    {
        var mode = IntParam(oscillator, "adaptive_harmonics", 0);
        if (mode == 0)
        {
            return;
        }

        var input = freqs.ToArray();
        Array.Clear(freqs);
        input[0] = Complex.Zero;
        var baseFrequency = 30.0 * Math.Pow(10.0, IntParam(oscillator, "adaptive_harmonics_base_frequency", 128) / 128.0);
        var power = (IntParam(oscillator, "adaptive_harmonics_power", 100) + 1.0) / 101.0;
        var ratio = Math.Pow(Math.Max(1.0, baseFrequencyHz) / baseFrequency, power);
        var down = false;
        if (ratio > 1.0)
        {
            ratio = 1.0 / ratio;
            down = true;
        }

        for (var i = 0; i < freqs.Length - 2; i++)
        {
            var position = i * ratio;
            var high = (int)position;
            var low = position - high;
            if (high >= freqs.Length - 2)
            {
                break;
            }

            if (down)
            {
                freqs[high] += (1.0 - low) * input[i];
                freqs[high + 1] += low * input[i];
            }
            else
            {
                freqs[i] = (1.0 - low) * input[high] + low * input[high + 1];
            }
        }

        if (!down)
        {
            freqs[0] *= ratio;
        }

        if (freqs.Length > 1)
        {
            freqs[1] += freqs[0];
        }
        freqs[0] = Complex.Zero;
        ApplyZynAdaptiveHarmonicPostprocess(freqs, mode, IntParam(oscillator, "adaptive_harmonics_par", 50));
    }

    private static void ApplyZynAdaptiveHarmonicPostprocess(Complex[] freqs, int mode, int parameter)
    {
        if (mode <= 1)
        {
            return;
        }

        var par = 1.0 - Math.Pow(1.0 - parameter * 0.01, 1.5);
        var input = freqs.ToArray();
        for (var i = 1; i < freqs.Length; i++)
        {
            freqs[i] *= 1.0 - par;
            input[i] *= par;
        }

        if (mode == 2)
        {
            for (var i = 1; i < freqs.Length; i += 2)
            {
                freqs[i] += input[i];
            }
            return;
        }

        var harmonic = (mode - 3) / 2 + 2;
        var subtractVsAdd = (mode - 3) % 2;
        if (subtractVsAdd == 0)
        {
            for (var i = harmonic; i < freqs.Length; i += harmonic)
            {
                freqs[i] += input[i];
            }
        }
        else
        {
            for (var source = 1; source * harmonic < freqs.Length; source++)
            {
                freqs[source * harmonic] += input[source];
            }
        }
    }

    private static void NormalizePeak(double[] samples)
    {
        var peak = samples.Select(Math.Abs).DefaultIfEmpty(0).Max();
        if (peak < 0.00001) peak = 1.0;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] /= peak;
        }
    }

    private static void NormalizeComplexPeak(Complex[] freqs)
    {
        var peak = freqs.Select(value => value.Magnitude).DefaultIfEmpty(0).Max();
        if (peak < 0.00001) peak = 1.0;
        for (var i = 0; i < freqs.Length; i++)
        {
            freqs[i] /= peak;
        }
    }

    private static HarmonicPartial ParsePadHarmonicMagnitude(XElement harmonic)
    {
        var id = IntAttribute(harmonic, "id");
        var ratio = Math.Max(1, id);
        var magnitude = IntParam(harmonic, "mag", 0);
        var hmagNew = 1.0f - MathF.Abs(magnitude / 64.0f - 1.0f);
        var gain = magnitude == 64 ? 0 : 1.0f - hmagNew;
        if (magnitude < 64)
        {
            gain = -gain;
        }
        return new HarmonicPartial(ratio, gain);
    }

    private static HarmonicPartial ParsePadDirectHarmonic(XElement harmonic)
    {
        var id = IntAttribute(harmonic, "id");
        var ratio = Math.Max(1, id);
        var magnitude = IntParam(harmonic, "mag", 0);
        return new HarmonicPartial(ratio, 0.16f * Math.Clamp(magnitude / 127f, 0, 1));
    }

    private static double ZynBaseFunction(int function, double x, double a)
    {
        x -= Math.Floor(x);
        return function switch
        {
            1 => ZynTriangle(x, a),
            2 => x < a ? -1.0 : 1.0,
            3 => ZynSaw(x, a),
            4 => Math.Pow(x, Math.Exp((a - 0.5) * 10.0)) * 2.0 - 1.0,
            5 => Math.Exp(-Math.Pow(x * 2.0 - 1.0, 2.0) * (Math.Exp(a * 8.0) + 5.0)) * 2.0 - 1.0,
            6 => ZynDiode(x, a),
            7 => Math.Sin(Math.Pow(x, Math.Exp((a - 0.5) * 5.0)) * Math.PI) * 2.0 - 1.0,
            8 => Math.Sin(Math.Clamp((x - 0.5) * Math.Exp((a - 0.5) * Math.Log(128.0)), -0.5, 0.5) * Math.Tau),
            9 => -Math.Sin(Math.Sign((x + 0.5) % 1.0 * 2.0 - 1.0) * Math.Pow(Math.Abs((x + 0.5) % 1.0 * 2.0 - 1.0), Math.Pow(3.0, (a - 0.5) * 8.0)) * Math.PI),
            _ => -Math.Sin(Math.Tau * x)
        };
    }

    private static double ZynTriangle(double x, double a)
    {
        x = (x + 0.25) % 1.0;
        a = Math.Max(0.00001, 1.0 - a);
        x = x < 0.5 ? x * 4.0 - 1.0 : (1.0 - x) * 4.0 - 1.0;
        return Math.Clamp(x / -a, -1.0, 1.0);
    }

    private static double ZynSaw(double x, double a)
    {
        a = Math.Clamp(a, 0.00001, 0.99999);
        return x < a ? x / a * 2.0 - 1.0 : (1.0 - x) / (1.0 - a) * 2.0 - 1.0;
    }

    private static double ZynDiode(double x, double a)
    {
        a = Math.Clamp(a, 0.00001, 0.99999) * 2.0 - 1.0;
        var y = Math.Cos((x + 0.5) * Math.Tau) - a;
        if (y < 0) y = 0;
        return y / (1.0 - a) * 2.0 - 1.0;
    }

    private static void ApplyZynSpectrumAdjust(double[] gains, int type, int parameter)
    {
        if (type == 0) return;
        var max = gains.DefaultIfEmpty(0).Max(Math.Abs);
        if (max <= 0) return;
        for (var i = 0; i < gains.Length; i++) gains[i] /= max;

        var par = parameter / 127.0;
        par = type switch
        {
            1 => par <= 0.5
                ? Math.Pow(5.0, 1.0 - par * 2.0)
                : Math.Pow(8.0, 1.0 - par * 2.0),
            2 or 3 => Math.Pow(10.0, (1.0 - par) * 3.0) * 0.001,
            _ => par
        };

        for (var i = 0; i < gains.Length; i++)
        {
            var sign = Math.Sign(gains[i]);
            var mag = Math.Abs(gains[i]);
            mag = type switch
            {
                1 => Math.Pow(mag, par),
                2 => mag < par ? 0.0 : mag,
                3 => Math.Min(1.0, mag / par),
                _ => mag
            };
            gains[i] = sign * mag;
        }
    }

    private static string ZynPadMode(int mode) => mode switch
    {
        1 => "discrete",
        2 => "continuous",
        _ => "bandwidth"
    };

    private static string ZynProfileText(XElement? profile)
    {
        var baseType = IntParam(profile, "base_type", 0) switch
        {
            1 => "square",
            2 => "double",
            _ => "gaussian"
        };
        var amplitudeType = IntParam(profile, "amplitude_multiplier_type", 0) switch
        {
            1 => "gaussian",
            2 => "sine",
            3 => "flat",
            _ => "off"
        };
        var amplitudeMode = IntParam(profile, "amplitude_multiplier_mode", 0) switch
        {
            1 => "mult",
            2 => "div1",
            3 => "div2",
            _ => "sum"
        };
        var half = IntParam(profile, "one_half", 0) switch
        {
            1 => "upper",
            2 => "lower",
            _ => "full"
        };
        var autoscale = NullableBoolParamValue(profile, "autoscale") ?? true;
        return string.Join(":",
            baseType,
            IntParam(profile, "base_par1", 80).ToString(CultureInfo.InvariantCulture),
            IntParam(profile, "frequency_multiplier", 0).ToString(CultureInfo.InvariantCulture),
            IntParam(profile, "modulator_par1", 0).ToString(CultureInfo.InvariantCulture),
            IntParam(profile, "modulator_frequency", 30).ToString(CultureInfo.InvariantCulture),
            IntParam(profile, "width", 127).ToString(CultureInfo.InvariantCulture),
            amplitudeType,
            amplitudeMode,
            IntParam(profile, "amplitude_multiplier_par1", 80).ToString(CultureInfo.InvariantCulture),
            IntParam(profile, "amplitude_multiplier_par2", 64).ToString(CultureInfo.InvariantCulture),
            autoscale ? "yes" : "no",
            half);
    }

    private static string ZynPositionText(XElement? position)
    {
        var type = IntParam(position, "type", 0) switch
        {
            1 => "shift_up",
            2 => "shift_down",
            3 => "power_up",
            4 => "power_down",
            5 => "sine",
            6 => "power",
            7 => "shift",
            _ => "harmonic"
        };
        return string.Join(":",
            type,
            IntParam(position, "parameter1", 0).ToString(CultureInfo.InvariantCulture),
            IntParam(position, "parameter2", 0).ToString(CultureInfo.InvariantCulture),
            IntParam(position, "parameter3", 0).ToString(CultureInfo.InvariantCulture));
    }

    private static float ZynPadLowPass(XElement? filter, float playbackFrequencyHz)
    {
        if (filter is null || !TryZynPadFilterCutoff(filter, playbackFrequencyHz, out var cutoffHz, out var category, out var type))
        {
            return 1;
        }

        if (!IsZynPadLowPassFilter(category, type))
        {
            return 1;
        }

        return Math.Clamp(cutoffHz / 18000.0f, 20.0f / 18000.0f, 1f);
    }

    private static int ZynPadLowPassOrder(XElement? filter)
    {
        if (filter is null)
        {
            return 1;
        }

        var category = DescendantIntParam(filter, "category", 0);
        var type = DescendantIntParam(filter, "type", 0);
        if (!IsZynPadLowPassFilter(category, type))
        {
            return 1;
        }

        var poles = (category, type) switch
        {
            (0, 0) => 1,
            (0, 2) => 2,
            (2, 0) => 1,
            _ => 1
        };
        return Math.Clamp(poles * (DescendantIntParam(filter, "stages", 0) + 1), 1, 12);
    }

    private static float ZynPadHighPass(XElement? filter, float playbackFrequencyHz)
    {
        if (filter is null || !TryZynPadFilterCutoff(filter, playbackFrequencyHz, out var cutoffHz, out var category, out var type))
        {
            return 0;
        }

        if (!IsZynPadHighPassFilter(category, type))
        {
            return 0;
        }

        return Math.Clamp(MathF.Sqrt(cutoffHz / 7000.0f), 0f, 1f);
    }

    private static float ZynPadBandPass(XElement? filter, float playbackFrequencyHz)
    {
        if (filter is null || !TryZynPadFilterCutoff(filter, playbackFrequencyHz, out var cutoffHz, out var category, out var type))
        {
            return 0;
        }

        if (!IsZynPadBandPassFilter(category, type))
        {
            return 0;
        }

        return Math.Clamp(cutoffHz / 18000.0f, 20.0f / 18000.0f, 1f);
    }

    private static float ZynPadNotch(XElement? filter, float playbackFrequencyHz)
    {
        if (filter is null || !TryZynPadFilterCutoff(filter, playbackFrequencyHz, out var cutoffHz, out var category, out var type))
        {
            return 0;
        }

        if (!IsZynPadNotchFilter(category, type))
        {
            return 0;
        }

        return Math.Clamp(cutoffHz / 18000.0f, 20.0f / 18000.0f, 1f);
    }

    private static int ZynPadHighPassOrder(XElement? filter)
    {
        if (filter is null)
        {
            return 1;
        }

        var category = DescendantIntParam(filter, "category", 0);
        var type = DescendantIntParam(filter, "type", 0);
        if (!IsZynPadHighPassFilter(category, type))
        {
            return 1;
        }

        var poles = (category, type) switch
        {
            (0, 1) => 1,
            (0, 3) => 2,
            (2, 1) => 1,
            _ => 1
        };
        return Math.Clamp(poles * (DescendantIntParam(filter, "stages", 0) + 1), 1, 12);
    }

    private static int ZynPadBandPassOrder(XElement? filter)
    {
        if (filter is null)
        {
            return 1;
        }

        var category = DescendantIntParam(filter, "category", 0);
        var type = DescendantIntParam(filter, "type", 0);
        if (!IsZynPadBandPassFilter(category, type))
        {
            return 1;
        }

        return Math.Clamp(2 * (DescendantIntParam(filter, "stages", 0) + 1), 1, 12);
    }

    private static int ZynPadNotchOrder(XElement? filter)
    {
        if (filter is null)
        {
            return 1;
        }

        var category = DescendantIntParam(filter, "category", 0);
        var type = DescendantIntParam(filter, "type", 0);
        if (!IsZynPadNotchFilter(category, type))
        {
            return 1;
        }

        return Math.Clamp(2 * (DescendantIntParam(filter, "stages", 0) + 1), 1, 12);
    }

    private static float ZynPadLowPassQ(XElement? filter)
    {
        if (filter is null)
        {
            return 0;
        }

        var category = DescendantIntParam(filter, "category", 0);
        var type = DescendantIntParam(filter, "type", 0);
        if (!IsZynPadLowPassFilter(category, type))
        {
            return 0;
        }

        var q = DescendantIntParam(filter, "q", 64) / 127.0f;
        return MathF.Exp(q * q * MathF.Log(1000.0f)) - 0.9f;
    }

    private static float ZynPadFilterQ(XElement? filter)
    {
        if (filter is null)
        {
            return 1;
        }

        var q = DescendantIntParam(filter, "q", 64) / 127.0f;
        return MathF.Exp(q * q * MathF.Log(1000.0f)) - 0.9f;
    }

    private static bool TryZynPadFilterCutoff(XElement filter, float playbackFrequencyHz, out float cutoffHz, out int category, out int type)
    {
        category = DescendantIntParam(filter, "category", 0);
        type = DescendantIntParam(filter, "type", 0);
        if (category is not (0 or 2))
        {
            cutoffHz = 0;
            return false;
        }

        cutoffHz = MathF.Pow(2.0f, (DescendantIntParam(filter, "freq", 127) / 64.0f - 1.0f) * 5.0f + 9.96578428f);
        var trackingPercent = (DescendantIntParam(filter, "freq_track", 64) - 64.0f) / 64.0f;
        cutoffHz *= MathF.Pow(2.0f, MathF.Log2(playbackFrequencyHz / 440.0f) * trackingPercent);
        return true;
    }

    private static bool IsZynPadLowPassFilter(int category, int type) =>
        (category == 0 && type is 0 or 2) ||
        (category == 2 && type == 0);

    private static bool IsZynPadHighPassFilter(int category, int type) =>
        (category == 0 && type is 1 or 3) ||
        (category == 2 && type == 1);

    private static bool IsZynPadBandPassFilter(int category, int type) =>
        (category == 0 && type == 4) ||
        (category == 2 && type == 2);

    private static bool IsZynPadNotchFilter(int category, int type) =>
        (category == 0 && type == 5) ||
        (category == 2 && type == 3);

    private static RateLevelEnvelope? ZynPadLowPassEnvelope(XElement? filter, float baseLowPass)
    {
        var envelope = filter?.Element("FILTER_ENVELOPE");
        if (envelope is null || baseLowPass >= 1)
        {
            return null;
        }

        static float OctaveOffset(int value) => (value - 64.0f) / 64.0f * 6.0f;
        float LevelOffset(int value) => Math.Clamp(baseLowPass * MathF.Pow(2.0f, OctaveOffset(value)) - baseLowPass, -1f, 1f);

        var start = LevelOffset(IntParam(envelope, "A_val", 64));
        var level1 = LevelOffset(IntParam(envelope, "D_val", 64));
        var level2 = LevelOffset(IntParam(envelope, "S_val", 64));
        var level4 = LevelOffset(IntParam(envelope, "R_val", 64));
        if (MathF.Abs(start) < 0.00001f &&
            MathF.Abs(level1) < 0.00001f &&
            MathF.Abs(level2) < 0.00001f &&
            MathF.Abs(level4) < 0.00001f)
        {
            return null;
        }

        return new RateLevelEnvelope(
            EnvelopeTime(envelope, "A_dt", 0.02f), level1,
            EnvelopeTime(envelope, "D_dt", 0.42f), level2,
            1.0f, level2,
            EnvelopeTime(envelope, "R_dt", 1.2f), level4,
            StartLevel: start);
    }

    private static RateLevelEnvelope? ZynPadHighPassEnvelope(XElement? filter, float baseHighPass)
    {
        var envelope = filter?.Element("FILTER_ENVELOPE");
        if (envelope is null || baseHighPass <= 0)
        {
            return null;
        }

        static float OctaveOffset(int value) => (value - 64.0f) / 64.0f * 6.0f;

        float LevelOffset(int value)
        {
            var baseCutoffHz = baseHighPass * baseHighPass * 7000.0f;
            var movedCutoffHz = baseCutoffHz * MathF.Pow(2.0f, OctaveOffset(value));
            var movedHighPass = MathF.Sqrt(Math.Clamp(movedCutoffHz / 7000.0f, 0f, 1f));
            return Math.Clamp(movedHighPass - baseHighPass, -1f, 1f);
        }

        var start = LevelOffset(IntParam(envelope, "A_val", 64));
        var level1 = LevelOffset(IntParam(envelope, "D_val", 64));
        var level2 = LevelOffset(IntParam(envelope, "S_val", 64));
        var level4 = LevelOffset(IntParam(envelope, "R_val", 64));
        if (MathF.Abs(start) < 0.00001f &&
            MathF.Abs(level1) < 0.00001f &&
            MathF.Abs(level2) < 0.00001f &&
            MathF.Abs(level4) < 0.00001f)
        {
            return null;
        }

        return new RateLevelEnvelope(
            EnvelopeTime(envelope, "A_dt", 0.02f), level1,
            EnvelopeTime(envelope, "D_dt", 0.42f), level2,
            1.0f, level2,
            EnvelopeTime(envelope, "R_dt", 1.2f), level4,
            StartLevel: start);
    }

    private static float ZynPadPlaybackFrequency(XElement? frequency, float playbackFrequencyHz)
    {
        var detuneCents = ZynDetuneCents(
            IntParam(frequency, "detune_type", 1),
            IntParam(frequency, "coarse_detune", 0),
            IntParam(frequency, "detune", 8192));
        return playbackFrequencyHz * MathF.Pow(2.0f, detuneCents / 1200.0f);
    }

    private static float ZynDetuneCents(int type, int coarseDetune, int fineDetune)
    {
        var octave = coarseDetune / 1024;
        if (octave >= 8)
        {
            octave -= 16;
        }

        var coarse = coarseDetune % 1024;
        if (coarse > 512)
        {
            coarse -= 1024;
        }

        var fine = fineDetune - 8192;
        float coarseCents;
        float fineCents;
        switch (type)
        {
            case 2:
                coarseCents = MathF.Abs(coarse * 10.0f);
                fineCents = MathF.Abs(fine / 8192.0f) * 10.0f;
                break;
            case 3:
                coarseCents = MathF.Abs(coarse * 100.0f);
                fineCents = MathF.Pow(10.0f, MathF.Abs(fine / 8192.0f) * 3.0f) / 10.0f - 0.1f;
                break;
            case 4:
                coarseCents = MathF.Abs(coarse * 701.95500087f);
                fineCents = (MathF.Pow(2.0f, MathF.Abs(fine / 8192.0f) * 12.0f) - 1.0f) / 4095.0f * 1200.0f;
                break;
            default:
                coarseCents = MathF.Abs(coarse * 50.0f);
                fineCents = MathF.Abs(fine / 8192.0f) * 35.0f;
                break;
        }

        if (fineDetune < 8192)
        {
            fineCents = -fineCents;
        }
        if (coarse < 0)
        {
            coarseCents = -coarseCents;
        }

        return octave * 1200.0f + coarseCents + fineCents;
    }

    private static int IntParam(XElement? root, string name, int fallback)
    {
        var value = root?.Elements("par")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static int DescendantIntParam(XElement? root, string name, int fallback)
    {
        var value = root?.Descendants("par")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static float RealParam(XElement? root, string name, float fallback)
    {
        var value = root?.Elements("par_real")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static IReadOnlyList<Formant> ParseZynVowelFrame(XElement formantFilter, int vowelId, int formantCount)
    {
        var center = IntParam(formantFilter, "center_freq", 64);
        var octaves = IntParam(formantFilter, "octaves_freq", 64);
        return formantFilter
            .Elements("VOWEL")
            .FirstOrDefault(vowel => IntAttribute(vowel, "id") == vowelId)
            ?.Elements("FORMANT")
            .OrderBy(formant => IntAttribute(formant, "id"))
            .Take(formantCount)
            .Select(formant =>
            {
                var frequency = ZynFormantFrequencyHz(IntParam(formant, "freq", 64), center, octaves);
                var q = Math.Clamp(MathF.Pow(25.0f, (IntParam(formant, "q", 64) - 32.0f) / 64.0f), 0.2f, 40.0f);
                var gain = MathF.Pow(0.1f, (1.0f - IntParam(formant, "amp", 127) / 127.0f) * 4.0f);
                return new Formant(frequency, Math.Max(10.0f, frequency / q), gain);
            })
            .ToArray() ?? [];
    }

    private static float ZynFormantFrequencyHz(int value, int center, int octaves)
    {
        var centerHz = 10000.0f * MathF.Pow(10.0f, -(1.0f - center / 127.0f) * 2.0f);
        var octaveSpan = 0.25f + 10.0f * octaves / 127.0f;
        var octaveFactor = MathF.Pow(2.0f, octaveSpan);
        return centerHz / MathF.Sqrt(octaveFactor) * MathF.Pow(octaveFactor, Math.Clamp(value, 0, 127) / 127.0f);
    }

    private static float EnvelopeTime(XElement? envelope, string name, float fallback)
    {
        var value = IntParam(envelope, name, -1);
        if (value < 0)
        {
            return fallback;
        }

        return Math.Clamp((MathF.Pow(2.0f, value / 127.0f * 12.0f) - 1.0f) / 100.0f, 0.0f, 41.0f);
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
    private static readonly HashSet<string> HandledPadOscilFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        "0",
        "1",
        "2",
        "6",
        "7",
        "13"
    };

    private static readonly HashSet<string> HandledPadWaveShapes = new(StringComparer.OrdinalIgnoreCase)
    {
        "0",
        "1",
        "4",
        "7",
        "11",
        "15"
    };

    private static readonly HashSet<string> HandledPadGlobalFilters = new(StringComparer.OrdinalIgnoreCase)
    {
        "cat=0 type=0",
        "cat=0 type=1",
        "cat=0 type=2",
        "cat=0 type=3",
        "cat=0 type=4",
        "cat=0 type=5",
        "cat=2 type=0",
        "cat=2 type=1",
        "cat=2 type=2",
        "cat=2 type=3"
    };

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

    public static ZynCoverageReport CoverageDirectory(string root, int examplesPerBucket = 3)
    {
        if (examplesPerBucket < 1) throw new ArgumentOutOfRangeException(nameof(examplesPerBucket));

        var files = Directory.GetFiles(root, "*.xiz", SearchOption.AllDirectories);
        var buckets = new Dictionary<(string Area, string Key), CoverageAccumulator>();
        var parsed = 0;
        var enabledItems = 0;
        var layeredInstruments = 0;

        foreach (var file in files)
        {
            XDocument document;
            try
            {
                document = ZynInstrumentReader.ParseDocumentForSurvey(File.ReadAllBytes(file));
                parsed++;
            }
            catch
            {
                AddBucket(
                    buckets,
                    "file",
                    "parse_error",
                    ZynCoverageStatus.Unknown,
                    Path.GetRelativePath(root, file),
                    "The survey could not parse this .xiz file.");
                continue;
            }

            var relativePath = Path.GetRelativePath(root, file);
            var instrument = document.Descendants("INSTRUMENT").FirstOrDefault();
            if (instrument is null)
            {
                AddBucket(
                    buckets,
                    "file",
                    "missing_instrument",
                    ZynCoverageStatus.Unknown,
                    relativePath,
                    "The .xiz file has no INSTRUMENT block.");
                continue;
            }

            var enabledInFile = 0;
            foreach (var item in instrument.Descendants("INSTRUMENT_KIT_ITEM"))
            {
                if (!BoolParam(item, "enabled"))
                {
                    continue;
                }

                enabledItems++;
                enabledInFile++;
                var engines = EnabledEngines(item).ToList();
                AddEngineBuckets(buckets, engines, relativePath);
                AddElementPressureBuckets(buckets, item, relativePath);

                if (engines.Contains(ZynEngine.PadSynth))
                {
                    AddPadPressureBuckets(buckets, item, relativePath);
                }
            }

            if (enabledInFile > 1)
            {
                layeredInstruments++;
                AddBucket(
                    buckets,
                    "kit",
                    "multi_item_layering",
                    ZynCoverageStatus.Handled,
                    relativePath,
                    "Named layer declarations own kit/layer identity; richer routing still needs target-specific parity.");
            }
        }

        return new ZynCoverageReport(
            files.Length,
            parsed,
            enabledItems,
            layeredInstruments,
            buckets.Values
                .Select(bucket => bucket.ToBucket(examplesPerBucket))
                .OrderBy(bucket => bucket.Status)
                .ThenBy(bucket => bucket.Area, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(bucket => bucket.Count)
                .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public static string CoverageMarkdown(ZynCoverageReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ZynAddSubFX Coverage Survey");
        builder.AppendLine();
        builder.AppendLine($"Files: {report.FileCount}");
        builder.AppendLine($"Parsed: {report.ParsedFileCount}");
        builder.AppendLine($"Enabled kit items: {report.EnabledKitItemCount}");
        builder.AppendLine($"Layered instruments: {report.LayeredInstrumentCount}");
        builder.AppendLine();

        foreach (var status in new[] { ZynCoverageStatus.Unknown, ZynCoverageStatus.Counted, ZynCoverageStatus.Handled })
        {
            builder.AppendLine($"## {status}");
            builder.AppendLine();
            foreach (var bucket in report.Buckets.Where(bucket => bucket.Status == status))
            {
                var examples = bucket.Examples.Count == 0 ? "" : $" Examples: {string.Join("; ", bucket.Examples)}.";
                builder.AppendLine($"- `{bucket.Area}:{bucket.Key}` count={bucket.Count}. {bucket.Notes}{examples}");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AddEngineBuckets(
        Dictionary<(string Area, string Key), CoverageAccumulator> buckets,
        IReadOnlyList<ZynEngine> engines,
        string relativePath)
    {
        if (engines.Count == 0)
        {
            AddBucket(buckets, "engine", "none", ZynCoverageStatus.Unknown, relativePath, "Enabled kit item has no active ADD/SUB/PAD engine.");
            return;
        }

        foreach (var engine in engines)
        {
            var (key, status, notes) = engine switch
            {
                ZynEngine.PadSynth => ("pad", ZynCoverageStatus.Handled, "PAD layers have generated spectrum rebuilds and parity pressure."),
                ZynEngine.AddSynth => ("add", ZynCoverageStatus.Counted, "ADD is counted and hand-sketched in rebuilds, but not parity-translated from Zyn."),
                ZynEngine.SubSynth => ("sub", ZynCoverageStatus.Counted, "SUB is counted and hand-sketched in rebuilds, but not parity-translated from Zyn."),
                _ => ("unknown", ZynCoverageStatus.Unknown, "Unknown Zyn engine.")
            };
            AddBucket(buckets, "engine", key, status, relativePath, notes);
        }

        if (engines.Count > 1)
        {
            AddBucket(
                buckets,
                "engine",
                string.Join("+", engines.Select(EngineKey).Order(StringComparer.OrdinalIgnoreCase)),
                ZynCoverageStatus.Counted,
                relativePath,
                "Mixed engines are counted; only PAD members are generated by the current parity translator.");
        }
    }

    private static void AddElementPressureBuckets(
        Dictionary<(string Area, string Key), CoverageAccumulator> buckets,
        XElement item,
        string relativePath)
    {
        foreach (var envelope in item.Descendants().Where(element => element.Name.LocalName.EndsWith("_ENVELOPE", StringComparison.OrdinalIgnoreCase)))
        {
            var free = BoolParam(envelope, "free_mode");
            AddBucket(
                buckets,
                "envelope",
                free ? $"{envelope.Name.LocalName}.free" : $"{envelope.Name.LocalName}.staged",
                free ? ZynCoverageStatus.Counted : ZynCoverageStatus.Handled,
                relativePath,
                free
                    ? "Free-mode envelopes are counted pressure; AquaSynth has staged rate/level contours, not arbitrary Zyn point envelopes."
                    : "Standard ADSR-ish Zyn envelopes can be approximated by AquaSynth staged rate/level or ADSR surfaces.");
        }

        foreach (var lfo in item.Descendants().Where(element => element.Name.LocalName.EndsWith("_LFO", StringComparison.OrdinalIgnoreCase)))
        {
            var intensity = IntParam(lfo, "intensity", 0);
            var status = intensity == 0 ? ZynCoverageStatus.Handled : ZynCoverageStatus.Counted;
            AddBucket(
                buckets,
                "lfo",
                lfo.Name.LocalName,
                status,
                relativePath,
                intensity == 0
                    ? "Inactive or neutral LFO block."
                    : "LFO presence is counted; Zyn LFO routing and waveform semantics are not parity-translated.");
        }

        foreach (var formant in item.Descendants("FORMANT_FILTER"))
        {
            AddBucket(
                buckets,
                "formant",
                "FORMANT_FILTER",
                ZynCoverageStatus.Counted,
                relativePath,
                "Static formant intent can be sketched; Zyn vowel/formant morphing remains parity pressure.");
        }

        foreach (var effect in item.Descendants().Where(element => element.Name.LocalName.Contains("EFFECT", StringComparison.OrdinalIgnoreCase)))
        {
            AddBucket(
                buckets,
                "effect",
                effect.Name.LocalName,
                ZynCoverageStatus.Counted,
                relativePath,
                "Effects are inventory only; AquaSynth does not translate Zyn effect chains.");
        }
    }

    private static void AddPadPressureBuckets(
        Dictionary<(string Area, string Key), CoverageAccumulator> buckets,
        XElement item,
        string relativePath)
    {
        var pad = item.Element("PAD_SYNTH_PARAMETERS");
        if (pad is null)
        {
            AddBucket(buckets, "pad", "missing_parameters", ZynCoverageStatus.Unknown, relativePath, "PAD engine is enabled but PAD_SYNTH_PARAMETERS is absent.");
            return;
        }

        AddBucket(buckets, "pad", $"mode={IntParam(pad, "mode", 0)}", ZynCoverageStatus.Handled, relativePath, "PAD mode is represented by AquaSynth pad_mode table-generation syntax.");
        AddBucket(buckets, "pad", $"bandwidth_scale={IntParam(pad, "bandwidth_scale", 0)}", ZynCoverageStatus.Handled, relativePath, "PAD bandwidth scale is represented by AquaSynth pad_bwscale syntax.");

        var oscillator = pad.Element("OSCIL");
        AddBucket(buckets, "pad.oscil.base_function", IntParam(oscillator, "base_function", 0).ToString(CultureInfo.InvariantCulture), ZynCoverageStatus.Handled, relativePath, "Base functions are approximated during PAD harmonic extraction.");
        AddBucket(buckets, "pad.oscil.base_modulation", IntParam(oscillator, "base_function_modulation", 0).ToString(CultureInfo.InvariantCulture), ZynCoverageStatus.Handled, relativePath, "Base-function modulation is approximated before PAD table spreading.");
        AddBucket(buckets, "pad.oscil.modulation", IntParam(oscillator, "modulation", 0).ToString(CultureInfo.InvariantCulture), ZynCoverageStatus.Handled, relativePath, "OscilGen modulation is approximated during source-table extraction.");
        AddBucket(buckets, "pad.oscil.adaptive", IntParam(oscillator, "adaptive_harmonics", 0).ToString(CultureInfo.InvariantCulture), ZynCoverageStatus.Handled, relativePath, "Adaptive harmonics are approximated at the PAD table root.");
        AddBucket(buckets, "pad.oscil.spectrum_adjust", IntParam(oscillator, "spectrum_adjust_type", 0).ToString(CultureInfo.InvariantCulture), ZynCoverageStatus.Handled, relativePath, "Spectrum adjustment is approximated after source-table extraction.");

        var oscilFilter = IntParam(oscillator, "filter_type", 0).ToString(CultureInfo.InvariantCulture);
        AddBucket(
            buckets,
            "pad.oscil.filter_type",
            oscilFilter,
            HandledPadOscilFilters.Contains(oscilFilter) ? ZynCoverageStatus.Handled : ZynCoverageStatus.Unknown,
            relativePath,
            HandledPadOscilFilters.Contains(oscilFilter)
                ? "This OscilGen filter type has an AquaSynth approximation."
                : "Observed OscilGen filter type is not implemented in AquaSynth's Zyn PAD source extractor.");

        var waveShape = IntParam(oscillator, "wave_shaping_function", 0).ToString(CultureInfo.InvariantCulture);
        AddBucket(
            buckets,
            "pad.oscil.wave_shape",
            waveShape,
            HandledPadWaveShapes.Contains(waveShape) ? ZynCoverageStatus.Handled : ZynCoverageStatus.Unknown,
            relativePath,
            HandledPadWaveShapes.Contains(waveShape)
                ? "This waveshaper has an AquaSynth approximation."
                : "Observed OscilGen waveshaper is not implemented in AquaSynth's Zyn PAD source extractor.");

        var filter = pad.Element("FILTER_PARAMETERS");
        if (filter is not null)
        {
            var filterKey = $"cat={DescendantIntParam(filter, "category", 0)} type={DescendantIntParam(filter, "type", 0)}";
            AddBucket(
                buckets,
                "pad.filter",
                filterKey,
                HandledPadGlobalFilters.Contains(filterKey) ? ZynCoverageStatus.Handled : ZynCoverageStatus.Unknown,
                relativePath,
                HandledPadGlobalFilters.Contains(filterKey)
                    ? "PAD filter category/type maps to AquaSynth native filter fields."
                    : "Observed PAD filter category/type is not translated by the current PAD rebuild path.");

            foreach (var filterLfo in filter.Elements("FILTER_LFO"))
            {
                AddBucket(
                    buckets,
                    "pad.filter_lfo",
                    $"intensity={IntParam(filterLfo, "intensity", 0)}",
                    IntParam(filterLfo, "intensity", 0) == 0 ? ZynCoverageStatus.Handled : ZynCoverageStatus.Counted,
                    relativePath,
                    IntParam(filterLfo, "intensity", 0) == 0
                        ? "Neutral PAD filter LFO."
                        : "PAD filter LFO is reported by rebuilds as missing lowering pressure.");
            }
        }
    }

    private static IEnumerable<ZynEngine> EnabledEngines(XElement item)
    {
        if (EngineEnabled(item, "add_enabled", "ADD_SYNTH_PARAMETERS")) yield return ZynEngine.AddSynth;
        if (EngineEnabled(item, "sub_enabled", "SUB_SYNTH_PARAMETERS")) yield return ZynEngine.SubSynth;
        if (EngineEnabled(item, "pad_enabled", "PAD_SYNTH_PARAMETERS")) yield return ZynEngine.PadSynth;
    }

    private static bool EngineEnabled(XElement item, string flagName, string sectionName) =>
        BoolParamValue(item, flagName) ?? item.Element(sectionName) is not null;

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

    private static int IntParam(XElement? root, string name, int fallback)
    {
        var value = root?.Elements("par")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static int DescendantIntParam(XElement? root, string name, int fallback)
    {
        var value = root?.Descendants("par")
            .FirstOrDefault(element => AttributeValue(element, "name") == name)
            ?.Attribute("value")
            ?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static string? AttributeValue(XElement element, string name) =>
        element.Attribute(name)?.Value;

    private static string EngineKey(ZynEngine engine) => engine switch
    {
        ZynEngine.AddSynth => "add",
        ZynEngine.SubSynth => "sub",
        ZynEngine.PadSynth => "pad",
        _ => "unknown"
    };

    private static void AddBucket(
        Dictionary<(string Area, string Key), CoverageAccumulator> buckets,
        string area,
        string key,
        ZynCoverageStatus status,
        string example,
        string notes)
    {
        var bucketKey = (area, key);
        if (!buckets.TryGetValue(bucketKey, out var bucket))
        {
            bucket = new CoverageAccumulator(area, key, status, notes);
            buckets.Add(bucketKey, bucket);
        }

        bucket.Count++;
        if (status > bucket.Status)
        {
            bucket.Status = status;
            bucket.Notes = notes;
        }
        if (bucket.Examples.Count < 10 && !bucket.Examples.Contains(example, StringComparer.OrdinalIgnoreCase))
        {
            bucket.Examples.Add(example);
        }
    }

    private sealed class CoverageAccumulator(string area, string key, ZynCoverageStatus status, string notes)
    {
        public string Area { get; } = area;
        public string Key { get; } = key;
        public ZynCoverageStatus Status { get; set; } = status;
        public int Count { get; set; }
        public List<string> Examples { get; } = [];
        public string Notes { get; set; } = notes;

        public ZynCoverageBucket ToBucket(int exampleCount) =>
            new(Area, Key, Status, Count, Examples.Take(exampleCount).ToList(), Notes);
    }
}

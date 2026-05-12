using System.Globalization;

namespace AquariumSynth.Dsl;

public sealed class PatchScriptException(int line, string message) : Exception($"line {line}: {message}")
{
    public int Line { get; } = line;
}

public static class PatchScript
{
    public static SynthPatch Parse(string script)
    {
        var compiler = new Compiler();
        var line = 1;
        foreach (var statement in PatchScriptStatements.Enumerate(script))
        {
            compiler.Apply(statement.Text, statement.Line);
            line = statement.Line;
        }

        if (compiler.Voices.Count == 0)
        {
            throw new PatchScriptException(line, "patch script produced no voices");
        }

        return compiler.Build();
    }

    private sealed class Compiler
    {
        private readonly Dictionary<string, Dictionary<string, string>> _templates = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ControlLane> _controls = [];
        private readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase);

        public List<Voice> Voices { get; } = [];
        private Repeat? Repeat { get; set; }
        private float Gain { get; set; } = 1;
        private bool SoftClip { get; set; } = true;

        public SynthPatch Build() => new()
        {
            Voices = Voices,
            Controls = _controls,
            Repeat = Repeat,
            Gain = Gain,
            SoftClip = SoftClip
        };

        public void Apply(string statement, int line)
        {
            var parts = statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var rawCommand = parts[0];
            var command = CanonicalCommand(parts[0]);
            var fields = ParseFields(parts.Skip(1), line);
            if (SfxrParams.Named(rawCommand) is { } namedParams)
            {
                AddSfxrPatch(ApplySfxrFields(namedParams, fields, line));
                return;
            }

            switch (command)
            {
                case "patch":
                    ApplyPatch(fields, line);
                    break;
                case "defaults":
                    Merge(_defaults, fields);
                    break;
                case "template":
                    var name = Required(fields, "name", line);
                    _templates[name] = Without(fields, "name");
                    break;
                case "voice":
                    Voices.Add(ParseVoice(ExpandVoiceFields(fields, line), line));
                    break;
                case "mod":
                    AddModBus(fields, line);
                    break;
                case "control":
                    AddControlLane(fields, line);
                    break;
                case "sfxr":
                    AddSfxrPatch(ParseSfxrCommand(fields, line));
                    break;
                default:
                    throw new PatchScriptException(line, $"unknown command `{parts[0]}`");
            }
        }

        private Dictionary<string, string> ExpandVoiceFields(Dictionary<string, string> fields, int line)
        {
            var expanded = new Dictionary<string, string>(_defaults, StringComparer.OrdinalIgnoreCase);
            if (TryGetAny(fields, ["use", "u"], out var templateName))
            {
                foreach (var name in templateName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!_templates.TryGetValue(name, out var template))
                    {
                        throw new PatchScriptException(line, $"unknown template `{name}`");
                    }
                    Merge(expanded, template);
                }
            }
            Merge(expanded, Without(fields, "use", "u"));
            return expanded;
        }

        private void ApplyPatch(IReadOnlyDictionary<string, string> fields, int line)
        {
            if (TryGetAny(fields, ["gain", "g"], out var gain)) Gain = ParseFloat(gain, line);
            if (TryGetAny(fields, ["soft_clip", "clip"], out var softClip)) SoftClip = ParseBool(softClip, line);
            if (TryGetAny(fields, ["repeat", "r", "rp"], out var repeat))
            {
                var interval = ParseFloat(repeat, line);
                Repeat = interval > 0 ? new Repeat(interval) : null;
            }
        }

        private Voice ParseVoice(IReadOnlyDictionary<string, string> fields, int line)
        {
            var waveform = TryGetAny(fields, ["wave", "w"], out var wave) ? ParseWaveform(wave, line) : Waveform.Sine;
            var formants = TryGetAny(fields, ["formants", "fs"], out var formantSpec)
                ? ParseFormants(formantSpec, line)
                : [];

            var modulators = TryGetAny(fields, ["mods", "m"], out var mods)
                ? ParseVoiceModulators(mods, line)
                : [];

            Arpeggio? arpeggio = null;
            var hasArpDelay = TryGetAny(fields, ["arp_delay", "ad"], out var arpDelay);
            var hasArpMult = TryGetAny(fields, ["arp_mult", "am"], out var arpMult);
            if (hasArpDelay || hasArpMult)
            {
                arpeggio = new Arpeggio(
                    ParseFloat(arpDelay ?? throw new PatchScriptException(line, "arpeggio needs arp_delay"), line),
                    ParseFloat(arpMult ?? throw new PatchScriptException(line, "arpeggio needs arp_mult"), line));
            }

            return new Voice
            {
                Oscillator = new Oscillator(
                    waveform,
                    GetFloat(fields, line, 440, "freq", "frequency", "f"),
                    GetFloat(fields, line, 0.5f, "duty", "du"),
                    GetFloat(fields, line, 0, "phase", "pa")),
                Envelope = new Envelope(
                    GetFloat(fields, line, 0, "attack", "a"),
                    GetFloat(fields, line, 0.1f, "sustain", "s"),
                    GetFloat(fields, line, 0.1f, "decay", "d"),
                    GetFloat(fields, line, 0, "punch", "pu")),
                Pitch = new PitchMotion(
                    GetFloat(fields, line, 20, "min_freq", "min"),
                    GetFloat(fields, line, 0, "pitch_ramp", "pr"),
                    GetFloat(fields, line, 0, "pitch_delta", "pd", "pitch_dramp", "pdr"),
                    GetFloat(fields, line, 0, "vibrato", "vi"),
                    GetFloat(fields, line, 0, "vibrato_hz", "vh"),
                    GetFloat(fields, line, 0, "vibrato_delay", "vd")),
                Duty = new DutyMotion(GetFloat(fields, line, 0, "duty_ramp", "dur")),
                Filter = new Filter(
                    GetFloat(fields, line, 1, "lpf", "l"),
                    GetFloat(fields, line, 0, "lpf_ramp", "lr"),
                    GetFloat(fields, line, 0, "resonance", "res"),
                    GetFloat(fields, line, 0, "hpf", "h"),
                    GetFloat(fields, line, 0, "hpf_ramp", "hr")),
                Phaser = new Phaser(
                    GetFloat(fields, line, 0, "phaser", "ph"),
                    GetFloat(fields, line, 0, "phaser_ramp", "phr")),
                Arpeggio = arpeggio,
                Fm = new FrequencyModulation(
                    GetFloat(fields, line, 1, "fm", "fmr", "fm_ratio"),
                    GetFloat(fields, line, 0, "fm_index", "fmi"),
                    GetFloat(fields, line, 0, "fm_decay", "fmd")),
                Color = new VoiceColor(
                    GetFloat(fields, line, 0, "noise", "nz"),
                    GetFloat(fields, line, 0, "drive", "drv"),
                    GetFloat(fields, line, 0, "fold", "fl"),
                    GetFloat(fields, line, 0, "tremolo", "tr"),
                    GetFloat(fields, line, 0, "tremolo_hz", "th"),
                    GetFloat(fields, line, 0, "formant_mix", "fmix")),
                Formants = formants,
                Modulators = modulators,
                Gain = GetFloat(fields, line, 0.2f, "gain", "g")
            };
        }

        private void AddModBus(IReadOnlyDictionary<string, string> fields, int line)
        {
            var name = GetAny(fields, ["name", "n"], "mod");
            var wave = TryGetAny(fields, ["wave", "w"], out var waveform)
                ? ParseModWaveform(waveform, line)
                : ModWaveform.Sine;
            var hz = GetFloat(fields, line, 1, "hz", "rate");
            var phase = GetFloat(fields, line, 0, "phase");

            if (TryGetAny(fields, ["to", "targets"], out var routeSpec))
            {
                foreach (var route in ParseRoutes(routeSpec, line))
                {
                    _controls.Add(new ControlLane(
                        $"{name}_{TargetSuffix(route.Target)}",
                        new Modulator(route.Target, wave, hz, route.Depth, phase)));
                }
            }

            foreach (var (key, target) in ModTargets)
            {
                if (!TryGetAny(fields, key, out var depthText)) continue;
                var depth = ParseFloat(depthText, line);
                _controls.Add(new ControlLane($"{name}_{key[0]}", new Modulator(target, wave, hz, depth, phase)));
            }
        }

        private void AddControlLane(IReadOnlyDictionary<string, string> fields, int line)
        {
            var name = GetAny(fields, ["name", "n"], "control");
            if (!TryGetAny(fields, ["target", "t"], out var targetText))
            {
                throw new PatchScriptException(line, "control lane needs target");
            }

            var wave = TryGetAny(fields, ["wave", "w"], out var waveform)
                ? ParseModWaveform(waveform, line)
                : ModWaveform.Sine;

            _controls.Add(new ControlLane(
                name,
                new Modulator(
                    ParseModTarget(targetText, line),
                    wave,
                    GetFloat(fields, line, 1, "hz", "rate"),
                    GetFloat(fields, line, 0, "depth", "d", "decay"),
                    GetFloat(fields, line, 0, "phase", "ph"),
                    GetFloat(fields, line, 0, "bias", "b"))));
        }

        private void AddSfxrPatch(SfxrParams parameters)
        {
            var mapped = parameters.ToPatch();
            Voices.AddRange(mapped.Voices);
            Repeat = mapped.Repeat;
            Gain *= mapped.Gain;
        }

        private static SfxrParams ParseSfxrCommand(IReadOnlyDictionary<string, string> fields, int line)
        {
            var parameters = TryGetAny(fields, ["preset", "p"], out var preset)
                ? SfxrParams.Named(preset) ?? throw new PatchScriptException(line, $"unknown sfxr preset `{preset}`")
                : new SfxrParams();
            return ApplySfxrFields(parameters, fields, line);
        }

        private static SfxrParams ApplySfxrFields(SfxrParams parameters, IReadOnlyDictionary<string, string> fields, int line)
        {
            if (TryGetAny(fields, ["mutate_seed", "ms"], out var seedText))
            {
                if (!ulong.TryParse(seedText, out var seed))
                {
                    throw new PatchScriptException(line, "mutate_seed must be an integer");
                }

                var amount = GetFloat(fields, line, 0.05f, "mutate", "m");
                parameters = parameters.Mutate(seed, amount);
            }

            if (TryGetAny(fields, ["wave", "w"], out var wave)) parameters = parameters with { WaveType = ParseWaveform(wave, line) };
            if (TryGetAny(fields, ["base", "b"], out var baseFreq)) parameters = parameters with { BaseFreq = Math.Clamp(ParseFloat(baseFreq, line), 0, 1) };
            if (TryGetAny(fields, ["limit", "lim"], out var limit)) parameters = parameters with { FreqLimit = Math.Clamp(ParseFloat(limit, line), 0, 1) };
            if (TryGetAny(fields, ["ramp", "r"], out var ramp)) parameters = parameters with { FreqRamp = Math.Clamp(ParseFloat(ramp, line), -1, 1) };
            if (TryGetAny(fields, ["dramp", "dr"], out var dramp)) parameters = parameters with { FreqDramp = Math.Clamp(ParseFloat(dramp, line), -1, 1) };
            if (TryGetAny(fields, ["duty", "du"], out var duty)) parameters = parameters with { Duty = Math.Clamp(ParseFloat(duty, line), 0, 1) };
            if (TryGetAny(fields, ["duty_ramp", "dur"], out var dutyRamp)) parameters = parameters with { DutyRamp = Math.Clamp(ParseFloat(dutyRamp, line), -1, 1) };
            if (TryGetAny(fields, ["vib", "vi"], out var vib)) parameters = parameters with { VibStrength = Math.Clamp(ParseFloat(vib, line), 0, 1) };
            if (TryGetAny(fields, ["vib_speed", "vs"], out var vibSpeed)) parameters = parameters with { VibSpeed = Math.Clamp(ParseFloat(vibSpeed, line), 0, 1) };
            if (TryGetAny(fields, ["vib_delay", "vd"], out var vibDelay)) parameters = parameters with { VibDelay = Math.Clamp(ParseFloat(vibDelay, line), 0, 1) };
            if (TryGetAny(fields, ["attack", "a"], out var attack)) parameters = parameters with { EnvAttack = Math.Clamp(ParseFloat(attack, line), 0, 1) };
            if (TryGetAny(fields, ["sustain", "s"], out var sustain)) parameters = parameters with { EnvSustain = Math.Clamp(ParseFloat(sustain, line), 0, 1) };
            if (TryGetAny(fields, ["decay", "d"], out var decay)) parameters = parameters with { EnvDecay = Math.Clamp(ParseFloat(decay, line), 0, 1) };
            if (TryGetAny(fields, ["punch", "pu"], out var punch)) parameters = parameters with { EnvPunch = Math.Clamp(ParseFloat(punch, line), -1, 1) };
            if (TryGetAny(fields, ["resonance", "res"], out var resonance)) parameters = parameters with { LpfResonance = Math.Clamp(ParseFloat(resonance, line), 0, 1) };
            if (TryGetAny(fields, ["lpf"], out var lpf)) parameters = parameters with { LpfFreq = Math.Clamp(ParseFloat(lpf, line), 0, 1) };
            if (TryGetAny(fields, ["lpf_ramp", "lpfr"], out var lpfRamp)) parameters = parameters with { LpfRamp = Math.Clamp(ParseFloat(lpfRamp, line), -1, 1) };
            if (TryGetAny(fields, ["hpf"], out var hpf)) parameters = parameters with { HpfFreq = Math.Clamp(ParseFloat(hpf, line), 0, 1) };
            if (TryGetAny(fields, ["hpf_ramp", "hpfr"], out var hpfRamp)) parameters = parameters with { HpfRamp = Math.Clamp(ParseFloat(hpfRamp, line), -1, 1) };
            if (TryGetAny(fields, ["phaser", "ph"], out var phaser)) parameters = parameters with { PhaOffset = Math.Clamp(ParseFloat(phaser, line), -1, 1) };
            if (TryGetAny(fields, ["phaser_ramp", "phr"], out var phaserRamp)) parameters = parameters with { PhaRamp = Math.Clamp(ParseFloat(phaserRamp, line), -1, 1) };
            if (TryGetAny(fields, ["repeat", "rep"], out var repeat)) parameters = parameters with { RepeatSpeed = Math.Clamp(ParseFloat(repeat, line), 0, 1) };
            if (TryGetAny(fields, ["arp"], out var arp)) parameters = parameters with { ArpSpeed = Math.Clamp(ParseFloat(arp, line), 0, 1) };
            if (TryGetAny(fields, ["arp_mod", "am"], out var arpMod)) parameters = parameters with { ArpMod = Math.Clamp(ParseFloat(arpMod, line), -1, 1) };
            return parameters;
        }
    }

    private static readonly (string[] Keys, ModTarget Target)[] ModTargets =
    [
        (["gain", "g"], ModTarget.Gain),
        (["pitch", "p"], ModTarget.Pitch),
        (["duty", "du"], ModTarget.Duty),
        (["lpf", "l"], ModTarget.LowPass),
        (["hpf", "h"], ModTarget.HighPass),
        (["noise", "nz"], ModTarget.Noise),
        (["drive", "drv"], ModTarget.Drive),
        (["fold", "fl"], ModTarget.Fold),
        (["formant_mix", "fmix", "formant"], ModTarget.FormantMix),
        (["fm_index", "fmi"], ModTarget.FmIndex)
    ];

    private static Dictionary<string, string> ParseFields(IEnumerable<string> tokens, int line)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var split = token.Split('=', 2);
            if (split.Length != 2 || split[0].Length == 0)
            {
                throw new PatchScriptException(line, $"bad field `{token}`");
            }
            fields[CanonicalField(split[0])] = split[1];
        }
        return fields;
    }

    private static List<Formant> ParseFormants(string value, int line) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var pieces = part.Split(':');
                if (pieces.Length != 3) throw new PatchScriptException(line, $"bad formant `{part}`");
                return new Formant(ParseFloat(pieces[0], line), ParseFloat(pieces[1], line), ParseFloat(pieces[2], line));
            })
            .ToList();

    private static List<Modulator> ParseVoiceModulators(string value, int line) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var pieces = part.Split(':');
                if (pieces.Length != 4) throw new PatchScriptException(line, $"bad modulator `{part}`");
                return new Modulator(
                    ParseModTarget(pieces[0], line),
                    ParseModWaveform(pieces[1], line),
                    ParseFloat(pieces[2], line),
                    ParseFloat(pieces[3], line));
            })
            .ToList();

    private static IEnumerable<(ModTarget Target, float Depth)> ParseRoutes(string value, int line) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var pieces = part.Split(':');
                if (pieces.Length != 2) throw new PatchScriptException(line, $"bad route `{part}`");
                return (ParseModTarget(pieces[0], line), ParseFloat(pieces[1], line));
            });

    private static string CanonicalCommand(string command) => command.ToLowerInvariant() switch
    {
        "p" or "patch" => "patch",
        "d" or "default" or "defaults" => "defaults",
        "def" or "t" or "template" => "template",
        "v" or "voice" => "voice",
        "mod" or "wob" or "wobble" or "bus" => "mod",
        "lfo" or "control" => "control",
        "s" or "sfxr" => "sfxr",
        _ => command
    };

    private static string CanonicalField(string field) => field.ToLowerInvariant() switch
    {
        "n" => "name",
        "u" => "use",
        "w" => "wave",
        "f" => "freq",
        "g" => "gain",
        "a" => "attack",
        "s" => "sustain",
        "d" => "decay",
        _ => field
    };

    private static Waveform ParseWaveform(string value, int line) => value.ToLowerInvariant() switch
    {
        "sin" or "sine" => Waveform.Sine,
        "sq" or "square" => Waveform.Square,
        "saw" or "sawtooth" => Waveform.Sawtooth,
        "tri" or "triangle" => Waveform.Triangle,
        "n" or "noise" => Waveform.Noise,
        _ => throw new PatchScriptException(line, $"unknown waveform `{value}`")
    };

    private static ModWaveform ParseModWaveform(string value, int line) => value.ToLowerInvariant() switch
    {
        "sin" or "sine" => ModWaveform.Sine,
        "tri" or "triangle" => ModWaveform.Triangle,
        "sq" or "square" => ModWaveform.Square,
        "hold" or "sample_hold" => ModWaveform.SampleHold,
        _ => throw new PatchScriptException(line, $"unknown mod waveform `{value}`")
    };

    private static ModTarget ParseModTarget(string value, int line)
    {
        foreach (var (keys, target) in ModTargets)
        {
            if (keys.Contains(value, StringComparer.OrdinalIgnoreCase) ||
                keys.Select(CanonicalField).Contains(CanonicalField(value), StringComparer.OrdinalIgnoreCase))
            {
                return target;
            }
        }
        if (value.Equals("formant", StringComparison.OrdinalIgnoreCase)) return ModTarget.FormantMix;
        throw new PatchScriptException(line, $"unknown mod target `{value}`");
    }

    private static string TargetSuffix(ModTarget target) => target switch
    {
        ModTarget.Gain => "gain",
        ModTarget.Pitch => "pitch",
        ModTarget.Duty => "duty",
        ModTarget.LowPass => "lpf",
        ModTarget.HighPass => "hpf",
        ModTarget.Noise => "noise",
        ModTarget.Drive => "drive",
        ModTarget.Fold => "fold",
        ModTarget.FormantMix => "formant_mix",
        ModTarget.FmIndex => "fm_index",
        _ => target.ToString().ToLowerInvariant()
    };

    private static string Required(IReadOnlyDictionary<string, string> fields, string key, int line) =>
        fields.TryGetValue(key, out var value) ? value : throw new PatchScriptException(line, $"missing `{key}`");

    private static string GetAny(IReadOnlyDictionary<string, string> fields, string[] keys, string fallback) =>
        TryGetAny(fields, keys, out var value) ? value : fallback;

    private static bool TryGetAny(IReadOnlyDictionary<string, string> fields, string[] keys, out string value)
    {
        foreach (var key in keys.Select(CanonicalField))
        {
            if (fields.TryGetValue(key, out value!)) return true;
        }
        value = "";
        return false;
    }

    private static float GetFloat(IReadOnlyDictionary<string, string> fields, int line, float fallback, params string[] keys) =>
        TryGetAny(fields, keys, out var value) ? ParseFloat(value, line) : fallback;

    private static float ParseFloat(string value, int line) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new PatchScriptException(line, $"bad number `{value}`");

    private static bool ParseBool(string value, int line) => value.ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "on" => true,
        "false" or "0" or "no" or "off" => false,
        _ => throw new PatchScriptException(line, $"bad bool `{value}`")
    };

    private static void Merge(IDictionary<string, string> target, IReadOnlyDictionary<string, string> source)
    {
        foreach (var (key, value) in source) target[key] = value;
    }

    private static Dictionary<string, string> Without(IReadOnlyDictionary<string, string> fields, params string[] keys)
    {
        var excluded = keys.Select(CanonicalField).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return fields.Where(pair => !excluded.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }
}

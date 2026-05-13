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

        if (!compiler.HasOutput)
        {
            throw new PatchScriptException(line, "patch script produced no voices or operator graphs");
        }

        return compiler.Build();
    }

    private sealed class Compiler
    {
        private readonly Dictionary<string, Dictionary<string, string>> _templates = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ControlLane> _controls = [];
        private readonly List<OperatorGraph> _operatorGraphs = [];
        private readonly List<PatchParameter> _parameters = [];
        private readonly List<ParameterBinding> _parameterBindings = [];
        private readonly Dictionary<string, string> _defaults = new(StringComparer.OrdinalIgnoreCase);
        private PendingOperatorGraph? _pendingOperatorGraph;

        public List<Voice> Voices { get; } = [];
        public List<OperatorGraph> OperatorGraphs => _operatorGraphs;
        public bool HasOutput => Voices.Count > 0 || _operatorGraphs.Count > 0 || _pendingOperatorGraph is not null;
        private Repeat? Repeat { get; set; }
        private float Gain { get; set; } = 1;
        private bool SoftClip { get; set; } = true;

        public SynthPatch Build()
        {
            FlushPendingOperatorGraph();
            return new SynthPatch
            {
                Voices = Voices,
                OperatorGraphs = _operatorGraphs,
                Controls = _controls,
                Parameters = _parameters,
                ParameterBindings = _parameterBindings,
                Repeat = Repeat,
                Gain = Gain,
                SoftClip = SoftClip
            };
        }

        public void Apply(string statement, int line)
        {
            var parts = statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var rawCommand = parts[0];
            var command = CanonicalCommand(parts[0]);
            var fields = ParseFields(parts.Skip(1), line);
            if (SfxrParams.Named(rawCommand) is { } namedParams)
            {
                FlushPendingOperatorGraph();
                AddSfxrPatch(ApplySfxrFields(namedParams, fields, line));
                return;
            }

            switch (command)
            {
                case "patch":
                    FlushPendingOperatorGraph();
                    ApplyPatch(fields, line);
                    break;
                case "defaults":
                    FlushPendingOperatorGraph();
                    Merge(_defaults, fields);
                    break;
                case "template":
                    FlushPendingOperatorGraph();
                    var name = Required(fields, "name", line);
                    _templates[name] = Without(fields, "name");
                    break;
                case "voice":
                    FlushPendingOperatorGraph();
                    Voices.Add(ParseVoice(ExpandVoiceFields(fields, line), Voices.Count, line));
                    break;
                case "opgraph":
                    StartOperatorGraph(fields, line);
                    break;
                case "operator":
                    AddOperator(fields, line);
                    break;
                case "route":
                    AddOperatorRoute(fields, line);
                    break;
                case "carrier":
                    AddOperatorCarrier(fields, line);
                    break;
                case "mod":
                    FlushPendingOperatorGraph();
                    AddModBus(fields, line);
                    break;
                case "control":
                    FlushPendingOperatorGraph();
                    AddControlLane(fields, line);
                    break;
                case "param":
                    FlushPendingOperatorGraph();
                    AddParameter(fields, line);
                    break;
                case "sfxr":
                    FlushPendingOperatorGraph();
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
            if (TryGetAny(fields, ["gain", "g"], out var gain)) Gain = ParseBoundFloat(gain, line, Gain, "/patch/gain");
            if (TryGetAny(fields, ["soft_clip", "clip"], out var softClip)) SoftClip = ParseBool(softClip, line);
            if (TryGetAny(fields, ["repeat", "r", "rp"], out var repeat))
            {
                var interval = ParseBoundFloat(repeat, line, 0.1f, "/patch/repeat");
                Repeat = interval > 0 ? new Repeat(interval) : null;
            }
        }

        private Voice ParseVoice(IReadOnlyDictionary<string, string> fields, int voiceIndex, int line)
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
                    ParseBoundFloat(arpDelay ?? throw new PatchScriptException(line, "arpeggio needs arp_delay"), line, 0, VoiceField(voiceIndex, "arpeggio/delay")),
                    ParseBoundFloat(arpMult ?? throw new PatchScriptException(line, "arpeggio needs arp_mult"), line, 1, VoiceField(voiceIndex, "arpeggio/multiplier")));
            }

            return new Voice
            {
                Oscillator = new Oscillator(
                    waveform,
                    GetBoundFloat(fields, line, 440, VoiceField(voiceIndex, "osc/freq"), "freq", "frequency", "f"),
                    GetBoundFloat(fields, line, 0.5f, VoiceField(voiceIndex, "osc/duty"), "duty", "du"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "osc/phase"), "phase", "pa")),
                Envelope = new Envelope(
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "env/attack"), "attack", "a"),
                    GetBoundFloat(fields, line, 0.1f, VoiceField(voiceIndex, "env/sustain"), "sustain", "s"),
                    GetBoundFloat(fields, line, 0.1f, VoiceField(voiceIndex, "env/decay"), "decay", "d"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "env/punch"), "punch", "pu")),
                Pitch = new PitchMotion(
                    GetBoundFloat(fields, line, 20, VoiceField(voiceIndex, "pitch/min_freq"), "min_freq", "min"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "pitch/ramp"), "pitch_ramp", "pr"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "pitch/delta"), "pitch_delta", "pd", "pitch_dramp", "pdr"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "pitch/vibrato"), "vibrato", "vi"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "pitch/vibrato_hz"), "vibrato_hz", "vh"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "pitch/vibrato_delay"), "vibrato_delay", "vd")),
                Duty = new DutyMotion(GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "duty/ramp"), "duty_ramp", "dur")),
                Filter = new Filter(
                    GetBoundFloat(fields, line, 1, VoiceField(voiceIndex, "filter/lpf"), "lpf", "l"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "filter/lpf_ramp"), "lpf_ramp", "lr"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "filter/resonance"), "resonance", "res"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "filter/hpf"), "hpf", "h"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "filter/hpf_ramp"), "hpf_ramp", "hr")),
                Phaser = new Phaser(
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "phaser/offset"), "phaser", "ph"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "phaser/ramp"), "phaser_ramp", "phr")),
                Arpeggio = arpeggio,
                Fm = new FrequencyModulation(
                    GetBoundFloat(fields, line, 1, VoiceField(voiceIndex, "fm/ratio"), "fm", "fmr", "fm_ratio"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "fm/index"), "fm_index", "fmi"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "fm/decay"), "fm_decay", "fmd")),
                Color = new VoiceColor(
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/noise"), "noise", "nz"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/drive"), "drive", "drv"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/fold"), "fold", "fl"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/tremolo"), "tremolo", "tr"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/tremolo_hz"), "tremolo_hz", "th"),
                    GetBoundFloat(fields, line, 0, VoiceField(voiceIndex, "color/formant_mix"), "formant_mix", "fmix")),
                Formants = formants,
                Modulators = modulators,
                Gain = GetBoundFloat(fields, line, 0.2f, VoiceField(voiceIndex, "gain"), "gain", "g")
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

        private void AddOperatorGraph(IReadOnlyDictionary<string, string> fields, int line)
        {
            var graphIndex = _operatorGraphs.Count;
            var graphPath = $"/opgraphs/{graphIndex}";
            var operators = ParseOperatorNodes(Required(fields, "ops", line), line);
            var edges = TryGetAny(fields, ["edges", "e"], out var edgeSpec)
                ? ParseOperatorEdges(edgeSpec, line)
                : [];
            var carriers = TryGetAny(fields, ["carriers", "c"], out var carrierSpec)
                ? ParseOperatorIds(carrierSpec, line)
                : operators.Select(op => op.Id).ToList();

            AddValidatedOperatorGraph(line, new OperatorGraph(
                GetAny(fields, ["name", "n"], $"opgraph{graphIndex}"),
                GetBoundFloat(fields, line, 440, $"{graphPath}/freq", "freq", "frequency", "f"),
                operators,
                edges,
                carriers,
                GetBoundFloat(fields, line, 0.2f, $"{graphPath}/gain", "gain", "g")));
        }

        private void StartOperatorGraph(IReadOnlyDictionary<string, string> fields, int line)
        {
            FlushPendingOperatorGraph();
            if (fields.ContainsKey("ops"))
            {
                AddOperatorGraph(fields, line);
                return;
            }

            var graphIndex = _operatorGraphs.Count;
            var graphPath = $"/opgraphs/{graphIndex}";
            _pendingOperatorGraph = new PendingOperatorGraph(
                line,
                GetAny(fields, ["name", "n"], $"opgraph{graphIndex}"),
                GetBoundFloat(fields, line, 440, $"{graphPath}/freq", "freq", "frequency", "f"),
                GetBoundFloat(fields, line, 0.2f, $"{graphPath}/gain", "gain", "g"));
        }

        private void AddOperator(IReadOnlyDictionary<string, string> fields, int line)
        {
            var graph = RequiredPendingOperatorGraph(line);
            var id = ParseOperatorId(Required(fields, "name", line), line);
            var envelope = TryGetAny(fields, ["env", "envelope"], out var envSpec)
                ? ParseEnvelopeSpec(envSpec, line)
                : new Envelope(
                    GetFloat(fields, line, 0, "attack", "a"),
                    GetFloat(fields, line, 0.1f, "sustain", "s"),
                    GetFloat(fields, line, 0.1f, "decay", "d"),
                    GetFloat(fields, line, 0, "punch", "pu"));

            graph.Operators.Add(new OperatorNode(
                id,
                GetFloat(fields, line, 1, "ratio", "r"),
                GetFloat(fields, line, 1, "level", "l"),
                GetFloat(fields, line, 0, "feedback", "fb"),
                0,
                GetFloat(fields, line, 0, "release"),
                envelope));
        }

        private void AddOperatorRoute(IReadOnlyDictionary<string, string> fields, int line)
        {
            var graph = RequiredPendingOperatorGraph(line);
            var source = ParseOperatorId(Required(fields, "from", line), line);
            var target = ParseOperatorId(Required(fields, "to", line), line);
            graph.Edges.Add(new OperatorEdge(source, target, GetFloat(fields, line, 1, "index", "amount", "depth")));
        }

        private void AddOperatorCarrier(IReadOnlyDictionary<string, string> fields, int line)
        {
            var graph = RequiredPendingOperatorGraph(line);
            graph.Carriers.Add(ParseOperatorId(Required(fields, "name", line), line));
        }

        private PendingOperatorGraph RequiredPendingOperatorGraph(int line) =>
            _pendingOperatorGraph ?? throw new PatchScriptException(line, "operator graph command needs an active opgraph");

        private void FlushPendingOperatorGraph()
        {
            if (_pendingOperatorGraph is null) return;
            var graph = _pendingOperatorGraph;
            _pendingOperatorGraph = null;
            AddValidatedOperatorGraph(graph.Line, new OperatorGraph(
                graph.Name,
                graph.FrequencyHz,
                graph.Operators,
                graph.Edges,
                graph.Carriers.Count > 0 ? graph.Carriers : graph.Operators.Select(op => op.Id).ToList(),
                graph.Gain));
        }

        private void AddValidatedOperatorGraph(int line, OperatorGraph graph)
        {
            if (graph.Operators.Count == 0)
            {
                throw new PatchScriptException(line, "operator graph needs at least one operator");
            }

            var operatorIds = graph.Operators.Select(op => op.Id).ToHashSet();
            foreach (var edge in graph.Edges)
            {
                if (!operatorIds.Contains(edge.SourceId) || !operatorIds.Contains(edge.TargetId))
                {
                    throw new PatchScriptException(line, $"operator edge `{edge.SourceId}>{edge.TargetId}` references an unknown operator");
                }
            }
            foreach (var carrier in graph.Carriers)
            {
                if (!operatorIds.Contains(carrier))
                {
                    throw new PatchScriptException(line, $"carrier `{carrier}` references an unknown operator");
                }
            }

            _operatorGraphs.Add(graph);
        }

        private void AddParameter(IReadOnlyDictionary<string, string> fields, int line)
        {
            var path = Required(fields, "path", line);
            if (!path.StartsWith('/'))
            {
                throw new PatchScriptException(line, "parameter path must start with `/`");
            }
            if (_parameters.Any(parameter => parameter.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PatchScriptException(line, $"duplicate parameter path `{path}`");
            }

            var label = TryGetAny(fields, ["label", "name", "n"], out var labelText)
                ? labelText
                : path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
            var min = GetFloat(fields, line, 0, "min");
            var max = GetFloat(fields, line, 1, "max");
            var step = GetFloat(fields, line, 0.001f, "step");
            var fallbackDefault = Math.Clamp(0.5f, Math.Min(min, max), Math.Max(min, max));
            var defaultValue = GetFloat(fields, line, fallbackDefault, "default", "value", "v");
            if (max < min)
            {
                throw new PatchScriptException(line, "parameter max must be greater than or equal to min");
            }
            if (step < 0)
            {
                throw new PatchScriptException(line, "parameter step must be non-negative");
            }
            if (defaultValue < min || defaultValue > max)
            {
                throw new PatchScriptException(line, "parameter default must be inside min/max");
            }

            _parameters.Add(new PatchParameter(
                path,
                label,
                defaultValue,
                min,
                max,
                step,
                GetAny(fields, ["unit"], ""),
                GetAny(fields, ["rate", "automation", "automation_rate"], "control"),
                GetAny(fields, ["notes", "note"], "")));
        }

        private float GetBoundFloat(
            IReadOnlyDictionary<string, string> fields,
            int line,
            float fallback,
            string fieldPath,
            params string[] keys) =>
            TryGetAny(fields, keys, out var value) ? ParseBoundFloat(value, line, fallback, fieldPath) : fallback;

        private float ParseBoundFloat(string value, int line, float fallback, string fieldPath)
        {
            if (!value.StartsWith('@'))
            {
                return ParseFloat(value, line);
            }

            var parameterPath = value[1..];
            if (!parameterPath.StartsWith('/'))
            {
                throw new PatchScriptException(line, "parameter reference must use `@/path`");
            }
            var parameter = _parameters.FirstOrDefault(candidate => candidate.Path.Equals(parameterPath, StringComparison.OrdinalIgnoreCase));
            if (parameter is null)
            {
                throw new PatchScriptException(line, $"unknown parameter `{parameterPath}`");
            }
            if (_parameterBindings.Any(binding => binding.FieldPath.Equals(fieldPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PatchScriptException(line, $"duplicate parameter binding for `{fieldPath}`");
            }

            _parameterBindings.Add(new ParameterBinding(fieldPath, parameter.Path));
            return parameter.Default;
        }

        private static string VoiceField(int voiceIndex, string field) => $"/voices/{voiceIndex}/{field}";

        private static List<OperatorNode> ParseOperatorNodes(string value, int line) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var pieces = part.Split(':');
                    if (pieces.Length is < 3 or > 6)
                    {
                        throw new PatchScriptException(line, $"bad operator `{part}`");
                    }

                    return new OperatorNode(
                        ParseInt(pieces[0], line),
                        ParseFloat(pieces[1], line),
                        ParseFloat(pieces[2], line),
                        pieces.Length >= 4 ? ParseFloat(pieces[3], line) : 0,
                        0,
                        0,
                        pieces.Length >= 6 ? new Envelope(0, ParseFloat(pieces[4], line), ParseFloat(pieces[5], line)) : new Envelope());
                })
                .ToList();

        private static List<OperatorEdge> ParseOperatorEdges(string value, int line) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var pieces = part.Split(':');
                    if (pieces.Length is < 1 or > 2)
                    {
                        throw new PatchScriptException(line, $"bad operator edge `{part}`");
                    }

                    var nodes = pieces[0].Split('>');
                    if (nodes.Length != 2)
                    {
                        throw new PatchScriptException(line, $"bad operator edge `{part}`");
                    }

                    return new OperatorEdge(
                        ParseInt(nodes[0], line),
                        ParseInt(nodes[1], line),
                        pieces.Length == 2 ? ParseFloat(pieces[1], line) : 1);
                })
                .ToList();

        private static List<int> ParseOperatorIds(string value, int line) =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => ParseInt(part, line))
                .ToList();

        private static Envelope ParseEnvelopeSpec(string value, int line)
        {
            var pieces = value.Split(':');
            return pieces[0].ToLowerInvariant() switch
            {
                "ad" when pieces.Length == 3 => new Envelope(
                    ParseFloat(pieces[1], line),
                    0,
                    ParseFloat(pieces[2], line)),
                "adsr" when pieces.Length == 5 => new Envelope(
                    ParseFloat(pieces[1], line),
                    ParseFloat(pieces[2], line),
                    ParseFloat(pieces[4], line),
                    ParseFloat(pieces[3], line)),
                _ => throw new PatchScriptException(line, $"bad envelope `{value}`")
            };
        }

        private static int ParseOperatorId(string value, int line)
        {
            var normalized = value.StartsWith("op", StringComparison.OrdinalIgnoreCase)
                ? value[2..]
                : value;
            return ParseInt(normalized, line);
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

        private sealed record PendingOperatorGraph(
            int Line,
            string Name,
            float FrequencyHz,
            float Gain)
        {
            public List<OperatorNode> Operators { get; } = [];
            public List<OperatorEdge> Edges { get; } = [];
            public List<int> Carriers { get; } = [];
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
        "opgraph" or "ops" or "operators" => "opgraph",
        "operator" or "op" => "operator",
        "route" or "edge" => "route",
        "carrier" or "out" => "carrier",
        "mod" or "wob" or "wobble" or "bus" => "mod",
        "lfo" or "control" => "control",
        "param" or "parameter" => "param",
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
        "from" or "src" => "from",
        "to" or "dst" => "to",
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

    private static int ParseInt(string value, int line) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new PatchScriptException(line, $"bad integer `{value}`");

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

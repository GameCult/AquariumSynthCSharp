using System.Security.Cryptography;

namespace AquariumSynth.Dsl;

public enum Dx7FrequencyMode
{
    Ratio,
    Fixed
}

public enum Dx7LfoWaveform
{
    Triangle,
    SawDown,
    SawUp,
    Square,
    Sine,
    SampleHold
}

public sealed record Dx7Envelope(
    int Rate1,
    int Rate2,
    int Rate3,
    int Rate4,
    int Level1,
    int Level2,
    int Level3,
    int Level4);

public sealed record Dx7EnvelopeApproximation(
    Envelope Envelope,
    float GateSeconds,
    string Notes)
{
    public string ToScriptSpec() =>
        $"env=adsr:{F(Envelope.AttackSeconds)}:{F(Envelope.DecaySeconds)}:{F(Envelope.SustainLevel)}:{F(Envelope.ReleaseSeconds)} gate={F(GateSeconds)}";

    private static string F(float value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record Dx7OperatorLevelApproximation(
    float LinearLevel,
    int ScaledOutputLevel,
    int KeyScalingOffset,
    int VelocityOffset,
    string Notes);

public sealed record Dx7RateLevelEnvelopeApproximation(
    RateLevelEnvelope Envelope,
    float GateSeconds,
    string Notes)
{
    public string ToScriptSpec()
    {
        var curves = Envelope.Curve1 == RateLevelCurve.Linear &&
                     Envelope.Curve2 == RateLevelCurve.Linear &&
                     Envelope.Curve3 == RateLevelCurve.Linear &&
                     Envelope.Curve4 == RateLevelCurve.Linear
            ? ""
            : $" curves={Curve(Envelope.Curve1)},{Curve(Envelope.Curve2)},{Curve(Envelope.Curve3)},{Curve(Envelope.Curve4)}";
        return $"env=rl rates={F(Envelope.Rate1Seconds)},{F(Envelope.Rate2Seconds)},{F(Envelope.Rate3Seconds)},{F(Envelope.Rate4Seconds)} " +
               $"levels={F(Envelope.Level1)},{F(Envelope.Level2)},{F(Envelope.Level3)},{F(Envelope.Level4)}{curves} gate={F(GateSeconds)}";
    }

    private static string F(float value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    private static string Curve(RateLevelCurve curve) => curve == RateLevelCurve.Exponential ? "exp" : "lin";
}

public sealed record Dx7EnvelopeTracePoint(float TimeSeconds, float Gain, int Stage);

public sealed record Dx7Operator(
    int Number,
    Dx7Envelope Envelope,
    int BreakPoint,
    int LeftDepth,
    int RightDepth,
    int LeftCurve,
    int RightCurve,
    int RateScaling,
    int AmplitudeModulationSensitivity,
    int KeyVelocitySensitivity,
    int OutputLevel,
    Dx7FrequencyMode FrequencyMode,
    int FrequencyCoarse,
    int FrequencyFine,
    int Detune);

public sealed record Dx7PitchEnvelope(Dx7Envelope Envelope);

public sealed record Dx7Lfo(
    int Speed,
    int Delay,
    int PitchModulationDepth,
    int AmplitudeModulationDepth,
    bool KeySync,
    Dx7LfoWaveform Waveform,
    int PitchModulationSensitivity);

public sealed record Dx7AlgorithmRomStep(
    int Operator,
    int TargetOperator,
    int Selector,
    bool FeedbackRegisterWrite,
    int MemoryRegisterMode,
    int OutputCompensation);

public sealed record Dx7ModulationEdge(
    IReadOnlyList<int> SourceOperators,
    int TargetOperator,
    string Kind);

public sealed record Dx7AlgorithmTopology(
    int Algorithm,
    IReadOnlyList<int> CarrierOperators,
    IReadOnlyList<Dx7ModulationEdge> ModulationEdges,
    IReadOnlyList<int> FeedbackSourceOperators,
    IReadOnlyList<int> SelfFeedbackOperators,
    IReadOnlyList<int> DelayedFeedbackTargets,
    IReadOnlyList<Dx7AlgorithmRomStep> RomSteps);

public sealed record Dx7Voice(
    string Name,
    IReadOnlyList<Dx7Operator> Operators,
    Dx7PitchEnvelope PitchEnvelope,
    int Algorithm,
    int Feedback,
    bool OscillatorSync,
    Dx7Lfo Lfo,
    int Transpose)
{
    public ReferencePatch ToReferencePatch(string id, ReferenceSource source) =>
        new(
            id,
            "dx7",
            Name,
            source,
            Features(),
            Array.Empty<PatchParameter>());

    public IReadOnlyList<ReferenceFeature> Features()
    {
        var topology = Dx7SysEx.AlgorithmTopology(Algorithm);
        var enabledOperators = Operators.Count(op => op.OutputLevel > 0);
        var loudest = Operators.OrderByDescending(op => op.OutputLevel).First();
        return
        [
            new("operator_count", Operators.Count.ToString(), "DX7 voices use six operators."),
            new("enabled_operator_count", enabledOperators.ToString(), "Operators with non-zero output level."),
            new("algorithm", Algorithm.ToString(), "DX7 algorithm number, 1-32."),
            new("carrier_operators", string.Join(",", topology.CarrierOperators), "Operators routed to the audio output."),
            new("modulation_edge_count", topology.ModulationEdges.Count.ToString(), "Number of extracted algorithm modulation edges."),
            new("feedback", Feedback.ToString(), "Global DX7 feedback level, 0-7."),
            new("feedback_sources", string.Join(",", topology.FeedbackSourceOperators), "Operators that write the DX7 feedback register in this algorithm."),
            new("self_feedback_operators", string.Join(",", topology.SelfFeedbackOperators), "Operators with direct self-feedback paths."),
            new("oscillator_sync", OscillatorSync ? "on" : "off"),
            new("lfo_waveform", Lfo.Waveform.ToString()),
            new("lfo_pitch_mod_depth", Lfo.PitchModulationDepth.ToString()),
            new("lfo_amp_mod_depth", Lfo.AmplitudeModulationDepth.ToString()),
            new("loudest_operator", loudest.Number.ToString(), $"Output level {loudest.OutputLevel}.")
        ];
    }
}

public sealed record Dx7VoiceBank(IReadOnlyList<Dx7Voice> Voices);

public static class Dx7SysEx
{
    public const int VoiceEditBufferLength = 155;
    public const int PackedVoiceLength = 128;
    public const int PackedBankVoiceCount = 32;
    public const int PackedBankDataLength = PackedVoiceLength * PackedBankVoiceCount;
    public const float OperatorModulationRouteIndex = 6.275f;
    public const float SummedOperatorModulationRouteIndex = 1.6f;

    public static Dx7Voice ParseVoice(ReadOnlySpan<byte> bytes)
    {
        var data = VoiceEditBuffer(bytes);
        return ParseVoiceEditBuffer(data);
    }

    public static Dx7EnvelopeApproximation ApproximateEnvelope(Dx7Envelope envelope)
    {
        var l1 = Level(envelope.Level1);
        var l2 = Level(envelope.Level2);
        var l3 = Level(envelope.Level3);
        var l4 = Level(envelope.Level4);
        var peak = Math.Max(0.0001f, l1);
        var attack = SegmentSeconds(envelope.Rate1, Math.Abs(l1 - l4));
        var decay = SegmentSeconds(envelope.Rate2, Math.Abs(l1 - l2)) +
                    SegmentSeconds(envelope.Rate3, Math.Abs(l2 - l3));
        var release = SegmentSeconds(envelope.Rate4, Math.Abs(l3 - l4));
        var sustain = Math.Clamp(l3 / peak, 0, 1);
        var gate = Math.Max(attack + decay, 0.02f);

        return new Dx7EnvelopeApproximation(
            new Envelope(attack, decay, sustain, release),
            gate,
            "Approximation: DX7 EG uses four rate/level segments; ADSR keeps one sustain level and a key-off release.");
    }

    public static Dx7OperatorLevelApproximation ApproximateOperatorLevel(
        Dx7Operator op,
        int midiNote = 60,
        int velocity = 100)
    {
        var scaled = Math.Clamp(op.OutputLevel, 0, 99);
        var keyScaling = ScaleLevel(
            midiNote,
            op.BreakPoint,
            op.LeftDepth,
            op.RightDepth,
            op.LeftCurve,
            op.RightCurve);
        var velocityOffset = ScaleVelocity(velocity, op.KeyVelocitySensitivity);
        var effectiveLevel = Math.Clamp(scaled + keyScaling + velocityOffset / 32f, 0, 99);
        var normalized = OperatorOutputAmplitude(effectiveLevel);

        return new Dx7OperatorLevelApproximation(
            normalized,
            scaled,
            keyScaling,
            velocityOffset,
            "Approximation: DX7 output level uses nonlinear scaling plus key/velocity offsets before envelope gain.");
    }

    public static float OperatorOutputAmplitude(float outputLevel) =>
        MathF.Pow(2, (Math.Clamp(outputLevel, 0, 99) - 99) / 8f);

    public static float OperatorFeedbackAmount(int feedback) =>
        FeedbackAmounts[Math.Clamp(feedback, 0, FeedbackAmounts.Length - 1)];

    public static float OperatorRouteIndex(Dx7AlgorithmTopology topology, Dx7ModulationEdge edge) =>
        edge.Kind == "sum" || FeedsSummedModulation(topology, edge.TargetOperator)
            ? SummedOperatorModulationRouteIndex
            : OperatorModulationRouteIndex;

    public static float OperatorOutputCompensation(Dx7AlgorithmTopology topology, int operatorNumber)
    {
        var outputStep = topology.RomSteps.FirstOrDefault(step =>
            step.TargetOperator == operatorNumber &&
            step.OutputCompensation > 0);
        return outputStep is null
            ? 1
            : 6f / (outputStep.OutputCompensation + 1);
    }

    public static float OperatorFrequencyRatio(Dx7Operator op, int midiNote = 60)
    {
        if (op.FrequencyMode == Dx7FrequencyMode.Fixed)
        {
            return Math.Max(0.5f, op.FrequencyCoarse);
        }

        var coarse = op.FrequencyCoarse == 0 ? 0.5f : op.FrequencyCoarse;
        var fine = 1 + op.FrequencyFine / 100f;
        return coarse * fine * RatioModeDetuneFactor(op.Detune, midiNote);
    }

    public static Dx7RateLevelEnvelopeApproximation ApproximateRateLevelEnvelope(Dx7Envelope envelope)
    {
        var l1 = Level(envelope.Level1);
        var l2 = Level(envelope.Level2);
        var l3 = Level(envelope.Level3);
        var l4 = Level(envelope.Level4);
        var r1 = SegmentSeconds(envelope.Rate1, Math.Abs(l1 - l4));
        var r2 = SegmentSeconds(envelope.Rate2, Math.Abs(l1 - l2));
        var r3 = SegmentSeconds(envelope.Rate3, Math.Abs(l2 - l3));
        var r4 = SegmentSeconds(envelope.Rate4, Math.Abs(l3 - l4));

        return new Dx7RateLevelEnvelopeApproximation(
            new RateLevelEnvelope(r1, l1, r2, l2, r3, l3, r4, l4),
            Math.Max(r1 + r2 + r3, 0.02f),
            "Approximation: DX7 EG uses four rate/level segments; Aquarium staged envelopes preserve the intermediate target levels.");
    }

    public static IReadOnlyList<Dx7EnvelopeTracePoint> TraceEnvelope(
        Dx7Envelope envelope,
        float gateSeconds,
        float durationSeconds,
        int outputLevel = 99,
        int sampleRate = 44100)
    {
        var state = Dx7EnvelopeState.Start(envelope, outputLevel, sampleRate);
        var sampleCount = Math.Max(1, (int)MathF.Round(Math.Max(1f / sampleRate, durationSeconds) * sampleRate));
        var gateSample = Math.Clamp((int)MathF.Round(Math.Max(0, gateSeconds) * sampleRate), 0, sampleCount);
        var points = new List<Dx7EnvelopeTracePoint>(sampleCount);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            if (sample == gateSample)
            {
                state.KeyDown(envelope, down: false);
            }
            state.Advance(envelope);
            points.Add(new Dx7EnvelopeTracePoint(sample / (float)sampleRate, state.Gain, state.Stage));
        }

        return points;
    }

    public static IReadOnlyList<Dx7EnvelopeTracePoint> TraceInterpolatedEnvelope(
        Dx7Envelope envelope,
        float gateSeconds,
        float durationSeconds,
        int outputLevel = 99,
        int sampleRate = 44100,
        int blockSize = 64)
    {
        var state = Dx7EnvelopeState.Start(envelope, outputLevel, sampleRate);
        var sampleCount = Math.Max(1, (int)MathF.Round(Math.Max(1f / sampleRate, durationSeconds) * sampleRate));
        var gateSample = Math.Clamp((int)MathF.Round(Math.Max(0, gateSeconds) * sampleRate), 0, sampleCount);
        var points = new List<Dx7EnvelopeTracePoint>(sampleCount);
        var previousGain = 0f;

        for (var blockStart = 0; blockStart < sampleCount; blockStart += Math.Max(1, blockSize))
        {
            if (blockStart >= gateSample)
            {
                state.KeyDown(envelope, down: false);
            }
            state.Advance(envelope);
            var currentGain = state.Gain;
            var count = Math.Min(blockSize, sampleCount - blockStart);
            for (var i = 0; i < count; i++)
            {
                var t = (i + 1) / (float)blockSize;
                var gain = previousGain + (currentGain - previousGain) * t;
                points.Add(new Dx7EnvelopeTracePoint((blockStart + i) / (float)sampleRate, gain, state.Stage));
            }
            previousGain = currentGain;
        }

        return points;
    }

    public static Dx7VoiceBank ParseBank(ReadOnlySpan<byte> bytes)
    {
        var data = PackedBankData(bytes);
        var voices = new List<Dx7Voice>(PackedBankVoiceCount);
        for (var i = 0; i < PackedBankVoiceCount; i++)
        {
            var packed = data.AsSpan(i * PackedVoiceLength, PackedVoiceLength);
            voices.Add(ParseVoiceEditBuffer(UnpackVoice(packed)));
        }

        return new Dx7VoiceBank(voices);
    }

    public static ReferenceSource SourceForBytes(string uri, string license, ReadOnlySpan<byte> bytes, string notes = "") =>
        new("dx7-sysex", uri, license, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), notes);

    public static Dx7AlgorithmTopology AlgorithmTopology(int algorithm)
    {
        if (algorithm is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "DX7 algorithm must be 1-32");
        }

        var steps = AlgorithmRom[algorithm - 1]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((entry, index) => ParseAlgorithmStep(operatorNumber: 6 - index, entry))
            .ToList();

        var carrierOperators = steps
            .Where(step => step.OutputCompensation > 0)
            .Select(step => step.TargetOperator)
            .Order()
            .ToList();
        if (carrierOperators.Count == 0)
        {
            carrierOperators.Add(1);
        }

        var memory = new SortedSet<int>();
        var feedbackSources = new SortedSet<int>();
        var selfFeedback = new SortedSet<int>();
        var delayedFeedbackTargets = new SortedSet<int>();
        var edges = new List<Dx7ModulationEdge>();

        foreach (var step in steps)
        {
            var nextMemory = step.MemoryRegisterMode switch
            {
                0 => [],
                1 => [step.Operator],
                2 => new SortedSet<int>(memory),
                3 => new SortedSet<int>(memory.Concat([step.Operator])),
                _ => new SortedSet<int>(memory)
            };

            switch (step.Selector)
            {
                case 1:
                    edges.Add(new Dx7ModulationEdge([step.Operator], step.TargetOperator, "direct"));
                    break;
                case 2 when nextMemory.Count > 0:
                    edges.Add(new Dx7ModulationEdge(nextMemory.ToArray(), step.TargetOperator, "sum"));
                    break;
                case 3 when memory.Count > 0:
                    edges.Add(new Dx7ModulationEdge(memory.ToArray(), step.TargetOperator, "delayed-sum"));
                    break;
                case 4 when feedbackSources.Count > 0:
                    delayedFeedbackTargets.Add(step.TargetOperator);
                    edges.Add(new Dx7ModulationEdge(feedbackSources.ToArray(), step.TargetOperator, "delayed-feedback"));
                    break;
                case 5:
                    selfFeedback.Add(step.TargetOperator);
                    edges.Add(new Dx7ModulationEdge([step.TargetOperator], step.TargetOperator, "self-feedback"));
                    break;
            }

            if (step.FeedbackRegisterWrite)
            {
                feedbackSources.Add(step.Operator);
            }
            memory = nextMemory;
        }

        return new Dx7AlgorithmTopology(
            algorithm,
            carrierOperators,
            edges,
            feedbackSources.ToArray(),
            selfFeedback.ToArray(),
            delayedFeedbackTargets.ToArray(),
            steps);
    }

    public static byte[] UnpackVoice(ReadOnlySpan<byte> packed)
    {
        if (packed.Length != PackedVoiceLength)
        {
            throw new ArgumentException($"packed DX7 voice must be {PackedVoiceLength} bytes", nameof(packed));
        }

        var voice = new byte[VoiceEditBufferLength];
        for (var op = 0; op < 6; op++)
        {
            var packedOffset = op * 17;
            var editOffset = op * 21;
            for (var i = 0; i <= 10; i++) voice[editOffset + i] = SevenBit(packed[packedOffset + i]);

            var curves = SevenBit(packed[packedOffset + 11]);
            voice[editOffset + 11] = (byte)(curves & 0x03);
            voice[editOffset + 12] = (byte)((curves >> 2) & 0x03);

            var rateScalingAndDetune = SevenBit(packed[packedOffset + 12]);
            voice[editOffset + 13] = (byte)(rateScalingAndDetune & 0x07);
            voice[editOffset + 20] = (byte)((rateScalingAndDetune >> 3) & 0x0F);

            var sensitivities = SevenBit(packed[packedOffset + 13]);
            voice[editOffset + 14] = (byte)(sensitivities & 0x03);
            voice[editOffset + 15] = (byte)((sensitivities >> 2) & 0x07);

            voice[editOffset + 16] = SevenBit(packed[packedOffset + 14]);

            var frequency = SevenBit(packed[packedOffset + 15]);
            voice[editOffset + 17] = (byte)(frequency & 0x01);
            voice[editOffset + 18] = (byte)((frequency >> 1) & 0x1F);
            voice[editOffset + 19] = SevenBit(packed[packedOffset + 16]);
        }

        for (var i = 0; i < 8; i++) voice[126 + i] = SevenBit(packed[102 + i]);
        voice[134] = (byte)(SevenBit(packed[110]) & 0x1F);

        var feedbackAndSync = SevenBit(packed[111]);
        voice[135] = (byte)(feedbackAndSync & 0x07);
        voice[136] = (byte)((feedbackAndSync >> 3) & 0x01);

        voice[137] = SevenBit(packed[112]);
        voice[138] = SevenBit(packed[113]);
        voice[139] = SevenBit(packed[114]);
        voice[140] = SevenBit(packed[115]);

        var lfo = SevenBit(packed[116]);
        voice[141] = (byte)(lfo & 0x01);
        voice[142] = (byte)((lfo >> 1) & 0x07);
        voice[143] = (byte)((lfo >> 4) & 0x07);
        voice[144] = SevenBit(packed[117]);

        for (var i = 0; i < 10; i++) voice[145 + i] = SevenBit(packed[118 + i]);
        return voice;
    }

    private static Dx7Voice ParseVoiceEditBuffer(ReadOnlySpan<byte> data)
    {
        if (data.Length != VoiceEditBufferLength)
        {
            throw new ArgumentException($"DX7 voice edit buffer must be {VoiceEditBufferLength} bytes", nameof(data));
        }

        var operators = new List<Dx7Operator>(6);
        for (var op = 0; op < 6; op++)
        {
            var offset = op * 21;
            operators.Add(new Dx7Operator(
                6 - op,
                new Dx7Envelope(
                    Value(data, offset + 0),
                    Value(data, offset + 1),
                    Value(data, offset + 2),
                    Value(data, offset + 3),
                    Value(data, offset + 4),
                    Value(data, offset + 5),
                    Value(data, offset + 6),
                    Value(data, offset + 7)),
                Value(data, offset + 8),
                Value(data, offset + 9),
                Value(data, offset + 10),
                Value(data, offset + 11),
                Value(data, offset + 12),
                Value(data, offset + 13),
                Value(data, offset + 14),
                Value(data, offset + 15),
                Value(data, offset + 16),
                Value(data, offset + 17) == 0 ? Dx7FrequencyMode.Ratio : Dx7FrequencyMode.Fixed,
                Value(data, offset + 18),
                Value(data, offset + 19),
                Value(data, offset + 20)));
        }

        var pitchEnvelope = new Dx7PitchEnvelope(new Dx7Envelope(
            Value(data, 126),
            Value(data, 127),
            Value(data, 128),
            Value(data, 129),
            Value(data, 130),
            Value(data, 131),
            Value(data, 132),
            Value(data, 133)));

        return new Dx7Voice(
            VoiceName(data.Slice(145, 10)),
            operators,
            pitchEnvelope,
            Value(data, 134) + 1,
            Value(data, 135),
            Value(data, 136) != 0,
            new Dx7Lfo(
                Value(data, 137),
                Value(data, 138),
                Value(data, 139),
                Value(data, 140),
                Value(data, 141) != 0,
                ParseLfoWaveform(Value(data, 142)),
                Value(data, 143)),
            Value(data, 144));
    }

    private static float Level(int value) => Math.Clamp(value, 0, 99) / 99f;

    private sealed class Dx7EnvelopeState
    {
        private const int JumpTarget = 1716;
        private readonly int _sampleRate;
        private readonly int _outputLevel;
        private long _level;
        private long _targetLevel;
        private bool _rising;
        private int _increment;
        private bool _down = true;

        private Dx7EnvelopeState(int outputLevel, int sampleRate)
        {
            _outputLevel = outputLevel;
            _sampleRate = sampleRate;
        }

        public int Stage { get; private set; }

        public float Gain =>
            MathF.Pow(2, ((_level / 65536f) - (14 << 8)) / 256f);

        public static Dx7EnvelopeState Start(Dx7Envelope envelope, int outputLevel, int sampleRate)
        {
            var state = new Dx7EnvelopeState(outputLevel, sampleRate);
            state.AdvanceStage(envelope, 0);
            return state;
        }

        public void KeyDown(Dx7Envelope envelope, bool down)
        {
            if (_down == down) return;
            _down = down;
            AdvanceStage(envelope, down ? 0 : 3);
        }

        public void Advance(Dx7Envelope envelope)
        {
            if (Stage >= 3 && !(Stage < 4 && !_down)) return;

            if (_rising)
            {
                if (_level < ((long)JumpTarget << 16))
                {
                    _level = (long)JumpTarget << 16;
                }
                _level += (((17L << 24) - _level) >> 24) * _increment;
                if (_level >= _targetLevel)
                {
                    _level = _targetLevel;
                    AdvanceStage(envelope, Stage + 1);
                }
            }
            else
            {
                _level -= _increment;
                if (_level <= _targetLevel)
                {
                    _level = _targetLevel;
                    AdvanceStage(envelope, Stage + 1);
                }
            }
        }

        private void AdvanceStage(Dx7Envelope envelope, int stage)
        {
            Stage = stage;
            if (Stage >= 4) return;

            var level = Stage switch
            {
                0 => envelope.Level1,
                1 => envelope.Level2,
                2 => envelope.Level3,
                _ => envelope.Level4
            };
            var actual = ScaleOutLevel(level) >> 1;
            actual = (actual << 6) + (ScaleOutLevel(_outputLevel) << 5) - 4256;
            actual = Math.Max(16, actual);
            _targetLevel = (long)actual << 16;
            _rising = _targetLevel > _level;

            var rate = Stage switch
            {
                0 => envelope.Rate1,
                1 => envelope.Rate2,
                2 => envelope.Rate3,
                _ => envelope.Rate4
            };
            var qrate = Math.Min((Math.Clamp(rate, 0, 99) * 41) >> 6, 63);
            var increment = (4 + (qrate & 3)) << (2 + 6 + (qrate >> 2));
            _increment = (int)((long)increment * 44100 / Math.Max(1, _sampleRate));
        }
    }

    private static float RatioModeDetuneFactor(int detune, int midiNote)
    {
        var logFrequency = MathF.Log2(440f) + (midiNote - 69) / 12f;
        var detuneRatio = 0.0209f * MathF.Exp(-0.396f * logFrequency) / 7f;
        return MathF.Pow(2, detuneRatio * logFrequency * (Math.Clamp(detune, 0, 14) - 7));
    }

    private static bool FeedsSummedModulation(Dx7AlgorithmTopology topology, int operatorNumber) =>
        topology.ModulationEdges.Any(edge =>
            edge.Kind == "sum" &&
            edge.SourceOperators.Contains(operatorNumber));

    private static int ScaleVelocity(int velocity, int sensitivity)
    {
        var clampedVelocity = Math.Clamp(velocity, 0, 127);
        var velValue = VelocityData[clampedVelocity >> 1] - 239;
        return (((Math.Clamp(sensitivity, 0, 7) * velValue + 7) >> 3) << 4);
    }

    private static int ScaleLevel(int midiNote, int breakPoint, int leftDepth, int rightDepth, int leftCurve, int rightCurve)
    {
        var offset = Math.Clamp(midiNote, 0, 127) - Math.Clamp(breakPoint, 0, 99) - 17;
        return offset >= 0
            ? ScaleCurve((offset + 1) / 3, rightDepth, rightCurve)
            : ScaleCurve(-(offset - 1) / 3, leftDepth, leftCurve);
    }

    private static int ScaleCurve(int group, int depth, int curve)
    {
        var clampedGroup = Math.Clamp(group, 0, ExpScaleData.Length - 1);
        var clampedDepth = Math.Clamp(depth, 0, 99);
        var scale = curve is 0 or 3
            ? (clampedGroup * clampedDepth * 329) >> 12
            : (ExpScaleData[clampedGroup] * clampedDepth * 329) >> 15;
        return curve < 2 ? -scale : scale;
    }

    private static int ScaleOutLevel(int outputLevel) =>
        outputLevel >= 20
            ? 28 + Math.Clamp(outputLevel, 0, 99)
            : LevelLookup[Math.Clamp(outputLevel, 0, 19)];

    private static float SegmentSeconds(int rate, float distance)
    {
        var normalizedRate = Math.Clamp(rate, 0, 99) / 99f;
        var scaledDistance = Math.Clamp(distance, 0, 1);
        return 0.004f + 12f * scaledDistance * MathF.Pow(1 - normalizedRate, 2.2f);
    }

    private static byte[] VoiceEditBuffer(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == VoiceEditBufferLength) return bytes.ToArray();

        if (bytes.Length == VoiceEditBufferLength + 8 &&
            bytes[0] == 0xF0 &&
            bytes[1] == 0x43 &&
            bytes[3] == 0x00 &&
            bytes[4] == 0x01 &&
            bytes[5] == 0x1B &&
            bytes[^1] == 0xF7)
        {
            VerifyChecksum(bytes.Slice(6, VoiceEditBufferLength), bytes[^2]);
            return bytes.Slice(6, VoiceEditBufferLength).ToArray();
        }

        if (bytes.Length == PackedVoiceLength) return UnpackVoice(bytes);

        throw new ArgumentException($"unsupported DX7 voice payload length {bytes.Length}", nameof(bytes));
    }

    private static byte[] PackedBankData(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == PackedBankDataLength) return bytes.ToArray();

        if (bytes.Length == PackedBankDataLength + 8 &&
            bytes[0] == 0xF0 &&
            bytes[1] == 0x43 &&
            bytes[3] == 0x09 &&
            bytes[4] == 0x20 &&
            bytes[5] == 0x00 &&
            bytes[^1] == 0xF7)
        {
            VerifyChecksum(bytes.Slice(6, PackedBankDataLength), bytes[^2]);
            return bytes.Slice(6, PackedBankDataLength).ToArray();
        }

        throw new ArgumentException($"unsupported DX7 bank payload length {bytes.Length}", nameof(bytes));
    }

    private static void VerifyChecksum(ReadOnlySpan<byte> data, byte checksum)
    {
        var sum = 0;
        foreach (var value in data) sum = (sum + SevenBit(value)) & 0x7F;
        if (((sum + SevenBit(checksum)) & 0x7F) != 0)
        {
            throw new ArgumentException("DX7 SysEx checksum is invalid");
        }
    }

    private static Dx7LfoWaveform ParseLfoWaveform(int value) => value switch
    {
        0 => Dx7LfoWaveform.Triangle,
        1 => Dx7LfoWaveform.SawDown,
        2 => Dx7LfoWaveform.SawUp,
        3 => Dx7LfoWaveform.Square,
        4 => Dx7LfoWaveform.Sine,
        5 => Dx7LfoWaveform.SampleHold,
        _ => Dx7LfoWaveform.Triangle
    };

    private static string VoiceName(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = SevenBit(bytes[i]);
            chars[i] = value is >= 32 and <= 126 ? (char)value : ' ';
        }

        return new string(chars).Trim();
    }

    private static int Value(ReadOnlySpan<byte> data, int index) => SevenBit(data[index]);

    private static byte SevenBit(byte value) => (byte)(value & 0x7F);

    private static readonly int[] VelocityData =
    [
        0, 70, 86, 97, 106, 114, 121, 126, 132, 138, 142, 148, 152, 156, 160, 163,
        166, 170, 173, 174, 178, 181, 184, 186, 189, 190, 194, 196, 198, 200, 202, 205,
        206, 209, 211, 213, 215, 217, 218, 220, 222, 224, 225, 227, 229, 230, 232, 233,
        235, 237, 238, 240, 241, 242, 243, 244, 246, 246, 248, 249, 250, 251, 252, 253,
        254
    ];

    private static readonly float[] FeedbackAmounts =
    [
        0f, 0.01f, 0.02f, 0.05f, 0.10f, 0.19f, 0.38f, 0.66f
    ];

    private static readonly int[] LevelLookup =
    [
        0, 5, 9, 13, 17, 20, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41, 42, 43, 45, 46
    ];

    private static readonly int[] ExpScaleData =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 14, 16, 19, 23, 27, 33, 39, 47, 56, 66,
        80, 94, 110, 126, 142, 158, 174, 190, 206, 222, 238, 250
    ];

    private static Dx7AlgorithmRomStep ParseAlgorithmStep(int operatorNumber, string entry)
    {
        var pieces = entry.Split('/');
        if (pieces.Length != 3 || pieces[1].Length != 3)
        {
            throw new FormatException($"bad DX7 algorithm ROM entry `{entry}`");
        }

        var selector = int.Parse(pieces[0]);
        var control = pieces[1];
        return new Dx7AlgorithmRomStep(
            operatorNumber,
            operatorNumber == 1 ? 6 : operatorNumber - 1,
            selector,
            control[0] == '1',
            int.Parse(control[1..], System.Globalization.NumberStyles.BinaryNumber),
            int.Parse(pieces[2]));
    }

    // Extracted from Ken Shirriff's reverse-engineered DX7 OPS algorithm ROM table.
    // Each row contains operator 6..1 entries as selector/feedback+memory/compensation.
    private static readonly string[] AlgorithmRom =
    [
        "1/100/0 1/000/0 1/000/1 0/001/0 1/010/1 5/011/0",
        "1/000/0 1/000/0 1/000/1 5/001/0 1/110/1 0/011/0",
        "1/100/0 1/000/1 0/001/0 1/010/0 1/010/1 5/011/0",
        "1/000/0 1/000/1 0/101/0 1/010/0 1/010/1 5/011/0",
        "1/100/2 0/001/0 1/010/2 0/011/0 1/010/2 5/011/0",
        "1/000/2 0/101/0 1/010/2 0/011/0 1/010/2 5/011/0",
        "1/100/0 0/001/0 2/011/1 0/001/0 1/010/1 5/011/0",
        "1/000/0 5/001/0 2/111/1 0/001/0 1/010/1 0/011/0",
        "1/000/0 0/001/0 2/011/1 5/001/0 1/110/1 0/011/0",
        "0/001/0 2/011/1 5/001/0 1/110/0 1/010/1 0/011/0",
        "0/101/0 2/011/1 0/001/0 1/010/0 1/010/1 5/011/0",
        "0/001/0 0/011/0 2/011/1 5/001/0 1/110/1 0/011/0",
        "0/101/0 0/011/0 2/011/1 0/001/0 1/010/1 5/011/0",
        "0/101/0 2/011/0 1/000/1 0/001/0 1/010/1 5/011/0",
        "0/001/0 2/011/0 1/000/1 5/001/0 1/110/1 0/011/0",
        "1/100/0 0/001/0 1/010/0 0/011/0 2/011/0 5/001/0",
        "1/000/0 0/001/0 1/010/0 5/011/0 2/111/0 0/001/0",
        "1/000/0 1/000/0 5/001/0 0/111/0 2/011/0 0/001/0",
        "1/100/2 4/001/2 0/011/0 1/010/0 1/010/2 5/011/0",
        "0/001/0 2/011/2 5/001/0 1/110/2 4/011/2 0/011/0",
        "1/001/3 3/001/3 5/011/0 1/110/3 4/011/3 0/011/0",
        "1/100/3 4/001/3 4/011/3 0/011/0 1/010/3 5/011/0",
        "1/100/3 4/001/3 0/011/0 1/010/3 0/011/3 5/011/0",
        "1/100/4 4/001/4 4/011/4 0/011/4 0/011/4 5/011/0",
        "1/100/4 4/001/4 0/011/4 0/011/4 0/011/4 5/011/0",
        "0/101/0 2/011/2 0/001/0 1/010/2 0/011/2 5/011/0",
        "0/001/0 2/011/2 5/001/0 1/110/2 0/011/2 0/011/0",
        "5/001/0 1/110/0 1/010/2 0/011/0 1/010/2 0/011/2",
        "1/100/3 0/001/0 1/010/3 0/011/3 0/011/3 5/011/0",
        "5/001/0 1/110/0 1/010/3 0/011/3 0/011/3 0/011/3",
        "1/100/4 0/001/4 0/011/4 0/011/4 0/011/4 5/011/0",
        "0/101/5 0/011/5 0/011/5 0/011/5 0/011/5 5/011/5"
    ];
}

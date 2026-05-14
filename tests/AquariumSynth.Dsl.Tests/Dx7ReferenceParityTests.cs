using System.Diagnostics;
using System.Text.Json;
using System.Text;

namespace AquariumSynth.Dsl.Tests;

public sealed class Dx7ReferenceParityTests
{
    [Fact]
    public void PublicDomainDx7FixtureParsesAsReferenceBank()
    {
        var path = FixturePath("Dx7", "PublicDomain", "analog1.syx");
        var data = File.ReadAllBytes(path);
        var bank = Dx7SysEx.ParseBank(data);

        Assert.Equal(Dx7SysEx.PackedBankVoiceCount, bank.Voices.Count);
        Assert.Contains(bank.Voices, voice => !string.IsNullOrWhiteSpace(voice.Name));
    }

    [Fact]
    public async Task DexedPyRendersPublicDomainDx7FixtureWhenInstalled()
    {
        var bankPath = FixturePath("Dx7", "PublicDomain", "analog1.syx");
        var render = await DexedPyRenderer.RenderAsync(bankPath, patchIndex: 0);

        if (render is null)
        {
            return;
        }

        Assert.True(render.Samples.Length > 1000, render.Stderr);
        Assert.True(render.Samples.Max(MathF.Abs) > 0.001f, render.Stderr);

        var analysis = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: render.SampleRate))
            .Analyze(render.Samples);
        Assert.True(analysis.Features.Rms > 0.0001f);
    }

    [Fact]
    public async Task PublicDomainDx7McMm53MeetsFirstRenderedParityThresholdWhenInstalled()
    {
        var bankPath = FixturePath("Dx7", "PublicDomain", "analog1.syx");
        var reference = await DexedPyRenderer.RenderAsync(
            bankPath,
            patchIndex: 13,
            noteDurationSeconds: 0.08f,
            renderDurationSeconds: 0.25f);

        if (reference is null)
        {
            return;
        }

        var bank = Dx7SysEx.ParseBank(File.ReadAllBytes(bankPath));
        Assert.Equal("MC-MM  5-3", bank.Voices[13].Name);

        var candidateSource = FaustEmitter.EmitScript(BuiltInScripts.Dx7StylePublicDomainMcMm53);
        var candidate = await FaustCompiler.RenderAsync(
            candidateSource.Source,
            new FaustRenderOptions(DurationSeconds: 0.25f));

        if (candidate is null)
        {
            return;
        }

        Assert.NotEmpty(reference.Samples);
        Assert.NotEmpty(candidate.Samples);

        var comparison = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: reference.SampleRate))
            .Compare(reference.Samples, candidate.Samples);

        Assert.True(comparison.Score >= 0.75f, ParityReport(comparison));
        Assert.True(comparison.LogMelDistance < 0.12f, ParityReport(comparison));
        Assert.True(comparison.EnvelopeDistance < 0.10f, ParityReport(comparison));
        Assert.InRange(comparison.DurationRatio, 0.90f, 1.10f);
        Assert.InRange(comparison.RmsRatio, 0.75f, 1.10f);
        Assert.InRange(comparison.ZeroCrossingRatio, 0.95f, 1.05f);
        Assert.InRange(comparison.CentroidRatio, 0.90f, 1.05f);
    }

    [Fact]
    public async Task PublicDomainDx7PrcSynth1WritesListeningWavsWhenInstalled()
    {
        var bankPath = FixturePath("Dx7", "PublicDomain", "analog1.syx");
        var reference = await DexedPyRenderer.RenderAsync(
            bankPath,
            patchIndex: 17,
            noteDurationSeconds: 0.85f,
            renderDurationSeconds: 1.4f);

        if (reference is null)
        {
            return;
        }

        var bank = Dx7SysEx.ParseBank(File.ReadAllBytes(bankPath));
        var voice = bank.Voices[17];
        Assert.Equal("PRC SYNTH1", voice.Name);

        var script = Dx7VoiceProbeScript(
            voice,
            graphName: "dx7_prc_synth1_probe",
            graphGain: 0.25f,
            envelopeScale: 0.62f,
            gateSeconds: null,
            useAppliedEnvelope: true);
        var candidateSource = FaustEmitter.EmitScript(script);
        var candidate = await FaustCompiler.RenderAsync(
            candidateSource.Source,
            new FaustRenderOptions(DurationSeconds: 1.4f));

        if (candidate is null)
        {
            return;
        }

        Assert.NotEmpty(reference.Samples);
        Assert.NotEmpty(candidate.Samples);

        var comparison = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: reference.SampleRate))
            .Compare(reference.Samples, candidate.Samples);
        var artifactDir = ArtifactPath("parity", "dx7-prc-synth1", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff", System.Globalization.CultureInfo.InvariantCulture));
        WriteListeningArtifacts(
            artifactDir,
            reference.Samples,
            candidate.Samples,
            reference.SampleRate,
            script,
            comparison);

        Assert.True(comparison.LogMelDistance <= 0.145f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
        Assert.True(comparison.EnvelopeDistance <= 0.08f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
        Assert.InRange(comparison.RmsRatio, 0.95f, 1.05f);
        Assert.True(comparison.ZeroCrossingRatio >= 0.75f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
        Assert.True(comparison.Score >= 0.75f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
    }

    [Fact]
    public async Task PublicDomainDx7MoogerAndPianoBassMeetRenderedParityWhenInstalled()
    {
        var cases = new[]
        {
            new CommunityDx7VoiceCase(19, "{ Mooger }", 0.65f, 1.0f, 0.25f),
            new CommunityDx7VoiceCase(22, "Piano Bass", 0.45f, 0.8f, 0.25f)
        };

        var comparisons = new List<(CommunityDx7VoiceCase Case, AudioComparison Comparison, string ArtifactDir)>();
        foreach (var item in cases)
        {
            var result = await RenderAndComparePublicDomainDx7VoiceAsync(item);
            if (result is null)
            {
                return;
            }

            comparisons.Add(result.Value);
        }

        var report = string.Join(
            Environment.NewLine + Environment.NewLine,
            comparisons.Select(item =>
                $"{item.Case.Index}: {item.Case.ExpectedName}{Environment.NewLine}" +
                $"{ParityReport(item.Comparison)}{Environment.NewLine}" +
                $"artifacts: {item.ArtifactDir}"));

        Assert.All(comparisons, item => Assert.True(item.Comparison.LogMelDistance <= 0.25f, report));
        Assert.All(comparisons, item => Assert.True(item.Comparison.EnvelopeDistance <= 0.14f, report));
        Assert.All(comparisons, item => Assert.InRange(item.Comparison.ZeroCrossingRatio, 0.8f, 1.1f));
        Assert.All(comparisons, item => Assert.True(item.Comparison.Score >= 0.6f, report));
    }

    [Fact]
    public async Task PublicDomainDx7AnlgSyn1KeepsBuzzingModulationWhenInstalled()
    {
        var result = await RenderAndComparePublicDomainDx7VoiceAsync(
            new CommunityDx7VoiceCase(7, "ANLGSYN 1", 0.65f, 1.0f, 0.25f));

        if (result is null)
        {
            return;
        }

        var report = $"{ParityReport(result.Value.Comparison)}{Environment.NewLine}artifacts: {result.Value.ArtifactDir}";

        Assert.True(result.Value.Comparison.LogMelDistance <= 0.20f, report);
        Assert.True(result.Value.Comparison.EnvelopeDistance <= 0.12f, report);
        Assert.InRange(result.Value.Comparison.ZeroCrossingRatio, 0.8f, 1.2f);
        Assert.True(result.Value.Comparison.Score >= 0.7f, report);
    }

    [Fact]
    public async Task ProjectAuthoredDx7AlgorithmEightSummedStackMeetsParityWhenInstalled()
    {
        var spec = new DexedPatchSpec(
            "AQ ALG8SUM",
            Algorithm: 8,
            Feedback: 0,
            Operators:
            [
                SilentOperator(1),
                SilentOperator(2),
                FullOperator(3, outputLevel: 99, coarse: 1),
                FullOperator(4, outputLevel: 88, coarse: 1),
                FullOperator(5, outputLevel: 86, coarse: 2),
                FullOperator(6, outputLevel: 82, coarse: 4)
            ]);

        var comparison = await RenderAndCompareProjectAuthoredDx7Async(
            spec,
            graphName: "dx7_algorithm8_summed_stack_probe",
            graphGain: 0.218f,
            noteDurationSeconds: 0.45f,
            renderDurationSeconds: 0.7f);

        if (comparison is null) return;

        Assert.True(comparison.LogMelDistance <= 0.06f, ParityReport(comparison));
        Assert.InRange(comparison.RmsRatio, 0.90f, 1.10f);
        Assert.True(comparison.Score >= 0.80f, ParityReport(comparison));
    }

    [Fact]
    public async Task ProjectAuthoredDx7AlgorithmEightCascadeProbeMeetsParityWhenInstalled()
    {
        var spec = new DexedPatchSpec(
            "AQ ALG8CAS",
            Algorithm: 8,
            Feedback: 0,
            Operators:
            [
                SilentOperator(1),
                SilentOperator(2),
                FullOperator(3, outputLevel: 99, coarse: 1),
                SilentOperator(4),
                FullOperator(5, outputLevel: 86, coarse: 2),
                FullOperator(6, outputLevel: 82, coarse: 4)
            ]);

        var comparison = await RenderAndCompareProjectAuthoredDx7Async(
            spec,
            graphName: "dx7_algorithm8_cascade_probe",
            graphGain: 0.25f,
            noteDurationSeconds: 0.45f,
            renderDurationSeconds: 0.7f);

        if (comparison is null) return;

        Assert.True(comparison.LogMelDistance <= 0.06f, ParityReport(comparison));
        Assert.InRange(comparison.RmsRatio, 0.90f, 1.10f);
        Assert.True(comparison.Score >= 0.85f, ParityReport(comparison));
    }

    [Fact]
    public async Task ProjectAuthoredDx7AlgorithmEightSummedPairProbeMeetsParityWhenInstalled()
    {
        var spec = new DexedPatchSpec(
            "AQ ALG8PAIR",
            Algorithm: 8,
            Feedback: 0,
            Operators:
            [
                SilentOperator(1),
                SilentOperator(2),
                FullOperator(3, outputLevel: 99, coarse: 1),
                FullOperator(4, outputLevel: 88, coarse: 1),
                FullOperator(5, outputLevel: 86, coarse: 2),
                SilentOperator(6)
            ]);

        var comparison = await RenderAndCompareProjectAuthoredDx7Async(
            spec,
            graphName: "dx7_algorithm8_summed_pair_probe",
            graphGain: 0.25f,
            noteDurationSeconds: 0.45f,
            renderDurationSeconds: 0.7f);

        if (comparison is null) return;

        Assert.True(comparison.LogMelDistance <= 0.06f, ParityReport(comparison));
        Assert.InRange(comparison.RmsRatio, 0.95f, 1.10f);
        Assert.True(comparison.Score >= 0.80f, ParityReport(comparison));
    }

    [Fact]
    public void ProjectAuthoredDx7EnvelopeTraceWritesComparisonArtifact()
    {
        var envelope = new Dx7Envelope(
            Rate1: 98,
            Rate2: 64,
            Rate3: 48,
            Rate4: 55,
            Level1: 99,
            Level2: 76,
            Level3: 43,
            Level4: 0);
        var dx7 = Dx7SysEx.TraceEnvelope(envelope, gateSeconds: 0.75f, durationSeconds: 1.1f);
        var dx7Applied = Dx7SysEx.TraceInterpolatedEnvelope(envelope, gateSeconds: 0.75f, durationSeconds: 1.1f);
        var approximation = ScaledEnvelope(Dx7SysEx.ApproximateRateLevelEnvelope(envelope), 0.62f, 0.75f);
        var aquarium = TraceRateLevelEnvelope(approximation.Envelope, approximation.GateSeconds, durationSeconds: 1.1f);
        var aquariumCurved = TraceRateLevelEnvelope(
            new RateLevelEnvelope(
                64f / 44100,
                2f,
                0.0522449f - 64f / 44100,
                0.297302f,
                0.5790476f - 0.0522449f,
                0.015625f,
                0.35f,
                0f,
                RateLevelCurve.Linear,
                RateLevelCurve.Exponential,
                RateLevelCurve.Exponential,
                RateLevelCurve.Exponential),
            gateSeconds: 0.75f,
            durationSeconds: 1.1f);
        var artifactDir = ArtifactPath("parity", "dx7-envelope-trace");
        Directory.CreateDirectory(artifactDir);
        var path = Path.Combine(artifactDir, "egstep.csv");

        using var writer = new StreamWriter(path);
        writer.WriteLine("time_seconds,dx7_raw_gain,dx7_applied_gain,aquarium_rl_gain,aquarium_curved_gain,dx7_stage");
        for (var i = 0; i < Math.Min(dx7.Count, aquarium.Length); i += 64)
        {
            writer.WriteLine(string.Join(
                ",",
                F(dx7[i].TimeSeconds),
                F(dx7[i].Gain),
                F(dx7Applied[i].Gain),
                F(aquarium[i]),
                F(aquariumCurved[i]),
                dx7[i].Stage.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var linearShape = NormalizedDistance(
            dx7Applied.Select(point => point.Gain).ToArray(),
            aquarium);
        var curvedShape = NormalizedDistance(
            dx7Applied.Select(point => point.Gain).ToArray(),
            aquariumCurved);
        Assert.True(linearShape > 0.55f, $"expected current Aquarium envelope to visibly differ; shape distance {linearShape}, artifact: {path}");
        Assert.True(curvedShape < linearShape * 0.6f, $"expected curved Aquarium envelope to improve shape; linear {linearShape}, curved {curvedShape}, artifact: {path}");
    }

    private static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    private static string ArtifactPath(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AquariumSynthCSharp.slnx")))
        {
            current = current.Parent;
        }

        var root = current?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine([root, "artifacts", .. parts]);
    }

    private static string Dx7VoiceProbeScript(
        Dx7Voice voice,
        string graphName,
        float graphGain,
        float envelopeScale,
        float? gateSeconds,
        bool useAppliedEnvelope = false)
    {
        var topology = Dx7SysEx.AlgorithmTopology(voice.Algorithm);
        var builder = new StringBuilder();
        builder.AppendLine("patch");
        builder.AppendLine("    gain=1.4");
        builder.AppendLine($"    soft_clip={F(NeedsDx7OutputNonlinearity(voice, topology))}");
        builder.AppendLine();
        builder.AppendLine("opgraph");
        builder.AppendLine($"    name={graphName}");
        var graphFrequency = Dx7SysEx.NoteFrequencyHz(midiNote: 60, voice.Transpose);
        var effectiveMidiNote = Math.Clamp(60 + voice.Transpose - 24, 0, 127);
        builder.AppendLine($"    freq={F(graphFrequency)}");
        builder.AppendLine($"    gain={F(graphGain)}");
        builder.AppendLine();

        foreach (var op in voice.Operators.OrderByDescending(op => op.Number))
        {
            var level = Dx7SysEx.ApproximateOperatorLevel(op).LinearLevel *
                        Dx7SysEx.OperatorOutputCompensation(topology, op.Number);
            builder.AppendLine($"operator name=op{op.Number}");
            builder.AppendLine($"    ratio={F(Dx7SysEx.OperatorFrequencyRatio(op, midiNote: effectiveMidiNote, baseFrequencyHz: graphFrequency))}");
            builder.AppendLine($"    level={F(level)}");
            if (topology.SelfFeedbackOperators.Contains(op.Number))
            {
                builder.AppendLine($"    feedback={F(Dx7SysEx.OperatorFeedbackAmount(voice.Feedback))}");
            }
            var envelope = useAppliedEnvelope
                ? Dx7SysEx.ApproximateAppliedRateLevelEnvelope(op.Envelope, gateSeconds ?? 0.85f)
                : ScaledEnvelope(Dx7SysEx.ApproximateRateLevelEnvelope(op.Envelope), envelopeScale, gateSeconds);
            builder.AppendLine($"    {envelope.ToScriptSpec()}");
            builder.AppendLine();
        }

        foreach (var edge in topology.ModulationEdges.Where(edge => edge.Kind != "self-feedback"))
        {
            foreach (var source in edge.SourceOperators)
            {
                builder.AppendLine($"route from=op{source} to=op{edge.TargetOperator} index={F(Dx7SysEx.OperatorRouteIndex(topology, edge))}");
            }
        }

        foreach (var carrier in topology.CarrierOperators)
        {
            builder.AppendLine($"carrier name=op{carrier}");
        }

        return builder.ToString();
    }

    private static Dx7Voice VoiceFromSpec(DexedPatchSpec spec) =>
        new(
            spec.Name,
            spec.Operators.Select(op =>
                new Dx7Operator(
                    op.Number,
                    new Dx7Envelope(
                        op.Rates[0],
                        op.Rates[1],
                        op.Rates[2],
                        op.Rates[3],
                        op.Levels[0],
                        op.Levels[1],
                        op.Levels[2],
                        op.Levels[3]),
                    BreakPoint: 39,
                    LeftDepth: 0,
                    RightDepth: 0,
                    LeftCurve: 0,
                    RightCurve: 0,
                    RateScaling: 0,
                    AmplitudeModulationSensitivity: 0,
                    KeyVelocitySensitivity: 0,
                    OutputLevel: op.OutputLevel,
                    FrequencyMode: op.FrequencyMode == 0 ? Dx7FrequencyMode.Ratio : Dx7FrequencyMode.Fixed,
                    FrequencyCoarse: op.FrequencyCoarse,
                    FrequencyFine: op.FrequencyFine,
                    Detune: op.Detune))
                .ToArray(),
            new Dx7PitchEnvelope(new Dx7Envelope(99, 99, 99, 99, 50, 50, 50, 50)),
            spec.Algorithm,
            spec.Feedback,
            OscillatorSync: true,
            new Dx7Lfo(35, 0, 0, 0, KeySync: true, Dx7LfoWaveform.Sine, PitchModulationSensitivity: 3),
            Transpose: 24);

    private static async Task<AudioComparison?> RenderAndCompareProjectAuthoredDx7Async(
        DexedPatchSpec spec,
        string graphName,
        float graphGain,
        float noteDurationSeconds,
        float renderDurationSeconds)
    {
        var reference = await DexedPyRenderer.RenderPatchAsync(
            spec,
            noteDurationSeconds: noteDurationSeconds,
            renderDurationSeconds: renderDurationSeconds);

        if (reference is null)
        {
            return null;
        }

        var script = Dx7VoiceProbeScript(
            VoiceFromSpec(spec),
            graphName,
            graphGain,
            envelopeScale: 0.62f,
            gateSeconds: noteDurationSeconds);
        var candidateSource = FaustEmitter.EmitScript(script);
        var candidate = await FaustCompiler.RenderAsync(
            candidateSource.Source,
            new FaustRenderOptions(DurationSeconds: renderDurationSeconds));

        if (candidate is null)
        {
            return null;
        }

        var comparison = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: reference.SampleRate))
            .Compare(reference.Samples, candidate.Samples);
        var artifactDir = ArtifactPath(
            "parity",
            "dx7-project-authored",
            $"{graphName}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}");
        WriteListeningArtifacts(
            artifactDir,
            reference.Samples,
            candidate.Samples,
            reference.SampleRate,
            script,
            comparison);
        return comparison;
    }

    private static async Task<(CommunityDx7VoiceCase Case, AudioComparison Comparison, string ArtifactDir)?> RenderAndComparePublicDomainDx7VoiceAsync(
        CommunityDx7VoiceCase item)
    {
        var bankPath = FixturePath("Dx7", "PublicDomain", "analog1.syx");
        var reference = await DexedPyRenderer.RenderAsync(
            bankPath,
            patchIndex: item.Index,
            noteDurationSeconds: item.NoteDurationSeconds,
            renderDurationSeconds: item.RenderDurationSeconds);

        if (reference is null)
        {
            return null;
        }

        var bank = Dx7SysEx.ParseBank(File.ReadAllBytes(bankPath));
        var voice = bank.Voices[item.Index];
        Assert.Equal(item.ExpectedName, voice.Name);

        var graphName = $"dx7_analog1_{item.Index}_{SafeName(voice.Name)}";
        var script = Dx7VoiceProbeScript(
            voice,
            graphName,
            item.GraphGain,
            envelopeScale: 0.62f,
            gateSeconds: item.NoteDurationSeconds,
            useAppliedEnvelope: true);
        var candidateSource = FaustEmitter.EmitScript(script);
        var candidate = await FaustCompiler.RenderAsync(
            candidateSource.Source,
            new FaustRenderOptions(DurationSeconds: item.RenderDurationSeconds));

        if (candidate is null)
        {
            return null;
        }

        var comparison = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: reference.SampleRate))
            .Compare(reference.Samples, candidate.Samples);
        var artifactDir = ArtifactPath(
            "parity",
            "dx7-community-analog1",
            $"{item.Index:00}-{SafeName(voice.Name)}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfff}");
        WriteListeningArtifacts(
            artifactDir,
            reference.Samples,
            candidate.Samples,
            reference.SampleRate,
            script,
            comparison);

        return (item, comparison, artifactDir);
    }

    private static DexedOperatorSpec SilentOperator(int number) =>
        FullOperator(number, outputLevel: 0, coarse: 1);

    private static DexedOperatorSpec FullOperator(int number, int outputLevel, int coarse) =>
        new(number, outputLevel, FrequencyCoarse: coarse, FrequencyFine: 0, Detune: 7, FrequencyMode: 0, Rates: [99, 99, 99, 99], Levels: [99, 99, 99, 0]);

    private static Dx7RateLevelEnvelopeApproximation ScaledEnvelope(Dx7RateLevelEnvelopeApproximation approximation, float scale, float? gateSeconds)
    {
        var envelope = approximation.Envelope;
        var scaled = new RateLevelEnvelope(
            envelope.Rate1Seconds * scale,
            envelope.Level1,
            envelope.Rate2Seconds * scale,
            envelope.Level2,
            envelope.Rate3Seconds * scale,
            envelope.Level3,
            envelope.Rate4Seconds * scale,
            envelope.Level4,
            envelope.Curve1,
            envelope.Curve2,
            envelope.Curve3,
            envelope.Curve4);
        return approximation with
        {
            Envelope = scaled,
            GateSeconds = gateSeconds ?? Math.Max((scaled.Rate1Seconds + scaled.Rate2Seconds + scaled.Rate3Seconds) * 0.68f, 0.02f)
        };
    }

    private static float[] TraceRateLevelEnvelope(RateLevelEnvelope envelope, float gateSeconds, float durationSeconds, int sampleRate = 44100)
    {
        var count = Math.Max(1, (int)MathF.Round(durationSeconds * sampleRate));
        var values = new float[count];
        for (var sample = 0; sample < values.Length; sample++)
        {
            var age = sample / (float)sampleRate;
            var releaseStart = Math.Max(gateSeconds, envelope.Rate1Seconds + envelope.Rate2Seconds + envelope.Rate3Seconds);
            values[sample] = age < envelope.Rate1Seconds
                ? Segment(age, 0, envelope.Rate1Seconds, 0, envelope.Level1, envelope.Curve1)
                : age < envelope.Rate1Seconds + envelope.Rate2Seconds
                    ? Segment(age, envelope.Rate1Seconds, envelope.Rate2Seconds, envelope.Level1, envelope.Level2, envelope.Curve2)
                    : age < envelope.Rate1Seconds + envelope.Rate2Seconds + envelope.Rate3Seconds
                        ? Segment(age, envelope.Rate1Seconds + envelope.Rate2Seconds, envelope.Rate3Seconds, envelope.Level2, envelope.Level3, envelope.Curve3)
                        : age < releaseStart
                            ? envelope.Level3
                            : age < releaseStart + envelope.Rate4Seconds
                                ? Segment(age, releaseStart, envelope.Rate4Seconds, envelope.Level3, envelope.Level4, envelope.Curve4)
                                : envelope.Level4;
        }

        return values;
    }

    private static float Segment(float time, float start, float duration, float from, float to, RateLevelCurve curve = RateLevelCurve.Linear)
    {
        var t = Math.Clamp((time - start) / Math.Max(0.0001f, duration), 0, 1);
        if (curve == RateLevelCurve.Exponential)
        {
            var a = Math.Max(0.00001f, from);
            var b = Math.Max(0.00001f, to);
            return MathF.Exp(MathF.Log(a) + (MathF.Log(b) - MathF.Log(a)) * t);
        }

        return from + (to - from) * t;
    }

    private static float NormalizedDistance(IReadOnlyList<float> reference, IReadOnlyList<float> candidate)
    {
        var length = Math.Max(1, Math.Max(reference.Count, candidate.Count));
        var error = 0f;
        var scale = 0f;
        for (var i = 0; i < length; i++)
        {
            var a = ResampledAt(reference, i, length);
            var b = ResampledAt(candidate, i, length);
            var delta = a - b;
            error += delta * delta;
            scale += a * a + b * b;
        }

        return MathF.Sqrt(error / Math.Max(float.Epsilon, scale));
    }

    private static float ResampledAt(IReadOnlyList<float> values, int index, int targetLength)
    {
        if (values.Count == 0) return 0;
        if (values.Count == 1 || targetLength <= 1) return values[0];
        var position = index * (values.Count - 1f) / (targetLength - 1);
        var left = (int)MathF.Floor(position);
        var right = Math.Min(left + 1, values.Count - 1);
        var t = position - left;
        return values[left] * (1 - t) + values[right] * t;
    }

    private static void WriteListeningArtifacts(
        string directory,
        IReadOnlyList<float> reference,
        IReadOnlyList<float> candidate,
        int sampleRate,
        string script,
        AudioComparison comparison)
    {
        Directory.CreateDirectory(directory);
        var peak = Math.Max(Peak(reference), Peak(candidate));
        var scale = peak <= 0 ? 1 : 0.98f / peak;
        WriteWav(Path.Combine(directory, "reference-dexed.wav"), reference, sampleRate, scale);
        WriteWav(Path.Combine(directory, "candidate-aquarium.wav"), candidate, sampleRate, scale);
        File.WriteAllText(Path.Combine(directory, "candidate.aqua"), script);
        File.WriteAllText(
            Path.Combine(directory, "report.txt"),
            string.Join(
                Environment.NewLine,
                ParityReport(comparison),
                $"wav normalization scale: {scale}",
                $"reference: {Path.Combine(directory, "reference-dexed.wav")}",
                $"candidate: {Path.Combine(directory, "candidate-aquarium.wav")}"));
    }

    private static void WriteWav(string path, IReadOnlyList<float> samples, int sampleRate, float scale)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var dataBytes = samples.Count * sizeof(short);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in samples)
        {
            writer.Write((short)MathF.Round(Math.Clamp(sample * scale, -1, 1) * short.MaxValue));
        }
    }

    private static float Peak(IReadOnlyList<float> samples)
    {
        var peak = 0f;
        foreach (var sample in samples) peak = Math.Max(peak, Math.Abs(sample));
        return peak;
    }

    private static string F(float value) =>
        value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

    private static string F(bool value) => value ? "true" : "false";

    private static bool NeedsDx7OutputNonlinearity(Dx7Voice voice, Dx7AlgorithmTopology topology) =>
        voice.Feedback >= 7 && topology.SelfFeedbackOperators.Count > 0;

    private static string SafeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static string ParityReport(AudioComparison comparison) =>
        string.Join(
            Environment.NewLine,
            $"score: {comparison.Score}",
            $"log-mel distance: {comparison.LogMelDistance}",
            $"envelope distance: {comparison.EnvelopeDistance}",
            $"duration ratio: {comparison.DurationRatio}",
            $"rms ratio: {comparison.RmsRatio}",
            $"zero-crossing ratio: {comparison.ZeroCrossingRatio}",
            $"centroid ratio: {comparison.CentroidRatio}",
            $"reference peak/rms: {comparison.Reference.Features.Peak}/{comparison.Reference.Features.Rms}",
            $"candidate peak/rms: {comparison.Candidate.Features.Peak}/{comparison.Candidate.Features.Rms}");
}

internal sealed record DexedPyRender(float[] Samples, int SampleRate, string Stdout, string Stderr);

internal sealed record DexedPatchSpec(
    string Name,
    int Algorithm,
    int Feedback,
    IReadOnlyList<DexedOperatorSpec> Operators);

internal sealed record DexedOperatorSpec(
    int Number,
    int OutputLevel,
    int FrequencyCoarse,
    int FrequencyFine,
    int Detune,
    int FrequencyMode,
    IReadOnlyList<int> Rates,
    IReadOnlyList<int> Levels);

internal sealed record CommunityDx7VoiceCase(
    int Index,
    string ExpectedName,
    float NoteDurationSeconds,
    float RenderDurationSeconds,
    float GraphGain);

internal static class DexedPyRenderer
{
    public static async Task<DexedPyRender?> RenderAsync(
        string bankPath,
        int patchIndex,
        int midiNote = 60,
        int velocity = 100,
        float noteDurationSeconds = 1,
        float renderDurationSeconds = 1.5f,
        int sampleRate = 44100,
        CancellationToken cancellationToken = default)
    {
        var python = await FindPythonWithDexedAsync(cancellationToken);
        if (python is null) return null;

        var tempDir = Path.Combine(Path.GetTempPath(), $"aquarium-dx7-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var scriptPath = Path.Combine(tempDir, "render_dx7.py");
            var outputPath = Path.Combine(tempDir, "reference.f32");
            await File.WriteAllTextAsync(scriptPath, ScriptSource(), cancellationToken);

            var result = await RunAsync(
                python.FileName,
                [
                    .. python.PrefixArguments,
                    scriptPath,
                    bankPath,
                    patchIndex.ToStringInvariant(),
                    outputPath,
                    midiNote.ToStringInvariant(),
                    velocity.ToStringInvariant(),
                    noteDurationSeconds.ToStringInvariant(),
                    renderDurationSeconds.ToStringInvariant(),
                    sampleRate.ToStringInvariant()
                ],
                cancellationToken);

            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                return new DexedPyRender([], sampleRate, result.Stdout, result.Stderr);
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            var samples = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * sizeof(float));
            return new DexedPyRender(samples, sampleRate, result.Stdout, result.Stderr);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    public static async Task<DexedPyRender?> RenderPatchAsync(
        DexedPatchSpec patch,
        int midiNote = 60,
        int velocity = 100,
        float noteDurationSeconds = 1,
        float renderDurationSeconds = 1.5f,
        int sampleRate = 44100,
        CancellationToken cancellationToken = default)
    {
        var python = await FindPythonWithDexedAsync(cancellationToken);
        if (python is null) return null;

        var tempDir = Path.Combine(Path.GetTempPath(), $"aquarium-dx7-patch-render-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var scriptPath = Path.Combine(tempDir, "render_project_dx7.py");
            var specPath = Path.Combine(tempDir, "patch.json");
            var outputPath = Path.Combine(tempDir, "reference.f32");
            await File.WriteAllTextAsync(scriptPath, PatchScriptSource(), cancellationToken);
            await File.WriteAllTextAsync(specPath, JsonSerializer.Serialize(patch), cancellationToken);

            var result = await RunAsync(
                python.FileName,
                [
                    .. python.PrefixArguments,
                    scriptPath,
                    specPath,
                    outputPath,
                    midiNote.ToStringInvariant(),
                    velocity.ToStringInvariant(),
                    noteDurationSeconds.ToStringInvariant(),
                    renderDurationSeconds.ToStringInvariant(),
                    sampleRate.ToStringInvariant()
                ],
                cancellationToken);

            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                return new DexedPyRender([], sampleRate, result.Stdout, result.Stderr);
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            var samples = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * sizeof(float));
            return new DexedPyRender(samples, sampleRate, result.Stdout, result.Stderr);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task<PythonCommand?> FindPythonWithDexedAsync(CancellationToken cancellationToken)
    {
        var configuredPython = Environment.GetEnvironmentVariable("AQUARIUM_DX7_PYTHON");
        var candidates = new List<PythonCommand>();
        if (!string.IsNullOrWhiteSpace(configuredPython))
        {
            candidates.Add(new PythonCommand(configuredPython, []));
        }

        candidates.AddRange(
        [
            new PythonCommand("py", ["-3"]),
            new PythonCommand("python", []),
            new PythonCommand("python3", [])
        ]);

        foreach (var candidate in candidates)
        {
            var result = await RunAsync(
                candidate.FileName,
                [.. candidate.PrefixArguments, "-c", "import dexed, numpy"],
                cancellationToken);
            if (result.ExitCode == 0) return candidate;
        }

        return null;
    }

    private static string ScriptSource() =>
        """
        import sys
        import numpy as np
        from dexed import Patch, DexedSynth

        bank_path = sys.argv[1]
        patch_index = int(sys.argv[2])
        output_path = sys.argv[3]
        midi_note = int(sys.argv[4])
        velocity = int(sys.argv[5])
        note_duration = float(sys.argv[6])
        render_duration = float(sys.argv[7])
        sample_rate = int(sys.argv[8])

        patches = Patch.load_bank(bank_path)
        synth = DexedSynth(sample_rate=sample_rate)
        synth.load_patch(patches[patch_index])
        audio = synth.render(
            midi_note=midi_note,
            velocity=velocity,
            note_duration=note_duration,
            render_duration=render_duration)
        np.asarray(audio, dtype=np.float32).tofile(output_path)
        """;

    private static string PatchScriptSource() =>
        """
        import json
        import sys
        import numpy as np
        from dexed import Patch, DexedSynth

        spec_path = sys.argv[1]
        output_path = sys.argv[2]
        midi_note = int(sys.argv[3])
        velocity = int(sys.argv[4])
        note_duration = float(sys.argv[5])
        render_duration = float(sys.argv[6])
        sample_rate = int(sys.argv[7])

        with open(spec_path, "r", encoding="utf-8") as f:
            spec = json.load(f)

        patch = Patch(spec["Name"])
        patch.algorithm = int(spec["Algorithm"]) - 1
        patch.feedback = int(spec["Feedback"])
        patch.transpose = 24
        for op in patch.op:
            op.output_level = 0
            op.envelope.rates = [99, 99, 99, 99]
            op.envelope.levels = [99, 99, 99, 0]
            op.frequency_coarse = 1
            op.frequency_fine = 0
            op.frequency_mode = 0
            op.detune = 7

        for op_spec in spec["Operators"]:
            op = patch.op[int(op_spec["Number"]) - 1]
            op.output_level = int(op_spec["OutputLevel"])
            op.frequency_coarse = int(op_spec["FrequencyCoarse"])
            op.frequency_fine = int(op_spec["FrequencyFine"])
            op.frequency_mode = int(op_spec["FrequencyMode"])
            op.detune = int(op_spec["Detune"])
            op.envelope.rates = [int(v) for v in op_spec["Rates"]]
            op.envelope.levels = [int(v) for v in op_spec["Levels"]]

        synth = DexedSynth(sample_rate=sample_rate)
        synth.load_patch(patch)
        audio = synth.render(
            midi_note=midi_note,
            velocity=velocity,
            note_duration=note_duration,
            render_duration=render_duration)
        np.asarray(audio, dtype=np.float32).tofile(output_path)
        """;

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var start = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);

            using var process = Process.Start(start);
            if (process is null) return (-1, "", $"failed to start `{fileName}`");

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return (process.ExitCode, await stdout, await stderr);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return (-1, "", exception.Message);
        }
    }

    private sealed record PythonCommand(string FileName, IReadOnlyList<string> PrefixArguments);
}

internal static class TestFormat
{
    public static string ToStringInvariant(this int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string ToStringInvariant(this float value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

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
            graphGain: 0.39f,
            envelopeScale: 0.62f,
            gateSeconds: null);
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

        Assert.True(comparison.LogMelDistance <= 0.255f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
        Assert.True(comparison.Score >= 0.40f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
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
        var reference = await DexedPyRenderer.RenderPatchAsync(
            spec,
            noteDurationSeconds: 0.45f,
            renderDurationSeconds: 0.7f);

        if (reference is null)
        {
            return;
        }

        var script = Dx7VoiceProbeScript(
            VoiceFromSpec(spec),
            graphName: "dx7_algorithm8_summed_stack_probe",
            graphGain: 0.39f,
            envelopeScale: 0.62f,
            gateSeconds: 0.45f);
        var candidateSource = FaustEmitter.EmitScript(script);
        var candidate = await FaustCompiler.RenderAsync(
            candidateSource.Source,
            new FaustRenderOptions(DurationSeconds: 0.7f));

        if (candidate is null)
        {
            return;
        }

        var comparison = new AudioAnalyzer(new AudioAnalysisConfig(SampleRate: reference.SampleRate))
            .Compare(reference.Samples, candidate.Samples);

        Assert.True(comparison.LogMelDistance <= 0.31f, ParityReport(comparison));
        Assert.True(comparison.Score >= 0.35f, ParityReport(comparison));
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
        float? gateSeconds)
    {
        var topology = Dx7SysEx.AlgorithmTopology(voice.Algorithm);
        var builder = new StringBuilder();
        builder.AppendLine("patch");
        builder.AppendLine("    gain=1.4");
        builder.AppendLine("    soft_clip=false");
        builder.AppendLine();
        builder.AppendLine("opgraph");
        builder.AppendLine($"    name={graphName}");
        builder.AppendLine("    freq=261.6256");
        builder.AppendLine($"    gain={F(graphGain)}");
        builder.AppendLine();

        foreach (var op in voice.Operators.OrderByDescending(op => op.Number))
        {
            var level = Dx7SysEx.ApproximateOperatorLevel(op).LinearLevel *
                        Dx7SysEx.OperatorOutputCompensation(topology, op.Number);
            var envelope = ScaledEnvelope(Dx7SysEx.ApproximateRateLevelEnvelope(op.Envelope), envelopeScale, gateSeconds);
            builder.AppendLine($"operator name=op{op.Number}");
            builder.AppendLine($"    ratio={F(Dx7SysEx.OperatorFrequencyRatio(op))}");
            builder.AppendLine($"    level={F(level)}");
            if (topology.SelfFeedbackOperators.Contains(op.Number))
            {
                builder.AppendLine($"    feedback={F(Dx7SysEx.OperatorFeedbackAmount(voice.Feedback))}");
            }
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
            envelope.Level4);
        return approximation with
        {
            Envelope = scaled,
            GateSeconds = gateSeconds ?? Math.Max((scaled.Rate1Seconds + scaled.Rate2Seconds + scaled.Rate3Seconds) * 0.68f, 0.02f)
        };
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

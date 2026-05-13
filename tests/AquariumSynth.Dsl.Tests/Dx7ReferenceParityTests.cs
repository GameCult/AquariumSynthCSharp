using System.Diagnostics;
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

        var script = PrcSynth1ProbeScript(voice);
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
        var artifactDir = ArtifactPath("parity", "dx7-prc-synth1");
        WriteListeningArtifacts(
            artifactDir,
            reference.Samples,
            candidate.Samples,
            reference.SampleRate,
            script,
            comparison);

        Assert.True(comparison.Score >= 0.40f, $"{ParityReport(comparison)}{Environment.NewLine}artifacts: {artifactDir}");
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

    private static string PrcSynth1ProbeScript(Dx7Voice voice)
    {
        var topology = Dx7SysEx.AlgorithmTopology(voice.Algorithm);
        var builder = new StringBuilder();
        builder.AppendLine("patch");
        builder.AppendLine("    gain=2.0");
        builder.AppendLine("    soft_clip=true");
        builder.AppendLine();
        builder.AppendLine("opgraph");
        builder.AppendLine("    name=dx7_prc_synth1_probe");
        builder.AppendLine("    freq=440");
        builder.AppendLine("    gain=0.55");
        builder.AppendLine();

        foreach (var op in voice.Operators.OrderByDescending(op => op.Number))
        {
            var level = Dx7SysEx.ApproximateOperatorLevel(op).LinearLevel;
            var envelope = ScaledEnvelope(Dx7SysEx.ApproximateRateLevelEnvelope(op.Envelope), 0.62f);
            builder.AppendLine($"operator name=op{op.Number}");
            builder.AppendLine($"    ratio={F(Dx7Ratio(op))}");
            builder.AppendLine($"    level={F(level)}");
            if (topology.SelfFeedbackOperators.Contains(op.Number))
            {
                builder.AppendLine($"    feedback={F(voice.Feedback * 0.08f)}");
            }
            builder.AppendLine($"    {envelope.ToScriptSpec()}");
            builder.AppendLine();
        }

        foreach (var edge in topology.ModulationEdges.Where(edge => edge.Kind != "self-feedback"))
        {
            foreach (var source in edge.SourceOperators)
            {
                builder.AppendLine($"route from=op{source} to=op{edge.TargetOperator} index=0.7");
            }
        }

        foreach (var carrier in topology.CarrierOperators)
        {
            builder.AppendLine($"carrier name=op{carrier}");
        }

        return builder.ToString();
    }

    private static float Dx7Ratio(Dx7Operator op)
    {
        if (op.FrequencyMode == Dx7FrequencyMode.Fixed)
        {
            return Math.Max(0.5f, op.FrequencyCoarse);
        }

        var coarse = op.FrequencyCoarse == 0 ? 0.5f : op.FrequencyCoarse;
        return coarse * (1 + op.FrequencyFine / 100f);
    }

    private static Dx7RateLevelEnvelopeApproximation ScaledEnvelope(Dx7RateLevelEnvelopeApproximation approximation, float scale)
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
            GateSeconds = Math.Max((scaled.Rate1Seconds + scaled.Rate2Seconds + scaled.Rate3Seconds) * 0.68f, 0.02f)
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

using System.Diagnostics;

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

    private static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);
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

using System.Diagnostics;

namespace AquariumSynth.Dsl.Tests;

public sealed class ZynReferenceParityTests
{
    [Fact]
    public async Task ZynAddSubFxReferenceSourceIsPinnedForTestOnlyParity()
    {
        var root = RepositoryRoot();
        var zynRoot = Path.Combine(root, "external", "zynaddsubfx");

        Assert.True(Directory.Exists(zynRoot), "ZynAddSubFX reference source submodule is missing. Run `git submodule update --init --recursive`.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "COPYING")), "ZynAddSubFX reference source should keep its GPL license file.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "src", "Params", "PADnoteParameters.cpp")), "PAD parameter implementation is the parity source, not Aquarium runtime code.");
        Assert.True(File.Exists(Path.Combine(zynRoot, "src", "Synth", "PADnote.cpp")), "PAD note implementation is the parity source, not Aquarium runtime code.");
        Assert.Contains("GNU GENERAL PUBLIC LICENSE", await File.ReadAllTextAsync(Path.Combine(zynRoot, "COPYING")));

        var revision = await RunAsync("git", ["-C", zynRoot, "rev-parse", "HEAD"]);
        Assert.Equal(0, revision.ExitCode);
        Assert.Equal("3ab608c432996ba4d582176572c0b0f82328c825", revision.Stdout.Trim());
    }

    [Fact]
    public async Task ZynPadReferenceRendererWritesListeningArtifactsWhenBuilt()
    {
        var root = RepositoryRoot();
        var bash = @"C:\msys64\usr\bin\bash.exe";
        var renderer = Path.Combine(root, "artifacts", "zyn-reference-build-msys", "src", "Tests", "ZynPadReference.exe");
        if (!File.Exists(bash) || !File.Exists(renderer))
        {
            return;
        }

        var artifactDir = Path.Combine(root, "artifacts", "parity", "zyn-pad-reference");
        Directory.CreateDirectory(artifactDir);

        var input = Path.Combine(root, "tests", "AquariumSynth.Dsl.Tests", "Fixtures", "ZynAddSubFX", "ProjectAuthored", "pad-texture.xiz");
        var zynRaw = Path.Combine(artifactDir, "pad-texture-zyn.f32");
        var zynWav = Path.Combine(artifactDir, "pad-texture-zyn.wav");
        var zynTableRaw = Path.Combine(artifactDir, "pad-texture-zyn-table0.f32");
        var zynTableWav = Path.Combine(artifactDir, "pad-texture-zyn-table0.wav");
        var aquaWav = Path.Combine(artifactDir, "pad-texture-aqua.wav");
        var reportPath = Path.Combine(artifactDir, "report.txt");

        var command = string.Join(' ', [
            "export PATH=/mingw64/bin:/usr/bin:/bin:$PATH;",
            $"cd {BashQuote(ToMsysPath(root))};",
            "./artifacts/zyn-reference-build-msys/src/Tests/ZynPadReference.exe",
            BashQuote(ToMsysPath(input)),
            BashQuote(ToMsysPath(zynRaw)),
            "0",
            "note",
            "261.6256",
            "1.5"
        ]);
        var reference = await RunAsync(bash, ["-lc", command]);
        Assert.Equal(0, reference.ExitCode);

        var tableCommand = string.Join(' ', [
            "export PATH=/mingw64/bin:/usr/bin:/bin:$PATH;",
            $"cd {BashQuote(ToMsysPath(root))};",
            "./artifacts/zyn-reference-build-msys/src/Tests/ZynPadReference.exe",
            BashQuote(ToMsysPath(input)),
            BashQuote(ToMsysPath(zynTableRaw)),
            "0",
            "0"
        ]);
        var tableReference = await RunAsync(bash, ["-lc", tableCommand]);
        Assert.Equal(0, tableReference.ExitCode);

        var zynSamples = await ReadFloat32Async(zynRaw);
        var zynTableSamples = await ReadFloat32Async(zynTableRaw);
        Assert.Equal(66150, zynSamples.Length);
        Assert.True(Peak(zynSamples) > 0.001f);
        Assert.True(zynTableSamples.Length > zynSamples.Length);
        Assert.True(Peak(zynTableSamples) > 0.001f);
        WriteWav(zynWav, zynSamples, 44100, 0.9f);
        WriteWav(zynTableWav, zynTableSamples, 44100, 0.9f);

        var report = new List<string>
        {
            "Zyn PAD reference renderer",
            $"renderer: {renderer}",
            $"input: {input}",
            $"stdout: {reference.Stdout.Trim()}",
            $"table_stdout: {tableReference.Stdout.Trim()}",
            $"zyn_samples: {zynSamples.Length}",
            $"zyn_peak: {Peak(zynSamples):0.######}",
            $"zyn_rms: {Rms(zynSamples):0.######}",
            $"zyn_table_samples: {zynTableSamples.Length}",
            $"zyn_table_peak: {Peak(zynTableSamples):0.######}",
            $"zyn_table_rms: {Rms(zynTableSamples):0.######}"
        };

        var aquaExport = FaustEmitter.EmitScript(BuiltInScripts.ZynStylePadTexture, new FaustExportOptions("zyn_pad_texture"));
        var aquaRender = await FaustCompiler.RenderAsync(aquaExport.Source, new FaustRenderOptions(DurationSeconds: 1.5f));
        if (aquaRender is { Samples.Length: > 0 })
        {
            WriteWav(aquaWav, aquaRender.Samples, aquaRender.SampleRate, 0.9f);
            var comparison = AudioAnalyzer.CompareAudio(zynSamples, aquaRender.Samples);
            report.Add($"aqua_samples: {aquaRender.Samples.Length}");
            report.Add($"log_mel_distance: {comparison.LogMelDistance:0.######}");
            report.Add($"envelope_distance: {comparison.EnvelopeDistance:0.######}");
            report.Add($"rms_ratio: {comparison.RmsRatio:0.######}");
            report.Add($"centroid_ratio: {comparison.CentroidRatio:0.######}");
            report.Add($"score: {comparison.Score:0.######}");
        }
        else
        {
            report.Add("aqua_render: skipped; Faust renderer unavailable or failed");
        }

        await File.WriteAllLinesAsync(reportPath, report);
    }

    [Fact]
    public async Task ZynPadReferenceRendererSurveysProjectAuthoredPadFixturesWhenBuilt()
    {
        var root = RepositoryRoot();
        var bash = @"C:\msys64\usr\bin\bash.exe";
        var renderer = Path.Combine(root, "artifacts", "zyn-reference-build-msys", "src", "Tests", "ZynPadReference.exe");
        if (!File.Exists(bash) || !File.Exists(renderer))
        {
            return;
        }

        var fixtureDir = Path.Combine(root, "tests", "AquariumSynth.Dsl.Tests", "Fixtures", "ZynAddSubFX", "ProjectAuthored");
        var artifactDir = Path.Combine(root, "artifacts", "parity", "zyn-pad-fixtures");
        Directory.CreateDirectory(artifactDir);

        var report = new List<string>
        {
            "Zyn PAD fixture survey",
            $"renderer: {renderer}"
        };

        foreach (var input in Directory.GetFiles(fixtureDir, "pad-*.xiz").Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(input);
            var instrument = ZynInstrumentReader.ParseFile(input);
            Assert.Contains(instrument.KitItems, item => item.Enabled && item.Engines.Contains(ZynEngine.PadSynth));

            var noteRaw = Path.Combine(artifactDir, $"{name}-zyn.f32");
            var noteWav = Path.Combine(artifactDir, $"{name}-zyn.wav");
            var tableRaw = Path.Combine(artifactDir, $"{name}-table0.f32");
            var tableWav = Path.Combine(artifactDir, $"{name}-table0.wav");

            var note = await RunAsync(bash, ["-lc", ZynPadCommand(root, input, noteRaw, 0, "note", "261.6256", "1.5")]);
            Assert.Equal(0, note.ExitCode);
            var table = await RunAsync(bash, ["-lc", ZynPadCommand(root, input, tableRaw, 0, "0")]);
            Assert.Equal(0, table.ExitCode);

            var noteSamples = await ReadFloat32Async(noteRaw);
            var tableSamples = await ReadFloat32Async(tableRaw);
            Assert.True(noteSamples.Length > 0);
            Assert.True(tableSamples.Length > noteSamples.Length);
            WriteWav(noteWav, noteSamples, 44100, 0.9f);
            WriteWav(tableWav, tableSamples, 44100, 0.9f);

            var features = AudioAnalyzer.AnalyzeAudio(noteSamples).Features;
            report.Add("");
            report.Add($"fixture: {name}");
            report.Add($"note_stdout: {note.Stdout.Trim()}");
            report.Add($"table_stdout: {table.Stdout.Trim()}");
            report.Add($"peak: {Peak(noteSamples):0.######}");
            report.Add($"rms: {Rms(noteSamples):0.######}");
            report.Add($"duration: {features.DurationSeconds:0.######}");
            report.Add($"centroid: {features.SpectralCentroidHz:0.######}");
            report.Add($"table_peak: {Peak(tableSamples):0.######}");
            report.Add($"table_rms: {Rms(tableSamples):0.######}");
        }

        await File.WriteAllLinesAsync(Path.Combine(artifactDir, "report.txt"), report);
    }

    [Fact]
    public async Task ZynPadReferenceRendererSurveysUpstreamGplPadFixturesWhenBuilt()
    {
        var root = RepositoryRoot();
        var bash = @"C:\msys64\usr\bin\bash.exe";
        var renderer = Path.Combine(root, "artifacts", "zyn-reference-build-msys", "src", "Tests", "ZynPadReference.exe");
        var bankRoot = Path.Combine(root, "external", "zynaddsubfx", "instruments", "banks");
        if (!File.Exists(bash) || !File.Exists(renderer) || !Directory.Exists(bankRoot))
        {
            return;
        }

        var selected = new[]
        {
            Path.Combine("Pads", "0002-sin2x  pad.xiz"),
            Path.Combine("Pads", "0065-Soft Pad.xiz"),
            Path.Combine("Dual", "0008-Organ Choir Pad2.xiz"),
            Path.Combine("Laba170bank", "0098-DoublePadBass.xiz"),
            Path.Combine("Companion", "0121-Ghost Ensemble.xiz")
        };
        var artifactDir = Path.Combine(root, "artifacts", "parity", "zyn-upstream-pad-fixtures");
        Directory.CreateDirectory(artifactDir);

        var report = new List<string>
        {
            "Zyn upstream GPL PAD fixture survey",
            "source: https://github.com/zynaddsubfx/instruments",
            "license: GPL test/development corpus via upstream ZynAddSubFX instruments submodule",
            $"renderer: {renderer}"
        };

        foreach (var relative in selected)
        {
            var input = Path.Combine(bankRoot, relative);
            if (!File.Exists(input))
            {
                continue;
            }

            var instrument = ZynInstrumentReader.ParseFile(input);
            var kitIndex = instrument.KitItems.FirstOrDefault(item => item.Enabled && item.Engines.Contains(ZynEngine.PadSynth))?.Id ?? -1;
            if (kitIndex < 0)
            {
                continue;
            }

            var name = SanitizeArtifactName(Path.GetFileNameWithoutExtension(input));
            var noteRaw = Path.Combine(artifactDir, $"{name}-zyn.f32");
            var noteWav = Path.Combine(artifactDir, $"{name}-zyn.wav");
            var tableRaw = Path.Combine(artifactDir, $"{name}-table0.f32");
            var tableWav = Path.Combine(artifactDir, $"{name}-table0.wav");

            var note = await RunAsync(bash, ["-lc", ZynPadCommand(root, input, noteRaw, kitIndex, "note", "261.6256", "1.5")]);
            Assert.Equal(0, note.ExitCode);
            var table = await RunAsync(bash, ["-lc", ZynPadCommand(root, input, tableRaw, kitIndex, "0")]);
            Assert.Equal(0, table.ExitCode);

            var noteSamples = await ReadFloat32Async(noteRaw);
            var tableSamples = await ReadFloat32Async(tableRaw);
            Assert.True(noteSamples.Length > 0);
            Assert.True(tableSamples.Length > noteSamples.Length);
            WriteWav(noteWav, noteSamples, 44100, 0.9f);
            WriteWav(tableWav, tableSamples, 44100, 0.9f);

            var features = AudioAnalyzer.AnalyzeAudio(noteSamples).Features;
            report.Add("");
            report.Add($"fixture: {relative}");
            report.Add($"instrument: {instrument.Name}");
            report.Add($"kit_index: {kitIndex}");
            report.Add($"note_stdout: {note.Stdout.Trim()}");
            report.Add($"table_stdout: {table.Stdout.Trim()}");
            report.Add($"peak: {Peak(noteSamples):0.######}");
            report.Add($"rms: {Rms(noteSamples):0.######}");
            report.Add($"duration: {features.DurationSeconds:0.######}");
            report.Add($"centroid: {features.SpectralCentroidHz:0.######}");
            report.Add($"table_peak: {Peak(tableSamples):0.######}");
            report.Add($"table_rms: {Rms(tableSamples):0.######}");
        }

        await File.WriteAllLinesAsync(Path.Combine(artifactDir, "report.txt"), report);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AquariumSynthCSharp.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("could not find repository root");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"failed to start `{fileName}`");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private static string ToMsysPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length < 3 || full[1] != ':' || full[2] != Path.DirectorySeparatorChar)
        {
            throw new ArgumentException($"cannot convert path to MSYS form: {path}", nameof(path));
        }

        return "/" + char.ToLowerInvariant(full[0]) + full[2..].Replace('\\', '/');
    }

    private static string BashQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string ZynPadCommand(string root, string input, string output, int kitIndex, params string[] modeArguments) =>
        string.Join(' ', [
            "export PATH=/mingw64/bin:/usr/bin:/bin:$PATH;",
            $"cd {BashQuote(ToMsysPath(root))};",
            "./artifacts/zyn-reference-build-msys/src/Tests/ZynPadReference.exe",
            BashQuote(ToMsysPath(input)),
            BashQuote(ToMsysPath(output)),
            kitIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ..modeArguments
        ]);

    private static string SanitizeArtifactName(string name)
    {
        var chars = name.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task<float[]> ReadFloat32Async(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var samples = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * sizeof(float));
        return samples;
    }

    private static float Peak(IReadOnlyList<float> samples) =>
        samples.Select(Math.Abs).DefaultIfEmpty(0).Max();

    private static float Rms(IReadOnlyList<float> samples)
    {
        if (samples.Count == 0) return 0;
        var sum = 0.0;
        foreach (var sample in samples) sum += sample * sample;
        return (float)Math.Sqrt(sum / samples.Count);
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
            writer.Write((short)Math.Clamp(sample * scale * short.MaxValue, short.MinValue, short.MaxValue));
        }
    }
}

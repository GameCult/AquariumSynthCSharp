using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace AquariumSynth.Dsl;

public sealed record FaustExportOptions(string Name = "aquarium_patch", bool Stereo = false);

public sealed record FaustExport(string Source, IReadOnlyList<string> Warnings);

public enum FaustTargetLanguage
{
    C,
    Cpp,
    CSharp,
    Rust
}

public sealed record FaustCompileOptions(FaustTargetLanguage Language, string OutputPath);

public sealed record FaustValidation(string Command, bool Success, int? StatusCode, string Stdout, string Stderr);

public static class FaustEmitter
{
    public static FaustExport EmitScript(string script, FaustExportOptions? options = null) =>
        Emit(PatchScript.Parse(script), options ?? new FaustExportOptions());

    public static FaustExport Emit(SynthPatch patch, FaustExportOptions? options = null)
    {
        options ??= new FaustExportOptions();
        if (patch.Voices.Count == 0) throw new ArgumentException("cannot export an empty patch", nameof(patch));

        var warnings = new List<string>();
        var source = new StringBuilder();
        source.AppendLine("import(\"stdfaust.lib\");");
        source.AppendLine($"declare name \"{Escape(options.Name)}\";");
        source.AppendLine("declare options \"[nvoices:1]\";");
        source.AppendLine();
        source.AppendLine("time = ba.time / ma.SR;");
        if (patch.Repeat is { } repeat)
        {
            source.AppendLine($"age = time - floor(time / {F(repeat.IntervalSeconds)}) * {F(repeat.IntervalSeconds)};");
        }
        else
        {
            source.AppendLine("age = time;");
        }
        source.AppendLine("clip01(x) = min(1.0, max(0.0, x));");
        source.AppendLine("wrap01(x) = x - floor(x);");
        source.AppendLine("softclip(x) = ma.tanh(x * 1.35);");
        source.AppendLine("fold(x) = 2.0 * abs(2.0 * (x / 4.0 - floor(x / 4.0)) - 1.0) - 1.0;");
        source.AppendLine("env(a,s,d,p) = select2(age < a, select2(age < a + s, select2(age < a + s + d, 0.0, 1.0 - (age - a - s) / max(0.0001, d)), 1.0 + (1.0 - (age - a) / max(0.0001, s)) * 2.0 * p), age / max(0.0001, a));");
        source.AppendLine("lfo_sin(hz, phase) = sin(2.0 * ma.PI * (age * hz + phase));");
        source.AppendLine("lfo_tri(hz, phase) = 1.0 - 4.0 * abs((age * hz + phase - floor(age * hz + phase)) - 0.5);");
        source.AppendLine("lfo_sq(hz, phase) = select2((age * hz + phase - floor(age * hz + phase)) < 0.5, -1.0, 1.0);");
        source.AppendLine("lfo_hold(hz, phase) = no.noise : ba.latch(os.oscrs(hz));");
        source.AppendLine();

        EmitParameterControls(source, patch, warnings);
        if (patch.Parameters.Count > 0)
        {
            source.AppendLine();
        }

        foreach (var (target, name) in ModTargets)
        {
            source.AppendLine($"patch_mod_{name} = {ModExpressionForTarget(patch.Controls, target)};");
        }
        source.AppendLine();

        var voices = new List<string>();
        for (var i = 0; i < patch.Voices.Count; i++)
        {
            var name = $"voice_{i}";
            EmitVoice(source, patch, patch.Voices[i], name, warnings);
            voices.Add(name);
        }

        var final = $"({string.Join(" + ", voices)}) * {F(patch.Gain)}";
        if (patch.SoftClip) final = $"softclip({final})";
        if (patch.Parameters.Count > 0)
        {
            final = $"({final}) + 0.0 * ({string.Join(" + ", patch.Parameters.Select((_, i) => ParameterIdentifier(i)))})";
        }
        source.AppendLine(options.Stereo ? $"process = {final} <: _,_;" : $"process = {final};");
        return new FaustExport(source.ToString(), warnings);
    }

    private static void EmitParameterControls(StringBuilder source, SynthPatch patch, List<string> warnings)
    {
        for (var i = 0; i < patch.Parameters.Count; i++)
        {
            var parameter = patch.Parameters[i];
            warnings.Add($"parameter {parameter.Path}: declared as a runtime control; target binding is not implemented yet");
            source.AppendLine($"{ParameterIdentifier(i)} = hslider(\"{Escape(parameter.Path)}\", {F(parameter.Default)}, {F(parameter.Min)}, {F(parameter.Max)}, {F(parameter.Step)}) : si.smoo;");
        }
    }

    private static void EmitVoice(StringBuilder source, SynthPatch patch, Voice voice, string name, List<string> warnings)
    {
        if (voice.Filter.LowPassResonance != 0)
        {
            warnings.Add($"{name}: low-pass resonance is approximated with Faust resonlp");
        }

        var pitch = ModExpressionForTarget(voice.Modulators, ModTarget.Pitch);
        var duty = ModExpressionForTarget(voice.Modulators, ModTarget.Duty);
        var gain = ModExpressionForTarget(voice.Modulators, ModTarget.Gain);
        var noise = ModExpressionForTarget(voice.Modulators, ModTarget.Noise);
        var drive = ModExpressionForTarget(voice.Modulators, ModTarget.Drive);
        var fold = ModExpressionForTarget(voice.Modulators, ModTarget.Fold);
        var fmIndexMod = ModExpressionForTarget(voice.Modulators, ModTarget.FmIndex);
        var formant = ModExpressionForTarget(voice.Modulators, ModTarget.FormantMix);
        var lpfMod = ModExpressionForTarget(voice.Modulators, ModTarget.LowPass);
        var hpfMod = ModExpressionForTarget(voice.Modulators, ModTarget.HighPass);

        var baseFreq = $"max({F(voice.Pitch.MinFrequencyHz)}, {F(voice.Oscillator.FrequencyHz)} * pow(2.0, {F(voice.Pitch.RampPerSecond)} * age + 0.5 * {F(voice.Pitch.DeltaRampPerSecond)} * age * age))";
        var vibrato = voice.Pitch.VibratoDepth != 0 && voice.Pitch.VibratoHz > 0
            ? $" * (1.0 + select2(age < {F(voice.Pitch.VibratoDelaySeconds)}, 0.0, sin(2.0 * ma.PI * (age - {F(voice.Pitch.VibratoDelaySeconds)}) * {F(voice.Pitch.VibratoHz)}) * {F(voice.Pitch.VibratoDepth)}))"
            : "";
        var arpeggio = voice.Arpeggio is null
            ? "1.0"
            : $"select2(age < {F(voice.Arpeggio.DelaySeconds)}, {F(voice.Arpeggio.Multiplier)}, 1.0)";
        var frequency = $"(({baseFreq}){vibrato}) * {arpeggio} * pow(2.0, patch_mod_pitch + {pitch})";
        var dutyExpression = $"clip01({F(voice.Oscillator.Duty)} + {F(voice.Duty.RampPerSecond)} * age + patch_mod_duty + {duty})";
        var fmIndex = $"max(0.0, {F(voice.Fm.Index)} + patch_mod_fm_index + {fmIndexMod}) * {FmDecay(voice.Fm.IndexDecaySeconds)}";
        var oscillator = OscillatorExpression(patch, voice, frequency, dutyExpression, fmIndex);
        var envelope = $"env({F(voice.Envelope.AttackSeconds)}, {F(voice.Envelope.SustainSeconds)}, {F(voice.Envelope.DecaySeconds)}, {F(voice.Envelope.Punch)})";
        var tremolo = voice.Color.TremoloDepth > 0 && voice.Color.TremoloHz > 0
            ? $" * (1.0 - {F(Math.Clamp(voice.Color.TremoloDepth, 0, 1))} * (0.5 + 0.5 * lfo_sin({F(voice.Color.TremoloHz)}, 0.0)))"
            : "";
        var noiseMix = $"clip01({F(voice.Color.NoiseMix)} + patch_mod_noise + {noise})";
        var driveExpression = $"clip01({F(voice.Color.Drive)} + patch_mod_drive + {drive})";
        var foldExpression = $"clip01({F(voice.Color.Fold)} + patch_mod_fold + {fold})";
        var formantMix = $"clip01({F(voice.Color.FormantMix)} + patch_mod_formant_mix + {formant})";
        var lpf = $"clip01({F(voice.Filter.LowPass)} * (1.0 + {F(voice.Filter.LowPassRamp)} * age * 1.8) + patch_mod_lpf + {lpfMod})";
        var hpf = $"clip01({F(voice.Filter.HighPass)} * (1.0 + {F(voice.Filter.HighPassRamp)} * age * 2.0) + patch_mod_hpf + {hpfMod})";
        var lowpass = voice.Filter.LowPassResonance > 0
            ? $"fi.resonlp(max(20.0, {lpf} * 18000.0), {F(0.7f + Math.Clamp(voice.Filter.LowPassResonance, 0, 1) * 18)}, 1.0)"
            : $"fi.lowpass(1, max(20.0, {lpf} * 18000.0))";

        source.AppendLine($"{name}_freq = {frequency};");
        source.AppendLine($"{name}_osc = {oscillator};");
        source.AppendLine($"{name}_colored = ({name}_osc * (1.0 - {noiseMix}) + no.noise * {noiseMix});");
        source.AppendLine($"{name}_driven = ma.tanh({name}_colored * (1.0 + {driveExpression} * 12.0)) / ma.tanh(1.0 + {driveExpression} * 12.0);");
        source.AppendLine($"{name}_folded = {name}_driven * (1.0 - {foldExpression}) + fold({name}_driven * (1.0 + {foldExpression} * 3.5)) * {foldExpression};");
        source.AppendLine($"{name}_filtered = {name}_folded : {lowpass} : fi.highpass(1, max(5.0, ({hpf}) * ({hpf}) * 7000.0));");
        if (voice.Phaser.OffsetSeconds != 0 || voice.Phaser.RampSecondsPerSecond != 0)
        {
            var delay = $"min(2047.0, max(0.0, abs({F(voice.Phaser.OffsetSeconds)} + {F(voice.Phaser.RampSecondsPerSecond)} * age) * ma.SR))";
            source.AppendLine($"{name}_phased = {name}_filtered + ({name}_filtered : de.fdelay(2048, {delay}));");
        }
        else
        {
            source.AppendLine($"{name}_phased = {name}_filtered;");
        }
        source.AppendLine($"{name}_formants = {FormantExpression(name, voice)};");
        source.AppendLine($"{name} = (({name}_phased * (1.0 - {formantMix}) + {name}_formants * {formantMix}) * {envelope}{tremolo} * max(0.0, 1.0 + patch_mod_gain + {gain}) * {F(voice.Gain)});");
        source.AppendLine();
    }

    private static string OscillatorExpression(SynthPatch patch, Voice voice, string frequency, string duty, string fmIndex)
    {
        var hasFmMod = patch.Controls.Any(control => control.Modulator.Target == ModTarget.FmIndex) ||
                       voice.Modulators.Any(modulator => modulator.Target == ModTarget.FmIndex);
        var phaseMod = voice.Fm.Index > 0 || voice.Fm.IndexDecaySeconds > 0 || hasFmMod
            ? $" + sin(2.0 * ma.PI * os.phasor(1.0, {frequency} * {F(Math.Max(voice.Fm.Ratio, 0))})) * ({fmIndex}) / ma.PI"
            : "";
        var phase = $"wrap01(os.phasor(1.0, {frequency}) + {F(voice.Oscillator.Phase)}{phaseMod})";
        return voice.Oscillator.Waveform switch
        {
            Waveform.Sine => $"sin(2.0 * ma.PI * {phase})",
            Waveform.Square => $"select2({phase} < {duty}, -0.5, 0.5)",
            Waveform.Sawtooth => $"2.0 * {phase} - 1.0",
            Waveform.Triangle => $"1.0 - 4.0 * abs({phase} - 0.5)",
            Waveform.Noise => "no.noise",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string FormantExpression(string name, Voice voice)
    {
        if (voice.Formants.Count == 0) return $"{name}_phased";
        var gainSum = Math.Max(voice.Formants.Sum(formant => Math.Abs(formant.Gain)), 0.001f);
        var parts = voice.Formants.Select(formant =>
        {
            var q = Math.Clamp(formant.FrequencyHz / Math.Max(formant.BandwidthHz, 10), 0.2f, 40);
            return $"({name}_phased : fi.resonbp({F(formant.FrequencyHz)}, {F(q)}, 1.0)) * {F(formant.Gain)}";
        });
        return $"({string.Join(" + ", parts)}) / {F(gainSum)}";
    }

    private static string ModExpressionForTarget(IEnumerable<ControlLane> controls, ModTarget target)
    {
        var expressions = controls.Where(control => control.Modulator.Target == target)
            .Select(control => ModExpression(control.Modulator))
            .ToList();
        return expressions.Count == 0 ? "0.0" : string.Join(" + ", expressions);
    }

    private static string ModExpressionForTarget(IEnumerable<Modulator> modulators, ModTarget target)
    {
        var expressions = modulators.Where(modulator => modulator.Target == target)
            .Select(ModExpression)
            .ToList();
        return expressions.Count == 0 ? "0.0" : string.Join(" + ", expressions);
    }

    private static string ModExpression(Modulator modulator)
    {
        var wave = modulator.Waveform switch
        {
            ModWaveform.Sine => "lfo_sin",
            ModWaveform.Triangle => "lfo_tri",
            ModWaveform.Square => "lfo_sq",
            ModWaveform.SampleHold => "lfo_hold",
            _ => throw new ArgumentOutOfRangeException()
        };
        return $"{F(modulator.Bias)} + {F(modulator.Depth)} * {wave}({F(modulator.FrequencyHz)}, {F(modulator.Phase)})";
    }

    private static readonly (ModTarget Target, string Name)[] ModTargets =
    [
        (ModTarget.Gain, "gain"),
        (ModTarget.Pitch, "pitch"),
        (ModTarget.Duty, "duty"),
        (ModTarget.LowPass, "lpf"),
        (ModTarget.HighPass, "hpf"),
        (ModTarget.Noise, "noise"),
        (ModTarget.Drive, "drive"),
        (ModTarget.Fold, "fold"),
        (ModTarget.FormantMix, "formant_mix"),
        (ModTarget.FmIndex, "fm_index")
    ];

    private static string F(float value) =>
        float.IsFinite(value) ? value.ToString("0.########", CultureInfo.InvariantCulture) : "0.0";

    private static string F(double value) =>
        double.IsFinite(value) ? value.ToString("0.########", CultureInfo.InvariantCulture) : "0.0";

    private static string FmDecay(float seconds) => seconds > 0 ? $"exp(-age / {F(Math.Max(seconds, 0.0001f))})" : "1.0";

    private static string ParameterIdentifier(int index) => $"patch_param_{index}";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public static class FaustCompiler
{
    public static string? FindFaust()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var names = OperatingSystem.IsWindows() ? new[] { "faust.exe", "faust" } : ["faust"];
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }

        var windowsDefault = @"C:\Program Files\Faust\bin\faust.exe";
        return File.Exists(windowsDefault) ? windowsDefault : null;
    }

    public static async Task<FaustValidation?> CompileAsync(
        string source,
        FaustCompileOptions options,
        string? faustPath = null,
        CancellationToken cancellationToken = default)
    {
        faustPath ??= FindFaust();
        if (faustPath is null) return null;

        var sourcePath = Path.Combine(Path.GetTempPath(), $"aquarium-synth-{Guid.NewGuid():N}.dsp");
        await File.WriteAllTextAsync(sourcePath, source, cancellationToken);
        try
        {
            var result = await RunAsync(
                faustPath,
                ["-lang", Language(options.Language), "-o", options.OutputPath, sourcePath],
                cancellationToken);
            return new FaustValidation(faustPath, result.ExitCode == 0, result.ExitCode, result.Stdout, result.Stderr);
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    public static async Task<FaustValidation?> ValidateAsync(
        string source,
        string? faustPath = null,
        CancellationToken cancellationToken = default)
    {
        var output = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        return await CompileAsync(source, new FaustCompileOptions(FaustTargetLanguage.Cpp, output), faustPath, cancellationToken);
    }

    private static string Language(FaustTargetLanguage language) => language switch
    {
        FaustTargetLanguage.C => "c",
        FaustTargetLanguage.Cpp => "cpp",
        FaustTargetLanguage.CSharp => "csharp",
        FaustTargetLanguage.Rust => "rust",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
    };

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"failed to start `{fileName}`");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stdout, await stderr);
    }
}

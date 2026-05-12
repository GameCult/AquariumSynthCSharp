using AquariumSynth.Dsl;

namespace AquariumSynth.Dsl.Tests;

public sealed class PatchScriptTests
{
    private const string WobbleTalker =
        "d w=saw f=55 g=.18 s=.8 d=.25 l=.34 h=.02 drv=.3 fl=.08 fm=2 fmi=.8 fmd=.7 fs=520:90:.7,1250:170:1,2600:320:.45 fmix=.35;" +
        "mod n=wob hz=4 w=tri g=.42 l=.48 fmix=.38 fmi=1.6 drv=.2 fl=.14;" +
        "v;" +
        "v f=110 g=.08 du=.42";

    [Fact]
    public void ParserExpandsDefaultsModBusAndVoices()
    {
        var patch = PatchScript.Parse(WobbleTalker);

        Assert.Equal(2, patch.Voices.Count);
        Assert.Equal(6, patch.Controls.Count);
        Assert.Equal(Waveform.Sawtooth, patch.Voices[0].Oscillator.Waveform);
        Assert.Equal(3, patch.Voices[0].Formants.Count);
    }

    [Fact]
    public void ParserSupportsRouteListModBus()
    {
        var patch = PatchScript.Parse("bus n=sway w=tri hz=2 to=g:.12,l:-.2,fmix:.35,fmi:1.4;v w=saw f=80");

        Assert.Equal(4, patch.Controls.Count);
        Assert.Contains(patch.Controls, lane => lane.Name == "sway_gain" && lane.Modulator.Target == ModTarget.Gain);
        Assert.Contains(patch.Controls, lane => lane.Name == "sway_lpf" && lane.Modulator.Target == ModTarget.LowPass);
        Assert.Contains(patch.Controls, lane => lane.Name == "sway_formant_mix" && lane.Modulator.Target == ModTarget.FormantMix);
        Assert.Contains(patch.Controls, lane => lane.Name == "sway_fm_index" && lane.Modulator.Target == ModTarget.FmIndex);
    }

    [Fact]
    public void ParserFoldsFieldOnlyLinesIntoPreviousCommand()
    {
        var patch = PatchScript.Parse("""
            voice
                wave=square
                freq=80
                gain=0.2
                sustain=0.1
                decay=0.2
            """);

        var voice = Assert.Single(patch.Voices);
        Assert.Equal(Waveform.Square, voice.Oscillator.Waveform);
        Assert.Equal(80, voice.Oscillator.FrequencyHz);
        Assert.Equal(0.2f, voice.Gain, 5);
    }

    [Fact]
    public void ParserSupportsExplicitControlLane()
    {
        var patch = PatchScript.Parse("lfo n=wob t=p w=sin hz=6 d=.04 ph=.25 b=.01;v w=saw f=80");

        var lane = Assert.Single(patch.Controls);
        Assert.Equal("wob", lane.Name);
        Assert.Equal(ModTarget.Pitch, lane.Modulator.Target);
        Assert.Equal(6, lane.Modulator.FrequencyHz);
        Assert.Equal(.04f, lane.Modulator.Depth, 5);
        Assert.Equal(.25f, lane.Modulator.Phase, 5);
        Assert.Equal(.01f, lane.Modulator.Bias, 5);
    }

    [Fact]
    public void BuiltInExampleParsesAndExportsFaust()
    {
        var patch = PatchScript.Parse(BuiltInScripts.PatchScriptExample);
        var export = FaustEmitter.Emit(patch);

        Assert.Equal(3, patch.Voices.Count);
        Assert.Equal(5, patch.Controls.Count);
        Assert.Contains("patch_mod_formant_mix", export.Source);
    }

    [Fact]
    public void SfxrAtomsAndMutationsParse()
    {
        var named = PatchScript.Parse("laser");
        var verbose = PatchScript.Parse("sfxr preset=laser mutate_seed=9 mutate=0.01");
        var golfed = PatchScript.Parse("s p=laser ms=9 m=0.01");

        Assert.Single(named.Voices);
        Assert.Single(verbose.Voices);
        Assert.Single(golfed.Voices);
        Assert.Equal(verbose.Voices[0].Oscillator.FrequencyHz, golfed.Voices[0].Oscillator.FrequencyHz);
    }

    [Fact]
    public void ScriptMetricsScoreTerseAndReadableInputs()
    {
        var terse = PatchScriptScoring.Measure("v w=sq f=80 g=.2 s=.1 d=.2");
        var readable = PatchScriptScoring.Measure("""
            voice wave=square freq=80 gain=0.2 sustain=0.1 decay=0.2
            """);

        Assert.True(terse.TerseScore > readable.TerseScore);
        Assert.True(readable.ReadabilityScore > terse.ReadabilityScore);
        Assert.InRange(terse.BalancedScore, 0, 1);
    }

    [Fact]
    public void AudioAnalyzerComparesSimpleBuffers()
    {
        var samples = Enumerable.Range(0, 2048)
            .Select(i => MathF.Sin(i * MathF.Tau * 440 / 44100) * 0.2f)
            .ToArray();

        var comparison = new AudioAnalyzer().Compare(samples, samples);

        Assert.True(comparison.Reference.Features.Peak > 0.19f);
        Assert.True(comparison.Score > 0.99f);
    }

    [Fact]
    public void AquariumPresetsExportFaust()
    {
        foreach (var patch in new[] { Presets.AquariumPluck(), Presets.AquariumHeartbeat(), Presets.AquariumVoice(), Presets.Sfxr("pickup") })
        {
            var export = FaustEmitter.Emit(patch);
            Assert.Contains("process =", export.Source);
        }
    }

    [Fact]
    public void ClassicAbstractGolfScriptParsesAndExportsFaust()
    {
        var patch = PatchScript.Parse(BuiltInScripts.ClassicSfxrAbstractGolfScript);
        var export = FaustEmitter.Emit(patch);

        Assert.Equal(7, patch.Voices.Count);
        Assert.Contains("process =", export.Source);
    }

    [Fact]
    public void BuiltInReferenceScriptsParseAndExportFaust()
    {
        foreach (var (family, name, script) in BuiltInScripts.ReferenceScripts())
        {
            var exception = Record.Exception(() =>
            {
                var patch = PatchScript.Parse(script);
                var export = FaustEmitter.Emit(patch, new FaustExportOptions($"{family}_{name}".Replace('-', '_')));
                Assert.Contains("process =", export.Source);
            });

            Assert.Null(exception);
        }
    }

    [Fact]
    public void FaustEmitterProducesWobbleSource()
    {
        var export = FaustEmitter.EmitScript(WobbleTalker, new FaustExportOptions("wobble_talker", Stereo: true));

        Assert.Contains("declare name \"wobble_talker\";", export.Source);
        Assert.Contains("patch_mod_fm_index", export.Source);
        Assert.Contains("fi.resonbp", export.Source);
        Assert.Contains("process =", export.Source);
        Assert.Contains("<: _,_;", export.Source);
    }

    [Fact]
    public async Task FaustCompilerValidatesGeneratedSourceWhenInstalled()
    {
        var export = FaustEmitter.EmitScript(WobbleTalker);
        var validation = await FaustCompiler.ValidateAsync(export.Source);

        if (validation is null)
        {
            return;
        }

        Assert.True(validation.Success, validation.Stderr);
    }

    [Fact]
    public async Task FaustCompilerCanEmitCSharpWhenInstalled()
    {
        var export = FaustEmitter.EmitScript("v w=sin f=440 g=.2 s=.1 d=.2");
        var output = Path.Combine(Path.GetTempPath(), $"aquarium-synth-{Guid.NewGuid():N}.cs");
        try
        {
            var validation = await FaustCompiler.CompileAsync(
                export.Source,
                new FaustCompileOptions(FaustTargetLanguage.CSharp, output));

            if (validation is null)
            {
                return;
            }

            Assert.True(validation.Success, validation.Stderr);
            Assert.True(File.Exists(output));
            Assert.Contains("class", await File.ReadAllTextAsync(output));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }
}

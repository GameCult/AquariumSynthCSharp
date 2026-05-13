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
    public void ParserPreservesDeclaredPatchParameters()
    {
        var patch = PatchScript.Parse("param name=brightness path=/macro/brightness default=.45 min=0 max=1 step=.001 unit=normalized rate=control;v w=saw f=80");

        var parameter = Assert.Single(patch.Parameters);
        Assert.Equal("/macro/brightness", parameter.Path);
        Assert.Equal("brightness", parameter.Label);
        Assert.Equal(.45f, parameter.Default, 5);
        Assert.Equal(0, parameter.Min);
        Assert.Equal(1, parameter.Max);
        Assert.Equal(.001f, parameter.Step, 5);
        Assert.Equal("normalized", parameter.Unit);
        Assert.Equal("control", parameter.AutomationRate);
    }

    [Fact]
    public void ParserRejectsDuplicatePatchParameterPaths()
    {
        var exception = Assert.Throws<PatchScriptException>(() =>
            PatchScript.Parse("param path=/macro/brightness;param path=/macro/brightness;v w=sin"));

        Assert.Contains("duplicate parameter path", exception.Message);
    }

    [Fact]
    public void ParserBindsParameterReferencesAtFieldSites()
    {
        var patch = PatchScript.Parse("param path=/macro/brightness default=.45 min=0 max=1 step=.001;v w=saw f=80 lpf=@/macro/brightness");

        var binding = Assert.Single(patch.ParameterBindings);
        Assert.Equal("/voices/0/filter/lpf", binding.FieldPath);
        Assert.Equal("/macro/brightness", binding.ParameterPath);
        Assert.Equal(.45f, patch.Voices[0].Filter.LowPass, 5);
    }

    [Fact]
    public void ParserRejectsUnknownParameterReferences()
    {
        var exception = Assert.Throws<PatchScriptException>(() =>
            PatchScript.Parse("v w=saw f=80 lpf=@/macro/brightness"));

        Assert.Contains("unknown parameter `/macro/brightness`", exception.Message);
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
    public void AdvancedReferenceScriptsExerciseLayeredPatchFeatures()
    {
        foreach (var (name, script) in BuiltInScripts.AdvancedReferenceScripts)
        {
            var patch = PatchScript.Parse(script);

            Assert.True(patch.Voices.Count >= 4, $"{name} should demonstrate layered voices.");
            Assert.True(patch.Controls.Count >= 1, $"{name} should demonstrate modulation.");
            Assert.Contains(patch.Voices, voice => voice.Fm.Index > 0 || voice.Formants.Count > 0 || voice.Color.NoiseMix > 0);
        }
    }

    [Fact]
    public void Dx7StyleReferenceRebuildsParseExportAndDeclarePressure()
    {
        foreach (var rebuild in ReferenceRebuildCatalog.Dx7Rebuilds)
        {
            var patch = PatchScript.Parse(rebuild.Script);
            var export = FaustEmitter.Emit(patch, new FaustExportOptions(rebuild.Id.Replace('/', '_').Replace('-', '_')));

            Assert.Contains("process =", export.Source);
            Assert.NotEmpty(rebuild.MatchedFeatures);
            Assert.NotEmpty(rebuild.MissingFeatures);
            Assert.All(rebuild.MissingFeatures, feature => Assert.False(string.IsNullOrWhiteSpace(feature.Notes)));
        }
    }

    [Fact]
    public void Dx7Algorithm32RebuildMatchesAdditiveCarrierShape()
    {
        var rebuild = ReferenceRebuildCatalog.Dx7Rebuilds.Single(item => item.ReferenceId == "dx7/algo32-additive-organ");
        var patch = PatchScript.Parse(rebuild.Script);
        var topology = Dx7SysEx.AlgorithmTopology(32);

        Assert.Equal(topology.CarrierOperators.Count, patch.Voices.Count);
        Assert.Contains(rebuild.MatchedFeatures, feature => feature.Name == "carrier_operators" && feature.Value == "1,2,3,4,5,6");
        Assert.Contains(rebuild.MatchedFeatures, feature => feature.Name == "modulation_edge_count" && feature.Value == "0");
        Assert.Equal(2, patch.Parameters.Count);
        Assert.Equal(2, patch.ParameterBindings.Count);
    }

    [Fact]
    public void Dx7Algorithm8RebuildRecordsMissingOperatorGraph()
    {
        var rebuild = ReferenceRebuildCatalog.Dx7Rebuilds.Single(item => item.ReferenceId == "dx7/algo8-bright-pair");
        var patch = PatchScript.Parse(rebuild.Script);
        var topology = Dx7SysEx.AlgorithmTopology(8);

        Assert.Equal(2, topology.CarrierOperators.Count);
        var graph = Assert.Single(patch.OperatorGraphs);
        Assert.Equal([1, 3], graph.Carriers);
        Assert.Equal(6, graph.Operators.Count);
        Assert.Contains(graph.Edges, edge => edge.SourceId == 6 && edge.TargetId == 5);
        Assert.Contains(graph.Edges, edge => edge.SourceId == 5 && edge.TargetId == 3);
        Assert.Contains(graph.Edges, edge => edge.SourceId == 4 && edge.TargetId == 3);
        Assert.Contains(graph.Edges, edge => edge.SourceId == 2 && edge.TargetId == 1);
        Assert.Contains(graph.Operators, op => op.Id == 4 && op.Feedback > 0);
        Assert.Contains(rebuild.MatchedFeatures, feature => feature.Name == "modulation_edges");
        Assert.Contains(rebuild.MissingFeatures, feature => feature.Name == "dx7_feedback_register");
        Assert.Equal(2, patch.Parameters.Count);
        Assert.Equal(3, patch.ParameterBindings.Count);
        Assert.Contains(patch.ParameterBindings, binding => binding.FieldPath == "/voices/0/fm/index");
        Assert.Contains(patch.ParameterBindings, binding => binding.FieldPath == "/voices/0/env/release");
        Assert.Contains(patch.ParameterBindings, binding => binding.FieldPath == "/voices/1/env/release");
    }

    [Fact]
    public void OperatorGraphScriptParsesAndExportsFaust()
    {
        var patch = PatchScript.Parse("opgraph name=pair freq=220 gain=.2 carriers=1 ops=2:2:.8,1:1:1 edges=2>1:1.4");
        var graph = Assert.Single(patch.OperatorGraphs);
        var export = FaustEmitter.Emit(patch);

        Assert.Equal("pair", graph.Name);
        Assert.Equal(2, graph.Operators.Count);
        Assert.Single(graph.Edges);
        Assert.Empty(patch.Voices);
        Assert.Contains("opgraph_0_op_2", export.Source);
        Assert.Contains("opgraph_0_op_1", export.Source);
        Assert.Contains("opgraph_0 = (opgraph_0_op_1) * 0.2;", export.Source);
    }

    [Fact]
    public void ReadableOperatorGraphSyntaxParsesRoutesCarriersAndEnvelopes()
    {
        var patch = PatchScript.Parse("""
            opgraph name=pair freq=220 gain=.2
            operator name=op2 ratio=2 level=.8 env=ad:.01:.2
            operator name=op1 ratio=1 level=1 env=adsr:.02:.1:.65:.3
            route from=op2 to=op1 index=1.4
            carrier name=op1
            """);

        var graph = Assert.Single(patch.OperatorGraphs);
        var op2 = graph.Operators.Single(op => op.Id == 2);
        var op1 = graph.Operators.Single(op => op.Id == 1);

        Assert.Equal("pair", graph.Name);
        Assert.Equal([1], graph.Carriers);
        Assert.Single(graph.Edges);
        Assert.Equal(.01f, op2.Envelope.AttackSeconds, 5);
        Assert.Equal(.2f, op2.Envelope.DecaySeconds, 5);
        Assert.Equal(.02f, op1.Envelope.AttackSeconds, 5);
        Assert.Equal(.1f, op1.Envelope.DecaySeconds, 5);
        Assert.Equal(.65f, op1.Envelope.SustainLevel, 5);
        Assert.Equal(.3f, op1.Envelope.ReleaseSeconds, 5);
        Assert.Equal(.21f, op2.Note.GateSeconds, 5);
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
    public void FaustEmitterExposesDeclaredPatchParametersAsControls()
    {
        var export = FaustEmitter.EmitScript("param path=/macro/brightness default=.45 min=0 max=1 step=.001;v w=saw f=80");

        Assert.Contains("patch_param_0 = hslider(\"/macro/brightness\", 0.45, 0, 1, 0.001) : si.smoo;", export.Source);
        Assert.Contains("patch_param_0", export.Source);
        Assert.Contains("declared but not bound", Assert.Single(export.Warnings));
    }

    [Fact]
    public void FaustEmitterUsesParameterReferencesAtBoundFieldSites()
    {
        var export = FaustEmitter.EmitScript("param path=/macro/brightness default=.45 min=0 max=1 step=.001;v w=saw f=80 lpf=@/macro/brightness");

        Assert.Contains("patch_param_0 = hslider(\"/macro/brightness\", 0.45, 0, 1, 0.001) : si.smoo;", export.Source);
        Assert.Contains("clip01(patch_param_0 * (1.0 + 0 * age * 1.8) + patch_mod_lpf + 0.0)", export.Source);
        Assert.Empty(export.Warnings);
    }

    [Fact]
    public void VoiceEnvelopeUsesStandardAdsrAndNoteGate()
    {
        var patch = PatchScript.Parse("v w=saw f=220 gate=.4 attack=.01 env_decay=.08 sustain_level=.6 release=.3");
        var voice = Assert.Single(patch.Voices);
        var export = FaustEmitter.Emit(patch);

        Assert.Equal(220, voice.Note.FrequencyHz);
        Assert.Equal(.4f, voice.Note.GateSeconds, 5);
        Assert.Equal(.01f, voice.Envelope.AttackSeconds, 5);
        Assert.Equal(.08f, voice.Envelope.DecaySeconds, 5);
        Assert.Equal(.6f, voice.Envelope.SustainLevel, 5);
        Assert.Equal(.3f, voice.Envelope.ReleaseSeconds, 5);
        Assert.Contains("oneshot_adsr", export.Source);
    }

    [Fact]
    public void LegacyPunchMapsToTransientPeakOverSustain()
    {
        var patch = PatchScript.Parse("v w=saw f=220 gain=.2 punch=.5");
        var voice = Assert.Single(patch.Voices);

        Assert.Equal(.3f, voice.Gain, 5);
        Assert.Equal(2f / 3f, voice.Envelope.SustainLevel, 5);
    }

    [Fact]
    public void MidiVoiceEmitsHostNoteControls()
    {
        var patch = PatchScript.Parse("v w=saw f=220 midi=true attack=.01 env_decay=.08 sustain_level=.6 release=.3");
        var export = FaustEmitter.Emit(patch);

        Assert.Equal(NoteSource.Host, patch.Voices[0].Note.Source);
        Assert.Equal(PlaybackMode.Mono, patch.Playback.Mode);
        Assert.True(patch.Playback.Midi);
        Assert.Contains("declare options \"[midi:on][nvoices:1]\";", export.Source);
        Assert.Contains("freq = nentry(\"freq\", 220", export.Source);
        Assert.Contains("gain = nentry(\"gain\", 1", export.Source);
        Assert.Contains("gate = button(\"gate\")", export.Source);
        Assert.Contains("en.adsr", export.Source);
    }

    [Fact]
    public void PolyphonicPatchUsesFaustStandardMidiSurface()
    {
        var patch = PatchScript.Parse("instrument midi=true polyphony=8; v w=saw f=330 attack=.01 env_decay=.08 sustain_level=.6 release=.3");
        var export = FaustEmitter.Emit(patch);

        Assert.Equal(PlaybackMode.Poly, patch.Playback.Mode);
        Assert.Equal(8, patch.Playback.Voices);
        Assert.Contains("declare options \"[midi:on][nvoices:8]\";", export.Source);
        Assert.Contains("freq = nentry(\"freq\", 440", export.Source);
        Assert.Contains("gain = nentry(\"gain\", 1", export.Source);
        Assert.Contains("gate = button(\"gate\")", export.Source);
        Assert.DoesNotContain("/voices/0/note/frequency", export.Source);
        Assert.DoesNotContain("/voices/0/note/gate", export.Source);
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
    public async Task FaustCompilerValidatesParameterizedPatchWhenInstalled()
    {
        var export = FaustEmitter.EmitScript("param path=/macro/brightness default=.45 min=0 max=1 step=.001;v w=saw f=80 lpf=@/macro/brightness");
        var validation = await FaustCompiler.ValidateAsync(export.Source);

        if (validation is null)
        {
            return;
        }

        Assert.True(validation.Success, validation.Stderr);
    }

    [Fact]
    public async Task FaustCompilerValidatesMidiGatePatchWhenInstalled()
    {
        var export = FaustEmitter.EmitScript("instrument midi=true polyphony=8; v w=saw f=220 attack=.01 env_decay=.08 sustain_level=.6 release=.3");
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

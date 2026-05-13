using AquariumSynth.Dsl;

namespace AquariumSynth.Dsl.Tests;

public sealed class SfxrPatchReferenceTests
{
    [Fact]
    public void ClassicSfxrPrimitiveScriptsMatchCanonicalPatchMapping()
    {
        foreach (var (name, script) in BuiltInScripts.ClassicSfxrPrimitiveGolfScripts)
        {
            var expected = SfxrParams.Named(name)?.ToPatch()
                ?? throw new InvalidOperationException($"Missing canonical SFXR preset `{name}`.");
            var actual = PatchScript.Parse(script);

            AssertPatchClose(expected, actual, name);
        }
    }

    private static void AssertPatchClose(SynthPatch expected, SynthPatch actual, string context)
    {
        Assert.Equal(expected.SoftClip, actual.SoftClip);
        AssertClose(expected.Gain, actual.Gain, context, "patch.gain");
        AssertRepeatClose(expected.Repeat, actual.Repeat, context);
        Assert.Equal(expected.Controls.Count, actual.Controls.Count);
        Assert.Equal(expected.Voices.Count, actual.Voices.Count);

        for (var index = 0; index < expected.Voices.Count; index++)
        {
            AssertVoiceClose(expected.Voices[index], actual.Voices[index], $"{context}.voices[{index}]");
        }
    }

    private static void AssertVoiceClose(Voice expected, Voice actual, string context)
    {
        AssertOscillatorClose(expected.Oscillator, actual.Oscillator, context);
        AssertNoteClose(expected.Note, actual.Note, context);
        AssertEnvelopeClose(expected.Envelope, actual.Envelope, context);
        AssertPitchClose(expected.Pitch, actual.Pitch, context);
        AssertClose(expected.Duty.RampPerSecond, actual.Duty.RampPerSecond, context, "duty.ramp");
        AssertFilterClose(expected.Filter, actual.Filter, context);
        AssertClose(expected.Phaser.OffsetSeconds, actual.Phaser.OffsetSeconds, context, "phaser.offset");
        AssertClose(expected.Phaser.RampSecondsPerSecond, actual.Phaser.RampSecondsPerSecond, context, "phaser.ramp");
        AssertArpeggioClose(expected.Arpeggio, actual.Arpeggio, context);
        AssertFrequencyModulationClose(expected.Fm, actual.Fm, context);
        AssertVoiceColorClose(expected.Color, actual.Color, context);
        AssertClose(expected.Gain, actual.Gain, context, "gain");
        Assert.Equal(expected.Formants.Count, actual.Formants.Count);
        Assert.Equal(expected.Modulators.Count, actual.Modulators.Count);
    }

    private static void AssertOscillatorClose(Oscillator expected, Oscillator actual, string context)
    {
        Assert.Equal(expected.Waveform, actual.Waveform);
        AssertClose(expected.FrequencyHz, actual.FrequencyHz, context, "osc.freq");
        AssertClose(expected.Duty, actual.Duty, context, "osc.duty");
        AssertClose(expected.Phase, actual.Phase, context, "osc.phase");
    }

    private static void AssertEnvelopeClose(Envelope expected, Envelope actual, string context)
    {
        AssertClose(expected.AttackSeconds, actual.AttackSeconds, context, "env.attack");
        AssertClose(expected.DecaySeconds, actual.DecaySeconds, context, "env.decay");
        AssertClose(expected.SustainLevel, actual.SustainLevel, context, "env.sustain_level");
        AssertClose(expected.ReleaseSeconds, actual.ReleaseSeconds, context, "env.release");
    }

    private static void AssertNoteClose(Note expected, Note actual, string context)
    {
        AssertClose(expected.FrequencyHz, actual.FrequencyHz, context, "note.frequency");
        AssertClose(expected.GateSeconds, actual.GateSeconds, context, "note.gate");
        Assert.Equal(expected.Source, actual.Source);
    }

    private static void AssertPitchClose(PitchMotion expected, PitchMotion actual, string context)
    {
        AssertClose(expected.MinFrequencyHz, actual.MinFrequencyHz, context, "pitch.min");
        AssertClose(expected.RampPerSecond, actual.RampPerSecond, context, "pitch.ramp");
        AssertClose(expected.DeltaRampPerSecond, actual.DeltaRampPerSecond, context, "pitch.delta_ramp");
        AssertClose(expected.VibratoDepth, actual.VibratoDepth, context, "pitch.vibrato");
        if (expected.VibratoDepth > 0 || actual.VibratoDepth > 0)
        {
            AssertClose(expected.VibratoHz, actual.VibratoHz, context, "pitch.vibrato_hz");
        }

        AssertClose(expected.VibratoDelaySeconds, actual.VibratoDelaySeconds, context, "pitch.vibrato_delay");
    }

    private static void AssertFilterClose(Filter expected, Filter actual, string context)
    {
        AssertClose(expected.LowPass, actual.LowPass, context, "filter.lpf");
        AssertClose(expected.LowPassRamp, actual.LowPassRamp, context, "filter.lpf_ramp");
        AssertClose(expected.LowPassResonance, actual.LowPassResonance, context, "filter.lpf_res");
        AssertClose(expected.HighPass, actual.HighPass, context, "filter.hpf");
        AssertClose(expected.HighPassRamp, actual.HighPassRamp, context, "filter.hpf_ramp");
    }

    private static void AssertArpeggioClose(Arpeggio? expected, Arpeggio? actual, string context)
    {
        Assert.Equal(expected is not null, actual is not null);
        if (expected is null || actual is null)
        {
            return;
        }

        AssertClose(expected.DelaySeconds, actual.DelaySeconds, context, "arpeggio.delay");
        AssertClose(expected.Multiplier, actual.Multiplier, context, "arpeggio.multiplier");
    }

    private static void AssertFrequencyModulationClose(FrequencyModulation expected, FrequencyModulation actual, string context)
    {
        AssertClose(expected.Ratio, actual.Ratio, context, "fm.ratio");
        AssertClose(expected.Index, actual.Index, context, "fm.index");
        AssertClose(expected.IndexDecaySeconds, actual.IndexDecaySeconds, context, "fm.index_decay");
    }

    private static void AssertVoiceColorClose(VoiceColor expected, VoiceColor actual, string context)
    {
        AssertClose(expected.NoiseMix, actual.NoiseMix, context, "color.noise");
        AssertClose(expected.Drive, actual.Drive, context, "color.drive");
        AssertClose(expected.Fold, actual.Fold, context, "color.fold");
        AssertClose(expected.TremoloDepth, actual.TremoloDepth, context, "color.tremolo");
        if (expected.TremoloDepth > 0 || actual.TremoloDepth > 0)
        {
            AssertClose(expected.TremoloHz, actual.TremoloHz, context, "color.tremolo_hz");
        }

        AssertClose(expected.FormantMix, actual.FormantMix, context, "color.formant_mix");
    }

    private static void AssertRepeatClose(Repeat? expected, Repeat? actual, string context)
    {
        Assert.Equal(expected is not null, actual is not null);
        if (expected is null || actual is null)
        {
            return;
        }

        AssertClose(expected.IntervalSeconds, actual.IntervalSeconds, context, "repeat.interval");
    }

    private static void AssertClose(float expected, float actual, string context, string field)
    {
        var tolerance = MathF.Max(0.0006f, MathF.Abs(expected) * 0.00002f);
        Assert.True(
            MathF.Abs(expected - actual) <= tolerance,
            $"{context} {field}: expected {expected:R}, got {actual:R}, tolerance {tolerance:R}.");
    }
}

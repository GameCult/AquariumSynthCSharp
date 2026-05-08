namespace AquariumSynth.Dsl;

public static class Presets
{
    public static SynthPatch AquariumPluck()
    {
        var envelope = new Envelope(0.002f, 0.09f, 0.43f, 0.38f);
        return new SynthPatch
        {
            Voices =
            [
                Simple(new Oscillator(Waveform.Sine, 440), envelope, 0.18f),
                Simple(new Oscillator(Waveform.Triangle, 880), envelope, 0.05f),
                Simple(new Oscillator(Waveform.Sine, 1760), envelope, 0.018f)
            ],
            Gain = 0.95f,
            SoftClip = true
        };
    }

    public static SynthPatch AquariumHeartbeat()
    {
        var envelope = new Envelope(0.004f, 0.08f, 0.2f, 0.62f);
        return new SynthPatch
        {
            Voices =
            [
                Simple(new Oscillator(Waveform.Sine, 72), envelope, 0.22f),
                Simple(new Oscillator(Waveform.Sine, 116), envelope, 0.09f)
            ],
            Gain = 0.9f,
            SoftClip = true
        };
    }

    public static SynthPatch AquariumVoice()
    {
        var voice = Simple(new Oscillator(Waveform.Triangle, 220), new Envelope(0.018f, 0.34f, 0.28f, 0.08f), 0.18f) with
        {
            Pitch = new PitchMotion(VibratoDepth: 0.018f, VibratoHz: 5.6f),
            Color = new VoiceColor(0.035f, 0.18f, 0.04f, 0.12f, 4.2f, 0.68f),
            Formants =
            [
                new Formant(520, 85, 0.9f),
                new Formant(1380, 180, 1),
                new Formant(2550, 300, 0.42f)
            ]
        };

        return new SynthPatch
        {
            Voices = [voice],
            Gain = 0.95f,
            SoftClip = true
        };
    }

    public static SynthPatch Sfxr(string name) =>
        SfxrParams.Named(name)?.ToPatch() ?? throw new ArgumentException($"Unknown SFXR preset `{name}`.", nameof(name));

    private static Voice Simple(Oscillator oscillator, Envelope envelope, float gain) => new()
    {
        Oscillator = oscillator,
        Envelope = envelope,
        Gain = gain
    };
}

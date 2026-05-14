namespace AquariumSynth.Dsl;

public enum Waveform
{
    Sine,
    Square,
    Sawtooth,
    Triangle,
    Noise
}

public enum ModWaveform
{
    Sine,
    Triangle,
    Square,
    SampleHold
}

public enum ModTarget
{
    Gain,
    Pitch,
    Duty,
    LowPass,
    HighPass,
    Noise,
    Drive,
    Fold,
    FormantMix,
    FmIndex
}

public sealed record Envelope(
    float AttackSeconds = 0,
    float DecaySeconds = 0.01f,
    float SustainLevel = 1,
    float ReleaseSeconds = 0.1f)
{
    public float DurationSeconds => AttackSeconds + DecaySeconds + ReleaseSeconds;
}

public enum RateLevelCurve
{
    Linear,
    Exponential
}

public sealed record RateLevelEnvelope(
    float Rate1Seconds,
    float Level1,
    float Rate2Seconds,
    float Level2,
    float Rate3Seconds,
    float Level3,
    float Rate4Seconds,
    float Level4,
    RateLevelCurve Curve1 = RateLevelCurve.Linear,
    RateLevelCurve Curve2 = RateLevelCurve.Linear,
    RateLevelCurve Curve3 = RateLevelCurve.Linear,
    RateLevelCurve Curve4 = RateLevelCurve.Linear);

public enum NoteSource
{
    OneShot,
    Host
}

public enum PlaybackMode
{
    OneShot,
    Mono,
    Poly
}

public sealed record Note(
    float FrequencyHz = 440,
    float GateSeconds = 0.1f,
    NoteSource Source = NoteSource.OneShot);

public sealed record Playback(
    PlaybackMode Mode = PlaybackMode.OneShot,
    int Voices = 1,
    bool Midi = false,
    float FrequencyHz = 440,
    float Gain = 1);

public sealed record Oscillator(
    Waveform Waveform = Waveform.Sine,
    float FrequencyHz = 440,
    float Duty = 0.5f,
    float Phase = 0);

public sealed record PitchMotion(
    float MinFrequencyHz = 20,
    float RampPerSecond = 0,
    float DeltaRampPerSecond = 0,
    float VibratoDepth = 0,
    float VibratoHz = 0,
    float VibratoDelaySeconds = 0);

public sealed record DutyMotion(float RampPerSecond = 0);

public sealed record Filter(
    float LowPass = 1,
    float LowPassRamp = 0,
    float LowPassResonance = 0,
    float HighPass = 0,
    float HighPassRamp = 0);

public sealed record Phaser(float OffsetSeconds = 0, float RampSecondsPerSecond = 0);

public sealed record Arpeggio(float DelaySeconds, float Multiplier);

public sealed record FrequencyModulation(float Ratio = 1, float Index = 0, float IndexDecaySeconds = 0);

public sealed record VoiceColor(
    float NoiseMix = 0,
    float Drive = 0,
    float Fold = 0,
    float TremoloDepth = 0,
    float TremoloHz = 0,
    float FormantMix = 0);

public sealed record Formant(float FrequencyHz, float BandwidthHz, float Gain);

public sealed record Modulator(
    ModTarget Target,
    ModWaveform Waveform = ModWaveform.Sine,
    float FrequencyHz = 1,
    float Depth = 0,
    float Phase = 0,
    float Bias = 0);

public sealed record ControlLane(string Name, Modulator Modulator);

public sealed record OperatorNode(
    int Id,
    float Ratio = 1,
    float Level = 1,
    float Feedback = 0,
    Note Note = null!,
    Envelope Envelope = null!,
    RateLevelEnvelope? RateLevelEnvelope = null)
{
    public Note Note { get; init; } = Note ?? new();
    public Envelope Envelope { get; init; } = Envelope ?? new();
}

public sealed record OperatorEdge(int SourceId, int TargetId, float Index = 1);

public sealed record OperatorGraph(
    string Name,
    float FrequencyHz,
    IReadOnlyList<OperatorNode> Operators,
    IReadOnlyList<OperatorEdge> Edges,
    IReadOnlyList<int> Carriers,
    Note Note = null!,
    float Gain = 0.2f,
    float VibratoDepth = 0,
    float VibratoHz = 0,
    float VibratoDelaySeconds = 0)
{
    public Note Note { get; init; } = Note ?? new();
}

public sealed record PatchParameter(
    string Path,
    string Label,
    float Default,
    float Min,
    float Max,
    float Step,
    string Unit = "",
    string AutomationRate = "control",
    string Notes = "");

public sealed record ParameterBinding(string FieldPath, string ParameterPath);

public sealed record ReferenceSource(
    string Kind,
    string Uri,
    string License,
    string Hash,
    string Notes = "");

public sealed record ReferenceFeature(string Name, string Value, string Notes = "");

public sealed record PatchLayer(
    string Name,
    string Engine = "",
    int? MinKey = null,
    int? MaxKey = null,
    float Gain = 1,
    string EffectSend = "");

public sealed record HarmonicPartial(float Ratio, float Gain);

public sealed record HarmonicBank(
    string LayerName,
    float RootFrequencyHz,
    IReadOnlyList<HarmonicPartial> Partials);

public sealed record ReferencePatch(
    string Id,
    string Family,
    string Name,
    ReferenceSource Source,
    IReadOnlyList<ReferenceFeature> Features,
    IReadOnlyList<PatchParameter> Parameters,
    string? AquariumScript = null);

public sealed record Repeat(float IntervalSeconds);

public sealed record Voice
{
    public PatchLayer? Layer { get; init; }
    public Oscillator Oscillator { get; init; } = new();
    public Note Note { get; init; } = new();
    public Envelope Envelope { get; init; } = new();
    public RateLevelEnvelope? RateLevelEnvelope { get; init; }
    public PitchMotion Pitch { get; init; } = new();
    public DutyMotion Duty { get; init; } = new();
    public Filter Filter { get; init; } = new();
    public Phaser Phaser { get; init; } = new();
    public Arpeggio? Arpeggio { get; init; }
    public FrequencyModulation Fm { get; init; } = new();
    public VoiceColor Color { get; init; } = new();
    public IReadOnlyList<Formant> Formants { get; init; } = Array.Empty<Formant>();
    public IReadOnlyList<Modulator> Modulators { get; init; } = Array.Empty<Modulator>();
    public float Gain { get; init; } = 0.2f;
}

public sealed record SynthPatch
{
    public IReadOnlyList<Voice> Voices { get; init; } = Array.Empty<Voice>();
    public IReadOnlyList<PatchLayer> Layers { get; init; } = Array.Empty<PatchLayer>();
    public IReadOnlyList<HarmonicBank> HarmonicBanks { get; init; } = Array.Empty<HarmonicBank>();
    public IReadOnlyList<OperatorGraph> OperatorGraphs { get; init; } = Array.Empty<OperatorGraph>();
    public IReadOnlyList<ControlLane> Controls { get; init; } = Array.Empty<ControlLane>();
    public IReadOnlyList<PatchParameter> Parameters { get; init; } = Array.Empty<PatchParameter>();
    public IReadOnlyList<ParameterBinding> ParameterBindings { get; init; } = Array.Empty<ParameterBinding>();
    public Playback Playback { get; init; } = new();
    public Repeat? Repeat { get; init; }
    public float Gain { get; init; } = 1;
    public bool SoftClip { get; init; } = true;
}

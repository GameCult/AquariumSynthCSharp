using AquariumSynth.Dsl;

namespace AquariumSynth.Dsl.Tests;

public sealed class Dx7SysExTests
{
    [Fact]
    public void ParsesSingleVoiceEditBuffer()
    {
        var data = InitVoice("AQ BRIGHT", algorithm: 5, feedback: 3);

        var voice = Dx7SysEx.ParseVoice(data);

        Assert.Equal("AQ BRIGHT", voice.Name);
        Assert.Equal(6, voice.Operators.Count);
        Assert.Equal(5, voice.Algorithm);
        Assert.Equal(3, voice.Feedback);
        Assert.True(voice.OscillatorSync);
        Assert.Equal(Dx7LfoWaveform.Sine, voice.Lfo.Waveform);
        Assert.Equal(99, voice.Operators.Single(op => op.Number == 1).OutputLevel);
        Assert.Equal(1, voice.Operators.Single(op => op.Number == 1).FrequencyCoarse);
    }

    [Fact]
    public void UnpacksPackedVoiceData()
    {
        var data = InitVoice("PACKED", algorithm: 32, feedback: 7);
        data[0] = 88;
        data[11] = 2;
        data[12] = 1;
        data[13] = 5;
        data[20] = 10;
        data[14] = 3;
        data[15] = 6;
        data[17] = 1;
        data[18] = 12;

        var voice = Dx7SysEx.ParseVoice(PackVoice(data));
        var op6 = voice.Operators.Single(op => op.Number == 6);

        Assert.Equal("PACKED", voice.Name);
        Assert.Equal(32, voice.Algorithm);
        Assert.Equal(7, voice.Feedback);
        Assert.Equal(88, op6.Envelope.Rate1);
        Assert.Equal(2, op6.LeftCurve);
        Assert.Equal(1, op6.RightCurve);
        Assert.Equal(5, op6.RateScaling);
        Assert.Equal(10, op6.Detune);
        Assert.Equal(3, op6.AmplitudeModulationSensitivity);
        Assert.Equal(6, op6.KeyVelocitySensitivity);
        Assert.Equal(Dx7FrequencyMode.Fixed, op6.FrequencyMode);
        Assert.Equal(12, op6.FrequencyCoarse);
    }

    [Fact]
    public void ParsesPackedThirtyTwoVoiceSysExBank()
    {
        var bankData = new byte[Dx7SysEx.PackedBankDataLength];
        for (var i = 0; i < Dx7SysEx.PackedBankVoiceCount; i++)
        {
            var name = $"AQ{i + 1:00}";
            PackVoice(InitVoice(name, algorithm: i % 32 + 1, feedback: i % 8))
                .CopyTo(bankData.AsSpan(i * Dx7SysEx.PackedVoiceLength));
        }

        var bank = Dx7SysEx.ParseBank(WrapSysEx(format: 9, data: bankData));

        Assert.Equal(32, bank.Voices.Count);
        Assert.Equal("AQ01", bank.Voices[0].Name);
        Assert.Equal("AQ32", bank.Voices[31].Name);
        Assert.Equal(32, bank.Voices[31].Algorithm);
        Assert.Equal(7, bank.Voices[31].Feedback);
    }

    [Fact]
    public void BuildsReferencePatchWithStructuralFeatures()
    {
        var data = InitVoice("FEATURES", algorithm: 12, feedback: 4);
        var source = Dx7SysEx.SourceForBytes("memory://features.syx", "project-authored", data);

        var reference = Dx7SysEx.ParseVoice(data).ToReferencePatch("dx7/features", source);

        Assert.Equal("dx7", reference.Family);
        Assert.Equal("FEATURES", reference.Name);
        Assert.Equal("dx7-sysex", reference.Source.Kind);
        Assert.Equal(64, reference.Source.Hash.Length);
        Assert.Contains(reference.Features, feature => feature.Name == "operator_count" && feature.Value == "6");
        Assert.Contains(reference.Features, feature => feature.Name == "algorithm" && feature.Value == "12");
        Assert.Contains(reference.Features, feature => feature.Name == "feedback" && feature.Value == "4");
        Assert.Contains(reference.Features, feature => feature.Name == "carrier_operators");
        Assert.Contains(reference.Features, feature => feature.Name == "modulation_edge_count");
    }

    [Fact]
    public void RejectsBadSysExChecksum()
    {
        var wrapped = WrapSysEx(format: 0, data: InitVoice("BAD SUM", algorithm: 1, feedback: 0));
        wrapped[^2] ^= 0x01;

        var exception = Assert.Throws<ArgumentException>(() => Dx7SysEx.ParseVoice(wrapped));

        Assert.Contains("checksum", exception.Message);
    }

    [Fact]
    public void AlgorithmEightTopologyMatchesDocumentedExample()
    {
        var topology = Dx7SysEx.AlgorithmTopology(8);

        Assert.Equal([1, 3], topology.CarrierOperators);
        Assert.Contains(topology.ModulationEdges, edge =>
            edge.Kind == "direct" &&
            edge.TargetOperator == 5 &&
            edge.SourceOperators.SequenceEqual([6]));
        Assert.Contains(topology.ModulationEdges, edge =>
            edge.Kind == "self-feedback" &&
            edge.TargetOperator == 4 &&
            edge.SourceOperators.SequenceEqual([4]));
        Assert.Contains(topology.ModulationEdges, edge =>
            edge.Kind == "sum" &&
            edge.TargetOperator == 3 &&
            edge.SourceOperators.SequenceEqual([4, 5]));
        Assert.Contains(topology.ModulationEdges, edge =>
            edge.Kind == "direct" &&
            edge.TargetOperator == 1 &&
            edge.SourceOperators.SequenceEqual([2]));
        Assert.Contains(4, topology.FeedbackSourceOperators);
        Assert.Equal([4], topology.SelfFeedbackOperators);
    }

    [Fact]
    public void AlgorithmSixteenFallsBackToSingleCarrierOperatorOne()
    {
        var topology = Dx7SysEx.AlgorithmTopology(16);

        Assert.Equal([1], topology.CarrierOperators);
        Assert.Contains(topology.ModulationEdges, edge =>
            edge.Kind == "sum" &&
            edge.TargetOperator == 1 &&
            edge.SourceOperators.Count >= 2);
    }

    [Fact]
    public void AlgorithmThirtyTwoExposesSixCarrierAdditiveTopology()
    {
        var topology = Dx7SysEx.AlgorithmTopology(32);

        Assert.Equal([1, 2, 3, 4, 5, 6], topology.CarrierOperators);
        Assert.DoesNotContain(topology.ModulationEdges, edge => edge.Kind is "direct" or "sum" or "delayed-sum");
        Assert.Equal([6], topology.SelfFeedbackOperators);
    }

    [Fact]
    public void ApproximatesDx7RateLevelEnvelopeAsAdsr()
    {
        var approximation = Dx7SysEx.ApproximateEnvelope(new Dx7Envelope(
            Rate1: 99,
            Rate2: 70,
            Rate3: 55,
            Rate4: 40,
            Level1: 99,
            Level2: 80,
            Level3: 50,
            Level4: 0));

        Assert.InRange(approximation.Envelope.AttackSeconds, 0.003f, 0.006f);
        Assert.InRange(approximation.Envelope.DecaySeconds, 0.2f, 2.5f);
        Assert.Equal(50 / 99f, approximation.Envelope.SustainLevel, 5);
        Assert.InRange(approximation.Envelope.ReleaseSeconds, 1f, 5f);
        Assert.True(approximation.GateSeconds >= approximation.Envelope.AttackSeconds + approximation.Envelope.DecaySeconds);
        Assert.Contains("Approximation", approximation.Notes);
        Assert.StartsWith("env=adsr:", approximation.ToScriptSpec());
    }

    [Fact]
    public void ApproximatesDx7OperatorLevelWithVelocityAndKeyScaling()
    {
        var operatorLevel = Dx7SysEx.ApproximateOperatorLevel(new Dx7Operator(
            Number: 5,
            Envelope: new Dx7Envelope(85, 16, 75, 61, 88, 44, 25, 0),
            BreakPoint: 15,
            LeftDepth: 0,
            RightDepth: 62,
            LeftCurve: 0,
            RightCurve: 1,
            RateScaling: 2,
            AmplitudeModulationSensitivity: 0,
            KeyVelocitySensitivity: 6,
            OutputLevel: 80,
            FrequencyMode: Dx7FrequencyMode.Ratio,
            FrequencyCoarse: 8,
            FrequencyFine: 0,
            Detune: 4));

        Assert.Equal(80, operatorLevel.ScaledOutputLevel);
        Assert.Equal(-5, operatorLevel.KeyScalingOffset);
        Assert.Equal(0, operatorLevel.VelocityOffset);
        Assert.Equal(.125f, operatorLevel.LinearLevel, 5);
        Assert.Contains("nonlinear", operatorLevel.Notes);
    }

    [Fact]
    public void MapsDx7OutputLevelToMeasuredCarrierAmplitudeCurve()
    {
        Assert.Equal(1, Dx7SysEx.OperatorOutputAmplitude(99));
        Assert.Equal(MathF.Pow(2, -1), Dx7SysEx.OperatorOutputAmplitude(91), 5);
        Assert.Equal(MathF.Pow(2, -2), Dx7SysEx.OperatorOutputAmplitude(83), 5);
        Assert.Equal(MathF.Pow(2, (80 - 99) / 8f), Dx7SysEx.OperatorOutputAmplitude(80), 5);
    }

    [Fact]
    public void ApproximatesDx7RateLevelEnvelopeAsStagedEnvelope()
    {
        var approximation = Dx7SysEx.ApproximateRateLevelEnvelope(new Dx7Envelope(
            Rate1: 98,
            Rate2: 72,
            Rate3: 75,
            Rate4: 61,
            Level1: 99,
            Level2: 89,
            Level3: 99,
            Level4: 0));

        Assert.Equal(1, approximation.Envelope.Level1);
        Assert.Equal(89 / 99f, approximation.Envelope.Level2, 5);
        Assert.Equal(1, approximation.Envelope.Level3);
        Assert.Equal(0, approximation.Envelope.Level4);
        Assert.True(approximation.GateSeconds >= approximation.Envelope.Rate1Seconds + approximation.Envelope.Rate2Seconds + approximation.Envelope.Rate3Seconds);
        Assert.StartsWith("env=rl rates=", approximation.ToScriptSpec());
        Assert.Contains("intermediate target levels", approximation.Notes);
    }

    private static byte[] InitVoice(string name, int algorithm, int feedback)
    {
        var data = new byte[Dx7SysEx.VoiceEditBufferLength];
        for (var op = 0; op < 6; op++)
        {
            var offset = op * 21;
            data[offset + 0] = 99;
            data[offset + 1] = 99;
            data[offset + 2] = 99;
            data[offset + 3] = 99;
            data[offset + 4] = 99;
            data[offset + 5] = 99;
            data[offset + 6] = 99;
            data[offset + 7] = op == 5 ? (byte)0 : (byte)99;
            data[offset + 16] = op == 5 ? (byte)99 : (byte)0;
            data[offset + 18] = 1;
            data[offset + 20] = 7;
        }

        data[126] = 99;
        data[127] = 99;
        data[128] = 99;
        data[129] = 99;
        data[130] = 50;
        data[131] = 50;
        data[132] = 50;
        data[133] = 50;
        data[134] = (byte)(algorithm - 1);
        data[135] = (byte)feedback;
        data[136] = 1;
        data[137] = 35;
        data[141] = 1;
        data[142] = 4;
        data[143] = 3;
        data[144] = 24;

        var paddedName = name.PadRight(10)[..10];
        for (var i = 0; i < paddedName.Length; i++) data[145 + i] = (byte)paddedName[i];
        return data;
    }

    private static byte[] PackVoice(byte[] edit)
    {
        var packed = new byte[Dx7SysEx.PackedVoiceLength];
        for (var op = 0; op < 6; op++)
        {
            var packedOffset = op * 17;
            var editOffset = op * 21;
            for (var i = 0; i <= 10; i++) packed[packedOffset + i] = edit[editOffset + i];
            packed[packedOffset + 11] = (byte)(edit[editOffset + 11] | (edit[editOffset + 12] << 2));
            packed[packedOffset + 12] = (byte)(edit[editOffset + 13] | (edit[editOffset + 20] << 3));
            packed[packedOffset + 13] = (byte)(edit[editOffset + 14] | (edit[editOffset + 15] << 2));
            packed[packedOffset + 14] = edit[editOffset + 16];
            packed[packedOffset + 15] = (byte)(edit[editOffset + 17] | (edit[editOffset + 18] << 1));
            packed[packedOffset + 16] = edit[editOffset + 19];
        }

        for (var i = 0; i < 8; i++) packed[102 + i] = edit[126 + i];
        packed[110] = edit[134];
        packed[111] = (byte)(edit[135] | (edit[136] << 3));
        packed[112] = edit[137];
        packed[113] = edit[138];
        packed[114] = edit[139];
        packed[115] = edit[140];
        packed[116] = (byte)(edit[141] | (edit[142] << 1) | (edit[143] << 4));
        packed[117] = edit[144];
        for (var i = 0; i < 10; i++) packed[118 + i] = edit[145 + i];
        return packed;
    }

    private static byte[] WrapSysEx(byte format, byte[] data)
    {
        var bytes = new byte[data.Length + 8];
        bytes[0] = 0xF0;
        bytes[1] = 0x43;
        bytes[2] = 0x00;
        bytes[3] = format;
        bytes[4] = (byte)((data.Length >> 7) & 0x7F);
        bytes[5] = (byte)(data.Length & 0x7F);
        data.CopyTo(bytes.AsSpan(6));
        bytes[^2] = Checksum(data);
        bytes[^1] = 0xF7;
        return bytes;
    }

    private static byte Checksum(byte[] data)
    {
        var sum = 0;
        foreach (var value in data) sum = (sum + value) & 0x7F;
        return (byte)((128 - sum) & 0x7F);
    }
}

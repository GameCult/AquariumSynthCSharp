using System.Numerics;

namespace AquariumSynth.Dsl;

internal static class PadSynthWaveform
{
    public const int SampleRate = 44100;

    public static float[] Generate(SpectralBank bank, int tableSize)
    {
        if (tableSize < 2 || (tableSize & (tableSize - 1)) != 0)
        {
            throw new ArgumentException("PAD spectral table size must be a power of two", nameof(tableSize));
        }

        var spectrum = new Complex[tableSize];
        var amplitudes = AmplitudeSpectrum(bank, tableSize);
        var random = new Random(StableSeed(bank, tableSize));
        var half = tableSize / 2;
        for (var bin = 1; bin < half; bin++)
        {
            if (amplitudes[bin] <= 0)
            {
                continue;
            }

            var phase = random.NextDouble() * Math.Tau;
            var value = Complex.FromPolarCoordinates(amplitudes[bin], phase);
            spectrum[bin] = value;
            spectrum[tableSize - bin] = Complex.Conjugate(value);
        }

        InverseFft(spectrum);

        var samples = new float[tableSize];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)spectrum[i].Real;
        }

        Normalize(samples, bank.Profile.Mode);
        return samples;
    }

    private static double[] AmplitudeSpectrum(SpectralBank bank, int tableSize)
    {
        return bank.Profile.Mode == PadSpectrumMode.Generic
            ? GenericAmplitudeSpectrum(bank, tableSize)
            : ZynAmplitudeSpectrum(bank, tableSize);
    }

    private static double[] GenericAmplitudeSpectrum(SpectralBank bank, int tableSize)
    {
        var amplitudes = new double[tableSize / 2 + 1];
        foreach (var partial in bank.Partials)
        {
            var ratio = Math.Max(0.0001, partial.Ratio);
            var centerCyclesPerSample = bank.RootFrequencyHz * ratio / SampleRate;
            var bandwidthCents = 1200.0 * Math.Log2(1.0 + Math.Max(0, bank.SpreadRatio));
            if (bandwidthCents <= 0)
            {
                var bin = (int)Math.Round(centerCyclesPerSample * tableSize);
                if (bin > 0 && bin < amplitudes.Length)
                {
                    amplitudes[bin] += partial.Gain;
                }

                continue;
            }

            var bandwidthHz = (Math.Pow(2.0, bandwidthCents / 1200.0) - 1.0) * bank.RootFrequencyHz * ratio;
            var bandwidthCyclesPerSample = Math.Max(1e-9, bandwidthHz / (2.0 * SampleRate));
            var centerBin = centerCyclesPerSample * tableSize;
            if (bandwidthCyclesPerSample * tableSize < 0.5)
            {
                var bin = (int)Math.Round(centerBin);
                if (bin > 0 && bin < amplitudes.Length)
                {
                    amplitudes[bin] += partial.Gain;
                }

                continue;
            }

            var radiusBins = Math.Max(2, (int)Math.Ceiling(bandwidthCyclesPerSample * tableSize * 6));
            var left = Math.Max(1, (int)Math.Floor(centerBin - radiusBins));
            var right = Math.Min(amplitudes.Length - 1, (int)Math.Ceiling(centerBin + radiusBins));

            for (var bin = left; bin <= right; bin++)
            {
                var cyclesPerSample = bin / (double)tableSize;
                var x = (cyclesPerSample - centerCyclesPerSample) / bandwidthCyclesPerSample;
                amplitudes[bin] += Math.Exp(-x * x) / bandwidthCyclesPerSample * partial.Gain;
            }
        }

        return amplitudes;
    }

    private static double[] ZynAmplitudeSpectrum(SpectralBank bank, int tableSize)
    {
        var amplitudes = new double[tableSize / 2 + 1];
        var harmonics = NormalizeHarmonics(bank.Partials);
        var profile = ZynProfile(bank.Profile.ZynProfile);
        if (bank.Profile.Mode == PadSpectrumMode.ZynBandwidth)
        {
            AddZynBandwidthSpectrum(amplitudes, harmonics, profile, bank, tableSize);
            return amplitudes;
        }

        AddZynPeakSpectrum(amplitudes, harmonics, bank, tableSize, bank.Profile.Mode == PadSpectrumMode.ZynContinuous);
        return amplitudes;
    }

    private static IReadOnlyList<HarmonicPartial> NormalizeHarmonics(IReadOnlyList<HarmonicPartial> partials)
    {
        var max = partials.Select(partial => partial.Gain).DefaultIfEmpty(0).Max();
        if (max <= 0)
        {
            return partials;
        }

        return partials
            .Select(partial => new HarmonicPartial(partial.Ratio, partial.Gain / max))
            .ToArray();
    }

    private static double[] ZynProfile(ZynHarmonicProfile profile)
    {
        const int profileSize = 512;
        const int superSample = 16;
        var smp = new double[profileSize];
        var basePar = Math.Pow(2.0, (1.0 - profile.BaseParameter / 127.0) * 12.0);
        var freqMult = Math.Floor(Math.Pow(2.0, profile.FrequencyMultiplier / 127.0 * 5.0) + 0.000001);
        var modFreq = Math.Floor(Math.Pow(2.0, profile.ModulatorFrequency / 127.0 * 5.0) + 0.000001);
        var modPar = Math.Pow(profile.ModulatorParameter / 127.0, 4.0) * 5.0 / Math.Sqrt(Math.Max(1.0, modFreq));
        var ampPar1 = Math.Pow(2.0, Math.Pow(profile.AmplitudeParameter1 / 127.0, 2.0) * 10.0) - 0.999;
        var ampPar2 = (1.0 - profile.AmplitudeParameter2 / 127.0) * 0.998 + 0.001;
        var width = Math.Pow(150.0 / (profile.Width + 22.0), 2.0);

        for (var i = 0; i < profileSize * superSample; i++)
        {
            var x = i / (double)(profileSize * superSample);
            var originalX = x * 2.0 - 1.0;
            x = (x - 0.5) * width + 0.5;
            if (x is < 0 or > 1)
            {
                continue;
            }

            if (profile.Half == ZynProfileHalf.Upper && x > 0.5)
            {
                continue;
            }
            if (profile.Half == ZynProfileHalf.Lower && x < 0.5)
            {
                continue;
            }

            var beforeFreqMult = x;
            x *= freqMult;
            x += Math.Sin(beforeFreqMult * Math.PI * modFreq) * modPar;
            x = (x + 1000.0) % 1.0 * 2.0 - 1.0;

            var value = profile.BaseType switch
            {
                ZynProfileBaseType.Square => Math.Exp(-(x * x) * basePar) < 0.4 ? 0.0 : 1.0,
                ZynProfileBaseType.DoubleExponential => Math.Exp(-Math.Abs(x) * Math.Sqrt(basePar)),
                _ => Math.Exp(-(x * x) * basePar)
            };

            value = ApplyZynAmplitudeMultiplier(value, originalX, ampPar1, ampPar2, profile);
            smp[i / superSample] += value / superSample;
        }

        var max = smp.DefaultIfEmpty(0).Max();
        if (max > 0)
        {
            for (var i = 0; i < smp.Length; i++)
            {
                smp[i] /= max;
            }
        }

        return smp;
    }

    private static double ApplyZynAmplitudeMultiplier(
        double value,
        double originalX,
        double ampPar1,
        double ampPar2,
        ZynHarmonicProfile profile)
    {
        if (profile.AmplitudeType == ZynProfileAmplitudeType.Off)
        {
            return value;
        }

        var amp = profile.AmplitudeType switch
        {
            ZynProfileAmplitudeType.Gaussian => Math.Exp(-(originalX * originalX) * 10.0 * ampPar1),
            ZynProfileAmplitudeType.Sine => 0.5 * (1.0 + Math.Cos(Math.PI * originalX * Math.Sqrt(ampPar1 * 4.0 + 1.0))),
            ZynProfileAmplitudeType.Flat => 1.0 / (Math.Pow(originalX * (ampPar1 * 2.0 + 0.8), 14.0) + 1.0),
            _ => 1.0
        };

        return profile.AmplitudeMode switch
        {
            ZynProfileAmplitudeMode.Mult => value * (amp * (1.0 - ampPar2) + ampPar2),
            ZynProfileAmplitudeMode.Div1 => value / (amp + Math.Pow(ampPar2, 4.0) * 20.0 + 0.0001),
            ZynProfileAmplitudeMode.Div2 => amp / (value + Math.Pow(ampPar2, 4.0) * 20.0 + 0.0001),
            _ => amp * (1.0 - ampPar2) + value * ampPar2
        };
    }

    private static void AddZynBandwidthSpectrum(
        double[] amplitudes,
        IReadOnlyList<HarmonicPartial> harmonics,
        double[] profile,
        SpectralBank bank,
        int tableSize)
    {
        var bandwidthCents = ZynBandwidthCents(bank.Profile.Bandwidth);
        var bandwidthScale = ZynBandwidthScalePower(bank.Profile.BandwidthScale);
        var bandwidthAdjust = Math.Max(0.001, ZynProfileBandwidthAdjust(bank.Profile.ZynProfile, profile));
        var spectrumSize = tableSize / 2;
        foreach (var harmonic in harmonics)
        {
            var realRatio = ZynHarmonicRatio(harmonic.Ratio, bank.Profile.ZynPosition);
            var realFrequency = realRatio * bank.RootFrequencyHz;
            if (realFrequency > SampleRate * 0.49999 || realFrequency < 20 || harmonic.Gain < 0.0001f)
            {
                continue;
            }

            var bandwidthHz = (Math.Pow(2.0, bandwidthCents / 1200.0) - 1.0)
                * bank.RootFrequencyHz
                / bandwidthAdjust
                * Math.Pow(realFrequency / bank.RootFrequencyHz, bandwidthScale);
            var profileBins = Math.Max(1, (int)(bandwidthHz / (SampleRate * 0.5) * spectrumSize) + 1);
            if (profileBins > profile.Length)
            {
                var rap = Math.Sqrt(profile.Length / (double)profileBins);
                var center = (int)(realFrequency / (SampleRate * 0.5) * spectrumSize) - profileBins / 2;
                for (var i = 0; i < profileBins; i++)
                {
                    var bin = center + i;
                    if (bin <= 0 || bin >= amplitudes.Length) continue;
                    var src = Math.Min(profile.Length - 1, (int)(i * rap * rap));
                    amplitudes[bin] += harmonic.Gain * profile[src] * rap;
                }
            }
            else
            {
                var rap = Math.Sqrt(profileBins / (double)profile.Length);
                var center = realFrequency / (SampleRate * 0.5) * spectrumSize;
                for (var i = 0; i < profile.Length; i++)
                {
                    var binPosition = center + (i / (double)profile.Length - 0.5) * profileBins;
                    var bin = (int)Math.Floor(binPosition);
                    var frac = binPosition - bin;
                    var value = harmonic.Gain * profile[i] * rap;
                    if (bin > 0 && bin < amplitudes.Length)
                    {
                        amplitudes[bin] += value * (1.0 - frac);
                    }
                    if (bin + 1 > 0 && bin + 1 < amplitudes.Length)
                    {
                        amplitudes[bin + 1] += value * frac;
                    }
                }
            }
        }
    }

    private static void AddZynPeakSpectrum(
        double[] amplitudes,
        IReadOnlyList<HarmonicPartial> harmonics,
        SpectralBank bank,
        int tableSize,
        bool continuous)
    {
        var previousBin = -1;
        var previousGain = 0.0;
        foreach (var harmonic in harmonics)
        {
            var frequency = ZynHarmonicRatio(harmonic.Ratio, bank.Profile.ZynPosition) * bank.RootFrequencyHz;
            var bin = (int)Math.Round(frequency / SampleRate * tableSize);
            if (bin <= 0 || bin >= amplitudes.Length)
            {
                continue;
            }

            amplitudes[bin] += harmonic.Gain;
            if (continuous && previousBin > 0 && bin > previousBin + 1)
            {
                for (var fill = previousBin + 1; fill < bin; fill++)
                {
                    var t = (fill - previousBin) / (double)(bin - previousBin);
                    amplitudes[fill] += previousGain * (1.0 - t) + harmonic.Gain * t;
                }
            }

            previousBin = bin;
            previousGain = harmonic.Gain;
        }
    }

    private static double ZynBandwidthCents(int bandwidth)
    {
        var result = Math.Pow(Math.Max(0, bandwidth) / 1000.0, 1.1);
        return Math.Pow(10.0, result * 4.0) * 0.25;
    }

    private static double ZynBandwidthScalePower(int scale) => scale switch
    {
        1 => 0.0,
        2 => 0.25,
        3 => 0.5,
        4 => 0.75,
        5 => 1.5,
        6 => 2.0,
        7 => -0.5,
        _ => 1.0
    };

    private static double ZynProfileBandwidthAdjust(ZynHarmonicProfile harmonicProfile, IReadOnlyList<double> profile)
    {
        if (!harmonicProfile.AutoScale)
        {
            return 0.5;
        }

        var sum = 0.0;
        for (var i = 0; i < profile.Count / 2; i++)
        {
            sum += profile[i] * profile[i] + profile[profile.Count - 1 - i] * profile[profile.Count - 1 - i];
            if (sum >= 4.0)
            {
                return Math.Clamp(1.0 - 2.0 * i / profile.Count, 0.001, 1.0);
            }
        }

        return 1.0;
    }

    private static double ZynHarmonicRatio(float harmonicNumber, ZynHarmonicPosition position)
    {
        var n = Math.Max(1.0, harmonicNumber);
        var n0 = n - 1.0;
        var par1 = Math.Pow(10.0, -(1.0 - position.Parameter1 / 255.0) * 3.0);
        var par2 = position.Parameter2 / 255.0;
        var result = position.Type switch
        {
            ZynHarmonicPositionType.ShiftUp => ZynShiftUp(n, n0, par1, par2),
            ZynHarmonicPositionType.ShiftDown => ZynShiftDown(n, n0, par1, par2),
            ZynHarmonicPositionType.PowerUp => Math.Pow(n0 / (par1 * 100.0 + 1.0), 1.0 - par2 * 0.8) * (par1 * 100.0 + 1.0) + 1.0,
            ZynHarmonicPositionType.PowerDown => n0 * (1.0 - par1) + Math.Pow(n0 * 0.1, par2 * 3.0 + 1.0) * par1 * 10.0 + 1.0,
            ZynHarmonicPositionType.Sine => n0 + Math.Sin(n0 * par2 * par2 * Math.PI * 0.999) * Math.Sqrt(par1) * 2.0 + 1.0,
            ZynHarmonicPositionType.Power => n0 * Math.Pow(1.0 + par1 * Math.Pow(n0 * 0.8, Math.Pow(par2 * 2.0, 2.0) + 0.1), Math.Pow(par2 * 2.0, 2.0) + 0.1) + 1.0,
            ZynHarmonicPositionType.Shift => (n + position.Parameter1 / 255.0) / (position.Parameter1 / 255.0 + 1.0),
            _ => n
        };
        var forced = position.Parameter3 / 255.0;
        var rounded = Math.Floor(result + 0.5);
        return Math.Max(0.0001, rounded + (1.0 - forced) * (result - rounded));
    }

    private static double ZynShiftUp(double n, double n0, double par1, double par2)
    {
        var threshold = par2 * par2 * 100.0 + 1.0;
        return n < threshold ? n : 1.0 + n0 + (n0 - threshold + 1.0) * par1 * 8.0;
    }

    private static double ZynShiftDown(double n, double n0, double par1, double par2)
    {
        var threshold = par2 * par2 * 100.0 + 1.0;
        return n < threshold ? n : 1.0 + n0 - (n0 - threshold + 1.0) * par1 * 0.90;
    }

    private static void InverseFft(Complex[] data)
    {
        BitReverse(data);
        for (var length = 2; length <= data.Length; length <<= 1)
        {
            var angle = Math.Tau / length;
            var step = Complex.FromPolarCoordinates(1.0, angle);
            for (var start = 0; start < data.Length; start += length)
            {
                var rotation = Complex.One;
                var half = length / 2;
                for (var offset = 0; offset < half; offset++)
                {
                    var even = data[start + offset];
                    var odd = data[start + offset + half] * rotation;
                    data[start + offset] = even + odd;
                    data[start + offset + half] = even - odd;
                    rotation *= step;
                }
            }
        }

        for (var i = 0; i < data.Length; i++)
        {
            data[i] /= data.Length;
        }
    }

    private static void BitReverse(Complex[] data)
    {
        var j = 0;
        for (var i = 1; i < data.Length; i++)
        {
            var bit = data.Length >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
        }
    }

    private static void Normalize(float[] samples, PadSpectrumMode mode)
    {
        if (mode != PadSpectrumMode.Generic)
        {
            NormalizeRms(samples, 0.09765625f);
            return;
        }

        var peak = samples.Select(Math.Abs).DefaultIfEmpty(0).Max();
        if (peak <= 0)
        {
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] /= peak;
        }
    }

    private static void NormalizeRms(float[] samples, float targetRms)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        var rms = Math.Sqrt(sum / Math.Max(1, samples.Length));
        if (rms <= 0)
        {
            return;
        }

        var gain = targetRms / rms;
        var peak = samples.Select(sample => Math.Abs(sample * gain)).DefaultIfEmpty(0).Max();
        if (peak > 1.0)
        {
            gain /= peak;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(samples[i] * gain);
        }
    }

    private static int StableSeed(SpectralBank bank, int tableSize)
    {
        var hash = 2166136261u;
        AddInt(tableSize);
        AddFloat(bank.RootFrequencyHz);
        AddFloat(bank.SpreadRatio);
        AddInt((int)bank.Profile.Mode);
        AddInt(bank.Profile.Bandwidth);
        AddInt(bank.Profile.BandwidthScale);
        AddInt((int)bank.Profile.ZynProfile.BaseType);
        AddInt(bank.Profile.ZynProfile.BaseParameter);
        AddInt(bank.Profile.ZynProfile.FrequencyMultiplier);
        AddInt(bank.Profile.ZynProfile.ModulatorParameter);
        AddInt(bank.Profile.ZynProfile.ModulatorFrequency);
        AddInt(bank.Profile.ZynProfile.Width);
        AddInt((int)bank.Profile.ZynProfile.AmplitudeType);
        AddInt((int)bank.Profile.ZynProfile.AmplitudeMode);
        AddInt(bank.Profile.ZynProfile.AmplitudeParameter1);
        AddInt(bank.Profile.ZynProfile.AmplitudeParameter2);
        AddInt(bank.Profile.ZynProfile.AutoScale ? 1 : 0);
        AddInt((int)bank.Profile.ZynProfile.Half);
        AddInt((int)bank.Profile.ZynPosition.Type);
        AddInt(bank.Profile.ZynPosition.Parameter1);
        AddInt(bank.Profile.ZynPosition.Parameter2);
        AddInt(bank.Profile.ZynPosition.Parameter3);
        foreach (var partial in bank.Partials)
        {
            AddFloat(partial.Ratio);
            AddFloat(partial.Gain);
        }

        return unchecked((int)hash);

        void AddFloat(float value) => AddUInt(BitConverter.SingleToUInt32Bits(value));
        void AddInt(int value) => AddUInt((uint)value);
        void AddUInt(uint value)
        {
            hash ^= value;
            hash *= 16777619u;
        }
    }
}

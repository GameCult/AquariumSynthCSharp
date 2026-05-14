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

        Normalize(samples);
        return samples;
    }

    private static double[] AmplitudeSpectrum(SpectralBank bank, int tableSize)
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

    private static void Normalize(float[] samples)
    {
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

    private static int StableSeed(SpectralBank bank, int tableSize)
    {
        var hash = 2166136261u;
        AddInt(tableSize);
        AddFloat(bank.RootFrequencyHz);
        AddFloat(bank.SpreadRatio);
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

using NAudio.Dsp;
using NAudio.Wave;

namespace WinRetroAmp.Core.Audio;

/// <summary>
/// Pass-through provider that keeps the most recent mono samples for the visualizer. The UI
/// polls <see cref="GetSpectrum"/> and <see cref="GetWaveform"/> at its own frame rate.
/// </summary>
public sealed class Analyzer
{
    public const int FftSize = 512;
    private const int RingSize = 4096;
    private readonly float[] _ring = new float[RingSize];
    private readonly Lock _lock = new();
    private int _write;
    private int _sampleRate = 44100;

    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_ring);
            _write = 0;
        }
    }

    internal void Push(ReadOnlySpan<float> interleaved, int channels, int sampleRate)
    {
        lock (_lock)
        {
            _sampleRate = sampleRate;
            for (int i = 0; i + channels - 1 < interleaved.Length; i += channels)
            {
                float sum = 0;
                for (int c = 0; c < channels; c++) sum += interleaved[i + c];
                _ring[_write] = sum / channels;
                _write = (_write + 1) % RingSize;
            }
        }
    }

    private void CopyLatest(Span<float> dest)
    {
        lock (_lock)
        {
            int start = (_write - dest.Length + RingSize) % RingSize;
            for (int i = 0; i < dest.Length; i++) dest[i] = _ring[(start + i) % RingSize];
        }
    }

    /// <summary>Returns <paramref name="count"/> samples (−1…1) for the oscilloscope.</summary>
    public float[] GetWaveform(int count)
    {
        var raw = new float[count * 4];
        CopyLatest(raw);
        var result = new float[count];
        for (int i = 0; i < count; i++) result[i] = raw[i * 4];
        return result;
    }

    /// <summary>
    /// Returns <paramref name="bars"/> levels in 0…1, log-spaced from ~40 Hz to ~16 kHz,
    /// computed from a Hann-windowed 512-point FFT of the latest samples.
    /// </summary>
    public double[] GetSpectrum(int bars)
    {
        var samples = new float[FftSize];
        CopyLatest(samples);
        int rate;
        lock (_lock) rate = _sampleRate;
        return ComputeSpectrum(samples, rate, bars);
    }

    public static double[] ComputeSpectrum(ReadOnlySpan<float> samples, int sampleRate, int bars)
    {
        int n = samples.Length;
        int m = (int)Math.Log2(n);
        var data = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            data[i].X = (float)(samples[i] * FastFourierTransform.HannWindow(i, n));
            data[i].Y = 0;
        }
        FastFourierTransform.FFT(true, m, data);

        var result = new double[bars];
        double binHz = sampleRate / (double)n;
        double fMin = 40, fMax = Math.Min(16000, sampleRate / 2.0);
        for (int b = 0; b < bars; b++)
        {
            double f0 = fMin * Math.Pow(fMax / fMin, b / (double)bars);
            double f1 = fMin * Math.Pow(fMax / fMin, (b + 1) / (double)bars);
            int i0 = Math.Clamp((int)(f0 / binHz), 1, n / 2 - 1);
            int i1 = Math.Clamp((int)Math.Ceiling(f1 / binHz), i0 + 1, n / 2);
            double peak = 0;
            for (int i = i0; i < i1; i++)
                peak = Math.Max(peak, Math.Sqrt(data[i].X * data[i].X + data[i].Y * data[i].Y));
            // NAudio's forward FFT divides by n; a full-scale sine lands near 0.25 after the Hann window.
            double db = 20 * Math.Log10(peak * 4 + 1e-9);
            result[b] = Math.Clamp((db + 60) / 60, 0, 1);
        }
        return result;
    }
}

/// <summary>Feeds the analyzer with everything that passes through.</summary>
public sealed class MeteringSampleProvider(ISampleProvider source, Analyzer analyzer) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        analyzer.Push(buffer[..read], WaveFormat.Channels, WaveFormat.SampleRate);
        return read;
    }
}

/// <summary>Software volume (0…100, perceptual curve) and balance (−100 left … +100 right) on stereo audio.</summary>
public sealed class VolumeBalanceSampleProvider(ISampleProvider source) : ISampleProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;

    public volatile int Volume = 100;
    public volatile int Balance;

    public static float Gain(int volume) => (float)Math.Pow(Math.Clamp(volume, 0, 100) / 100.0, 2);

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        float g = Gain(Volume);
        int bal = Math.Clamp(Balance, -100, 100);
        float left = g * (bal > 0 ? 1 - bal / 100f : 1);
        float right = g * (bal < 0 ? 1 + bal / 100f : 1);
        if (WaveFormat.Channels == 2)
        {
            for (int i = 0; i + 1 < read; i += 2)
            {
                buffer[i] *= left;
                buffer[i + 1] *= right;
            }
        }
        else
        {
            for (int i = 0; i < read; i++) buffer[i] *= g;
        }
        return read;
    }
}

using NAudio.Wave;

namespace WinRetroAmp.Core.Audio;

/// <summary>Equalizer state shared by the UI and the audio thread. Gains are in dB (−12…+12).</summary>
public sealed class EqualizerSettings
{
    public static readonly double[] Frequencies = [60, 170, 310, 600, 1000, 3000, 6000, 12000, 14000, 16000];
    public const double MaxDb = 12;

    private readonly double[] _bands = new double[10];
    private double _preamp;
    private bool _enabled = true;

    public event EventHandler? Changed;

    /// <summary>Bumped on every change so the audio thread knows to recompute coefficients.</summary>
    public int Version { get; private set; }

    public bool Enabled { get => _enabled; set { if (_enabled != value) { _enabled = value; Touch(); } } }

    public bool Auto { get; set; }

    public double Preamp { get => _preamp; set => SetValue(ref _preamp, value); }

    public double this[int band]
    {
        get => _bands[band];
        set => SetValue(ref _bands[band], value);
    }

    public double[] Bands => (double[])_bands.Clone();

    public void SetAll(double preamp, IReadOnlyList<double> bands)
    {
        _preamp = Math.Clamp(preamp, -MaxDb, MaxDb);
        for (int i = 0; i < 10 && i < bands.Count; i++) _bands[i] = Math.Clamp(bands[i], -MaxDb, MaxDb);
        Touch();
    }

    private void SetValue(ref double field, double value)
    {
        value = Math.Clamp(value, -MaxDb, MaxDb);
        if (Math.Abs(field - value) < 1e-9) return;
        field = value;
        Touch();
    }

    private void Touch()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>10-band peaking equalizer with preamp, as an NAudio sample provider.</summary>
public sealed class EqualizerSampleProvider : ISampleProvider
{
    private const double Q = 1.2;
    private readonly ISampleProvider _source;
    private readonly EqualizerSettings _settings;
    private readonly Biquad[,] _filters;
    private readonly int _channels;
    private int _appliedVersion = -1;
    private float _preampGain = 1;
    private bool _active;

    public EqualizerSampleProvider(ISampleProvider source, EqualizerSettings settings)
    {
        _source = source;
        _settings = settings;
        _channels = source.WaveFormat.Channels;
        _filters = new Biquad[_channels, 10];
        for (int c = 0; c < _channels; c++)
            for (int b = 0; b < 10; b++)
                _filters[c, b] = new Biquad();
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int read = _source.Read(buffer);
        if (_appliedVersion != _settings.Version) Update();
        if (!_active) return read;

        for (int i = 0; i < read; i++)
        {
            int ch = i % _channels;
            float s = buffer[i] * _preampGain;
            for (int b = 0; b < 10; b++) s = _filters[ch, b].Process(s);
            buffer[i] = s;
        }
        return read;
    }

    private void Update()
    {
        _appliedVersion = _settings.Version;
        _active = _settings.Enabled;
        _preampGain = (float)Math.Pow(10, _settings.Preamp / 20);
        double rate = WaveFormat.SampleRate;
        for (int b = 0; b < 10; b++)
        {
            double gain = _settings[b];
            for (int c = 0; c < _channels; c++) _filters[c, b].SetPeaking(rate, EqualizerSettings.Frequencies[b], Q, gain);
        }
    }
}

using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WinRetroAmp.Core.Audio;

public enum PlayerState { Stopped, Playing, Paused }

/// <summary>
/// Owns the output device and the decoding chain:
/// reader → stereo → equalizer → analyzer → volume/balance → WaveOut.
/// All events are raised on the thread that created the engine (the UI thread).
/// </summary>
public sealed class PlaybackEngine : IDisposable
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly Lock _readerLock = new();
    private WaveOut? _output;
    private WaveStream? _reader;
    private IcyStreamSource? _stream;
    private VolumeBalanceSampleProvider? _volumeStage;
    private bool _stopRequested;
    private int _volume = 75, _balance;
    private int _openGeneration;

    public EqualizerSettings Equalizer { get; } = new();
    public Analyzer Analyzer { get; } = new();

    public PlayerState State { get; private set; }
    public string? Source { get; private set; }
    public bool IsStream => _stream is not null;

    /// <summary>kbps, kHz and channel count of what is playing (0 when unknown).</summary>
    public int Bitrate { get; private set; }
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }

    /// <summary>The device index for WaveOut (−1 = Windows default).</summary>
    public int DeviceNumber { get; set; } = -1;

    /// <summary>Raised when a track reaches its end on its own (not on Stop).</summary>
    public event EventHandler? TrackEnded;
    public event EventHandler? StateChanged;
    public event EventHandler<string>? StreamTitleChanged;
    public event EventHandler<string>? Error;

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            if (_volumeStage is not null) _volumeStage.Volume = _volume;
        }
    }

    public int Balance
    {
        get => _balance;
        set
        {
            _balance = Math.Clamp(value, -100, 100);
            if (_volumeStage is not null) _volumeStage.Balance = _balance;
        }
    }

    public TimeSpan? Duration
    {
        get
        {
            lock (_readerLock) return _reader is { CanSeek: true } r ? r.TotalTime : null;
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_readerLock) return _reader?.CurrentTime ?? TimeSpan.Zero;
        }
        set
        {
            lock (_readerLock)
            {
                if (_reader is not { CanSeek: true } r) return;
                var t = value < TimeSpan.Zero ? TimeSpan.Zero : value > r.TotalTime ? r.TotalTime : value;
                r.CurrentTime = t;
            }
            Analyzer.Reset();
        }
    }

    public static readonly string[] SupportedExtensions = [".mp3", ".mp2", ".wav", ".wma", ".m4a", ".aac", ".mp4", ".flac", ".ogg", ".oga", ".aif", ".aiff", ".3gp", ".adts"];

    public static bool IsSupportedFile(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static bool IsUrl(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lists output devices for the preferences dialog (index −1 is the default device).</summary>
    public static IReadOnlyList<(int Index, string Name)> GetDevices()
    {
        var list = new List<(int, string)> { (-1, "Windows default device") };
        for (int i = 0; i < WaveOut.DeviceCount; i++)
            list.Add((i, WaveOut.GetCapabilities(i).ProductName));
        return list;
    }

    /// <summary>Opens a file or URL and starts playing it.</summary>
    public async Task PlayAsync(string source)
    {
        int generation = ++_openGeneration;
        CloseCurrent();
        Source = source;
        try
        {
            ISampleProvider provider;
            if (IsUrl(source))
            {
                provider = await OpenUrlAsync(source, generation);
            }
            else
            {
                var reader = await Task.Run(() => OpenFile(source));
                if (generation != _openGeneration) { reader.Dispose(); return; }
                lock (_readerLock) _reader = reader;
                SampleRate = reader.WaveFormat.SampleRate;
                Channels = reader.WaveFormat.Channels;
                Bitrate = 0;
                provider = new LockedSampleProvider(reader.ToSampleProvider(), _readerLock);
            }
            if (generation != _openGeneration) return;

            if (provider.WaveFormat.Channels == 1) provider = new MonoToStereoSampleProvider(provider);
            else if (provider.WaveFormat.Channels > 2) provider = new DownmixToStereo(provider);
            provider = new EqualizerSampleProvider(provider, Equalizer);
            provider = new MeteringSampleProvider(provider, Analyzer);
            _volumeStage = new VolumeBalanceSampleProvider(provider) { Volume = _volume, Balance = _balance };

            _output = new WaveOut { DeviceNumber = DeviceNumber, BufferMilliseconds = 60, NumberOfBuffers = 4 };
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_volumeStage.ToWaveProvider());
            _stopRequested = false;
            _output.Play();
            SetState(PlayerState.Playing);
        }
        catch (Exception ex) when (generation == _openGeneration)
        {
            CloseCurrent();
            SetState(PlayerState.Stopped);
            Error?.Invoke(this, $"Cannot play \"{source}\": {ex.Message}");
        }
    }

    private static WaveStream OpenFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".ogg" or ".oga") return new VorbisWaveReader(path);
        if (ext is ".wav") return new WaveFileReader(path);
        if (ext is ".aif" or ".aiff") return new AiffFileReader(path);
        return new MediaFoundationReader(path);
    }

    private async Task<ISampleProvider> OpenUrlAsync(string url, int generation)
    {
        var response = await IcyStreamSource.ConnectAsync(url, CancellationToken.None);
        string? type = response.Content.Headers.ContentType?.MediaType;
        bool live = response.Content.Headers.ContentLength is null || response.Headers.Contains("icy-metaint");
        if (live && type is "audio/mpeg" or "audio/mp3" or "audio/x-mpeg")
        {
            var src = new IcyStreamSource(_context);
            src.TitleChanged += t => StreamTitleChanged?.Invoke(this, t);
            src.Failed += ex =>
            {
                Error?.Invoke(this, "Stream interrupted: " + ex.Message);
                Stop();
            };
            _stream = src;
            await src.StartAsync(url, response);
            Bitrate = src.Bitrate;
            SampleRate = src.SampleRate;
            Channels = src.Channels;
            if (src.StationName is { Length: > 0 } name) StreamTitleChanged?.Invoke(this, name);
            return src.Buffer!.ToSampleProvider();
        }

        // A plain file on a web server: let Media Foundation download it progressively.
        response.Dispose();
        var reader = await Task.Run(() => new MediaFoundationReader(url));
        if (generation != _openGeneration) { reader.Dispose(); throw new OperationCanceledException(); }
        lock (_readerLock) _reader = reader;
        SampleRate = reader.WaveFormat.SampleRate;
        Channels = reader.WaveFormat.Channels;
        return new LockedSampleProvider(reader.ToSampleProvider(), _readerLock);
    }

    /// <summary>Overrides the reported bitrate (taken from tags for files).</summary>
    public void SetBitrate(int kbps) => Bitrate = kbps;

    public void Pause()
    {
        if (State != PlayerState.Playing || _output is null) return;
        _output.Pause();
        SetState(PlayerState.Paused);
    }

    public void Resume()
    {
        if (State != PlayerState.Paused || _output is null) return;
        _output.Play();
        SetState(PlayerState.Playing);
    }

    public void Stop()
    {
        _openGeneration++;
        CloseCurrent();
        SetState(PlayerState.Stopped);
    }

    private void CloseCurrent()
    {
        _stopRequested = true;
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            try { _output.Stop(); } catch { /* device already gone */ }
            _output.Dispose();
            _output = null;
        }
        _stream?.Dispose();
        _stream = null;
        lock (_readerLock)
        {
            _reader?.Dispose();
            _reader = null;
        }
        _volumeStage = null;
        Bitrate = SampleRate = Channels = 0;
        Analyzer.Reset();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        void Handle()
        {
            if (!ReferenceEquals(sender, _output)) return;
            if (e.Exception is not null)
            {
                CloseCurrent();
                SetState(PlayerState.Stopped);
                Error?.Invoke(this, e.Exception.Message);
                return;
            }
            if (_stopRequested) return;
            CloseCurrent();
            SetState(PlayerState.Stopped);
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }

        if (_context is null || SynchronizationContext.Current == _context) Handle();
        else _context.Post(_ => Handle(), null);
    }

    private void SetState(PlayerState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        CloseCurrent();
    }

    /// <summary>Serializes reads with seeks so the UI thread can move the position safely.</summary>
    private sealed class LockedSampleProvider(ISampleProvider inner, Lock gate) : ISampleProvider
    {
        public WaveFormat WaveFormat => inner.WaveFormat;

        public int Read(Span<float> buffer)
        {
            lock (gate) return inner.Read(buffer);
        }
    }

    /// <summary>Folds 5.1 and other multichannel sources down to stereo.</summary>
    private sealed class DownmixToStereo : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[] _scratch = [];

        public DownmixToStereo(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(Span<float> buffer)
        {
            int frames = buffer.Length / 2;
            int need = frames * _channels;
            if (_scratch.Length < need) _scratch = new float[need];
            int read = _source.Read(_scratch.AsSpan(0, need));
            int got = read / _channels;
            for (int f = 0; f < got; f++)
            {
                float l = 0, r = 0;
                for (int c = 0; c < _channels; c++)
                {
                    float s = _scratch[f * _channels + c];
                    if (c % 2 == 0) l += s; else r += s;
                }
                buffer[f * 2] = l / ((_channels + 1) / 2);
                buffer[f * 2 + 1] = r / (_channels / 2);
            }
            return got * 2;
        }
    }
}

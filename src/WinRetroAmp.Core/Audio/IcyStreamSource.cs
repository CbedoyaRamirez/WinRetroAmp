using System.Net.Http.Headers;
using System.Text;
using NAudio.Wave;

namespace WinRetroAmp.Core.Audio;

/// <summary>
/// Plays an endless HTTP/Shoutcast MP3 radio stream: strips ICY metadata (reporting the
/// "StreamTitle"), decodes MP3 frames with ACM and buffers PCM for the output device.
/// </summary>
internal sealed class IcyStreamSource : IDisposable
{
    private static readonly HttpClient Http = CreateClient();
    private readonly CancellationTokenSource _cts = new();
    private readonly SynchronizationContext? _context;
    private BufferedWaveProvider? _buffer;
    private Task? _pump;

    public IcyStreamSource(SynchronizationContext? context) => _context = context;

    public event Action<string>? TitleChanged;
    public event Action<Exception>? Failed;

    public BufferedWaveProvider? Buffer => _buffer;
    public int Bitrate { get; private set; }
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public string? StationName { get; private set; }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true }) { Timeout = Timeout.InfiniteTimeSpan };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WinRetroAmp/1.0");
        return c;
    }

    /// <summary>Connects, waits for the first frame and returns once PCM is flowing.</summary>
    public async Task StartAsync(string url, HttpResponseMessage response)
    {
        var headers = response.Headers;
        int metaInt = TryHeader(headers, response.Content.Headers, "icy-metaint") is { } mi && int.TryParse(mi, out int v) ? v : 0;
        StationName = TryHeader(headers, response.Content.Headers, "icy-name");
        Stream net = await response.Content.ReadAsStreamAsync(_cts.Token);
        Stream audio = metaInt > 0 ? new IcyMetadataStripper(net, metaInt, t => Post(() => TitleChanged?.Invoke(t))) : net;
        var reader = new ReadFullyStream(audio);

        // Read the first frame synchronously (off the UI thread) to learn the format.
        Mp3Frame first = await Task.Run(() => Mp3Frame.LoadFromStream(reader), _cts.Token) ?? throw new InvalidDataException("The stream is not MP3 audio.");
        Bitrate = first.BitRate / 1000;
        SampleRate = first.SampleRate;
        Channels = first.ChannelMode == ChannelMode.Mono ? 1 : 2;
        var mp3Format = new Mp3WaveFormat(first.SampleRate, Channels, first.FrameLength, first.BitRate);
        var decompressor = new AcmMp3FrameDecompressor(mp3Format);
        _buffer = new BufferedWaveProvider(decompressor.OutputFormat, TimeSpan.FromSeconds(20)) { DiscardOnBufferOverflow = true };

        var pcm = new byte[16384 * 4];
        int n = decompressor.DecompressFrame(first, pcm, 0);
        _buffer.AddSamples(pcm, 0, n);
        _pump = Task.Run(() => Pump(reader, decompressor, pcm), _cts.Token);

        // Pre-buffer a little so playback does not stutter right away.
        var until = DateTime.UtcNow.AddSeconds(5);
        while (_buffer.BufferedDuration < TimeSpan.FromSeconds(1.5) && DateTime.UtcNow < until && !_cts.IsCancellationRequested)
            await Task.Delay(50, _cts.Token);
    }

    private void Pump(Stream reader, AcmMp3FrameDecompressor decompressor, byte[] pcm)
    {
        try
        {
            using (decompressor)
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_buffer!.BufferedDuration > TimeSpan.FromSeconds(15))
                    {
                        Thread.Sleep(200);
                        continue;
                    }
                    var frame = Mp3Frame.LoadFromStream(reader);
                    if (frame is null) break;
                    int n = decompressor.DecompressFrame(frame, pcm, 0);
                    _buffer.AddSamples(pcm, 0, n);
                }
            }
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            Post(() => Failed?.Invoke(ex));
        }
        catch
        {
            // Cancelled: the connection was closed on purpose.
        }
    }

    private void Post(Action a)
    {
        if (_context is null) a();
        else _context.Post(_ => a(), null);
    }

    private static string? TryHeader(HttpResponseHeaders h, HttpContentHeaders ch, string name) =>
        h.TryGetValues(name, out var v) || ch.TryGetValues(name, out v) ? v.FirstOrDefault() : null;

    /// <summary>Opens a URL asking for ICY metadata. The caller decides if it is a live stream.</summary>
    public static async Task<HttpResponseMessage> ConnectAsync(string url, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Icy-MetaData", "1");
        var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        return resp;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>Removes the metadata blocks injected every <c>metaInt</c> bytes.</summary>
    private sealed class IcyMetadataStripper : Stream
    {
        private readonly Stream inner;
        private readonly int interval;
        private readonly Action<string> onTitle;
        private int _untilMeta;

        public IcyMetadataStripper(Stream inner, int interval, Action<string> onTitle)
        {
            this.inner = inner;
            this.interval = interval;
            this.onTitle = onTitle;
            _untilMeta = interval;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_untilMeta == 0)
            {
                ReadMetadata();
                _untilMeta = interval;
            }
            int n = inner.Read(buffer, offset, Math.Min(count, _untilMeta));
            _untilMeta -= n;
            return n;
        }

        private void ReadMetadata()
        {
            int len = inner.ReadByte();
            if (len <= 0) return;
            var meta = new byte[len * 16];
            inner.ReadExactly(meta);
            string text = Encoding.UTF8.GetString(meta).TrimEnd('\0');
            const string key = "StreamTitle='";
            int i = text.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return;
            int end = text.IndexOf("';", i + key.Length, StringComparison.Ordinal);
            string title = end > 0 ? text[(i + key.Length)..end] : text[(i + key.Length)..].TrimEnd('\'', ';');
            if (!string.IsNullOrWhiteSpace(title)) onTitle(title.Trim());
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Mp3Frame.LoadFromStream needs reads that return exactly what was asked for.</summary>
    private sealed class ReadFullyStream(Stream inner) : Stream
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = inner.Read(buffer, offset + total, count - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

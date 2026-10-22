using System.Threading.Channels;
using System.Windows.Threading;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Eq;
using WinRetroAmp.Core.Playlist;
using WinRetroAmp.Core.Settings;
using WinRetroAmp.Core.Skins;
using WinRetroAmp.Core.Tags;
using PlaylistModel = WinRetroAmp.Core.Playlist.Playlist;

namespace WinRetroAmp.Controller;

/// <summary>
/// The player's brain: owns settings, the audio engine, the playlist, the skin and the preset
/// library, and exposes the classic transport behaviour. Windows only render its state.
/// </summary>
public sealed class PlayerController : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Channel<PlaylistItem> _metadataQueue = Channel.CreateUnbounded<PlaylistItem>();
    private readonly CancellationTokenSource _cts = new();
    private string? _message;
    private DateTime _messageUntil;
    private bool _changePending;

    public PlayerController()
    {
        Settings = SettingsStore.Load();
        Presets = new PresetLibrary(SettingsStore.PresetsPath);
        Engine = new PlaybackEngine
        {
            Volume = Settings.Volume,
            Balance = Settings.Balance,
            DeviceNumber = Settings.DeviceNumber,
        };
        Engine.Equalizer.SetAll(Settings.EqPreamp, Settings.EqBands);
        Engine.Equalizer.Enabled = Settings.EqEnabled;
        Engine.Equalizer.Auto = Settings.EqAuto;
        Engine.Equalizer.Changed += (_, _) => RaiseChanged();
        Engine.StateChanged += (_, _) => RaiseChanged();
        Engine.TrackEnded += (_, _) => OnTrackEnded();
        Engine.Error += (_, msg) => ShowMessage(msg, 4);
        Engine.StreamTitleChanged += (_, title) =>
        {
            StreamTitle = title;
            RaiseChanged();
        };

        Playlist = new PlaylistModel { Shuffle = Settings.Shuffle, Repeat = Settings.Repeat };
        Playlist.Changed += (_, _) => RaiseChanged();

        Skin = LoadSkinOrDefault(Settings.SkinPath);
        _ = Task.Run(() => MetadataWorker(_cts.Token));
    }

    public AppSettings Settings { get; }
    public PresetLibrary Presets { get; }
    public PlaybackEngine Engine { get; }
    public PlaylistModel Playlist { get; }
    public Skin Skin { get; private set; }
    public TrackInfo? CurrentInfo { get; private set; }
    public string? StreamTitle { get; private set; }

    /// <summary>Raised (coalesced, on the UI thread) whenever anything visible changed.</summary>
    public event EventHandler? Changed;
    public event EventHandler? SkinChanged;

    public void RaiseChanged()
    {
        if (_changePending) return;
        _changePending = true;
        _dispatcher.BeginInvoke(() =>
        {
            _changePending = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Render);
    }

    // ------------------------------------------------------------------ marquee messages

    /// <summary>Text that temporarily replaces the song title (volume, seek, errors...).</summary>
    public string? Message => _message is not null && DateTime.UtcNow < _messageUntil ? _message : null;

    public void ShowMessage(string text, double seconds = 1.2)
    {
        _message = text;
        _messageUntil = DateTime.UtcNow.AddSeconds(seconds);
        RaiseChanged();
    }

    public void ClearMessage() => _message = null;

    /// <summary>The scrolling title: "3. Artist - Title (4:12)".</summary>
    public string MarqueeText
    {
        get
        {
            var item = Playlist.Current;
            if (item is null) return "WinRetro Amp  1.0";
            int n = Playlist.CurrentIndex + 1;
            if (PlaybackEngine.IsUrl(item.Path))
                return $"{n}. {(string.IsNullOrWhiteSpace(StreamTitle) ? item.DisplayTitle : StreamTitle)}";
            string dur = item.Duration is { } d ? $" ({SkinDraw.FormatTime(d)})" : "";
            return $"{n}. {item.DisplayTitle}{dur}";
        }
    }

    // ------------------------------------------------------------------ transport

    public PlayerState State => Engine.State;

    public async Task PlayIndexAsync(int index)
    {
        if (index < 0 || index >= Playlist.Count) return;
        Playlist.SetCurrent(index);
        var item = Playlist[index];
        StreamTitle = null;
        CurrentInfo = null;
        ClearMessage();

        if (!PlaybackEngine.IsUrl(item.Path))
        {
            var info = await Task.Run(() => TrackInfo.TryRead(item.Path));
            if (Playlist.Current != item) return;
            CurrentInfo = info;
            if (info is not null)
            {
                item.Title = info.Format(Settings.TitleFormat);
                if (info.Duration > TimeSpan.Zero) item.Duration = info.Duration;
            }
            ApplyAutoPreset(item.Path);
        }

        await Engine.PlayAsync(item.Path);
        if (CurrentInfo is { Bitrate: > 0 } ci) Engine.SetBitrate(ci.Bitrate);
        RaiseChanged();
    }

    /// <summary>Play button: resume, restart the current track, or start the selected/first one.</summary>
    public async Task PlayAsync()
    {
        switch (Engine.State)
        {
            case PlayerState.Paused:
                Engine.Resume();
                return;
            case PlayerState.Playing:
                if (Engine.Duration is not null) Engine.Position = TimeSpan.Zero;
                return;
        }
        if (Playlist.Count == 0) return;
        int index = Playlist.CurrentIndex >= 0 ? Playlist.CurrentIndex : 0;
        await PlayIndexAsync(index);
    }

    public void PauseToggle()
    {
        if (Engine.State == PlayerState.Playing) Engine.Pause();
        else if (Engine.State == PlayerState.Paused) Engine.Resume();
    }

    public void Stop() => Engine.Stop();

    public async Task NextAsync(int count = 1)
    {
        int index = Playlist.CurrentIndex;
        for (int i = 0; i < count; i++)
        {
            int next = i == 0 ? Playlist.PeekNext(userInitiated: true) : Math.Min(index + 1, Playlist.Count - 1);
            if (next < 0) return;
            index = next;
        }
        await GoToAsync(index);
    }

    public async Task PreviousAsync(int count = 1)
    {
        int index = Playlist.CurrentIndex;
        for (int i = 0; i < count; i++)
        {
            int prev = i == 0 ? Playlist.PeekPrevious() : Math.Max(index - 1, 0);
            if (prev < 0) return;
            index = prev;
        }
        await GoToAsync(index);
    }

    private async Task GoToAsync(int index)
    {
        if (Engine.State == PlayerState.Stopped)
        {
            Playlist.SetCurrent(index);
            StreamTitle = null;
            return;
        }
        await PlayIndexAsync(index);
    }

    private async void OnTrackEnded()
    {
        int next = Playlist.PeekNext(userInitiated: false);
        if (next < 0)
        {
            RaiseChanged();
            return;
        }
        await PlayIndexAsync(next);
    }

    public void Seek(TimeSpan position)
    {
        if (Engine.Duration is null) return;
        Engine.Position = position;
        RaiseChanged();
    }

    public void SeekBy(double seconds) => Seek(Engine.Position + TimeSpan.FromSeconds(seconds));

    public int Volume
    {
        get => Engine.Volume;
        set
        {
            Engine.Volume = value;
            Settings.Volume = Engine.Volume;
            ShowMessage($"VOLUME: {Engine.Volume}%");
        }
    }

    public int Balance
    {
        get => Engine.Balance;
        set
        {
            Engine.Balance = Math.Abs(value) < 6 ? 0 : value;
            Settings.Balance = Engine.Balance;
            int b = Engine.Balance;
            ShowMessage(b == 0 ? "BALANCE: CENTER" : $"BALANCE: {Math.Abs(b)}% {(b < 0 ? "LEFT" : "RIGHT")}");
        }
    }

    public void ToggleShuffle()
    {
        Playlist.Shuffle = Settings.Shuffle = !Playlist.Shuffle;
        RaiseChanged();
    }

    public void ToggleRepeat()
    {
        Playlist.Repeat = Settings.Repeat = !Playlist.Repeat;
        RaiseChanged();
    }

    // ------------------------------------------------------------------ playlist building

    /// <summary>Adds files/folders/playlists/URLs. With <paramref name="replace"/> the list is cleared and playback starts.</summary>
    public async Task OpenAsync(IEnumerable<string> paths, bool replace, int insertAt = -1)
    {
        var list = paths.ToList();
        var items = await Task.Run(() => PlaylistFormats.Expand(list));
        if (items.Count == 0) return;
        if (replace)
        {
            Engine.Stop();
            Playlist.Clear();
        }
        int at = insertAt < 0 ? Playlist.Count : insertAt;
        Playlist.Insert(at, items);
        foreach (var i in items) _metadataQueue.Writer.TryWrite(i);
        if (replace) await PlayIndexAsync(at);
    }

    public void QueueMetadata(IEnumerable<PlaylistItem> items)
    {
        foreach (var i in items) _metadataQueue.Writer.TryWrite(i);
    }

    private async Task MetadataWorker(CancellationToken ct)
    {
        try
        {
            await foreach (var item in _metadataQueue.Reader.ReadAllAsync(ct))
            {
                if (PlaybackEngine.IsUrl(item.Path) || (item.Title is not null && item.Duration is not null)) continue;
                var info = TrackInfo.TryRead(item.Path);
                if (info is null) continue;
                string title = info.Format(Settings.TitleFormat);
                await _dispatcher.InvokeAsync(() =>
                {
                    item.Title = title;
                    if (info.Duration > TimeSpan.Zero) item.Duration = info.Duration;
                    RaiseChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>Re-reads every title (after the title format changed or tags were edited).</summary>
    public void RefreshTitles(IEnumerable<PlaylistItem>? items = null)
    {
        foreach (var i in items ?? Playlist.Items)
        {
            if (PlaybackEngine.IsUrl(i.Path)) continue;
            i.Title = null;
            i.Duration = null;
            _metadataQueue.Writer.TryWrite(i);
        }
    }

    // ------------------------------------------------------------------ equalizer

    public void ApplyPreset(EqPreset preset)
    {
        Engine.Equalizer.SetAll(preset.Preamp, preset.Bands);
        SaveEqToSettings();
    }

    public void SaveEqToSettings()
    {
        var eq = Engine.Equalizer;
        Settings.EqBands = eq.Bands;
        Settings.EqPreamp = eq.Preamp;
        Settings.EqEnabled = eq.Enabled;
        Settings.EqAuto = eq.Auto;
    }

    public void SaveAutoPreset(string path)
    {
        var eq = Engine.Equalizer;
        Settings.AutoPresets[path] = [.. eq.Bands, eq.Preamp];
    }

    private void ApplyAutoPreset(string path)
    {
        if (!Engine.Equalizer.Auto || !Settings.AutoPresets.TryGetValue(path, out var v) || v.Length != 11) return;
        Engine.Equalizer.SetAll(v[10], v[..10]);
        SaveEqToSettings();
    }

    // ------------------------------------------------------------------ skins

    private static Skin LoadSkinOrDefault(string? path)
    {
        if (string.IsNullOrEmpty(path)) return SkinLoader.Default;
        try
        {
            return SkinLoader.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            return SkinLoader.Default;
        }
    }

    public bool LoadSkin(string? path)
    {
        try
        {
            Skin = path is null ? SkinLoader.Default : SkinLoader.Load(path);
            Settings.SkinPath = path;
            SkinChanged?.Invoke(this, EventArgs.Empty);
            RaiseChanged();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowMessage("CANNOT LOAD SKIN: " + ex.Message, 4);
            return false;
        }
    }

    // ------------------------------------------------------------------ persistence

    public void RestoreLastSession()
    {
        if (!File.Exists(SettingsStore.LastPlaylistPath)) return;
        try
        {
            var items = PlaylistFormats.Load(SettingsStore.LastPlaylistPath);
            Playlist.Add(items);
            QueueMetadata(items);
            if (Settings.LastIndex >= 0 && Settings.LastIndex < Playlist.Count) Playlist.SetCurrent(Settings.LastIndex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to restore.
        }
    }

    public void SaveSession()
    {
        Settings.LastIndex = Playlist.CurrentIndex;
        Settings.LastPositionSeconds = Engine.Position.TotalSeconds;
        Settings.Shuffle = Playlist.Shuffle;
        Settings.Repeat = Playlist.Repeat;
        SaveEqToSettings();
        try
        {
            SettingsStore.Save(Settings);
            PlaylistFormats.Save(SettingsStore.LastPlaylistPath, Playlist.Items);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Read-only profile: settings simply are not remembered.
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        Engine.Dispose();
    }
}

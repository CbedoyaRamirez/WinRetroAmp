using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Playlist;
using WinRetroAmp.Core.Settings;
using WinRetroAmp.Services;
using WinRetroAmp.Ui.Dialogs;

namespace WinRetroAmp.Ui;

/// <summary>Creates the three windows and hosts every app-level action (menus and shortcuts call these).</summary>
public sealed class AppShell
{
    private readonly DispatcherTimer _frame;
    private TrayIcon? _tray;
    private GlobalHotkeys? _hotkeys;
    private bool _exiting;

    public AppShell(PlayerController controller)
    {
        Instance = this;
        C = controller;
        Docking = new WindowDocking();
        Main = new MainPlayerWindow(C, Docking);
        Equalizer = new EqualizerWindow(C, Docking);
        PlaylistWindow = new PlaylistWindow(C, Docking);
        Docking.Main = Main;
        Docking.Register(Main);
        Docking.Register(Equalizer);
        Docking.Register(PlaylistWindow);

        // Alt+F4 or the taskbar's "Close window" on the main window quits; on the others it just hides them.
        Main.Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            Exit();
        };
        Equalizer.Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            if (Equalizer.IsVisible) ToggleEqualizer();
        };
        PlaylistWindow.Closing += (_, e) =>
        {
            if (_exiting) return;
            e.Cancel = true;
            if (PlaylistWindow.IsVisible) TogglePlaylist();
        };

        C.Changed += (_, _) => InvalidateAll();
        C.SkinChanged += (_, _) => InvalidateAll();
        _frame = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _frame.Tick += (_, _) =>
        {
            Main.Tick();
            Equalizer.Tick();
            PlaylistWindow.Tick();
        };
    }

    public static AppShell Instance { get; private set; } = null!;

    public PlayerController C { get; }
    public WindowDocking Docking { get; }
    public MainPlayerWindow Main { get; }
    public EqualizerWindow Equalizer { get; }
    public PlaylistWindow PlaylistWindow { get; }

    public void InvalidateAll()
    {
        Main.Invalidate();
        Equalizer.Invalidate();
        PlaylistWindow.Invalidate();
    }

    public void Show()
    {
        var s = C.Settings;
        Main.Topmost = s.AlwaysOnTop;
        Main.Show();
        Main.UpdateSize();
        // Owned windows minimize with the main window and stay out of the taskbar.
        Equalizer.Owner = Main;
        PlaylistWindow.Owner = Main;
        double scale = Main.Scale;
        if (double.IsNaN(s.Main.Left) || !OnScreen(s.Main.Left, s.Main.Top))
        {
            s.Main.Left = (SystemParameters.WorkArea.Width - Main.Width) / 2;
            s.Main.Top = Math.Max(0, (SystemParameters.WorkArea.Height - 116 * scale * 3) / 2);
            s.Equalizer.Left = s.Playlist.Left = s.Main.Left;
            s.Equalizer.Top = s.Main.Top + 116 * scale;
            s.Playlist.Top = s.Equalizer.Top + 116 * scale;
        }
        Main.Left = s.Main.Left;
        Main.Top = s.Main.Top;
        Place(Equalizer, s.Equalizer);
        Place(PlaylistWindow, s.Playlist);
        _frame.Start();

        _hotkeys = new GlobalHotkeys(Main, C);
        if (s.ShowTrayIcon) _tray = new TrayIcon(C, this);
    }

    private void Place(SkinnedWindow w, Core.Settings.WindowState state)
    {
        w.Left = double.IsNaN(state.Left) ? Main.Left : state.Left;
        w.Top = double.IsNaN(state.Top) ? Main.Top + Main.Height : state.Top;
        if (state.Visible)
        {
            w.Show();
            w.UpdateSize();
        }
    }

    private static bool OnScreen(double left, double top) =>
        left > SystemParameters.VirtualScreenLeft - 200 && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 20
        && top > SystemParameters.VirtualScreenTop - 10 && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 10;

    // ------------------------------------------------------------------ windows

    public void ToggleEqualizer() => ToggleWindow(Equalizer, C.Settings.Equalizer);
    public void TogglePlaylist() => ToggleWindow(PlaylistWindow, C.Settings.Playlist);

    private void ToggleWindow(SkinnedWindow w, Core.Settings.WindowState state)
    {
        if (w.IsVisible)
        {
            w.Hide();
            state.Visible = false;
        }
        else
        {
            w.Show();
            w.UpdateSize();
            state.Visible = true;
            w.Activate();
        }
        Main.Invalidate();
    }

    public void ToggleMainShade() => Main.Shaded = !Main.Shaded;
    public void ToggleEqShade() => Equalizer.Shaded = !Equalizer.Shaded;
    public void TogglePlaylistShade() => PlaylistWindow.Shaded = !PlaylistWindow.Shaded;

    public void ToggleDoubleSize()
    {
        Docking.Rescale(() =>
        {
            C.Settings.DoubleSize = !C.Settings.DoubleSize;
            Main.UpdateSize();
            Equalizer.UpdateSize();
            PlaylistWindow.UpdateSize();
        });
    }

    public void ToggleAlwaysOnTop()
    {
        C.Settings.AlwaysOnTop = !C.Settings.AlwaysOnTop;
        Main.Topmost = C.Settings.AlwaysOnTop;
        Main.Invalidate();
    }

    public void Minimize()
    {
        if (C.Settings.MinimizeToTray && _tray is not null)
        {
            Main.Hide();
            Equalizer.Hide();
            PlaylistWindow.Hide();
        }
        else
        {
            Main.WindowState = System.Windows.WindowState.Minimized;
        }
    }

    public void Restore()
    {
        Main.Show();
        if (Main.WindowState == System.Windows.WindowState.Minimized) Main.WindowState = System.Windows.WindowState.Normal;
        if (C.Settings.Equalizer.Visible) Equalizer.Show();
        if (C.Settings.Playlist.Visible) PlaylistWindow.Show();
        Main.Activate();
    }

    public void SetTrayIcon(bool show)
    {
        C.Settings.ShowTrayIcon = show;
        if (show && _tray is null) _tray = new TrayIcon(C, this);
        else if (!show && _tray is not null)
        {
            _tray.Dispose();
            _tray = null;
        }
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        var s = C.Settings;
        s.Main.Left = Main.Left;
        s.Main.Top = Main.Top;
        s.Equalizer.Left = Equalizer.Left;
        s.Equalizer.Top = Equalizer.Top;
        s.Playlist.Left = PlaylistWindow.Left;
        s.Playlist.Top = PlaylistWindow.Top;
        _frame.Stop();
        C.SaveSession();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        C.Dispose();
        Application.Current.Shutdown();
    }

    // ------------------------------------------------------------------ files

    private static string AudioFilter =>
        "All supported files|" + string.Join(";", PlaybackEngine.SupportedExtensions.Concat(PlaylistFormats.Extensions).Select(e => "*" + e))
        + "|Audio files|" + string.Join(";", PlaybackEngine.SupportedExtensions.Select(e => "*" + e))
        + "|Playlists (*.m3u;*.m3u8;*.pls)|*.m3u;*.m3u8;*.pls|All files (*.*)|*.*";

    public async Task OpenFilesDialog(bool replace)
    {
        var dlg = new OpenFileDialog
        {
            Title = replace ? "Open file(s)" : "Add file(s) to playlist",
            Filter = AudioFilter,
            Multiselect = true,
            InitialDirectory = C.Settings.LastFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        C.Settings.LastFolder = Path.GetDirectoryName(dlg.FileNames[0]);
        await C.OpenAsync(dlg.FileNames, replace);
    }

    public async Task OpenFolderDialog(bool replace)
    {
        var dlg = new OpenFolderDialog
        {
            Title = replace ? "Play folder" : "Add folder to playlist",
            InitialDirectory = C.Settings.LastFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Multiselect = true,
        };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        C.Settings.LastFolder = dlg.FolderNames[0];
        await C.OpenAsync(dlg.FolderNames, replace);
    }

    public async Task OpenUrlDialog(bool replace)
    {
        var dlg = new OpenUrlWindow(C.Settings.RecentUrls) { Owner = ActiveWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Url)) return;
        string url = dlg.Url.Trim();
        if (!url.Contains("://")) url = "http://" + url;
        C.Settings.RecentUrls.Remove(url);
        C.Settings.RecentUrls.Insert(0, url);
        if (C.Settings.RecentUrls.Count > 15) C.Settings.RecentUrls.RemoveAt(15);
        await C.OpenAsync([url], replace);
    }

    public async Task LoadPlaylistDialog()
    {
        var dlg = new OpenFileDialog { Title = "Load playlist", Filter = "Playlists (*.m3u;*.m3u8;*.pls)|*.m3u;*.m3u8;*.pls|All files (*.*)|*.*" };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        await C.OpenAsync([dlg.FileName], replace: true);
    }

    public void SavePlaylistDialog()
    {
        var dlg = new SaveFileDialog { Title = "Save playlist", Filter = "M3U8 playlist (UTF-8)|*.m3u8|M3U playlist|*.m3u|PLS playlist|*.pls", FileName = "playlist.m3u8" };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        try
        {
            PlaylistFormats.Save(dlg.FileName, C.Playlist.Items);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ActiveWindow, ex.Message, "WinRetro Amp", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Writes the playlist as a simple HTML page and opens it in the browser.</summary>
    public void ExportHtmlPlaylist()
    {
        var pl = C.Playlist;
        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><meta charset=\"utf-8\"><title>WinRetro Amp playlist</title>")
          .Append("<style>body{background:#111;color:#3bf07a;font:14px Arial,sans-serif;margin:24px}h1{color:#46d2e6;font-size:18px}")
          .Append("ol{padding-left:2.5em}li{padding:2px 0}span{color:#8e97a8;margin-left:8px}</style>")
          .Append($"<h1>WinRetro Amp playlist</h1><p>{pl.Count} tracks, {Core.Skins.SkinDraw.FormatTime(pl.TotalDuration, hours: true)}</p><ol>");
        foreach (var i in pl.Items)
        {
            string dur = i.Duration is { } d ? Core.Skins.SkinDraw.FormatTime(d) : "";
            sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(i.DisplayTitle)}<span>{dur}</span></li>");
        }
        sb.Append("</ol>");
        string path = Path.Combine(Path.GetTempPath(), "WinRetroAmp-playlist.html");
        File.WriteAllText(path, sb.ToString());
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>Runs a playlist mutation that may remove the playing track (stops playback first).</summary>
    public void StopIfRemoved(Action change)
    {
        C.Stop();
        change();
    }

    // ------------------------------------------------------------------ dialogs

    private Window ActiveWindow => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Main;

    public void ShowFileInfo(PlaylistItem? item = null)
    {
        item ??= C.Playlist.Current ?? C.Playlist.SelectedItems.FirstOrDefault();
        if (item is null) return;
        var dlg = new FileInfoWindow(item, C) { Owner = ActiveWindow };
        dlg.ShowDialog();
    }

    public void ShowJumpToFile()
    {
        var dlg = new JumpToFileWindow(C) { Owner = ActiveWindow };
        if (dlg.ShowDialog() == true && dlg.SelectedIndex >= 0)
        {
            PlaylistWindow.ScrollTo(dlg.SelectedIndex);
            _ = C.PlayIndexAsync(dlg.SelectedIndex);
        }
    }

    public void ShowPreferences()
    {
        var dlg = new PreferencesWindow(C, this) { Owner = ActiveWindow };
        dlg.ShowDialog();
        InvalidateAll();
    }

    public void ShowAbout() => new AboutWindow { Owner = ActiveWindow }.ShowDialog();

    public void LoadSkinDialog()
    {
        var dlg = new OpenFileDialog { Title = "Load skin", Filter = "Classic skins (*.wsz;*.zip)|*.wsz;*.zip|All files (*.*)|*.*" };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        // Keep a copy in the skins folder so it shows up in the skin list.
        try
        {
            Directory.CreateDirectory(SettingsStore.SkinsFolder);
            string dest = Path.Combine(SettingsStore.SkinsFolder, Path.GetFileName(dlg.FileName));
            if (!string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(dlg.FileName, dest, overwrite: true);
            C.LoadSkin(dest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            C.LoadSkin(dlg.FileName);
        }
    }

    public void ExportDefaultSkin()
    {
        var dlg = new SaveFileDialog { Title = "Export default skin", Filter = "Classic skin (*.wsz)|*.wsz", FileName = "WinRetroAmp-Graphite.wsz" };
        if (dlg.ShowDialog(ActiveWindow) == true) Core.Skins.SkinLoader.ExportDefault(dlg.FileName);
    }

    public void SavePresetDialog()
    {
        var dlg = new InputWindow("Save preset", "Preset name:", "") { Owner = ActiveWindow };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Value)) return;
        var eq = C.Engine.Equalizer;
        C.Presets.AddOrReplace(new Core.Eq.EqPreset(dlg.Value.Trim(), eq.Preamp, eq.Bands));
    }

    public void ImportEqfDialog()
    {
        var dlg = new OpenFileDialog { Title = "Load EQF", Filter = "EQ presets (*.eqf;*.q1)|*.eqf;*.q1|All files (*.*)|*.*" };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        try
        {
            var presets = Core.Eq.EqfFormat.Read(File.ReadAllBytes(dlg.FileName));
            if (presets.Count == 0) return;
            C.ApplyPreset(presets[0]);
            // Libraries with several presets (.q1) are merged into the preset list.
            if (presets.Count > 1) foreach (var p in presets) C.Presets.AddOrReplace(p);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            MessageBox.Show(ActiveWindow, ex.Message, "WinRetro Amp", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ExportEqfDialog()
    {
        var dlg = new SaveFileDialog { Title = "Save EQF", Filter = "EQ preset (*.eqf)|*.eqf", FileName = "preset.eqf" };
        if (dlg.ShowDialog(ActiveWindow) != true) return;
        var eq = C.Engine.Equalizer;
        File.WriteAllBytes(dlg.FileName, Core.Eq.EqfFormat.Write([new Core.Eq.EqPreset(Path.GetFileNameWithoutExtension(dlg.FileName), eq.Preamp, eq.Bands)]));
    }
}

using System.Windows.Input;

namespace WinRetroAmp.Ui;

/// <summary>The classic keyboard shortcuts, active in all three windows.</summary>
public static class Shortcuts
{
    public static bool Handle(SkinnedWindow window, KeyEventArgs e)
    {
        var shell = AppShell.Instance;
        var c = shell.C;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;
        bool ctrl = mods.HasFlag(ModifierKeys.Control), shift = mods.HasFlag(ModifierKeys.Shift), alt = mods.HasFlag(ModifierKeys.Alt);

        if (window is PlaylistWindow pl && !ctrl && pl.HandleKey(key, mods)) return true;

        switch (key)
        {
            case Key.Z when !ctrl && !alt: _ = c.PreviousAsync(); return true;
            case Key.X when !ctrl && !alt: _ = c.PlayAsync(); return true;
            case Key.C when !ctrl && !alt: c.PauseToggle(); return true;
            case Key.V when !ctrl && !alt: c.Stop(); return true;
            case Key.B when !ctrl && !alt: _ = c.NextAsync(); return true;
            case Key.NumPad4: _ = c.PreviousAsync(); return true;
            case Key.NumPad5: _ = c.PlayAsync(); return true;
            case Key.NumPad6: _ = c.NextAsync(); return true;
            case Key.NumPad1: _ = c.PreviousAsync(10); return true;
            case Key.NumPad3: _ = c.NextAsync(10); return true;
            case Key.NumPad8: c.Volume += 2; return true;
            case Key.NumPad2: c.Volume -= 2; return true;
            case Key.NumPad7: c.SeekBy(-5); return true;
            case Key.NumPad9: c.SeekBy(5); return true;
            case Key.Up when window is not PlaylistWindow: c.Volume += 2; return true;
            case Key.Down when window is not PlaylistWindow: c.Volume -= 2; return true;
            case Key.Left: c.SeekBy(ctrl ? -60 : -5); return true;
            case Key.Right: c.SeekBy(ctrl ? 60 : 5); return true;
            case Key.L when ctrl: _ = shell.OpenUrlDialog(window is not PlaylistWindow); return true;
            case Key.L when shift: _ = shell.OpenFolderDialog(window is not PlaylistWindow); return true;
            case Key.L: _ = shell.OpenFilesDialog(window is not PlaylistWindow); return true;
            case Key.J when !ctrl: shell.ShowJumpToFile(); return true;
            case Key.S when alt: shell.LoadSkinDialog(); return true;
            case Key.S when !ctrl: c.ToggleShuffle(); return true;
            case Key.R when !ctrl: c.ToggleRepeat(); return true;
            case Key.W when alt && !ctrl: shell.Main.Activate(); return true;
            case Key.E when alt: shell.TogglePlaylist(); return true;
            case Key.G when alt: shell.ToggleEqualizer(); return true;
            case Key.D3 when alt: shell.ShowFileInfo(); return true;
            case Key.F1 when alt: shell.ShowAbout(); return true;
            case Key.F4 when alt: shell.Exit(); return true;
            case Key.D when ctrl: shell.ToggleDoubleSize(); return true;
            case Key.A when ctrl && window is PlaylistWindow: c.Playlist.SelectAll(); return true;
            case Key.A when ctrl: shell.ToggleAlwaysOnTop(); return true;
            case Key.T when ctrl:
                c.Settings.TimeMode = c.Settings.TimeMode == Core.Settings.TimeMode.Elapsed ? Core.Settings.TimeMode.Remaining : Core.Settings.TimeMode.Elapsed;
                shell.InvalidateAll();
                return true;
            case Key.P when ctrl: shell.ShowPreferences(); return true;
            case Key.W when ctrl && shift: shell.TogglePlaylistShade(); return true;
            case Key.W when ctrl && alt: shell.ToggleEqShade(); return true;
            case Key.W when ctrl: shell.ToggleMainShade(); return true;
            case Key.I when ctrl && window is PlaylistWindow: c.Playlist.InvertSelection(); return true;
        }
        return false;
    }
}

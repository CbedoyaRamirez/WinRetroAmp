using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinRetroAmp.Core.Playlist;
using WinRetroAmp.Core.Settings;

namespace WinRetroAmp.Ui;

/// <summary>Builds the right-click and button menus (standard Windows menus, like the original).</summary>
public static class Menus
{
    private static AppShell Shell => AppShell.Instance;

    private static MenuItem Item(string header, Action action, string? gesture = null, bool? isChecked = null, bool enabled = true)
    {
        var mi = new MenuItem { Header = header, InputGestureText = gesture ?? "", IsEnabled = enabled };
        if (isChecked is { } c)
        {
            mi.IsCheckable = true;
            mi.IsChecked = c;
        }
        mi.Click += (_, _) => action();
        return mi;
    }

    private static MenuItem Sub(string header, params object[] items)
    {
        var mi = new MenuItem { Header = header };
        foreach (var i in items) mi.Items.Add(i);
        return mi;
    }

    private static void Open(UIElement target, params object[] items)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.MousePoint };
        foreach (var i in items) menu.Items.Add(i);
        menu.IsOpen = true;
    }

    public static void ShowMainMenu(SkinnedWindow w)
    {
        Open(w,
            Item("WinRetro Amp...", Shell.ShowAbout, "Alt+F1"),
            new Separator(),
            Item("Play file...", () => _ = Shell.OpenFilesDialog(true), "L"),
            Item("Play folder...", () => _ = Shell.OpenFolderDialog(true), "Shift+L"),
            Item("Play URL...", () => _ = Shell.OpenUrlDialog(true), "Ctrl+L"),
            new Separator(),
            Item("Main window", () => { }, "Alt+W", true, enabled: false),
            Item("Playlist editor", Shell.TogglePlaylist, "Alt+E", Shell.PlaylistWindow.IsVisible),
            Item("Equalizer", Shell.ToggleEqualizer, "Alt+G", Shell.Equalizer.IsVisible),
            new Separator(),
            SkinsMenu(),
            OptionsSubMenu(),
            PlaybackSubMenu(),
            VisSubMenu(),
            new Separator(),
            Item("Exit", Shell.Exit, "Alt+F4"));
    }

    public static void ShowOptionsMenu(SkinnedWindow w) => Open(w, [.. OptionsItems()]);

    private static MenuItem OptionsSubMenu() => Sub("Options", [.. OptionsItems()]);

    private static IEnumerable<object> OptionsItems()
    {
        var c = Shell.C;
        var s = c.Settings;
        yield return Item("Preferences...", Shell.ShowPreferences, "Ctrl+P");
        yield return new Separator();
        yield return Item("Time elapsed", () => s.TimeMode = TimeMode.Elapsed, "Ctrl+T toggles", s.TimeMode == TimeMode.Elapsed);
        yield return Item("Time remaining", () => s.TimeMode = TimeMode.Remaining, null, s.TimeMode == TimeMode.Remaining);
        yield return new Separator();
        yield return Item("Always on top", Shell.ToggleAlwaysOnTop, "Ctrl+A", s.AlwaysOnTop);
        yield return Item("Double size", Shell.ToggleDoubleSize, "Ctrl+D", s.DoubleSize);
        yield return Item("Windowshade mode", Shell.ToggleMainShade, "Ctrl+W", s.Main.Shaded);
        yield return new Separator();
        yield return Item("Repeat", c.ToggleRepeat, "R", c.Playlist.Repeat);
        yield return Item("Shuffle", c.ToggleShuffle, "S", c.Playlist.Shuffle);
    }

    private static MenuItem PlaybackSubMenu()
    {
        var c = Shell.C;
        return Sub("Playback",
            Item("Previous", () => _ = c.PreviousAsync(), "Z"),
            Item("Play", () => _ = c.PlayAsync(), "X"),
            Item("Pause", c.PauseToggle, "C"),
            Item("Stop", c.Stop, "V"),
            Item("Next", () => _ = c.NextAsync(), "B"),
            new Separator(),
            Item("Back 5 seconds", () => c.SeekBy(-5), "Left"),
            Item("Forward 5 seconds", () => c.SeekBy(5), "Right"),
            Item("Back 10 tracks", () => _ = c.PreviousAsync(10), "Num 1"),
            Item("Forward 10 tracks", () => _ = c.NextAsync(10), "Num 3"),
            new Separator(),
            Item("Jump to file...", Shell.ShowJumpToFile, "J"),
            Item("File info...", () => Shell.ShowFileInfo(), "Alt+3"));
    }

    public static void ShowVisMenu(SkinnedWindow w) => Open(w, [.. VisItems()]);

    private static MenuItem VisSubMenu() => Sub("Visualization", [.. VisItems()]);

    private static IEnumerable<object> VisItems()
    {
        var s = Shell.C.Settings;
        void Set(Action a)
        {
            a();
            Shell.Main.Invalidate();
        }
        return
        [
            Item("Spectrum analyzer", () => Set(() => s.VisMode = VisMode.Spectrum), null, s.VisMode == VisMode.Spectrum),
            Item("Oscilloscope", () => Set(() => s.VisMode = VisMode.Oscilloscope), null, s.VisMode == VisMode.Oscilloscope),
            Item("Off", () => Set(() => s.VisMode = VisMode.Off), null, s.VisMode == VisMode.Off),
            new Separator(),
            Item("Thick bands", () => Set(() => s.ThinBars = false), null, !s.ThinBars),
            Item("Thin bands", () => Set(() => s.ThinBars = true), null, s.ThinBars),
            Item("Show peaks", () => Set(() => s.VisPeaks = !s.VisPeaks), null, s.VisPeaks),
            new Separator(),
            Sub("Analyzer falloff",
                Item("Slowest", () => s.VisFalloff = 0, null, s.VisFalloff == 0),
                Item("Slow", () => s.VisFalloff = 1, null, s.VisFalloff == 1),
                Item("Moderate", () => s.VisFalloff = 2, null, s.VisFalloff == 2),
                Item("Fast", () => s.VisFalloff = 3, null, s.VisFalloff == 3),
                Item("Fastest", () => s.VisFalloff = 4, null, s.VisFalloff == 4)),
        ];
    }

    private static MenuItem SkinsMenu()
    {
        var c = Shell.C;
        var items = new List<object>
        {
            Item("Load skin...", Shell.LoadSkinDialog, "Alt+S"),
            Item("Open skins folder", () =>
            {
                Directory.CreateDirectory(SettingsStore.SkinsFolder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SettingsStore.SkinsFolder) { UseShellExecute = true });
            }),
            Item("Export default skin as .wsz...", Shell.ExportDefaultSkin),
            new Separator(),
            Item("<Default: Graphite>", () => c.LoadSkin(null), null, c.Settings.SkinPath is null),
        };
        foreach (var edition in Core.Skins.BuiltInSkins.All)
            items.Add(Item(edition.Name, () => c.LoadSkin(edition.Path), null, c.Settings.SkinPath == edition.Path));
        if (Directory.Exists(SettingsStore.SkinsFolder))
        {
            foreach (string f in Directory.EnumerateFiles(SettingsStore.SkinsFolder)
                         .Where(f => f.EndsWith(".wsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
            {
                string path = f;
                items.Add(Item(Path.GetFileNameWithoutExtension(f).Replace("_", "__"), () => c.LoadSkin(path), null,
                    string.Equals(c.Settings.SkinPath, path, StringComparison.OrdinalIgnoreCase)));
            }
        }
        return Sub("Skins", [.. items]);
    }

    // ------------------------------------------------------------------ equalizer

    public static void ShowPresetsMenu(SkinnedWindow w)
    {
        var c = Shell.C;
        var load = new MenuItem { Header = "Load" };
        foreach (var p in c.Presets.Presets)
        {
            var preset = p;
            load.Items.Add(Item(p.Name, () => c.ApplyPreset(preset)));
        }
        load.Items.Add(new Separator());
        load.Items.Add(Item("Auto-load preset for this file", () =>
        {
            if (c.Playlist.Current is { } cur && c.Settings.AutoPresets.TryGetValue(cur.Path, out var v) && v.Length == 11)
                c.ApplyPreset(new Core.Eq.EqPreset("auto", v[10], v[..10]));
        }, enabled: c.Playlist.Current is { } cur0 && c.Settings.AutoPresets.ContainsKey(cur0.Path)));
        load.Items.Add(Item("From EQF file...", Shell.ImportEqfDialog));

        var delete = new MenuItem { Header = "Delete" };
        foreach (var p in c.Presets.Presets)
        {
            string name = p.Name;
            delete.Items.Add(Item(name, () => c.Presets.Remove(name)));
        }

        Open(w,
            load,
            Sub("Save",
                Item("Preset...", Shell.SavePresetDialog),
                Item("Auto-load preset for this file", () =>
                {
                    if (c.Playlist.Current is { } cur) c.SaveAutoPreset(cur.Path);
                }, enabled: c.Playlist.Current is not null),
                Item("To EQF file...", Shell.ExportEqfDialog)),
            delete,
            new Separator(),
            Item("Reset to flat", () => c.ApplyPreset(Core.Eq.PresetLibrary.BuiltIn[0])));
    }

    // ------------------------------------------------------------------ playlist

    public static void ShowRemoveMiscMenu(SkinnedWindow w)
    {
        var pl = Shell.C.Playlist;
        Open(w,
            Item("Remove duplicate entries", pl.RemoveDuplicates),
            Item("Remove dead files", pl.RemoveDead),
            Item("Remove all", () => Shell.StopIfRemoved(pl.Clear)));
    }

    public static void ShowSortMenu(SkinnedWindow w)
    {
        var pl = Shell.C.Playlist;
        Open(w,
            Item("Sort list by title", () => pl.Sort(SortKey.Title)),
            Item("Sort list by file name", () => pl.Sort(SortKey.FileName)),
            Item("Sort list by path and file name", () => pl.Sort(SortKey.Path)),
            new Separator(),
            Item("Reverse list", pl.Reverse),
            Item("Randomize list", pl.Randomize));
    }

    public static void ShowPlaylistOptionsMenu(SkinnedWindow w)
    {
        var c = Shell.C;
        Open(w,
            Item("Generate HTML playlist...", () => Shell.ExportHtmlPlaylist()),
            Item("Refresh titles from tags", () => c.RefreshTitles()),
            new Separator(),
            Item("Playlist title format...", Shell.ShowPreferences));
    }

    public static void ShowPlaylistItemMenu(SkinnedWindow w)
    {
        var c = Shell.C;
        var pl = c.Playlist;
        var first = pl.SelectedItems.FirstOrDefault();
        Open(w,
            Item("Play item", () =>
            {
                if (first is not null) _ = c.PlayIndexAsync(pl.Items.ToList().IndexOf(first));
            }, "Enter", enabled: first is not null),
            Item("File info...", () => Shell.ShowFileInfo(first), "Alt+3", enabled: first is not null),
            new Separator(),
            Item("Remove selected", pl.RemoveSelected, "Del"),
            Item("Crop selected", pl.Crop),
            new Separator(),
            Item("Select all", () => pl.SelectAll(), "Ctrl+A"),
            Item("Select none", () => pl.SelectAll(false)),
            Item("Invert selection", pl.InvertSelection),
            new Separator(),
            Item("Add file(s)...", () => _ = Shell.OpenFilesDialog(false), "L"),
            Item("Add folder...", () => _ = Shell.OpenFolderDialog(false), "Shift+L"),
            Item("Add URL...", () => _ = Shell.OpenUrlDialog(false), "Ctrl+L"));
    }
}

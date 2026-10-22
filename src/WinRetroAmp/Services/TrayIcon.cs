using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using WinRetroAmp.Controller;
using WinRetroAmp.Ui;

namespace WinRetroAmp.Services;

/// <summary>Notification-area icon with transport controls; left click restores the player.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayIcon(PlayerController c, AppShell shell)
    {
        var menu = new ContextMenu();
        void Add(string text, Action a)
        {
            var mi = new MenuItem { Header = text };
            mi.Click += (_, _) => a();
            menu.Items.Add(mi);
        }
        Add("Restore WinRetro Amp", shell.Restore);
        menu.Items.Add(new Separator());
        Add("Previous", () => _ = c.PreviousAsync());
        Add("Play", () => _ = c.PlayAsync());
        Add("Pause", c.PauseToggle);
        Add("Stop", c.Stop);
        Add("Next", () => _ = c.NextAsync());
        menu.Items.Add(new Separator());
        Add("Exit", shell.Exit);

        _icon = new TaskbarIcon
        {
            Icon = AppIcon.CreateTrayIcon(),
            ToolTipText = "WinRetro Amp",
            ContextMenu = menu,
            NoLeftClickDelay = true,
            LeftClickCommand = new RelayCommand(shell.Restore),
        };
        _icon.ForceCreate(enablesEfficiencyMode: false);

        c.Changed += (_, _) =>
        {
            string tip = c.Playlist.Current is { } cur ? $"{c.Playlist.CurrentIndex + 1}. {cur.DisplayTitle} - WinRetro Amp" : "WinRetro Amp";
            // Notification-area tooltips are limited to 127 characters.
            _icon.ToolTipText = tip.Length > 127 ? tip[..124] + "..." : tip;
        };
    }

    public void Dispose() => _icon.Dispose();
}

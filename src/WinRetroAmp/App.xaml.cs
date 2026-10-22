using System.Windows;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Services;
using WinRetroAmp.Ui;

namespace WinRetroAmp;

public partial class App : Application
{
    private SingleInstance? _instance;
    private PlayerController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            LogError(ex.Exception);
            MessageBox.Show(ex.Exception.Message, "WinRetro Amp", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        _instance = SingleInstance.TryStart(e.Args);
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        _controller = new PlayerController();
        var shell = new AppShell(_controller);
        // Save settings and the playlist when Windows logs off or shuts down.
        SessionEnding += (_, _) => shell.Exit();
        shell.Main.Icon = AppIcon.Create(32);
        _controller.RestoreLastSession();
        shell.Show();

        _instance.Listen(args => Dispatcher.BeginInvoke(async () =>
        {
            shell.Restore();
            await HandleArgs(_controller, args);
        }));

        if (e.Args.Length > 0)
        {
            await HandleArgs(_controller, e.Args);
        }
        else if (_controller.Settings.ResumeOnStart && _controller.Playlist.Current is { } cur)
        {
            await _controller.PlayIndexAsync(_controller.Playlist.CurrentIndex);
            if (!PlaybackEngine.IsUrl(cur.Path)) _controller.Seek(TimeSpan.FromSeconds(_controller.Settings.LastPositionSeconds));
        }
    }

    /// <summary>"/ADD file..." enqueues; plain paths replace the playlist and start playing.</summary>
    private static async Task HandleArgs(PlayerController c, string[] args)
    {
        bool add = args.Any(a => a.Equals("/ADD", StringComparison.OrdinalIgnoreCase));
        var paths = args.Where(a => !a.StartsWith('/') || File.Exists(a) || Directory.Exists(a)).ToList();
        if (paths.Count > 0) await c.OpenAsync(paths, replace: !add);
    }

    private static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Core.Settings.SettingsStore.DataFolder);
            string entry = $"[{DateTime.Now:u}] {ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(Core.Settings.SettingsStore.DataFolder, "error.log"), entry);
        }
        catch (IOException)
        {
            // Nowhere to log.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}

using Microsoft.Win32;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Playlist;

namespace WinRetroAmp.Services;

/// <summary>
/// Registers the player for audio files and playlists for the current user only (HKCU), so it
/// shows in "Open with" and can be chosen as default app. No administrator rights needed.
/// </summary>
public static class FileAssociations
{
    private const string ProgId = "WinRetroAmp.AudioFile";
    private const string AppKey = @"Software\Classes\Applications\WinRetroAmp.exe";

    private static string Exe => Environment.ProcessPath ?? "WinRetroAmp.exe";

    public static void Register()
    {
        string command = $"\"{Exe}\" \"%1\"";
        using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            prog.SetValue("", "WinRetro Amp media file");
            using (var icon = prog.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{Exe}\",0");
            using (var open = prog.CreateSubKey(@"shell\open\command")) open.SetValue("", command);
            using (var enqueue = prog.CreateSubKey(@"shell\Enqueue")) enqueue.SetValue("", "Add to WinRetro Amp playlist");
            using (var enqueueCmd = prog.CreateSubKey(@"shell\Enqueue\command")) enqueueCmd.SetValue("", $"\"{Exe}\" /ADD \"%1\"");
        }
        using (var app = Registry.CurrentUser.CreateSubKey(AppKey))
        {
            app.SetValue("FriendlyAppName", "WinRetro Amp");
            using (var open = app.CreateSubKey(@"shell\open\command")) open.SetValue("", command);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (string ext in AllExtensions) types.SetValue(ext, "");
        }
        foreach (string ext in AllExtensions)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids");
            key.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }
        NotifyShell();
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(AppKey, throwOnMissingSubKey: false);
        foreach (string ext in AllExtensions)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}\OpenWithProgids", writable: true);
            key?.DeleteValue(ProgId, throwOnMissingValue: false);
        }
        NotifyShell();
    }

    private static IEnumerable<string> AllExtensions => PlaybackEngine.SupportedExtensions.Concat(PlaylistFormats.Extensions);

    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

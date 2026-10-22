using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinRetroAmp.Core.Settings;

public enum TimeMode { Elapsed, Remaining }
public enum VisMode { Spectrum, Oscilloscope, Off }

public sealed class WindowState
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public bool Visible { get; set; } = true;
    public bool Shaded { get; set; }
}

/// <summary>Everything the player remembers between sessions.</summary>
public sealed class AppSettings
{
    public WindowState Main { get; set; } = new();
    public WindowState Equalizer { get; set; } = new();
    public WindowState Playlist { get; set; } = new();
    public int PlaylistWidth { get; set; } = 275;
    public int PlaylistHeight { get; set; } = 232;

    public bool DoubleSize { get; set; }
    public bool AlwaysOnTop { get; set; }
    public int Volume { get; set; } = 75;
    public int Balance { get; set; }
    public bool Shuffle { get; set; }
    public bool Repeat { get; set; }
    public TimeMode TimeMode { get; set; }
    public VisMode VisMode { get; set; }
    public bool ThinBars { get; set; }
    public int VisFalloff { get; set; } = 2;
    public bool VisPeaks { get; set; } = true;

    public bool EqEnabled { get; set; } = true;
    public bool EqAuto { get; set; }
    public double EqPreamp { get; set; }
    public double[] EqBands { get; set; } = new double[10];
    /// <summary>Auto-load presets: file path → bands (preamp is element 10).</summary>
    public Dictionary<string, double[]> AutoPresets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? SkinPath { get; set; }
    public string TitleFormat { get; set; } = "%1 - %2";
    public bool ScrollTitle { get; set; } = true;
    public int ScrollSpeed { get; set; } = 2;
    public int DeviceNumber { get; set; } = -1;
    public bool MinimizeToTray { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public bool ResumeOnStart { get; set; }
    public int LastIndex { get; set; } = -1;
    public double LastPositionSeconds { get; set; }
    public string? LastFolder { get; set; }
    public List<string> RecentUrls { get; set; } = [];
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>%APPDATA%\WinRetroAmp, or the folder in WINRETROAMP_DATA (handy for portable use and testing).</summary>
    public static string DataFolder { get; set; } =
        Environment.GetEnvironmentVariable("WINRETROAMP_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinRetroAmp");

    public static string SettingsPath => Path.Combine(DataFolder, "settings.json");
    public static string LastPlaylistPath => Path.Combine(DataFolder, "last.m3u8");
    public static string PresetsPath => Path.Combine(DataFolder, "presets.json");
    public static string SkinsFolder => Path.Combine(DataFolder, "Skins");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Options) is { } s)
            {
                if (s.EqBands is not { Length: 10 }) s.EqBands = new double[10];
                s.AutoPresets = new Dictionary<string, double[]>(s.AutoPresets ?? [], StringComparer.OrdinalIgnoreCase);
                return s;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A broken settings file must never stop the player from starting.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataFolder);
        string tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, SettingsPath, overwrite: true);
    }
}

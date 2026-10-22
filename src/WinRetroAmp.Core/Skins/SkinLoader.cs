using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using WinRetroAmp.Core.Imaging;

namespace WinRetroAmp.Core.Skins;

/// <summary>
/// Loads classic 2.x skins (.wsz / .zip archives or extracted folders). File names are matched
/// case-insensitively and may sit in a sub-folder; anything missing falls back to the default skin.
/// </summary>
public static partial class SkinLoader
{
    private static Skin? _default;

    public static Skin Default => _default ??= DefaultSkinPainter.Create();

    public static Skin Load(string path)
    {
        if (path.StartsWith("builtin:", StringComparison.Ordinal)) return BuiltInSkins.Create(path);
        var files = Directory.Exists(path) ? ReadFolder(path) : ReadArchive(path);
        return FromFiles(Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/')), files);
    }

    /// <summary>Builds a skin from a file-name → bytes map (names are compared without folders or case).</summary>
    public static Skin FromFiles(string name, IReadOnlyDictionary<string, byte[]> files)
    {
        var fallback = Default;
        var byName = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, data) in files)
            byName.TryAdd(Path.GetFileName(key.Replace('\\', '/')), data);

        var sheets = new Dictionary<string, PixelImage>();
        bool userNumsEx = false, userNumbers = false;
        foreach (string sheet in SpriteMap.Sheets.All)
        {
            PixelImage? img = null;
            if (byName.TryGetValue(sheet + ".bmp", out var bytes) || byName.TryGetValue(sheet + ".png", out bytes))
                img = ImageCodec.TryDecode(bytes);
            if (img is null)
            {
                sheets[sheet] = fallback[sheet];
                continue;
            }
            if (sheet == SpriteMap.Sheets.NumsEx) userNumsEx = true;
            if (sheet == SpriteMap.Sheets.Numbers) userNumbers = true;
            sheets[sheet] = PadToMinimum(img, fallback[sheet], SpriteMap.Sheets.Sizes[sheet]);
        }

        // Prefer the skin's own nums_ex; if it only ships numbers.bmp, use those digits.
        bool hasNumsEx = userNumsEx || !userNumbers;

        uint[] vis = byName.TryGetValue("viscolor.txt", out var visBytes) ? ParseVisColors(Decode(visBytes), fallback.VisColors) : fallback.VisColors;
        var pl = byName.TryGetValue("pledit.txt", out var plBytes) ? ParsePlEdit(Decode(plBytes), fallback.Playlist) : fallback.Playlist;
        return new Skin(name, sheets, vis, pl, hasNumsEx);
    }

    private static PixelImage PadToMinimum(PixelImage img, PixelImage fallback, (int W, int H) min)
    {
        if (img.Width >= min.W && img.Height >= min.H) return img;
        var padded = fallback.Width >= min.W && fallback.Height >= min.H ? fallback.Clone() : new PixelImage(Math.Max(min.W, img.Width), Math.Max(min.H, img.Height));
        if (padded.Width < img.Width || padded.Height < img.Height)
        {
            var bigger = new PixelImage(Math.Max(padded.Width, img.Width), Math.Max(padded.Height, img.Height));
            bigger.Blit(padded, 0, 0, padded.Width, padded.Height, 0, 0);
            padded = bigger;
        }
        padded.Blit(img, 0, 0, img.Width, img.Height, 0, 0);
        return padded;
    }

    private static Dictionary<string, byte[]> ReadArchive(string path)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries)
        {
            if (entry.Length == 0 || entry.Length > 8 * 1024 * 1024) continue;
            string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (ext is not (".bmp" or ".png" or ".txt")) continue;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            files.TryAdd(entry.FullName, ms.ToArray());
        }
        return files;
    }

    private static Dictionary<string, byte[]> ReadFolder(string path)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext is ".bmp" or ".png" or ".txt") files.TryAdd(Path.GetRelativePath(path, f), File.ReadAllBytes(f));
        }
        return files;
    }

    private static string Decode(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>Parses viscolor.txt: up to 24 "r,g,b" lines, anything after the numbers is a comment.</summary>
    public static uint[] ParseVisColors(string text, uint[] fallback)
    {
        var colors = (uint[])fallback.Clone();
        int i = 0;
        foreach (string raw in text.Split('\n'))
        {
            if (i >= 24) break;
            var m = VisLine().Match(raw);
            if (!m.Success) continue;
            colors[i++] = Imaging.PixelImage.Rgb(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }
        return colors;
    }

    /// <summary>Parses pledit.txt ([Text] section with Normal, Current, NormalBG, SelectedBG and Font).</summary>
    public static PlaylistStyle ParsePlEdit(string text, PlaylistStyle fallback)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        uint Color(string key, uint def)
        {
            if (!values.TryGetValue(key, out var v)) return def;
            v = v.TrimStart('#');
            if (v.Length >= 6 && uint.TryParse(v[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return 0xFF000000 | rgb;
            return def;
        }
        string font = values.TryGetValue("Font", out var f) && !string.IsNullOrWhiteSpace(f) ? f : fallback.Font;
        return new PlaylistStyle(Color("Normal", fallback.Normal), Color("Current", fallback.Current), Color("NormalBG", fallback.NormalBg), Color("SelectedBG", fallback.SelectedBg), font);
    }

    /// <summary>Writes the default skin as a .wsz so people can use it as a template for their own.</summary>
    public static void ExportDefault(string path)
        => Export(Default, path);

    public static void Export(Skin skin, string path)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (string sheet in SpriteMap.Sheets.All)
        {
            var entry = zip.CreateEntry(sheet.ToUpperInvariant() + ".BMP");
            using var s = entry.Open();
            s.Write(ImageCodec.EncodeBmp(skin[sheet]));
        }
        var vis = new StringBuilder();
        foreach (uint c in skin.VisColors) vis.Append(CultureInfo.InvariantCulture, $"{(c >> 16) & 0xFF},{(c >> 8) & 0xFF},{c & 0xFF}\r\n");
        WriteText(zip, "VISCOLOR.TXT", vis.ToString());
        var p = skin.Playlist;
        WriteText(zip, "PLEDIT.TXT", $"[Text]\r\nNormal=#{p.Normal & 0xFFFFFF:X6}\r\nCurrent=#{p.Current & 0xFFFFFF:X6}\r\nNormalBG=#{p.NormalBg & 0xFFFFFF:X6}\r\nSelectedBG=#{p.SelectedBg & 0xFFFFFF:X6}\r\nFont={p.Font}\r\n");
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var s = zip.CreateEntry(name).Open();
        s.Write(Encoding.Latin1.GetBytes(text));
    }

    [GeneratedRegex(@"^\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})")]
    private static partial Regex VisLine();
}

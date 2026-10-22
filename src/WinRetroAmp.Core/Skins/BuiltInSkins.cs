using WinRetroAmp.Core.Imaging;

namespace WinRetroAmp.Core.Skins;

/// <summary>Original colour editions, including every sprite and playlist/visualizer colours.</summary>
public static class BuiltInSkins
{
    public sealed record Edition(string Name, uint Metal, uint Accent, uint Display)
    {
        public string Path => "builtin:" + Name;
    }

    public static IReadOnlyList<Edition> All { get; } = Array.AsReadOnly(new[]
    {
        new Edition("Amber", 0xFF968064, 0xFFFFBA50, 0xFFFFCC62),
        new Edition("Arctic", 0xFFC0D8E8, 0xFF64DFFF, 0xFFA9EEFF),
        new Edition("Rose", 0xFFAB798C, 0xFFFF8DAF, 0xFFFFBCD2),
        new Edition("Copper", 0xFFC18B64, 0xFFFFAD70, 0xFFFFDE9E),
        new Edition("Emerald", 0xFF648D7C, 0xFF66E6B2, 0xFF9CFFC0),
        new Edition("Violet", 0xFF8977AA, 0xFFC49AFF, 0xFFE3C6FF),
    });

    public static Skin Create(string path)
    {
        var edition = All.FirstOrDefault(e => e.Path == path)
            ?? throw new ArgumentException("Unknown built-in skin.", nameof(path));
        uint Tint(uint color)
        {
            double r = (color >> 16) & 255, g = (color >> 8) & 255, b = color & 255;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            // Keep red/yellow transport and level warnings recognizable.
            if (r > g * 1.1 && r > b * 1.4 || r > 180 && g > 150 && b < 100) return color;
            bool chromatic = max - min > max * 0.3;
            uint target = chromatic ? (g > b * 1.2 ? edition.Display : edition.Accent) : edition.Metal;
            double level = chromatic ? max / 240.0 : (r * .2126 + g * .7152 + b * .0722) / 145.0;
            // Pale highlights retain their brightness and dark wells retain their contrast.
            if (!chromatic && min > 180) return color;
            return PixelImage.Rgb((int)(((target >> 16) & 255) * level),
                (int)(((target >> 8) & 255) * level), (int)((target & 255) * level));
        }

        var source = SkinLoader.Default;
        var sheets = source.Sheets.ToDictionary(pair => pair.Key,
            pair => new PixelImage(pair.Value.Width, pair.Value.Height, pair.Value.Pixels.Select(Tint).ToArray()));
        var p = source.Playlist;
        return new Skin(edition.Name, sheets, source.VisColors.Select(Tint).ToArray(),
            new PlaylistStyle(Tint(p.Normal), Tint(p.Current), Tint(p.NormalBg), Tint(p.SelectedBg), p.Font), true);
    }
}

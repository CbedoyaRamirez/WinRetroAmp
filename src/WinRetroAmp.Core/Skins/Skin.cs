using WinRetroAmp.Core.Imaging;

namespace WinRetroAmp.Core.Skins;

/// <summary>Colours and font of the playlist editor (pledit.txt).</summary>
public sealed record PlaylistStyle(uint Normal, uint Current, uint NormalBg, uint SelectedBg, string Font);

/// <summary>A loaded skin: every classic sheet plus visualizer and playlist colours.</summary>
public sealed class Skin
{
    public Skin(string name, IReadOnlyDictionary<string, PixelImage> sheets, uint[] visColors, PlaylistStyle playlist, bool hasNumsEx)
    {
        if (visColors.Length != 24) throw new ArgumentException("A skin needs 24 visualizer colours.", nameof(visColors));
        Name = name;
        Sheets = sheets;
        VisColors = visColors;
        Playlist = playlist;
        HasNumsEx = hasNumsEx;
    }

    public string Name { get; }
    public IReadOnlyDictionary<string, PixelImage> Sheets { get; }

    /// <summary>0 = background, 1 = dots, 2..17 = analyzer (top to bottom), 18..22 = oscilloscope, 23 = peaks.</summary>
    public uint[] VisColors { get; }

    public PlaylistStyle Playlist { get; }

    /// <summary>True when the time digits come from nums_ex.bmp (which has its own minus sign).</summary>
    public bool HasNumsEx { get; }

    public PixelImage this[string sheet] => Sheets[sheet];

    public PixelImage Digits => HasNumsEx ? Sheets[SpriteMap.Sheets.NumsEx] : Sheets[SpriteMap.Sheets.Numbers];
}

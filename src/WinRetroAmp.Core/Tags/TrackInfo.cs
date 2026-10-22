using WinRetroAmp.Core.Audio;

namespace WinRetroAmp.Core.Tags;

/// <summary>Editable tag fields plus read-only technical details of a local file.</summary>
public sealed class TrackInfo
{
    public string Path { get; init; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string Year { get; set; } = "";
    public string Track { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Comment { get; set; } = "";
    public TimeSpan Duration { get; init; }
    public int Bitrate { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public string Codec { get; init; } = "";
    public long FileSize { get; init; }

    /// <summary>Reads tags and audio properties; returns null for URLs or unreadable files.</summary>
    public static TrackInfo? TryRead(string path)
    {
        if (PlaybackEngine.IsUrl(path) || !File.Exists(path)) return null;
        try
        {
            using var file = TagLib.File.Create(path);
            var t = file.Tag;
            var p = file.Properties;
            return new TrackInfo
            {
                Path = path,
                Title = t.Title ?? "",
                Artist = t.JoinedPerformers ?? "",
                Album = t.Album ?? "",
                Year = t.Year > 0 ? t.Year.ToString() : "",
                Track = t.Track > 0 ? t.Track.ToString() : "",
                Genre = t.JoinedGenres ?? "",
                Comment = t.Comment ?? "",
                Duration = p?.Duration ?? TimeSpan.Zero,
                Bitrate = p?.AudioBitrate ?? 0,
                SampleRate = p?.AudioSampleRate ?? 0,
                Channels = p?.AudioChannels ?? 0,
                Codec = p?.Description ?? "",
                FileSize = new FileInfo(path).Length,
            };
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException or TagLib.UnsupportedFormatException or IOException or UnauthorizedAccessException or NotImplementedException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Writes the editable fields back to the file.</summary>
    public void Save()
    {
        using var file = TagLib.File.Create(Path);
        var t = file.Tag;
        t.Title = NullIfEmpty(Title);
        t.Performers = string.IsNullOrWhiteSpace(Artist) ? [] : [Artist.Trim()];
        t.Album = NullIfEmpty(Album);
        t.Year = uint.TryParse(Year, out uint y) ? y : 0;
        t.Track = uint.TryParse(Track, out uint tr) ? tr : 0;
        t.Genres = string.IsNullOrWhiteSpace(Genre) ? [] : [Genre.Trim()];
        t.Comment = NullIfEmpty(Comment);
        file.Save();
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Formats the playlist title. Tokens: %1 artist, %2 title, %3 album, %4 year, %5 track,
    /// %6 genre, %7 file name. With no title tag, the file name is used.
    /// </summary>
    public string Format(string pattern)
    {
        if (string.IsNullOrWhiteSpace(Title)) return System.IO.Path.GetFileNameWithoutExtension(Path);
        string s = pattern
            .Replace("%1", Artist).Replace("%2", Title).Replace("%3", Album).Replace("%4", Year)
            .Replace("%5", Track).Replace("%6", Genre).Replace("%7", System.IO.Path.GetFileNameWithoutExtension(Path));
        // Collapse separators left dangling by empty fields ("  - Title" → "Title").
        s = s.Trim();
        foreach (string sep in new[] { "-", "/", "|", ":" })
        {
            if (s.StartsWith(sep, StringComparison.Ordinal)) s = s[1..].Trim();
            if (s.EndsWith(sep, StringComparison.Ordinal)) s = s[..^1].Trim();
        }
        return s.Length > 0 ? s : Title;
    }
}

using System.Globalization;
using System.Text;
using WinRetroAmp.Core.Audio;

namespace WinRetroAmp.Core.Playlist;

/// <summary>Reads and writes M3U, M3U8 (extended) and PLS playlists.</summary>
public static class PlaylistFormats
{
    public static readonly string[] Extensions = [".m3u", ".m3u8", ".pls"];

    public static bool IsPlaylist(string path) => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static List<PlaylistItem> Load(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        // .m3u is traditionally ANSI; .m3u8 and files with a BOM are UTF-8.
        var encoding = ext == ".m3u" ? DetectM3uEncoding(path) : Encoding.UTF8;
        string text = File.ReadAllText(path, encoding);
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
        return ext == ".pls" ? ParsePls(text, baseDir) : ParseM3u(text, baseDir);
    }

    private static Encoding DetectM3uEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8;
        try
        {
            new UTF8Encoding(false, true).GetString(bytes);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1;
        }
    }

    public static List<PlaylistItem> ParseM3u(string text, string baseDir)
    {
        var result = new List<PlaylistItem>();
        string? title = null;
        TimeSpan? duration = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                string info = line[8..];
                int comma = info.IndexOf(',');
                string secs = comma >= 0 ? info[..comma] : info;
                // Ignore attributes such as tvg-id="..." that some generators add.
                int space = secs.IndexOf(' ');
                if (space >= 0) secs = secs[..space];
                duration = int.TryParse(secs, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && s >= 0 ? TimeSpan.FromSeconds(s) : null;
                title = comma >= 0 ? info[(comma + 1)..].Trim() : null;
                continue;
            }
            if (line.StartsWith('#')) continue;
            result.Add(new PlaylistItem(Resolve(line, baseDir), string.IsNullOrEmpty(title) ? null : title, duration));
            title = null;
            duration = null;
        }
        return result;
    }

    public static List<PlaylistItem> ParsePls(string text, string baseDir)
    {
        var files = new SortedDictionary<int, string>();
        var titles = new Dictionary<int, string>();
        var lengths = new Dictionary<int, int>();
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
            if (TryIndex(key, "File", out int n)) files[n] = value;
            else if (TryIndex(key, "Title", out n)) titles[n] = value;
            else if (TryIndex(key, "Length", out n) && int.TryParse(value, out int len)) lengths[n] = len;
        }
        return files.Select(f => new PlaylistItem(
            Resolve(f.Value, baseDir),
            titles.GetValueOrDefault(f.Key),
            lengths.TryGetValue(f.Key, out int l) && l >= 0 ? TimeSpan.FromSeconds(l) : null)).ToList();
    }

    private static bool TryIndex(string key, string prefix, out int index)
    {
        index = 0;
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && int.TryParse(key.AsSpan(prefix.Length), out index);
    }

    private static string Resolve(string entry, string baseDir)
    {
        if (PlaybackEngine.IsUrl(entry)) return entry;
        if (entry.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(entry, UriKind.Absolute, out var uri))
            return uri.LocalPath;
        return Path.IsPathRooted(entry) ? Path.GetFullPath(entry) : Path.GetFullPath(Path.Combine(baseDir, entry));
    }

    public static void Save(string path, IEnumerable<PlaylistItem> items)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string text = ext == ".pls" ? ToPls(items) : ToM3u(items);
        File.WriteAllText(path, text, ext == ".m3u" ? Encoding.Latin1 : new UTF8Encoding(false));
    }

    public static string ToM3u(IEnumerable<PlaylistItem> items)
    {
        var sb = new StringBuilder("#EXTM3U\r\n");
        foreach (var i in items)
        {
            int secs = i.Duration is { } d ? (int)d.TotalSeconds : -1;
            sb.Append(CultureInfo.InvariantCulture, $"#EXTINF:{secs},{i.DisplayTitle}\r\n{i.Path}\r\n");
        }
        return sb.ToString();
    }

    public static string ToPls(IEnumerable<PlaylistItem> items)
    {
        var list = items.ToList();
        var sb = new StringBuilder("[playlist]\r\n");
        for (int n = 0; n < list.Count; n++)
        {
            var i = list[n];
            sb.Append(CultureInfo.InvariantCulture, $"File{n + 1}={i.Path}\r\nTitle{n + 1}={i.DisplayTitle}\r\nLength{n + 1}={(i.Duration is { } d ? (int)d.TotalSeconds : -1)}\r\n");
        }
        sb.Append(CultureInfo.InvariantCulture, $"NumberOfEntries={list.Count}\r\nVersion=2\r\n");
        return sb.ToString();
    }

    /// <summary>Expands files, folders (recursively) and playlists into playable entries.</summary>
    public static List<PlaylistItem> Expand(IEnumerable<string> paths)
    {
        var result = new List<PlaylistItem>();
        foreach (string p in paths)
        {
            if (PlaybackEngine.IsUrl(p))
            {
                result.Add(new PlaylistItem(p));
            }
            else if (Directory.Exists(p))
            {
                var files = Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                    .Where(PlaybackEngine.IsSupportedFile)
                    .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase);
                result.AddRange(files.Select(f => new PlaylistItem(f)));
            }
            else if (IsPlaylist(p) && File.Exists(p))
            {
                result.AddRange(Load(p));
            }
            else if (PlaybackEngine.IsSupportedFile(p))
            {
                result.Add(new PlaylistItem(Path.GetFullPath(p)));
            }
        }
        return result;
    }
}

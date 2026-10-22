using System.Text;
using System.Text.Json;

namespace WinRetroAmp.Core.Eq;

/// <summary>A named equalizer setting: preamp and 10 bands, all in dB (−12…+12).</summary>
public sealed record EqPreset(string Name, double Preamp, double[] Bands);

/// <summary>
/// Reads and writes .eqf preset files (the classic binary format: a text header followed by
/// 257-byte names and 11 values from 0 = +12 dB to 63 = −12 dB, bands first then preamp).
/// </summary>
public static class EqfFormat
{
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("Winamp EQ library file v1.1\x1A!--");
    private const int NameLength = 257;

    public static byte ToByte(double db) => (byte)Math.Clamp((int)Math.Round(31.5 - db / 12 * 31.5), 0, 63);
    public static double ToDb(byte v) => Math.Round((31.5 - Math.Min(v, (byte)63)) / 31.5 * 12, 1);

    public static List<EqPreset> Read(byte[] data)
    {
        if (data.Length < Header.Length || !data.AsSpan(0, Header.Length - 3).SequenceEqual(Header.AsSpan(0, Header.Length - 3)))
            throw new InvalidDataException("Not an EQF preset file.");
        var result = new List<EqPreset>();
        int pos = Header.Length;
        while (pos + NameLength + 11 <= data.Length)
        {
            var nameBytes = data.AsSpan(pos, NameLength);
            int end = nameBytes.IndexOf((byte)0);
            string name = Encoding.Latin1.GetString(end >= 0 ? nameBytes[..end] : nameBytes);
            pos += NameLength;
            var bands = new double[10];
            for (int i = 0; i < 10; i++) bands[i] = ToDb(data[pos + i]);
            double preamp = ToDb(data[pos + 10]);
            pos += 11;
            result.Add(new EqPreset(name, preamp, bands));
        }
        return result;
    }

    public static byte[] Write(IEnumerable<EqPreset> presets)
    {
        using var ms = new MemoryStream();
        ms.Write(Header);
        foreach (var p in presets)
        {
            var name = new byte[NameLength];
            Encoding.Latin1.GetBytes(p.Name.Length > NameLength - 1 ? p.Name[..(NameLength - 1)] : p.Name).CopyTo(name, 0);
            ms.Write(name);
            foreach (double b in p.Bands) ms.WriteByte(ToByte(b));
            ms.WriteByte(ToByte(p.Preamp));
        }
        return ms.ToArray();
    }
}

/// <summary>The user's preset library (JSON in the app data folder) seeded with built-in presets.</summary>
public sealed class PresetLibrary
{
    private readonly string _path;

    public PresetLibrary(string path)
    {
        _path = path;
        Presets = Load();
    }

    public List<EqPreset> Presets { get; }

    /// <summary>Our own starting presets (values tuned for this player).</summary>
    public static IReadOnlyList<EqPreset> BuiltIn { get; } =
    [
        new("Flat", 0, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass Boost", -2, [8, 6.5, 4, 1.5, 0, 0, 0, 0, 0, 0]),
        new("Treble Boost", -2, [0, 0, 0, 0, 0, 1.5, 4, 6.5, 7.5, 8]),
        new("Rock", -1, [5, 3.5, -1.5, -3, -1, 2, 4.5, 6, 6, 6]),
        new("Pop", -1, [-1, 2, 4, 5, 3.5, 0, -1, -1, -1, -1]),
        new("Dance", -1.5, [7, 5, 1.5, 0, 0, -3, -4, -4, 0, 0]),
        new("Classical", 0, [0, 0, 0, 0, 0, 0, -4, -4, -4, -6]),
        new("Jazz", 0, [3, 2, 0.5, 1.5, -1, -1, 0, 1.5, 2.5, 3]),
        new("Vocal", -1, [-3, -2, 0, 3, 5, 5, 3, 1, 0, -1]),
        new("Live", 0, [-3, 0, 2.5, 3, 3, 3, 2.5, 1.5, 1.5, 1.5]),
        new("Headphones", -1.5, [4, 7, 3, -2, -1.5, 1, 3.5, 6.5, 8, 8.5]),
        new("Small Speakers", -2, [7, 6, 4.5, 2, 0, -2, -3.5, -4, -4, -4]),
        new("Loudness", -2.5, [6, 4, 0, 0, -1.5, 0, -1, 2, 5, 6]),
        new("Night", 0, [-4, -3, -1, 0, 1, 1.5, 0, -2, -3, -4]),
    ];

    private List<EqPreset> Load()
    {
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<List<EqPreset>>(File.ReadAllText(_path)) is { Count: > 0 } list)
                return list.Where(p => p.Bands is { Length: 10 }).ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt or locked preset file: fall back to the built-in list.
        }
        return [.. BuiltIn];
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Presets, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void AddOrReplace(EqPreset preset)
    {
        int i = Presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) Presets[i] = preset; else Presets.Add(preset);
        Save();
    }

    public void Remove(string name)
    {
        Presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        Save();
    }
}

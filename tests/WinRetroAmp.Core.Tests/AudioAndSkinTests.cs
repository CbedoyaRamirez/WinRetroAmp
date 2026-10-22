using System.IO.Compression;
using System.Text;
using NAudio.Wave;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Eq;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Skins;
using WinRetroAmp.Core.Tags;

namespace WinRetroAmp.Core.Tests;

public class AudioTests
{
    private sealed class Sine(double freq, int rate = 44100, int channels = 2, double amp = 0.25) : ISampleProvider
    {
        private long _n;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);

        public int Read(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i += channels)
            {
                float v = (float)(amp * Math.Sin(2 * Math.PI * freq * _n++ / rate));
                for (int c = 0; c < channels && i + c < buffer.Length; c++) buffer[i + c] = v;
            }
            return buffer.Length;
        }
    }

    private static double RmsDb(ISampleProvider p)
    {
        var buf = new float[44100 * 2];
        p.Read(buf); // let the filters settle
        p.Read(buf);
        double sum = buf.Sum(s => (double)s * s);
        return 10 * Math.Log10(sum / buf.Length);
    }

    [Fact]
    public void Equalizer_boosts_the_matching_band_by_about_12_dB()
    {
        double flat = RmsDb(new EqualizerSampleProvider(new Sine(1000), new EqualizerSettings()));
        var eq = new EqualizerSettings();
        eq[4] = 12; // 1 kHz
        double boosted = RmsDb(new EqualizerSampleProvider(new Sine(1000), eq));
        Assert.InRange(boosted - flat, 11, 13);
    }

    [Fact]
    public void Equalizer_preamp_and_disable()
    {
        var eq = new EqualizerSettings { Preamp = -6 };
        double flat = RmsDb(new Sine(440));
        Assert.InRange(RmsDb(new EqualizerSampleProvider(new Sine(440), eq)) - flat, -6.5, -5.5);
        eq.Enabled = false;
        Assert.InRange(RmsDb(new EqualizerSampleProvider(new Sine(440), eq)) - flat, -0.1, 0.1);
    }

    [Fact]
    public void Spectrum_peaks_in_the_bar_that_contains_the_tone()
    {
        var samples = new float[Analyzer.FftSize];
        new Sine(1000, channels: 1, amp: 0.8).Read(samples);
        var bars = Analyzer.ComputeSpectrum(samples, 44100, 19);
        int loudest = Array.IndexOf(bars, bars.Max());
        double f0 = 40 * Math.Pow(16000 / 40.0, loudest / 19.0), f1 = 40 * Math.Pow(16000 / 40.0, (loudest + 1) / 19.0);
        Assert.InRange(1000, f0 - 100, f1 + 100);
        Assert.True(bars.Max() > 0.8);
    }

    [Fact]
    public void Volume_and_balance()
    {
        var vb = new VolumeBalanceSampleProvider(new Sine(440, amp: 1)) { Volume = 100, Balance = 100 };
        var buf = new float[2000];
        vb.Read(buf);
        Assert.All(Enumerable.Range(0, 1000).Select(i => buf[i * 2]), l => Assert.Equal(0, l, 5));
        Assert.Contains(Enumerable.Range(0, 1000).Select(i => buf[i * 2 + 1]), r => Math.Abs(r) > 0.5);
        Assert.Equal(0.25f, VolumeBalanceSampleProvider.Gain(50), 3);
    }

    [Fact]
    public void Eqf_round_trip()
    {
        var presets = new[] { new EqPreset("Mi preset", -3, [12, 6, 0, -6, -12, 3, 3, 3, 3, 3]) };
        var back = EqfFormat.Read(EqfFormat.Write(presets));
        Assert.Single(back);
        Assert.Equal("Mi preset", back[0].Name);
        Assert.Equal(-3, back[0].Preamp, 0);
        for (int i = 0; i < 10; i++) Assert.Equal(presets[0].Bands[i], back[0].Bands[i], 0);
        Assert.Equal(0, EqfFormat.ToByte(12));
        Assert.Equal(63, EqfFormat.ToByte(-12));
    }

    [Fact]
    public void Tags_round_trip_on_a_wav_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wra-{Guid.NewGuid():N}.wav");
        try
        {
            using (var w = new WaveFileWriter(path, new WaveFormat(44100, 16, 2)))
                w.Write(new byte[44100 * 4]);
            var info = TrackInfo.TryRead(path)!;
            Assert.Equal(1, info.Duration.TotalSeconds, 1);
            info.Title = "Canción";
            info.Artist = "Grupo";
            info.Save();
            var back = TrackInfo.TryRead(path)!;
            Assert.Equal("Canción", back.Title);
            Assert.Equal("Grupo - Canción", back.Format("%1 - %2"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Title_format_falls_back_to_file_name()
    {
        var info = new TrackInfo { Path = @"C:\m\track 01.mp3", Title = "" };
        Assert.Equal("track 01", info.Format("%1 - %2"));
        var noArtist = new TrackInfo { Path = @"C:\m\x.mp3", Title = "Solo" };
        Assert.Equal("Solo", noArtist.Format("%1 - %2"));
    }
}

public class SkinTests
{
    [Fact]
    public void Default_skin_has_every_sheet_at_least_its_classic_size()
    {
        var skin = SkinLoader.Default;
        foreach (var (name, (w, h)) in SpriteMap.Sheets.Sizes)
        {
            Assert.True(skin[name].Width >= w && skin[name].Height >= h, $"{name} is {skin[name].Width}x{skin[name].Height}");
        }
        Assert.Equal(24, skin.VisColors.Length);
    }

    [Fact]
    public void Loads_wsz_with_mixed_case_names_in_a_subfolder_and_falls_back_for_missing_sheets()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wra-{Guid.NewGuid():N}.wsz");
        try
        {
            var main = new PixelImage(275, 116, PixelImage.Rgb(200, 10, 10));
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var s = zip.CreateEntry("MySkin/Main.BMP").Open()) s.Write(ImageCodec.EncodeBmp(main));
                using (var s = zip.CreateEntry("MySkin/VISCOLOR.TXT").Open()) s.Write(Encoding.ASCII.GetBytes("1,2,3, // bg\r\n255,255,255\r\n"));
                using (var s = zip.CreateEntry("MySkin/pledit.txt").Open())
                    s.Write(Encoding.ASCII.GetBytes("[Text]\r\nNormal=#00FF00\r\nCurrent=#FFFFFF\r\nNormalBG=#000000\r\nSelectedBG=#0000C6\r\nFont=Tahoma\r\n"));
            }
            var skin = SkinLoader.Load(path);
            Assert.Equal(PixelImage.Rgb(200, 10, 10), skin[SpriteMap.Sheets.Main][5, 5]);
            Assert.Same(SkinLoader.Default[SpriteMap.Sheets.CButtons], skin[SpriteMap.Sheets.CButtons]);
            Assert.Equal(PixelImage.Rgb(1, 2, 3), skin.VisColors[0]);
            Assert.Equal(PixelImage.Rgb(255, 255, 255), skin.VisColors[1]);
            Assert.Equal(SkinLoader.Default.VisColors[2], skin.VisColors[2]);
            Assert.Equal("Tahoma", skin.Playlist.Font);
            Assert.Equal(0xFF0000C6u, skin.Playlist.SelectedBg);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Undersized_sheets_are_padded_from_the_default()
    {
        var tiny = new PixelImage(100, 10, PixelImage.Rgb(1, 1, 1));
        var skin = SkinLoader.FromFiles("t", new Dictionary<string, byte[]> { ["posbar.bmp"] = ImageCodec.EncodeBmp(tiny) });
        var pos = skin[SpriteMap.Sheets.PosBar];
        Assert.Equal(307, pos.Width);
        Assert.Equal(PixelImage.Rgb(1, 1, 1), pos[50, 5]);
        Assert.Equal(SkinLoader.Default[SpriteMap.Sheets.PosBar][260, 5], pos[260, 5]);
    }

    [Fact]
    public void Numbers_only_skin_uses_its_own_digits()
    {
        var digits = new PixelImage(99, 13, PixelImage.Rgb(9, 9, 9));
        var skin = SkinLoader.FromFiles("n", new Dictionary<string, byte[]> { ["NUMBERS.BMP"] = ImageCodec.EncodeBmp(digits) });
        Assert.False(skin.HasNumsEx);
        Assert.Equal(PixelImage.Rgb(9, 9, 9), skin.Digits[0, 0]);
        Assert.True(SkinLoader.FromFiles("e", new Dictionary<string, byte[]>()).HasNumsEx);
    }

    [Fact]
    public void Exported_default_skin_loads_back_identically()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wra-{Guid.NewGuid():N}.wsz");
        try
        {
            SkinLoader.ExportDefault(path);
            var skin = SkinLoader.Load(path);
            Assert.Equal(SkinLoader.Default[SpriteMap.Sheets.Main].Pixels, skin[SpriteMap.Sheets.Main].Pixels);
            Assert.Equal(SkinLoader.Default.VisColors, skin.VisColors);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Text_font_lookup()
    {
        Assert.Equal((0, 0), SpriteMap.CharCell('a'));
        Assert.Equal((25 * 5, 0), SpriteMap.CharCell('Z'));
        Assert.Equal((0, 6), SpriteMap.CharCell('0'));
        Assert.Equal((12 * 5, 6), SpriteMap.CharCell(':'));
        Assert.Equal(SpriteMap.CharCell('A'), SpriteMap.CharCell('á'));
        Assert.Equal((150, 0), SpriteMap.CharCell('\u4E00'));
    }
}

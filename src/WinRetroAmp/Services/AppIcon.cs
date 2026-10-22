using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinRetroAmp.Services;

/// <summary>The app's original icon: a cyan ring around a "W" on a graphite disc, drawn with WPF.</summary>
public static class AppIcon
{
    public static BitmapSource Create(int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            double s = size;
            var center = new Point(s / 2, s / 2);
            var disc = new RadialGradientBrush(Color.FromRgb(0x4C, 0x52, 0x5E), Color.FromRgb(0x14, 0x16, 0x1B)) { GradientOrigin = new Point(0.35, 0.3) };
            dc.DrawEllipse(disc, null, center, s * 0.47, s * 0.47);
            var ring = new Pen(new SolidColorBrush(Color.FromRgb(0x46, 0xD2, 0xE6)), Math.Max(1.5, s * 0.07));
            dc.DrawEllipse(null, ring, center, s * 0.40, s * 0.40);

            // A bold "W" stroke made of four segments.
            var w = new StreamGeometry();
            using (var ctx = w.Open())
            {
                ctx.BeginFigure(new Point(s * 0.24, s * 0.33), false, false);
                ctx.LineTo(new Point(s * 0.37, s * 0.69), true, true);
                ctx.LineTo(new Point(s * 0.50, s * 0.43), true, true);
                ctx.LineTo(new Point(s * 0.63, s * 0.69), true, true);
                ctx.LineTo(new Point(s * 0.76, s * 0.33), true, true);
            }
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF0)), Math.Max(1.5, s * 0.09)) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, pen, w);
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Writes a multi-size .ico (PNG-compressed entries).</summary>
    public static void WriteIco(string path, params int[] sizes) => File.WriteAllBytes(path, IcoBytes(sizes));

    /// <summary>A Win32 icon for the notification area.</summary>
    public static System.Drawing.Icon CreateTrayIcon() => new(new MemoryStream(IcoBytes(16, 20, 24, 32, 48)));

    public static byte[] IcoBytes(params int[] sizes)
    {
        var images = sizes.Select(sz =>
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(Create(sz)));
            using var ms = new MemoryStream();
            enc.Save(ms);
            return (sz, data: ms.ToArray());
        }).ToList();

        using var fs = new MemoryStream();
        using var bw = new BinaryWriter(fs);
        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)images.Count);
        int offset = 6 + 16 * images.Count;
        foreach (var (sz, data) in images)
        {
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(data.Length);
            bw.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in images) bw.Write(data);
        bw.Flush();
        return fs.ToArray();
    }
}

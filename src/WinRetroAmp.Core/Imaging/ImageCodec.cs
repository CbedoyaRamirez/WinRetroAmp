using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinRetroAmp.Core.Imaging;

public static class ImageCodec
{
    /// <summary>
    /// Decodes any image WPF understands (classic skins mix 4/8/24-bit and RLE BMPs, and some
    /// "BMP" files are really PNGs). Returns null when the data cannot be decoded.
    /// </summary>
    public static PixelImage? TryDecode(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data, writable: false);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];
            if (frame.Format != PixelFormats.Bgra32)
                frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int w = frame.PixelWidth, h = frame.PixelHeight;
            var pixels = new uint[w * h];
            frame.CopyPixels(pixels, w * 4, 0);
            // Skins are opaque; some 32-bit BMPs carry a zero alpha channel.
            for (int i = 0; i < pixels.Length; i++) pixels[i] |= 0xFF000000;
            return new PixelImage(w, h, pixels);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException or OverflowException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Writes an uncompressed 24-bit BMP (the format classic skins use).</summary>
    public static byte[] EncodeBmp(PixelImage img)
    {
        int stride = (img.Width * 3 + 3) & ~3;
        int dataSize = stride * img.Height;
        var bytes = new byte[54 + dataSize];
        void I32(int at, int v) => BitConverter.TryWriteBytes(bytes.AsSpan(at, 4), v);
        void I16(int at, short v) => BitConverter.TryWriteBytes(bytes.AsSpan(at, 2), v);
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        I32(2, bytes.Length);
        I32(10, 54);
        I32(14, 40);
        I32(18, img.Width);
        I32(22, img.Height);
        I16(26, 1);
        I16(28, 24);
        I32(34, dataSize);
        I32(38, 2835);
        I32(42, 2835);
        for (int y = 0; y < img.Height; y++)
        {
            int row = 54 + (img.Height - 1 - y) * stride;
            for (int x = 0; x < img.Width; x++)
            {
                uint p = img.Pixels[y * img.Width + x];
                bytes[row + x * 3] = (byte)p;
                bytes[row + x * 3 + 1] = (byte)(p >> 8);
                bytes[row + x * 3 + 2] = (byte)(p >> 16);
            }
        }
        return bytes;
    }
}

namespace WinRetroAmp.Core.Imaging;

/// <summary>
/// A simple 32-bit BGRA bitmap (one uint per pixel, 0xAARRGGBB). Every skinned window is
/// composed by blitting sprites into one of these and then copied to a WriteableBitmap.
/// </summary>
public sealed class PixelImage
{
    public PixelImage(int width, int height, uint fill = 0xFF000000)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
        if (fill != 0) Array.Fill(Pixels, fill);
    }

    public PixelImage(int width, int height, uint[] pixels)
    {
        if (pixels.Length != width * height) throw new ArgumentException("Pixel buffer size mismatch.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public uint this[int x, int y]
    {
        get => (uint)x < (uint)Width && (uint)y < (uint)Height ? Pixels[y * Width + x] : 0;
        set
        {
            if ((uint)x < (uint)Width && (uint)y < (uint)Height) Pixels[y * Width + x] = value;
        }
    }

    public static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);

    public static uint Lerp(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int L(int shift) => (int)Math.Round(((a >> shift) & 0xFF) + ((((b >> shift) & 0xFF) - (double)((a >> shift) & 0xFF)) * t));
        return Rgb(L(16), L(8), L(0));
    }

    public static uint Shade(uint c, double factor) =>
        Rgb((int)(((c >> 16) & 0xFF) * factor), (int)(((c >> 8) & 0xFF) * factor), (int)((c & 0xFF) * factor));

    public PixelImage Clone() => new(Width, Height, (uint[])Pixels.Clone());

    public void Clear(uint color) => Array.Fill(Pixels, color);

    public void Fill(int x, int y, int w, int h, uint color)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
        for (int yy = y0; yy < y1; yy++)
            Pixels.AsSpan(yy * Width + x0, Math.Max(0, x1 - x0)).Fill(color);
    }

    public void VerticalGradient(int x, int y, int w, int h, uint top, uint bottom)
    {
        for (int i = 0; i < h; i++)
            Fill(x, y + i, w, 1, Lerp(top, bottom, h == 1 ? 0 : i / (double)(h - 1)));
    }

    public void HorizontalGradient(int x, int y, int w, int h, uint left, uint right)
    {
        for (int i = 0; i < w; i++)
            Fill(x + i, y, 1, h, Lerp(left, right, w == 1 ? 0 : i / (double)(w - 1)));
    }

    /// <summary>Draws a 1px raised (or sunken) bevel frame.</summary>
    public void Bevel(int x, int y, int w, int h, uint light, uint dark)
    {
        Fill(x, y, w, 1, light);
        Fill(x, y, 1, h, light);
        Fill(x, y + h - 1, w, 1, dark);
        Fill(x + w - 1, y, 1, h, dark);
    }

    public void Rect(int x, int y, int w, int h, uint color) => Bevel(x, y, w, h, color, color);

    public void Line(int x0, int y0, int x1, int y1, uint color)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            this[x0, y0] = color;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>Draws a pattern where '#' is <paramref name="color"/> and any other char is skipped.</summary>
    public void Pattern(int x, int y, string[] rows, uint color)
    {
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == '#') this[x + c, y + r] = color;
    }

    /// <summary>Copies a rectangle from <paramref name="src"/>, clipping on both sides.</summary>
    public void Blit(PixelImage src, int sx, int sy, int w, int h, int dx, int dy)
    {
        if (sx < 0) { w += sx; dx -= sx; sx = 0; }
        if (sy < 0) { h += sy; dy -= sy; sy = 0; }
        if (dx < 0) { w += dx; sx -= dx; dx = 0; }
        if (dy < 0) { h += dy; sy -= dy; dy = 0; }
        w = Math.Min(w, Math.Min(src.Width - sx, Width - dx));
        h = Math.Min(h, Math.Min(src.Height - sy, Height - dy));
        if (w <= 0 || h <= 0) return;
        for (int row = 0; row < h; row++)
            src.Pixels.AsSpan((sy + row) * src.Width + sx, w).CopyTo(Pixels.AsSpan((dy + row) * Width + dx, w));
    }

    public void Blit(PixelImage src, Sprite s, int dx, int dy) => Blit(src, s.X, s.Y, s.W, s.H, dx, dy);

    /// <summary>Repeats a sprite to fill a region (used by the resizable playlist frame).</summary>
    public void Tile(PixelImage src, Sprite s, int dx, int dy, int w, int h)
    {
        for (int y = 0; y < h; y += s.H)
            for (int x = 0; x < w; x += s.W)
                Blit(src, s.X, s.Y, Math.Min(s.W, w - x), Math.Min(s.H, h - y), dx + x, dy + y);
    }

    public PixelImage Crop(int x, int y, int w, int h)
    {
        var img = new PixelImage(w, h, 0xFF000000);
        img.Blit(this, x, y, w, h, 0, 0);
        return img;
    }
}

/// <summary>A rectangle inside a skin sheet.</summary>
public readonly record struct Sprite(int X, int Y, int W, int H)
{
    public Sprite Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };
}

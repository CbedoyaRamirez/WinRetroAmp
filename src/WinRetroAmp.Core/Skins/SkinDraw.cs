using WinRetroAmp.Core.Imaging;

namespace WinRetroAmp.Core.Skins;

/// <summary>Small drawing helpers shared by the window renderers.</summary>
public static class SkinDraw
{
    /// <summary>Draws text with the skin's text.bmp font, clipped to <paramref name="maxWidth"/> pixels.</summary>
    public static void Text(PixelImage dst, Skin skin, string text, int x, int y, int maxWidth = int.MaxValue)
    {
        var font = skin[SpriteMap.Sheets.Text];
        int limit = maxWidth == int.MaxValue ? int.MaxValue : x + maxWidth;
        foreach (char c in text)
        {
            if (x >= limit) break;
            var (sx, sy) = SpriteMap.CharCell(c);
            dst.Blit(font, sx, sy, Math.Min(SpriteMap.CharW, limit - x), SpriteMap.CharH, x, y);
            x += SpriteMap.CharW;
        }
    }

    /// <summary>Fills an area with the text background colour (the blank cell of text.bmp).</summary>
    public static void TextBackground(PixelImage dst, Skin skin, int x, int y, int w, int h)
    {
        uint bg = skin[SpriteMap.Sheets.Text][30 * SpriteMap.CharW + 2, 2];
        dst.Fill(x, y, w, h, bg);
    }

    /// <summary>
    /// Draws a scrolling marquee: the text is repeated with a separator and shifted by
    /// <paramref name="offset"/> pixels, wrapping around.
    /// </summary>
    public static void Marquee(PixelImage dst, Skin skin, string text, int x, int y, int width, int offset)
    {
        TextBackground(dst, skin, x, y, width, SpriteMap.CharH);
        int textW = text.Length * SpriteMap.CharW;
        if (textW <= width)
        {
            Text(dst, skin, text, x, y, width);
            return;
        }
        string loop = text + "  ***  ";
        int loopW = loop.Length * SpriteMap.CharW;
        offset = ((offset % loopW) + loopW) % loopW;
        var strip = new PixelImage(loopW * 2, SpriteMap.CharH);
        Text(strip, skin, loop + loop, 0, 0);
        dst.Blit(strip, offset, 0, width, SpriteMap.CharH, x, y);
    }

    public static string FormatTime(TimeSpan t, bool hours = false)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        if (hours && t.TotalHours >= 1) return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}

using WinRetroAmp.Core.Imaging;
using static WinRetroAmp.Core.Imaging.PixelImage;

namespace WinRetroAmp.Core.Skins;

/// <summary>
/// Paints the original default skin ("Graphite") from code. The sheets follow the classic file
/// layout so the same renderer draws this skin and any .wsz the user loads, but every pixel of
/// art here is our own: graphite body, cyan accents, green LCD and the "WRA" emblem.
/// </summary>
public static class DefaultSkinPainter
{
    private static readonly uint BodyTop = Rgb(0x4C, 0x52, 0x5E), BodyBottom = Rgb(0x2B, 0x2F, 0x37);
    private static readonly uint Light = Rgb(0x8E, 0x97, 0xA8), Dark = Rgb(0x14, 0x16, 0x1B), Mid = Rgb(0x5E, 0x66, 0x74);
    private static readonly uint Accent = Rgb(0x46, 0xD2, 0xE6), AccentDim = Rgb(0x1F, 0x5E, 0x6A);
    private static readonly uint Lcd = Rgb(0x06, 0x0B, 0x09), LcdOn = Rgb(0x3B, 0xF0, 0x7A), LcdOff = Rgb(0x10, 0x2A, 0x1B), LcdMid = Rgb(0x1E, 0x8A, 0x48);
    private static readonly uint Label = Rgb(0xB8, 0xC0, 0xCC), Icon = Rgb(0x1A, 0x1D, 0x24), IconLight = Rgb(0xE6, 0xEA, 0xF0);
    private static readonly uint BtnTop = Rgb(0x9A, 0xA2, 0xB0), BtnBottom = Rgb(0x66, 0x6E, 0x7C);

    public static Skin Create()
    {
        var sheets = new Dictionary<string, PixelImage>
        {
            [SpriteMap.Sheets.Main] = PaintMain(),
            [SpriteMap.Sheets.CButtons] = PaintCButtons(),
            [SpriteMap.Sheets.TitleBar] = PaintTitleBar(),
            [SpriteMap.Sheets.ShufRep] = PaintShufRep(),
            [SpriteMap.Sheets.PosBar] = PaintPosBar(),
            [SpriteMap.Sheets.Volume] = PaintVolume(),
            [SpriteMap.Sheets.Balance] = PaintBalance(),
            [SpriteMap.Sheets.MonoSter] = PaintMonoSter(),
            [SpriteMap.Sheets.PlayPaus] = PaintPlayPaus(),
            [SpriteMap.Sheets.NumsEx] = PaintNumbers(withMinus: true),
            [SpriteMap.Sheets.Numbers] = PaintNumbers(withMinus: false),
            [SpriteMap.Sheets.Text] = PaintText(),
            [SpriteMap.Sheets.EqMain] = PaintEqMain(),
            [SpriteMap.Sheets.EqEx] = PaintEqEx(),
            [SpriteMap.Sheets.PlEdit] = PaintPlEdit(),
        };
        return new Skin("Graphite (default)", sheets, VisColors(), PlaylistColors, hasNumsEx: true);
    }

    public static readonly PlaylistStyle PlaylistColors = new(LcdOn, Rgb(0xFF, 0xFF, 0xFF), Lcd, Rgb(0x1B, 0x4A, 0x5A), "Arial");

    public static uint[] VisColors()
    {
        var c = new uint[24];
        c[0] = Lcd;
        c[1] = Rgb(0x1C, 0x2A, 0x26);
        for (int i = 0; i < 16; i++)
        {
            double t = i / 15.0; // 0 = top bar, 1 = bottom
            c[2 + i] = t < 0.5 ? Lerp(Rgb(0xF2, 0xFD, 0xFF), Accent, t * 2) : Lerp(Accent, Rgb(0x15, 0x6E, 0x8E), (t - 0.5) * 2);
        }
        for (int i = 0; i < 5; i++) c[18 + i] = Lerp(Rgb(0xE8, 0xFC, 0xFF), Rgb(0x2A, 0x8C, 0xA0), i / 4.0);
        c[23] = Rgb(0xFF, 0xFF, 0xFF);
        return c;
    }

    // ------------------------------------------------------------------ helpers

    private static void Body(PixelImage img, int x, int y, int w, int h)
    {
        img.VerticalGradient(x, y, w, h, BodyTop, BodyBottom);
        // Fine horizontal brushing.
        for (int yy = y + 1; yy < y + h; yy += 2)
            for (int xx = x; xx < x + w; xx++)
                img[xx, yy] = Shade(img[xx, yy], 0.94);
        img.Bevel(x, y, w, h, Light, Dark);
    }

    private static void Slot(PixelImage img, int x, int y, int w, int h, uint fill)
    {
        img.Fill(x, y, w, h, fill);
        img.Bevel(x - 1, y - 1, w + 2, h + 2, Dark, Light);
    }

    private static void Button(PixelImage img, int x, int y, int w, int h, bool pressed)
    {
        if (pressed)
        {
            img.VerticalGradient(x, y, w, h, BtnBottom, Shade(BtnBottom, 0.85));
            img.Bevel(x, y, w, h, Dark, Light);
        }
        else
        {
            img.VerticalGradient(x, y, w, h, BtnTop, BtnBottom);
            img.Bevel(x, y, w, h, IconLight, Dark);
        }
    }

    private static void TriangleRight(PixelImage img, int x, int y, int h, uint c)
    {
        for (int i = 0; i < h; i++)
        {
            int len = Math.Min(i, h - 1 - i) + 1;
            img.Fill(x, y + i, len, 1, c);
        }
    }

    private static void TriangleLeft(PixelImage img, int x, int y, int h, uint c)
    {
        int max = h / 2 + 1;
        for (int i = 0; i < h; i++)
        {
            int len = Math.Min(i, h - 1 - i) + 1;
            img.Fill(x + max - len, y + i, len, 1, c);
        }
    }

    private static void TriangleUp(PixelImage img, int x, int y, int w, uint c)
    {
        for (int i = 0; i <= w / 2; i++)
            img.Fill(x + w / 2 - i, y + i, i * 2 + 1, 1, c);
    }

    // ------------------------------------------------------------------ main.bmp

    private static PixelImage PaintMain()
    {
        var img = new PixelImage(275, 116);
        Body(img, 0, 0, 275, 116);

        // LCD panel with clutter bar recess.
        Slot(img, 10, 22, 8, 43, Dark);
        Slot(img, 22, 23, 80, 38, Lcd);
        for (int x = 24; x < 100; x += 2) img[x, 60] = LcdOff;

        // Marquee and info windows.
        Slot(img, 109, 24, 157, 12, Lcd);
        Slot(img, 110, 41, 17, 9, Lcd);
        Slot(img, 155, 41, 12, 9, Lcd);
        PixelFont.Draw(img, 130, 43, "KBPS", Label);
        PixelFont.Draw(img, 170, 43, "KHZ", Label);

        // Slider and position grooves (sprites cover them, this is just the bed).
        Slot(img, 107, 57, 68, 13, Dark);
        Slot(img, 177, 57, 38, 13, Dark);
        Slot(img, 16, 72, 248, 10, Dark);

        // Separator groove above the transport row.
        img.Fill(12, 85, 251, 1, Dark);
        img.Fill(12, 86, 251, 1, Mid);

        // Emblem: an original "W" badge with a cyan ring.
        int ex = 253, ey = 91;
        img.Fill(ex, ey, 13, 15, Dark);
        img.VerticalGradient(ex + 1, ey + 1, 11, 13, Rgb(0x26, 0x2A, 0x32), Rgb(0x12, 0x14, 0x18));
        img.Rect(ex + 1, ey + 1, 11, 13, AccentDim);
        img.Pattern(ex + 2, ey + 4, ["#.....#", "#.....#", "#..#..#", "#.#.#.#", ".#...#."], Accent);
        img.Fill(ex + 3, ey + 11, 7, 1, AccentDim);
        return img;
    }

    // ------------------------------------------------------------------ titlebar.bmp

    private static void TitleStrip(PixelImage img, int y, bool active, string caption)
    {
        const int x = 27, w = 275, h = 14;
        img.VerticalGradient(x, y, w, h, active ? Rgb(0x5A, 0x62, 0x70) : Rgb(0x48, 0x4C, 0x54), active ? Rgb(0x30, 0x35, 0x3E) : Rgb(0x2E, 0x31, 0x37));
        img.Bevel(x, y, w, h, active ? Light : Mid, Dark);
        uint ridge = active ? Accent : Rgb(0x6A, 0x70, 0x7A), ridgeDark = active ? AccentDim : Rgb(0x34, 0x37, 0x3D);
        int textW = PixelFont.Measure(caption);
        int tx = x + (w - textW) / 2;
        for (int ry = y + 4; ry <= y + 9; ry += 2)
        {
            img.Fill(x + 18, ry, tx - x - 24, 1, ridge);
            img.Fill(x + 18, ry + 1, tx - x - 24, 1, ridgeDark);
            img.Fill(tx + textW + 6, ry, x + 238 - (tx + textW + 6), 1, ridge);
            img.Fill(tx + textW + 6, ry + 1, x + 238 - (tx + textW + 6), 1, ridgeDark);
        }
        PixelFont.Draw(img, tx, y + 5, caption, active ? IconLight : Label);
    }

    private static void SmallButton(PixelImage img, int x, int y, bool pressed, string[] icon)
    {
        img.Fill(x, y, 9, 9, Dark);
        img.VerticalGradient(x + 1, y + 1, 7, 7, pressed ? BtnBottom : BtnTop, pressed ? Shade(BtnBottom, 0.8) : BtnBottom);
        if (pressed) img.Bevel(x + 1, y + 1, 7, 7, Dark, BtnTop);
        img.Pattern(x + 2 + (pressed ? 1 : 0), y + 2 + (pressed ? 1 : 0), icon, Icon);
    }

    private static readonly string[] IconMenu = ["#####", ".....", "#####", ".....", "#####"];
    private static readonly string[] IconMinimize = [".....", ".....", ".....", ".....", "#####"];
    private static readonly string[] IconClose = ["#...#", ".#.#.", "..#..", ".#.#.", "#...#"];
    private static readonly string[] IconShade = ["#####", "#...#", "#####", ".....", "....."];
    private static readonly string[] IconUnshade = ["#####", "#...#", "#...#", "#...#", "#####"];

    private static PixelImage PaintTitleBar()
    {
        var img = new PixelImage(344, 87, Dark);
        SmallButton(img, 0, 0, false, IconMenu);
        SmallButton(img, 0, 9, true, IconMenu);
        SmallButton(img, 9, 0, false, IconMinimize);
        SmallButton(img, 9, 9, true, IconMinimize);
        SmallButton(img, 18, 0, false, IconClose);
        SmallButton(img, 18, 9, true, IconClose);
        SmallButton(img, 0, 18, false, IconShade);
        SmallButton(img, 9, 18, true, IconShade);
        SmallButton(img, 0, 27, false, IconUnshade);
        SmallButton(img, 9, 27, true, IconUnshade);

        TitleStrip(img, 0, true, "WINRETRO AMP");
        TitleStrip(img, 15, false, "WINRETRO AMP");
        TitleStrip(img, 57, true, "PIXELS STILL ROCK");
        TitleStrip(img, 72, false, "PIXELS STILL ROCK");

        foreach (var (y, active) in new[] { (29, true), (42, false) })
        {
            img.VerticalGradient(27, y, 275, 14, active ? Rgb(0x5A, 0x62, 0x70) : Rgb(0x48, 0x4C, 0x54), active ? Rgb(0x30, 0x35, 0x3E) : Rgb(0x2E, 0x31, 0x37));
            img.Bevel(27, y, 275, 14, active ? Light : Mid, Dark);
            img.Fill(27 + 79, y + 5, 38, 5, Lcd);            // mini visualizer
            img.Fill(27 + 125, y + 3, 26, 8, Lcd);           // mini time
            img.Fill(27 + 226, y + 4, 17, 7, Dark);          // mini seek bar
            // Tiny transport glyphs for the shaded transport hot-spots.
            uint g = active ? IconLight : Label;
            int bx = 27 + 169, by = y + 4;
            img.Pattern(bx, by, ["#..#", "#.##", "####", "#.##", "#..#"], g);
            img.Pattern(bx + 10, by, ["#...", "##..", "###.", "##..", "#..."], g);
            img.Pattern(bx + 20, by, ["#.#.", "#.#.", "#.#.", "#.#.", "#.#."], g);
            img.Pattern(bx + 29, by, ["###.", "###.", "###.", "###.", "...."], g);
            img.Pattern(bx + 38, by, ["#..#", "##.#", "####", "##.#", "#..#"], g);
            img.Pattern(bx + 48, by, [".#..", "###.", "....", "###.", "...."], g);
        }

        img.Fill(0, 36, 17, 7, Lcd);
        img.Rect(0, 36, 17, 7, Mid);
        img.Fill(17, 36, 3, 7, Accent);
        img.Fill(20, 36, 3, 7, Accent);
        img.Fill(23, 36, 3, 7, Accent);

        // Clutter bar: O A I D V.
        PaintClutter(img, 304, enabled: true);
        PaintClutter(img, 312, enabled: false);
        var pressed = SpriteMap.Main.ClutterPressed;
        string letters = "OAIDV";
        for (int i = 0; i < pressed.Length; i++)
        {
            var s = pressed[i].Sprite;
            img.Fill(s.X, s.Y, s.W, s.H, AccentDim);
            img.Pattern(s.X + 2, s.Y + (s.H - 5) / 2 + 1, Mini(letters[i]), IconLight);
        }
        return img;
    }

    private static string[] Mini(char c) => c switch
    {
        'O' => [".#.", "#.#", "#.#", "#.#", ".#."],
        'A' => [".#.", "#.#", "###", "#.#", "#.#"],
        'I' => ["###", ".#.", ".#.", ".#.", "###"],
        'D' => ["##.", "#.#", "#.#", "#.#", "##."],
        _ => ["#.#", "#.#", "#.#", "#.#", ".#."],
    };

    private static void PaintClutter(PixelImage img, int x, bool enabled)
    {
        img.VerticalGradient(x, 0, 8, 43, Rgb(0x26, 0x2A, 0x31), Rgb(0x1A, 0x1C, 0x21));
        string letters = "OAIDV";
        var pressed = SpriteMap.Main.ClutterPressed;
        for (int i = 0; i < 5; i++)
        {
            var (s, top) = pressed[i];
            img.Pattern(x + 2, top + (s.H - 5) / 2 + 1, Mini(letters[i]), enabled ? Label : Mid);
        }
    }

    // ------------------------------------------------------------------ cbuttons.bmp

    private static PixelImage PaintCButtons()
    {
        var img = new PixelImage(136, 36, Dark);
        var normal = SpriteMap.Main.Transport;
        var down = SpriteMap.Main.TransportDown;
        for (int i = 0; i < 6; i++)
        {
            foreach (var (s, p) in new[] { (normal[i], false), (down[i], true) })
            {
                Button(img, s.X, s.Y, s.W, s.H, p);
                int o = p ? 1 : 0;
                int cx = s.X + s.W / 2 + o, cy = s.Y + s.H / 2 + o;
                uint c = p ? Accent : Icon;
                switch (i)
                {
                    case 0: // previous
                        img.Fill(cx - 6, cy - 4, 2, 9, c);
                        TriangleLeft(img, cx - 4, cy - 4, 9, c);
                        TriangleLeft(img, cx + 1, cy - 4, 9, c);
                        break;
                    case 1: // play
                        TriangleRight(img, cx - 2, cy - 4, 9, c);
                        break;
                    case 2: // pause
                        img.Fill(cx - 4, cy - 4, 3, 9, c);
                        img.Fill(cx + 1, cy - 4, 3, 9, c);
                        break;
                    case 3: // stop
                        img.Fill(cx - 4, cy - 4, 8, 8, c);
                        break;
                    case 4: // next
                        TriangleRight(img, cx - 6, cy - 4, 9, c);
                        TriangleRight(img, cx - 1, cy - 4, 9, c);
                        img.Fill(cx + 4, cy - 4, 2, 9, c);
                        break;
                    case 5: // eject
                        TriangleUp(img, cx - 5, cy - 4, 10, c);
                        img.Fill(cx - 5, cy + 3, 11, 2, c);
                        break;
                }
            }
        }
        return img;
    }

    // ------------------------------------------------------------------ shufrep.bmp

    private static void LedButton(PixelImage img, Sprite s, bool on, bool pressed, string text)
    {
        Button(img, s.X, s.Y, s.W, s.H, pressed);
        int o = pressed ? 1 : 0;
        int ledY = s.Y + s.H / 2 - 1 + o;
        img.Fill(s.X + 3 + o, ledY, 3, 3, on ? Accent : Rgb(0x30, 0x36, 0x40));
        if (on) img[s.X + 4 + o, ledY + 1] = IconLight;
        PixelFont.Draw(img, s.X + 8 + o, s.Y + (s.H - 5) / 2 + o, text, on ? Rgb(0x0B, 0x2F, 0x38) : Icon);
    }

    private static PixelImage PaintShufRep()
    {
        var img = new PixelImage(92, 85, Dark);
        LedButton(img, SpriteMap.Main.RepeatOff, false, false, "REP");
        LedButton(img, SpriteMap.Main.RepeatOffDown, false, true, "REP");
        LedButton(img, SpriteMap.Main.RepeatOn, true, false, "REP");
        LedButton(img, SpriteMap.Main.RepeatOnDown, true, true, "REP");
        LedButton(img, SpriteMap.Main.ShuffleOff, false, false, "SHUFFLE");
        LedButton(img, SpriteMap.Main.ShuffleOffDown, false, true, "SHUFFLE");
        LedButton(img, SpriteMap.Main.ShuffleOn, true, false, "SHUFFLE");
        LedButton(img, SpriteMap.Main.ShuffleOnDown, true, true, "SHUFFLE");
        LedButton(img, SpriteMap.Main.EqOff, false, false, "EQ");
        LedButton(img, SpriteMap.Main.EqOffDown, false, true, "EQ");
        LedButton(img, SpriteMap.Main.EqOn, true, false, "EQ");
        LedButton(img, SpriteMap.Main.EqOnDown, true, true, "EQ");
        LedButton(img, SpriteMap.Main.PlOff, false, false, "PL");
        LedButton(img, SpriteMap.Main.PlOffDown, false, true, "PL");
        LedButton(img, SpriteMap.Main.PlOn, true, false, "PL");
        LedButton(img, SpriteMap.Main.PlOnDown, true, true, "PL");
        return img;
    }

    // ------------------------------------------------------------------ posbar / volume / balance

    private static void Thumb(PixelImage img, int x, int y, int w, int h, bool pressed)
    {
        img.VerticalGradient(x, y, w, h, pressed ? Accent : BtnTop, pressed ? AccentDim : BtnBottom);
        img.Bevel(x, y, w, h, IconLight, Dark);
        for (int gx = x + w / 2 - 2; gx <= x + w / 2 + 2; gx += 2)
            img.Fill(gx, y + 2, 1, h - 4, pressed ? IconLight : Mid);
    }

    private static PixelImage PaintPosBar()
    {
        var img = new PixelImage(307, 10, Dark);
        img.Fill(0, 0, 248, 10, Rgb(0x12, 0x14, 0x18));
        img.Fill(2, 4, 244, 2, Rgb(0x26, 0x2B, 0x33));
        img.Bevel(0, 0, 248, 10, Dark, Mid);
        Thumb(img, 248, 0, 29, 10, false);
        Thumb(img, 278, 0, 29, 10, true);
        return img;
    }

    private static uint LevelColor(double t) =>
        t < 0.5 ? Lerp(Rgb(0x2E, 0xDC, 0x6A), Rgb(0xF0, 0xE0, 0x40), t * 2) : Lerp(Rgb(0xF0, 0xE0, 0x40), Rgb(0xF0, 0x4A, 0x3A), (t - 0.5) * 2);

    private static PixelImage PaintVolume()
    {
        var img = new PixelImage(68, 433, Dark);
        for (int i = 0; i < 28; i++)
        {
            int y = i * 15;
            img.Fill(0, y, 68, 13, Rgb(0x12, 0x14, 0x18));
            img.Bevel(0, y, 68, 13, Dark, Mid);
            int len = (int)Math.Round(64 * (i / 27.0));
            // A wedge that grows in height and colour with the volume.
            for (int x = 0; x < len; x++)
            {
                int hh = 2 + x * 7 / 64;
                img.Fill(2 + x, y + 11 - hh, 1, hh, LevelColor(i / 27.0));
            }
        }
        Thumb(img, 15, 422, 14, 11, false);
        Thumb(img, 0, 422, 14, 11, true);
        return img;
    }

    private static PixelImage PaintBalance()
    {
        var img = new PixelImage(47, 433, Dark);
        for (int i = 0; i < 28; i++)
        {
            int y = i * 15;
            img.Fill(9, y, 38, 13, Rgb(0x12, 0x14, 0x18));
            img.Bevel(9, y, 38, 13, Dark, Mid);
            img.Fill(11, y + 6, 34, 1, LevelColor(i / 27.0));
            img.Fill(27, y + 3, 2, 7, Mid);
        }
        Thumb(img, 15, 422, 14, 11, false);
        Thumb(img, 0, 422, 14, 11, true);
        return img;
    }

    // ------------------------------------------------------------------ monoster / playpaus

    private static PixelImage PaintMonoSter()
    {
        var img = new PixelImage(58, 24, Lcd);
        PixelFont.Draw(img, 0, 4, "STEREO", LcdOn);
        PixelFont.Draw(img, 0, 16, "STEREO", LcdOff);
        PixelFont.Draw(img, 33, 4, "MONO", LcdOn);
        PixelFont.Draw(img, 33, 16, "MONO", LcdOff);
        return img;
    }

    private static PixelImage PaintPlayPaus()
    {
        var img = new PixelImage(42, 9, Lcd);
        TriangleRight(img, 2, 1, 7, LcdOn);
        img.Fill(10, 1, 2, 7, Rgb(0xF0, 0xE0, 0x40));
        img.Fill(14, 1, 2, 7, Rgb(0xF0, 0xE0, 0x40));
        img.Fill(19, 1, 7, 7, Rgb(0xF0, 0x4A, 0x3A));
        img.Fill(37, 2, 1, 5, LcdOff);
        img.Fill(40, 2, 1, 5, LcdOn);
        return img;
    }

    // ------------------------------------------------------------------ numbers

    // Segments: a=top, b=top-right, c=bottom-right, d=bottom, e=bottom-left, f=top-left, g=middle.
    private static readonly string[] Segments = ["abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg"];

    private static void SevenSegment(PixelImage img, int x, int y, string on)
    {
        void Seg(char s, uint c)
        {
            switch (s)
            {
                case 'a': img.Fill(x + 2, y + 0, 5, 2, c); break;
                case 'g': img.Fill(x + 2, y + 5, 5, 2, c); break;
                case 'd': img.Fill(x + 2, y + 11, 5, 2, c); break;
                case 'f': img.Fill(x + 0, y + 1, 2, 5, c); break;
                case 'b': img.Fill(x + 7, y + 1, 2, 5, c); break;
                case 'e': img.Fill(x + 0, y + 7, 2, 5, c); break;
                case 'c': img.Fill(x + 7, y + 7, 2, 5, c); break;
            }
        }
        foreach (char s in "abcdefg") Seg(s, LcdOff);
        foreach (char s in on) Seg(s, LcdOn);
    }

    private static PixelImage PaintNumbers(bool withMinus)
    {
        var img = new PixelImage(withMinus ? 108 : 99, 13, Lcd);
        for (int d = 0; d < 10; d++) SevenSegment(img, d * 9, 0, Segments[d]);
        SevenSegment(img, 90, 0, "");
        if (withMinus)
        {
            img.Fill(99, 0, 9, 13, Lcd);
            img.Fill(101, 5, 5, 2, LcdOn);
        }
        return img;
    }

    // ------------------------------------------------------------------ text.bmp

    private static PixelImage PaintText()
    {
        var img = new PixelImage(155, 18, Lcd);
        for (int row = 0; row < 3; row++)
        {
            string chars = SpriteMap.FontRow(row);
            for (int i = 0; i < chars.Length; i++)
                PixelFont.Draw(img, i * 5, row * 6, chars[i].ToString(), LcdOn);
        }
        return img;
    }

    // ------------------------------------------------------------------ eqmain.bmp

    private static PixelImage PaintEqMain()
    {
        var img = new PixelImage(275, 315, Dark);
        Body(img, 0, 0, 275, 116);
        Slot(img, 86, 17, 113, 19, Lcd);

        // Slider beds and labels.
        Slot(img, 21, 38, 14, 63, Dark);
        for (int b = 0; b < 10; b++) Slot(img, 78 + b * 18, 38, 14, 63, Dark);
        PixelFont.Draw(img, 41, 38, "+12", Label);
        PixelFont.Draw(img, 46, 67, "0", Label);
        PixelFont.Draw(img, 41, 95, "-12", Label);
        PixelFont.DrawCentered(img, 28, 104, "PRE", Label);
        string[] freqs = ["60", "170", "310", "600", "1K", "3K", "6K", "12K", "14K", "16K"];
        for (int b = 0; b < 10; b++) PixelFont.DrawCentered(img, 78 + b * 18 + 7, 104, freqs[b], Label);

        // Title bars (close button drawn in, pressed version separate).
        foreach (var (y, active) in new[] { (134, true), (149, false) })
        {
            var tmp = new PixelImage(344, 87, Dark);
            TitleStrip(tmp, 0, active, "EQUALIZER");
            img.Blit(tmp, 27, 0, 275, 14, 0, y);
            SmallButton(img, 254, y + 3, false, IconShade);
            SmallButton(img, 264, y + 3, false, IconClose);
        }
        SmallButton(img, 0, 116, false, IconClose);
        SmallButton(img, 0, 116, true, IconClose);
        SmallButton(img, 0, 125, true, IconClose);

        // ON / AUTO buttons.
        LedButton(img, SpriteMap.Eq.OnOff, false, false, "ON");
        LedButton(img, SpriteMap.Eq.OnOn, true, false, "ON");
        LedButton(img, SpriteMap.Eq.OnOffDown, false, true, "ON");
        LedButton(img, SpriteMap.Eq.OnOnDown, true, true, "ON");
        LedButton(img, SpriteMap.Eq.AutoOff, false, false, "AUTO");
        LedButton(img, SpriteMap.Eq.AutoOn, true, false, "AUTO");
        LedButton(img, SpriteMap.Eq.AutoOffDown, false, true, "AUTO");
        LedButton(img, SpriteMap.Eq.AutoOnDown, true, true, "AUTO");

        // Slider backgrounds: frame 0 = bottom (-12 dB), frame 27 = top (+12 dB).
        for (int f = 0; f < 28; f++)
        {
            var s = SpriteMap.Eq.SliderBg(f);
            img.Fill(s.X, s.Y, s.W, s.H, Rgb(0x12, 0x14, 0x18));
            img.Bevel(s.X, s.Y, s.W, s.H, Dark, Mid);
            int fill = (int)Math.Round((s.H - 4) * (f / 27.0));
            for (int i = 0; i < fill; i++)
                img.Fill(s.X + 5, s.Y + s.H - 3 - i, 4, 1, LevelColor(i / (double)(s.H - 4)));
            img.Fill(s.X + 2, s.Y + s.H / 2, 2, 1, Mid);
            img.Fill(s.X + 10, s.Y + s.H / 2, 2, 1, Mid);
        }
        Thumb(img, 0, 164, 11, 11, false);
        Thumb(img, 0, 176, 11, 11, true);

        Button(img, 224, 164, 44, 12, false);
        PixelFont.Draw(img, 229, 167, "PRESETS", Icon);
        Button(img, 224, 176, 44, 12, true);
        PixelFont.Draw(img, 230, 180, "PRESETS", Accent);

        // Graph background with a faint grid, line colour ramp and preamp line.
        img.Fill(0, 294, 113, 19, Lcd);
        for (int x = 2; x < 113; x += 12) img.Fill(x, 294, 1, 19, Rgb(0x12, 0x22, 0x1C));
        img.Fill(0, 303, 113, 1, Rgb(0x1E, 0x3A, 0x2E));
        for (int i = 0; i < 19; i++) img[115, 294 + i] = LevelColor(1 - i / 18.0);
        img.Fill(0, 314, 113, 1, Rgb(0x8C, 0x96, 0xA4));
        return img;
    }

    private static PixelImage PaintEqEx()
    {
        var img = new PixelImage(275, 56, Dark);
        foreach (var (y, active) in new[] { (0, true), (15, false) })
        {
            var tmp = new PixelImage(344, 87, Dark);
            TitleStrip(tmp, 0, active, "EQUALIZER");
            img.Blit(tmp, 27, 0, 275, 14, 0, y);
            SmallButton(img, 254, y + 3, false, IconUnshade);
            SmallButton(img, 264, y + 3, false, IconClose);
        }
        SmallButton(img, 254, 30, true, IconShade);
        SmallButton(img, 1, 47, true, IconUnshade);
        SmallButton(img, 11, 47, true, IconClose);
        return img;
    }

    // ------------------------------------------------------------------ pledit.bmp

    private static PixelImage PaintPlEdit()
    {
        var img = new PixelImage(280, 186, Dark);

        void Top(int x, int y, int w, bool active, string? caption)
        {
            var tmp = new PixelImage(w, 20);
            tmp.VerticalGradient(0, 0, w, 20, active ? Rgb(0x5A, 0x62, 0x70) : Rgb(0x48, 0x4C, 0x54), active ? Rgb(0x30, 0x35, 0x3E) : Rgb(0x2B, 0x2E, 0x34));
            tmp.Fill(0, 0, w, 1, active ? Light : Mid);
            tmp.Fill(0, 19, w, 1, Dark);
            uint ridge = active ? Accent : Rgb(0x6A, 0x70, 0x7A), ridgeDark = active ? AccentDim : Rgb(0x34, 0x37, 0x3D);
            if (caption is null)
            {
                for (int ry = 5; ry <= 11; ry += 3)
                {
                    tmp.Fill(0, ry, w, 1, ridge);
                    tmp.Fill(0, ry + 1, w, 1, ridgeDark);
                }
            }
            else
            {
                PixelFont.DrawCentered(tmp, w / 2, 7, caption, active ? IconLight : Label);
            }
            img.Blit(tmp, 0, 0, w, 20, x, y);
        }

        foreach (var (y, active) in new[] { (0, true), (21, false) })
        {
            Top(0, y, 25, active, null);
            img.Fill(0, y, 1, 20, active ? Light : Mid);
            Top(26, y, 100, active, "PLAYLIST");
            Top(127, y, 25, active, null);
            Top(153, y, 25, active, null);
            img.Fill(177, y, 1, 20, Dark);
            SmallButton(img, 153 + 5, y + 3, false, IconShade);
            SmallButton(img, 153 + 14, y + 3, false, IconClose);
        }

        // Side tiles: left border, right border with scroll groove.
        img.VerticalGradient(0, 42, 12, 29, BodyTop, BodyTop);
        img.Fill(0, 42, 1, 29, Light);
        img.Fill(11, 42, 1, 29, Dark);
        img.Fill(31, 42, 20, 29, BodyTop);
        img.Fill(31, 42, 1, 29, Light);
        img.Fill(50, 42, 1, 29, Dark);
        img.Fill(36, 42, 8, 29, Rgb(0x12, 0x14, 0x18));

        SmallButton(img, 52, 42, true, IconClose);
        SmallButton(img, 62, 42, true, IconShade);
        SmallButton(img, 150, 42, true, IconUnshade);
        Thumb(img, 52, 53, 8, 18, false);
        Thumb(img, 61, 53, 8, 18, true);

        // Shade-mode pieces.
        img.VerticalGradient(72, 42, 25, 14, Rgb(0x5A, 0x62, 0x70), Rgb(0x30, 0x35, 0x3E));
        img.VerticalGradient(72, 57, 25, 14, Rgb(0x5A, 0x62, 0x70), Rgb(0x30, 0x35, 0x3E));
        img.VerticalGradient(99, 57, 50, 14, Rgb(0x48, 0x4C, 0x54), Rgb(0x2E, 0x31, 0x37));
        img.VerticalGradient(99, 42, 50, 14, Rgb(0x5A, 0x62, 0x70), Rgb(0x30, 0x35, 0x3E));
        foreach (int y in new[] { 42, 57 })
        {
            SmallButton(img, 99 + 31, y + 3, false, IconUnshade);
            SmallButton(img, 99 + 40, y + 3, false, IconClose);
        }

        // Bottom pieces.
        Body(img, 0, 72, 125, 38);
        img.Fill(0, 72, 125, 1, Mid);
        string[] labels = ["ADD", "REM", "SEL", "MISC"];
        for (int i = 0; i < 4; i++)
        {
            int bx = SpriteMap.Pl.MenuButtonX[i];
            Button(img, bx, 72 + 8, 22, 18, false);
            PixelFont.DrawCentered(img, bx + 11, 72 + 15, labels[i], Icon);
        }
        Body(img, 126, 72, 150, 38);
        img.Fill(126, 72, 150, 1, Mid);
        Button(img, 126 + 104, 72 + 8, 22, 18, false);
        PixelFont.DrawCentered(img, 126 + 115, 72 + 15, "LIST", Icon);
        Slot(img, 126 + 7, 72 + 9, 90, 8, Lcd);           // running time
        Slot(img, 126 + 66, 72 + 22, 30, 8, Lcd);         // mini time
        // Mini transport: prev, play, pause, stop, next, eject.
        int mx = 126 + 6, my = 72 + 23;
        img.Pattern(mx, my, ["#..#", "#.##", "####", "#.##", "#..#"], Label);
        img.Pattern(mx + 9, my, ["#...", "##..", "###.", "##..", "#..."], Label);
        img.Pattern(mx + 18, my, ["#.#.", "#.#.", "#.#.", "#.#.", "#.#."], Label);
        img.Pattern(mx + 26, my, ["###.", "###.", "###.", "....", "...."], Label);
        img.Pattern(mx + 34, my, ["#..#", "##.#", "####", "##.#", "#..#"], Label);
        img.Pattern(mx + 43, my, [".#..", "###.", "....", "###.", "...."], Label);
        // Scroll arrows next to the mini time.
        img.Pattern(126 + 100, 72 + 23, ["..#..", ".###.", "#####"], Label);
        img.Pattern(126 + 100, 72 + 27, ["#####", ".###.", "..#.."], Label);
        // Resize grip.
        for (int i = 0; i < 4; i++)
            img.Line(126 + 148 - i * 4, 72 + 36, 126 + 148, 72 + 36 - i * 4, Mid);

        Body(img, 179, 0, 25, 38);
        img.Fill(179, 0, 25, 1, Mid);
        img.Fill(205, 0, 75, 38, Lcd);

        // Pop-up menus.
        string[][] menuText = [["FILE", "DIR", "URL"], ["MISC", "SEL", "CROP", "ALL"], ["ALL", "NONE", "INV"], ["OPTS", "INFO", "SORT"], ["LOAD", "SAVE", "NEW"]];
        for (int m = 0; m < SpriteMap.Pl.Menus.Length; m++)
        {
            var (bar, items) = SpriteMap.Pl.Menus[m];
            img.Fill(bar.X, bar.Y, bar.W, bar.H, Accent);
            for (int i = 0; i < items.Length; i++)
            {
                var (n, p) = items[i];
                img.Fill(n.X, n.Y, n.W, n.H, Rgb(0x22, 0x26, 0x2D));
                img.Bevel(n.X, n.Y, n.W, n.H, Mid, Dark);
                PixelFont.DrawCentered(img, n.X + 11, n.Y + 7, menuText[m][i], Label);
                img.Fill(p.X, p.Y, p.W, p.H, AccentDim);
                img.Bevel(p.X, p.Y, p.W, p.H, Accent, Dark);
                PixelFont.DrawCentered(img, p.X + 11, p.Y + 7, menuText[m][i], IconLight);
            }
        }
        return img;
    }
}

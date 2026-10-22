using WinRetroAmp.Core.Imaging;

namespace WinRetroAmp.Core.Skins;

/// <summary>
/// Sprite coordinates of the classic 2.x skin file format (sheet name → rectangles) and the
/// on-window positions of every element. Kept in one place so the default skin painter, the
/// skin loader and the renderers all agree on the layout.
/// </summary>
public static class SpriteMap
{
    public const int MainWidth = 275, MainHeight = 116, ShadeHeight = 14;

    public static class Sheets
    {
        public const string Main = "main", CButtons = "cbuttons", TitleBar = "titlebar", ShufRep = "shufrep",
            PosBar = "posbar", Volume = "volume", Balance = "balance", MonoSter = "monoster", PlayPaus = "playpaus",
            Numbers = "numbers", NumsEx = "nums_ex", Text = "text", EqMain = "eqmain", EqEx = "eq_ex", PlEdit = "pledit";

        public static readonly string[] All = [Main, CButtons, TitleBar, ShufRep, PosBar, Volume, Balance, MonoSter, PlayPaus, Numbers, NumsEx, Text, EqMain, EqEx, PlEdit];

        /// <summary>Minimum sheet sizes; a user sheet smaller than this is padded from the default skin.</summary>
        public static readonly Dictionary<string, (int W, int H)> Sizes = new()
        {
            [Main] = (275, 116), [CButtons] = (136, 36), [TitleBar] = (344, 87), [ShufRep] = (92, 85),
            [PosBar] = (307, 10), [Volume] = (68, 433), [Balance] = (47, 433), [MonoSter] = (58, 24),
            [PlayPaus] = (42, 9), [Numbers] = (99, 13), [NumsEx] = (108, 13), [Text] = (155, 18),
            [EqMain] = (275, 315), [EqEx] = (275, 56), [PlEdit] = (280, 186),
        };
    }

    // ---------- MAIN window ----------
    public static class Main
    {
        public static readonly Sprite Background = new(0, 0, 275, 116);

        // titlebar.bmp
        public static readonly Sprite TitleActive = new(27, 0, 275, 14), TitleInactive = new(27, 15, 275, 14);
        public static readonly Sprite ShadeBgActive = new(27, 29, 275, 14), ShadeBgInactive = new(27, 42, 275, 14);
        public static readonly Sprite OptionsBtn = new(0, 0, 9, 9), OptionsBtnDown = new(0, 9, 9, 9);
        public static readonly Sprite MinimizeBtn = new(9, 0, 9, 9), MinimizeBtnDown = new(9, 9, 9, 9);
        public static readonly Sprite CloseBtn = new(18, 0, 9, 9), CloseBtnDown = new(18, 9, 9, 9);
        public static readonly Sprite ShadeBtn = new(0, 18, 9, 9), ShadeBtnDown = new(9, 18, 9, 9);
        public static readonly Sprite UnshadeBtn = new(0, 27, 9, 9), UnshadeBtnDown = new(9, 27, 9, 9);
        public static readonly Sprite ShadePosBg = new(0, 36, 17, 7), ShadePosThumb = new(20, 36, 3, 7);
        public static readonly Sprite ShadePosThumbLeft = new(17, 36, 3, 7), ShadePosThumbRight = new(23, 36, 3, 7);
        public static readonly Sprite ClutterBg = new(304, 0, 8, 43), ClutterBgDisabled = new(312, 0, 8, 43);
        /// <summary>Clutter bar O, A, I, D, V pressed sprites (sheet rect, y offset inside the bar).</summary>
        public static readonly (Sprite Sprite, int Top)[] ClutterPressed =
        [
            (new(304, 47, 8, 8), 3), (new(312, 55, 8, 7), 11), (new(320, 62, 8, 7), 18), (new(328, 69, 8, 8), 25), (new(336, 77, 8, 7), 33),
        ];

        public const int OptionsX = 6, MinimizeX = 244, ShadeX = 254, CloseX = 264, ButtonsY = 3;
        public const int ClutterX = 10, ClutterY = 22;

        // cbuttons.bmp: prev, play, pause, stop, next, eject
        public static readonly Sprite[] Transport =
            [new(0, 0, 23, 18), new(23, 0, 23, 18), new(46, 0, 23, 18), new(69, 0, 23, 18), new(92, 0, 22, 18), new(114, 0, 22, 16)];
        public static readonly Sprite[] TransportDown =
            [new(0, 18, 23, 18), new(23, 18, 23, 18), new(46, 18, 23, 18), new(69, 18, 23, 18), new(92, 18, 22, 18), new(114, 16, 22, 16)];
        public static readonly (int X, int Y)[] TransportPos = [(16, 88), (39, 88), (62, 88), (85, 88), (108, 88), (136, 89)];

        // shufrep.bmp
        public static readonly Sprite RepeatOff = new(0, 0, 28, 15), RepeatOffDown = new(0, 15, 28, 15), RepeatOn = new(0, 30, 28, 15), RepeatOnDown = new(0, 45, 28, 15);
        public static readonly Sprite ShuffleOff = new(28, 0, 47, 15), ShuffleOffDown = new(28, 15, 47, 15), ShuffleOn = new(28, 30, 47, 15), ShuffleOnDown = new(28, 45, 47, 15);
        public static readonly Sprite EqOff = new(0, 61, 23, 12), EqOffDown = new(46, 61, 23, 12), EqOn = new(0, 73, 23, 12), EqOnDown = new(46, 73, 23, 12);
        public static readonly Sprite PlOff = new(23, 61, 23, 12), PlOffDown = new(69, 61, 23, 12), PlOn = new(23, 73, 23, 12), PlOnDown = new(69, 73, 23, 12);
        public const int ShuffleX = 164, RepeatX = 210, ShufRepY = 89, EqBtnX = 219, PlBtnX = 242, EqPlY = 58;

        // posbar.bmp
        public static readonly Sprite PosBg = new(0, 0, 248, 10), PosThumb = new(248, 0, 29, 10), PosThumbDown = new(278, 0, 29, 10);
        public const int PosX = 16, PosY = 72, PosWidth = 248, PosThumbWidth = 29;

        // volume.bmp / balance.bmp (28 backgrounds stacked 15px apart, thumbs at the bottom)
        public static Sprite VolumeBg(int frame) => new(0, Math.Clamp(frame, 0, 27) * 15, 68, 13);
        public static Sprite BalanceBg(int frame) => new(9, Math.Clamp(frame, 0, 27) * 15, 38, 13);
        public static readonly Sprite SliderThumb = new(15, 422, 14, 11), SliderThumbDown = new(0, 422, 14, 11);
        public const int VolumeX = 107, BalanceX = 177, SliderY = 57, VolumeWidth = 68, BalanceWidth = 38;

        // monoster.bmp
        public static readonly Sprite StereoOn = new(0, 0, 29, 12), StereoOff = new(0, 12, 29, 12), MonoOn = new(29, 0, 27, 12), MonoOff = new(29, 12, 27, 12);
        public const int MonoX = 212, StereoX = 239, MonoStereoY = 41;

        // playpaus.bmp
        public static readonly Sprite Playing = new(0, 0, 9, 9), Paused = new(9, 0, 9, 9), Stopped = new(18, 0, 9, 9);
        public static readonly Sprite NotWorking = new(36, 0, 3, 9), Working = new(39, 0, 3, 9);
        public const int StatusX = 26, StatusY = 28, WorkX = 24;

        // numbers.bmp / nums_ex.bmp
        public static Sprite Digit(int d) => new(d * 9, 0, 9, 13);
        public static readonly Sprite DigitBlank = new(90, 0, 9, 13), MinusEx = new(99, 0, 9, 13);
        public static readonly Sprite MinusClassic = new(20, 6, 5, 1), NoMinusClassic = new(9, 6, 5, 1);
        public const int MinusX = 36, TimeY = 26;
        public static readonly int[] DigitX = [48, 60, 78, 90];

        // text areas
        public const int MarqueeX = 111, MarqueeY = 27, MarqueeWidth = 154;
        public const int KbpsX = 111, KhzX = 156, InfoY = 43;
        public const int VisX = 24, VisY = 43, VisWidth = 76, VisHeight = 16;
        public static readonly (int X, int Y, int W, int H) AboutArea = (253, 91, 13, 15);

        // shaded main window
        public const int ShadeTimeX = 127, ShadeTimeY = 4, ShadePosX = 226, ShadePosY = 4, ShadeVisX = 79, ShadeVisY = 5;
        public static readonly (int X, int W)[] ShadeTransport = [(169, 8), (177, 10), (187, 9), (196, 9), (205, 10), (215, 10)];
    }

    // ---------- EQUALIZER window ----------
    public static class Eq
    {
        public static readonly Sprite Background = new(0, 0, 275, 116);
        public static readonly Sprite TitleActive = new(0, 134, 275, 14), TitleInactive = new(0, 149, 275, 14);
        public static readonly Sprite CloseBtnDown = new(0, 116, 9, 9);
        public static readonly Sprite OnOff = new(10, 119, 26, 12), OnOn = new(69, 119, 26, 12), OnOffDown = new(128, 119, 26, 12), OnOnDown = new(187, 119, 26, 12);
        public static readonly Sprite AutoOff = new(36, 119, 32, 12), AutoOn = new(95, 119, 32, 12), AutoOffDown = new(154, 119, 32, 12), AutoOnDown = new(213, 119, 32, 12);
        public static readonly Sprite Presets = new(224, 164, 44, 12), PresetsDown = new(224, 176, 44, 12);
        public static readonly Sprite Thumb = new(0, 164, 11, 11), ThumbDown = new(0, 176, 11, 11);
        public static readonly Sprite GraphBg = new(0, 294, 113, 19), GraphLineColors = new(115, 294, 1, 19), PreampLine = new(0, 314, 113, 1);
        public static Sprite SliderBg(int frame)
        {
            frame = Math.Clamp(frame, 0, 27);
            return new(13 + frame % 14 * 15, 164 + frame / 14 * 65, 14, 63);
        }

        // eq_ex.bmp (shade mode)
        public static readonly Sprite ShadeActive = new(0, 0, 275, 14), ShadeInactive = new(0, 15, 275, 14);
        public static readonly Sprite ShadeBtnDown = new(254, 30, 9, 9), UnshadeBtnDown = new(1, 47, 9, 9), ShadeCloseDown = new(11, 47, 9, 9);

        public const int OnX = 14, AutoX = 40, ButtonsY = 18, PresetsX = 217, GraphX = 86, GraphY = 17;
        public const int PreampX = 21, BandsX = 78, BandStep = 18, SliderY = 38, SliderHeight = 63, ThumbRange = 51;
        public const int CloseX = 264, ShadeX = 254, ButtonsTop = 3;
    }

    // ---------- PLAYLIST window ----------
    public static class Pl
    {
        public const int MinWidth = 275, MinHeight = 116, StepW = 25, StepH = 29;
        public static readonly Sprite TopLeft = new(0, 0, 25, 20), TopLeftInactive = new(0, 21, 25, 20);
        public static readonly Sprite Title = new(26, 0, 100, 20), TitleInactive = new(26, 21, 100, 20);
        public static readonly Sprite TopTile = new(127, 0, 25, 20), TopTileInactive = new(127, 21, 25, 20);
        public static readonly Sprite TopRight = new(153, 0, 25, 20), TopRightInactive = new(153, 21, 25, 20);
        public static readonly Sprite LeftTile = new(0, 42, 12, 29), RightTile = new(31, 42, 20, 29);
        public static readonly Sprite BottomTile = new(179, 0, 25, 38), BottomLeft = new(0, 72, 125, 38), BottomRight = new(126, 72, 150, 38);
        public static readonly Sprite ScrollThumb = new(52, 53, 8, 18), ScrollThumbDown = new(61, 53, 8, 18);
        public static readonly Sprite CloseDown = new(52, 42, 9, 9), ShadeDown = new(62, 42, 9, 9), UnshadeDown = new(150, 42, 9, 9);
        public static readonly Sprite ShadeLeft = new(72, 42, 25, 14), ShadeTile = new(72, 57, 25, 14), ShadeRight = new(99, 57, 50, 14), ShadeRightActive = new(99, 42, 50, 14);

        /// <summary>Pop-up button menus: bar plus (normal, pressed) items listed bottom to top.</summary>
        public static readonly (Sprite Bar, (Sprite Normal, Sprite Pressed)[] Items)[] Menus =
        [
            // ADD: file, dir, url
            (new(48, 111, 3, 54), [(new(0, 149, 22, 18), new(23, 149, 22, 18)), (new(0, 130, 22, 18), new(23, 130, 22, 18)), (new(0, 111, 22, 18), new(23, 111, 22, 18))]),
            // REM: misc, selected, crop, all
            (new(100, 111, 3, 72), [(new(54, 168, 22, 18), new(77, 168, 22, 18)), (new(54, 149, 22, 18), new(77, 149, 22, 18)), (new(54, 130, 22, 18), new(77, 130, 22, 18)), (new(54, 111, 22, 18), new(77, 111, 22, 18))]),
            // SEL: all, none, invert
            (new(150, 111, 3, 54), [(new(104, 149, 22, 18), new(127, 149, 22, 18)), (new(104, 130, 22, 18), new(127, 130, 22, 18)), (new(104, 111, 22, 18), new(127, 111, 22, 18))]),
            // MISC: options, file info, sort
            (new(200, 111, 3, 54), [(new(154, 149, 22, 18), new(177, 149, 22, 18)), (new(154, 130, 22, 18), new(177, 130, 22, 18)), (new(154, 111, 22, 18), new(177, 111, 22, 18))]),
            // LIST: load, save, new
            (new(250, 111, 3, 54), [(new(204, 149, 22, 18), new(227, 149, 22, 18)), (new(204, 130, 22, 18), new(227, 130, 22, 18)), (new(204, 111, 22, 18), new(227, 111, 22, 18))]),
        ];

        public const int ListLeft = 12, ListTop = 20, RightBorder = 20, BottomBorder = 38, RowHeight = 13;
        /// <summary>X of the ADD, REM, SEL, MISC buttons (LIST is measured from the right edge).</summary>
        public static readonly int[] MenuButtonX = [14, 43, 72, 101];
        public const int ListButtonFromRight = 46, MenuButtonFromBottom = 30;
    }

    // ---------- text.bmp font ----------
    public const int CharW = 5, CharH = 6;
    private const string Row0 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ\"@";
    private const string Row1 = "0123456789….:()-'!_+\\/[]^&%,=$#";
    private const string Row2 = "ÅÖÄ?*";

    /// <summary>Location of a character in text.bmp (unknown characters render as a space).</summary>
    public static (int X, int Y) CharCell(char c)
    {
        c = char.ToUpperInvariant(c);
        c = c switch
        {
            '<' => '(', '>' => ')', '{' => '(', '}' => ')', '|' => '!', '~' => '-', '`' => '\'', ';' => ':', '"' => '"',
            'Á' or 'À' or 'Â' or 'Ã' => 'A',
            'É' or 'È' or 'Ê' or 'Ë' => 'E',
            'Í' or 'Ì' or 'Î' or 'Ï' => 'I',
            'Ó' or 'Ò' or 'Ô' or 'Õ' => 'O',
            'Ú' or 'Ù' or 'Û' or 'Ü' => 'U',
            'Ñ' => 'N', 'Ç' => 'C',
            _ => c,
        };
        int i = Row0.IndexOf(c);
        if (i >= 0) return (i * CharW, 0);
        i = Row1.IndexOf(c);
        if (i >= 0) return (i * CharW, CharH);
        i = Row2.IndexOf(c);
        if (i >= 0) return (i * CharW, CharH * 2);
        return (30 * CharW, 0); // blank cell
    }

    internal static string FontRow(int row) => row switch { 0 => Row0, 1 => Row1, _ => Row2 };
}

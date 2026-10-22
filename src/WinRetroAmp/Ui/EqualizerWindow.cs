using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Skins;
using E = WinRetroAmp.Core.Skins.SpriteMap.Eq;
using Sheets = WinRetroAmp.Core.Skins.SpriteMap.Sheets;

namespace WinRetroAmp.Ui;

/// <summary>The 10-band equalizer window with preamp, ON/AUTO, presets and response graph.</summary>
public sealed class EqualizerWindow : SkinnedWindow
{
    private enum Part { None, Close, Shade, On, Auto, Presets, Preamp, Band0 }

    private Part _pressed;
    private int _band = -1; // -1 = preamp when dragging a slider
    private bool _pressedInside;

    public EqualizerWindow(PlayerController c, WindowDocking d) : base(c, d)
    {
        Title = "Equalizer";
        ShowInTaskbar = false;
    }

    public bool Shaded
    {
        get => C.Settings.Equalizer.Shaded;
        set
        {
            if (Shaded == value) return;
            Docking.Resize(this, () =>
            {
                C.Settings.Equalizer.Shaded = value;
                UpdateSize();
            });
        }
    }

    protected override (int W, int H) LogicalSize => (275, Shaded ? SpriteMap.ShadeHeight : 116);

    private EqualizerSettings Eq => C.Engine.Equalizer;

    private bool Down(Part p) => _pressed == p && _pressedInside;

    private static int SliderX(int band) => band < 0 ? E.PreampX : E.BandsX + band * E.BandStep;

    private static int ThumbY(double db) => E.SliderY + (int)Math.Round((1 - (db + 12) / 24) * E.ThumbRange);

    protected override void Render(PixelImage g)
    {
        var sheet = Skin[Sheets.EqMain];
        if (Shaded)
        {
            var ex = Skin[Sheets.EqEx];
            g.Blit(ex, IsTitleActive ? E.ShadeActive : E.ShadeInactive, 0, 0);
            if (Down(Part.Shade)) g.Blit(ex, E.UnshadeBtnDown, E.ShadeX, E.ButtonsTop);
            if (Down(Part.Close)) g.Blit(ex, E.ShadeCloseDown, E.CloseX, E.ButtonsTop);
            return;
        }

        g.Blit(sheet, E.Background, 0, 0);
        g.Blit(sheet, IsTitleActive ? E.TitleActive : E.TitleInactive, 0, 0);
        if (Down(Part.Close)) g.Blit(sheet, E.CloseBtnDown, E.CloseX, E.ButtonsTop);
        if (Down(Part.Shade)) g.Blit(Skin[Sheets.EqEx], E.ShadeBtnDown, E.ShadeX, E.ButtonsTop);

        bool on = Eq.Enabled, auto = Eq.Auto;
        g.Blit(sheet, on ? (Down(Part.On) ? E.OnOnDown : E.OnOn) : (Down(Part.On) ? E.OnOffDown : E.OnOff), E.OnX, E.ButtonsY);
        g.Blit(sheet, auto ? (Down(Part.Auto) ? E.AutoOnDown : E.AutoOn) : (Down(Part.Auto) ? E.AutoOffDown : E.AutoOff), E.AutoX, E.ButtonsY);
        g.Blit(sheet, Down(Part.Presets) ? E.PresetsDown : E.Presets, E.PresetsX, E.ButtonsY);

        RenderGraph(g, sheet);

        for (int b = -1; b < 10; b++)
        {
            double db = b < 0 ? Eq.Preamp : Eq[b];
            int frame = (int)Math.Round((db + 12) / 24 * 27);
            int sx = SliderX(b);
            g.Blit(sheet, E.SliderBg(frame), sx, E.SliderY);
            bool dragging = (_pressed == Part.Preamp && b < 0) || (_pressed == Part.Band0 && _band == b);
            g.Blit(sheet, dragging ? E.ThumbDown : E.Thumb, sx + 1, ThumbY(db));
        }
    }

    /// <summary>Response curve through the 10 band values (cubic spline), coloured by height.</summary>
    private void RenderGraph(PixelImage g, PixelImage sheet)
    {
        g.Blit(sheet, E.GraphBg, E.GraphX, E.GraphY);
        int h = E.GraphBg.H;
        int preY = E.GraphY + (int)Math.Round((1 - (Eq.Preamp + 12) / 24) * (h - 1));
        g.Blit(sheet, E.PreampLine, E.GraphX, preY);

        var values = Eq.Bands;
        var ys = Spline(values, 109);
        int prev = -1;
        for (int i = 0; i < ys.Length; i++)
        {
            int y = Math.Clamp((int)Math.Round((1 - (ys[i] + 12) / 24) * (h - 1)), 0, h - 1);
            int from = prev < 0 ? y : Math.Min(prev, y), to = prev < 0 ? y : Math.Max(prev, y);
            for (int k = from; k <= to; k++)
                g[E.GraphX + 2 + i, E.GraphY + k] = sheet[E.GraphLineColors.X, E.GraphLineColors.Y + k];
            prev = y;
        }
    }

    /// <summary>Natural cubic spline through equally spaced points, sampled at <paramref name="samples"/> positions.</summary>
    internal static double[] Spline(double[] p, int samples)
    {
        int n = p.Length;
        var m = new double[n];
        var a = new double[n];
        var b = new double[n];
        for (int i = 1; i < n - 1; i++)
        {
            double r = 6 * (p[i + 1] - 2 * p[i] + p[i - 1]);
            double denom = 4 - (i > 1 ? a[i - 1] : 0);
            a[i] = 1 / denom;
            b[i] = (r - (i > 1 ? b[i - 1] : 0)) / denom;
        }
        for (int i = n - 2; i >= 1; i--) m[i] = b[i] - a[i] * m[i + 1];
        var result = new double[samples];
        for (int s = 0; s < samples; s++)
        {
            double t = s * (n - 1) / (double)(samples - 1);
            int i = Math.Min((int)t, n - 2);
            double u = t - i;
            result[s] = (1 - u) * p[i] + u * p[i + 1] + ((1 - u) * (1 - u) * (1 - u) - (1 - u)) * m[i] / 6 + (u * u * u - u) * m[i + 1] / 6;
        }
        return result;
    }

    private Part HitTest(int x, int y, out int band)
    {
        band = -1;
        if (Hit(x, y, E.CloseX, E.ButtonsTop, 9, 9)) return Part.Close;
        if (Hit(x, y, E.ShadeX, E.ButtonsTop, 9, 9)) return Part.Shade;
        if (Shaded) return Part.None;
        if (Hit(x, y, E.OnX, E.ButtonsY, 26, 12)) return Part.On;
        if (Hit(x, y, E.AutoX, E.ButtonsY, 32, 12)) return Part.Auto;
        if (Hit(x, y, E.PresetsX, E.ButtonsY, 44, 12)) return Part.Presets;
        if (Hit(x, y, E.PreampX, E.SliderY, 14, E.SliderHeight)) return Part.Preamp;
        for (int b = 0; b < 10; b++)
        {
            if (Hit(x, y, SliderX(b), E.SliderY, 14, E.SliderHeight))
            {
                band = b;
                return Part.Band0;
            }
        }
        return Part.None;
    }

    private static double DbFromY(int y) => Math.Clamp(Math.Round((1 - (y - E.SliderY - 5) / (double)E.ThumbRange) * 24 - 12, 1), -12, 12);

    protected override void OnSkinDown(int x, int y)
    {
        var part = HitTest(x, y, out int band);
        if (part == Part.None)
        {
            BeginWindowDrag();
            return;
        }
        _pressed = part;
        _band = band;
        _pressedInside = true;
        if (part is Part.Preamp or Part.Band0) OnSkinMove(x, y, true);
    }

    protected override void OnSkinMove(int x, int y, bool leftDown)
    {
        if (!leftDown || _pressed == Part.None) return;
        if (_pressed == Part.Preamp)
        {
            Eq.Preamp = DbFromY(y);
            C.ShowMessage($"EQ: PREAMP {Eq.Preamp:+0.0;-0.0;0.0} DB");
        }
        else if (_pressed == Part.Band0)
        {
            Eq[_band] = DbFromY(y);
            double f = EqualizerSettings.Frequencies[_band];
            C.ShowMessage($"EQ: {(f >= 1000 ? $"{f / 1000:0}KHZ" : $"{f:0}HZ")} {Eq[_band]:+0.0;-0.0;0.0} DB");
        }
        else
        {
            _pressedInside = HitTest(x, y, out int b) == _pressed;
        }
        Invalidate();
    }

    protected override void OnSkinUp(int x, int y)
    {
        var part = _pressed;
        bool inside = _pressedInside && HitTest(x, y, out _) == part;
        _pressed = Part.None;
        if (part is Part.Preamp or Part.Band0)
        {
            C.SaveEqToSettings();
            return;
        }
        if (!inside) return;
        switch (part)
        {
            case Part.Close: AppShell.Instance.ToggleEqualizer(); break;
            case Part.Shade: Shaded = !Shaded; break;
            case Part.On:
                Eq.Enabled = !Eq.Enabled;
                C.SaveEqToSettings();
                break;
            case Part.Auto:
                Eq.Auto = !Eq.Auto;
                C.SaveEqToSettings();
                break;
            case Part.Presets: Menus.ShowPresetsMenu(this); break;
        }
    }

    protected override void OnSkinWheel(int x, int y, int delta)
    {
        var part = HitTest(x, y, out int band);
        double step = delta > 0 ? 1 : -1;
        if (part == Part.Preamp) Eq.Preamp += step;
        else if (part == Part.Band0) Eq[band] += step;
        else
        {
            base.OnSkinWheel(x, y, delta);
            return;
        }
        C.SaveEqToSettings();
        Invalidate();
    }

    protected override bool OnSkinDoubleClick(int x, int y)
    {
        var part = HitTest(x, y, out int band);
        // Double-clicking a slider resets it to 0 dB.
        if (part == Part.Preamp) { Eq.Preamp = 0; C.SaveEqToSettings(); return true; }
        if (part == Part.Band0) { Eq[band] = 0; C.SaveEqToSettings(); return true; }
        if (y < 14 && part == Part.None)
        {
            Shaded = !Shaded;
            return true;
        }
        return false;
    }
}

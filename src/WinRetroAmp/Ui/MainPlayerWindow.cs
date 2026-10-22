using System.Windows;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Settings;
using WinRetroAmp.Core.Skins;
using M = WinRetroAmp.Core.Skins.SpriteMap.Main;
using Sheets = WinRetroAmp.Core.Skins.SpriteMap.Sheets;

namespace WinRetroAmp.Ui;

/// <summary>The 275×116 main window (and its 275×14 "windowshade" mode).</summary>
public sealed class MainPlayerWindow : SkinnedWindow
{
    private enum Part
    {
        None, Options, Minimize, Shade, Close, Clutter0, Clutter1, Clutter2, Clutter3, Clutter4,
        Prev, Play, Pause, Stop, Next, Eject, Shuffle, Repeat, EqBtn, PlBtn,
        Volume, Balance, Position, Marquee, About, ShadePos, ShadeT0, ShadeT1, ShadeT2, ShadeT3, ShadeT4, ShadeT5,
    }

    private Part _pressed;
    private bool _pressedInside;
    private double _seekFraction;
    private int _marqueeOffset, _marqueeDragStart, _marqueeDragOffset;
    private DateTime _lastScroll = DateTime.UtcNow;
    private readonly Visualizer _vis = new();

    public MainPlayerWindow(PlayerController c, WindowDocking d) : base(c, d)
    {
        Title = "WinRetro Amp";
        ShowInTaskbar = true;
        AllowDrop = true;
        Drop += async (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files) await C.OpenAsync(files, replace: true);
        };
    }

    public bool Shaded
    {
        get => C.Settings.Main.Shaded;
        set
        {
            if (Shaded == value) return;
            Docking.Resize(this, () =>
            {
                C.Settings.Main.Shaded = value;
                UpdateSize();
            });
        }
    }

    protected override (int W, int H) LogicalSize => (M.Background.W, Shaded ? SpriteMap.ShadeHeight : M.Background.H);

    public override bool IsAnimated => true;

    private int ActiveTransport(Part p) => p switch
    {
        Part.Prev or Part.ShadeT0 => 0, Part.Play or Part.ShadeT1 => 1, Part.Pause or Part.ShadeT2 => 2,
        Part.Stop or Part.ShadeT3 => 3, Part.Next or Part.ShadeT4 => 4, Part.Eject or Part.ShadeT5 => 5, _ => -1,
    };

    private bool Down(Part p) => _pressed == p && _pressedInside;

    // ================================================================== rendering

    protected override void Render(PixelImage g)
    {
        var titleSheet = Skin[Sheets.TitleBar];
        if (Shaded)
        {
            RenderShaded(g, titleSheet);
            return;
        }

        g.Blit(Skin[Sheets.Main], M.Background, 0, 0);
        g.Blit(titleSheet, IsTitleActive ? M.TitleActive : M.TitleInactive, 0, 0);
        g.Blit(titleSheet, Down(Part.Options) ? M.OptionsBtnDown : M.OptionsBtn, M.OptionsX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Minimize) ? M.MinimizeBtnDown : M.MinimizeBtn, M.MinimizeX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Shade) ? M.ShadeBtnDown : M.ShadeBtn, M.ShadeX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Close) ? M.CloseBtnDown : M.CloseBtn, M.CloseX, M.ButtonsY);

        // Clutter bar: A (always on top) and D (double size) show as pressed while active.
        g.Blit(titleSheet, M.ClutterBg, M.ClutterX, M.ClutterY);
        for (int i = 0; i < 5; i++)
        {
            bool on = Down(Part.Clutter0 + i) || (i == 1 && C.Settings.AlwaysOnTop) || (i == 3 && C.Settings.DoubleSize);
            if (on) g.Blit(titleSheet, M.ClutterPressed[i].Sprite, M.ClutterX, M.ClutterY + M.ClutterPressed[i].Top);
        }

        var state = C.State;
        var playpaus = Skin[Sheets.PlayPaus];
        g.Blit(playpaus, state switch { PlayerState.Playing => M.Playing, PlayerState.Paused => M.Paused, _ => M.Stopped }, M.StatusX, M.StatusY);
        g.Blit(playpaus, state == PlayerState.Playing ? M.Working : M.NotWorking, M.WorkX, M.StatusY);

        RenderTime(g);
        _vis.Render(g, C, M.VisX, M.VisY, M.VisWidth, M.VisHeight, C.Settings.VisMode);
        RenderMarquee(g);

        // kbps / kHz
        var engine = C.Engine;
        if (state != PlayerState.Stopped)
        {
            if (engine.Bitrate > 0) SkinDraw.Text(g, Skin, Math.Min(engine.Bitrate, 999).ToString().PadLeft(3), M.KbpsX, M.InfoY);
            if (engine.SampleRate > 0) SkinDraw.Text(g, Skin, Math.Min(engine.SampleRate / 1000, 99).ToString().PadLeft(2), M.KhzX, M.InfoY);
        }
        var ms = Skin[Sheets.MonoSter];
        bool stereo = state != PlayerState.Stopped && engine.Channels >= 2, mono = state != PlayerState.Stopped && engine.Channels == 1;
        g.Blit(ms, mono ? M.MonoOn : M.MonoOff, M.MonoX, M.MonoStereoY);
        g.Blit(ms, stereo ? M.StereoOn : M.StereoOff, M.StereoX, M.MonoStereoY);

        // Volume and balance.
        int vol = C.Volume;
        g.Blit(Skin[Sheets.Volume], M.VolumeBg((int)Math.Round(vol / 100.0 * 27)), M.VolumeX, M.SliderY);
        g.Blit(Skin[Sheets.Volume], _pressed == Part.Volume ? M.SliderThumbDown : M.SliderThumb, M.VolumeX + (int)Math.Round(vol / 100.0 * 51), M.SliderY + 1);
        int bal = C.Balance;
        g.Blit(Skin[Sheets.Balance], M.BalanceBg((int)Math.Round(Math.Abs(bal) / 100.0 * 27)), M.BalanceX, M.SliderY);
        g.Blit(Skin[Sheets.Balance], _pressed == Part.Balance ? M.SliderThumbDown : M.SliderThumb, M.BalanceX + (int)Math.Round((bal + 100) / 200.0 * 24), M.SliderY + 1);

        // EQ / PL toggles.
        var shufrep = Skin[Sheets.ShufRep];
        bool eqOn = AppShell.Instance.Equalizer.IsVisible, plOn = AppShell.Instance.PlaylistWindow.IsVisible;
        g.Blit(shufrep, eqOn ? (Down(Part.EqBtn) ? M.EqOnDown : M.EqOn) : (Down(Part.EqBtn) ? M.EqOffDown : M.EqOff), M.EqBtnX, M.EqPlY);
        g.Blit(shufrep, plOn ? (Down(Part.PlBtn) ? M.PlOnDown : M.PlOn) : (Down(Part.PlBtn) ? M.PlOffDown : M.PlOff), M.PlBtnX, M.EqPlY);

        // Seek bar.
        var pos = Skin[Sheets.PosBar];
        g.Blit(pos, M.PosBg, M.PosX, M.PosY);
        if (state != PlayerState.Stopped && engine.Duration is { TotalMilliseconds: > 0 } dur)
        {
            double f = _pressed == Part.Position ? _seekFraction : engine.Position / dur;
            int tx = M.PosX + (int)Math.Round(Math.Clamp(f, 0, 1) * (M.PosWidth - M.PosThumbWidth));
            g.Blit(pos, _pressed == Part.Position ? M.PosThumbDown : M.PosThumb, tx, M.PosY);
        }

        // Transport and shuffle/repeat.
        var cb = Skin[Sheets.CButtons];
        for (int i = 0; i < 6; i++)
        {
            bool down = ActiveTransport(_pressed) == i && _pressedInside;
            g.Blit(cb, down ? M.TransportDown[i] : M.Transport[i], M.TransportPos[i].X, M.TransportPos[i].Y);
        }
        bool sh = C.Playlist.Shuffle, rp = C.Playlist.Repeat;
        g.Blit(shufrep, sh ? (Down(Part.Shuffle) ? M.ShuffleOnDown : M.ShuffleOn) : (Down(Part.Shuffle) ? M.ShuffleOffDown : M.ShuffleOff), M.ShuffleX, M.ShufRepY);
        g.Blit(shufrep, rp ? (Down(Part.Repeat) ? M.RepeatOnDown : M.RepeatOn) : (Down(Part.Repeat) ? M.RepeatOffDown : M.RepeatOff), M.RepeatX, M.ShufRepY);
    }

    /// <summary>Elapsed or remaining time (nothing when stopped, blinking while paused).</summary>
    private (bool Show, bool Negative, TimeSpan Time) DisplayTime()
    {
        var engine = C.Engine;
        if (C.State == PlayerState.Stopped) return (false, false, TimeSpan.Zero);
        if (C.State == PlayerState.Paused && DateTime.Now.Millisecond >= 500) return (false, false, TimeSpan.Zero);
        var pos = engine.Position;
        if (C.Settings.TimeMode == TimeMode.Remaining && engine.Duration is { } d) return (true, true, d - pos);
        return (true, false, pos);
    }

    private void RenderTime(PixelImage g)
    {
        var (show, negative, t) = DisplayTime();
        if (!show) return;
        var digits = Skin.Digits;
        int minutes = Math.Min(99, (int)t.TotalMinutes), seconds = t.Seconds;
        if (negative)
        {
            if (Skin.HasNumsEx) g.Blit(digits, M.MinusEx, M.MinusX, M.TimeY);
            else g.Blit(digits, M.MinusClassic, M.MinusX + 3, M.TimeY + 6);
        }
        int[] d = [minutes / 10, minutes % 10, seconds / 10, seconds % 10];
        for (int i = 0; i < 4; i++) g.Blit(digits, M.Digit(d[i]), M.DigitX[i], M.TimeY);
    }

    private void RenderMarquee(PixelImage g)
    {
        string? message = C.Message;
        if (message is not null)
        {
            SkinDraw.TextBackground(g, Skin, M.MarqueeX, M.MarqueeY, M.MarqueeWidth, SpriteMap.CharH);
            SkinDraw.Text(g, Skin, message, M.MarqueeX, M.MarqueeY, M.MarqueeWidth);
            return;
        }
        if (C.Settings.ScrollTitle && _pressed != Part.Marquee)
        {
            int interval = 220 / Math.Clamp(C.Settings.ScrollSpeed, 1, 4);
            if ((DateTime.UtcNow - _lastScroll).TotalMilliseconds >= interval)
            {
                _marqueeOffset += SpriteMap.CharW;
                _lastScroll = DateTime.UtcNow;
            }
        }
        SkinDraw.Marquee(g, Skin, C.MarqueeText, M.MarqueeX, M.MarqueeY, M.MarqueeWidth, C.Settings.ScrollTitle || _pressed == Part.Marquee ? _marqueeOffset : 0);
    }

    private void RenderShaded(PixelImage g, PixelImage titleSheet)
    {
        g.Blit(titleSheet, IsTitleActive ? M.ShadeBgActive : M.ShadeBgInactive, 0, 0);
        g.Blit(titleSheet, Down(Part.Options) ? M.OptionsBtnDown : M.OptionsBtn, M.OptionsX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Minimize) ? M.MinimizeBtnDown : M.MinimizeBtn, M.MinimizeX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Shade) ? M.UnshadeBtnDown : M.UnshadeBtn, M.ShadeX, M.ButtonsY);
        g.Blit(titleSheet, Down(Part.Close) ? M.CloseBtnDown : M.CloseBtn, M.CloseX, M.ButtonsY);

        var (show, negative, t) = DisplayTime();
        if (show)
        {
            string s = $"{(negative ? "-" : " ")}{Math.Min(99, (int)t.TotalMinutes),2}:{t.Seconds:00}";
            SkinDraw.Text(g, Skin, s, M.ShadeTimeX - 2, M.ShadeTimeY);
        }

        _vis.RenderMini(g, C, M.ShadeVisX, M.ShadeVisY, 38, 5);

        g.Blit(titleSheet, M.ShadePosBg, M.ShadePosX, M.ShadePosY);
        if (C.State != PlayerState.Stopped && C.Engine.Duration is { TotalMilliseconds: > 0 } dur)
        {
            double f = _pressed == Part.ShadePos ? _seekFraction : C.Engine.Position / dur;
            int x = M.ShadePosX + (int)Math.Round(Math.Clamp(f, 0, 1) * (M.ShadePosBg.W - 3));
            var thumb = x <= M.ShadePosX ? M.ShadePosThumbLeft : x >= M.ShadePosX + M.ShadePosBg.W - 3 ? M.ShadePosThumbRight : M.ShadePosThumb;
            g.Blit(titleSheet, thumb, x, M.ShadePosY);
        }
    }

    // ================================================================== input

    private Part HitTest(int x, int y)
    {
        if (Hit(x, y, M.OptionsX, M.ButtonsY, 9, 9)) return Part.Options;
        if (Hit(x, y, M.MinimizeX, M.ButtonsY, 9, 9)) return Part.Minimize;
        if (Hit(x, y, M.ShadeX, M.ButtonsY, 9, 9)) return Part.Shade;
        if (Hit(x, y, M.CloseX, M.ButtonsY, 9, 9)) return Part.Close;
        if (Shaded)
        {
            for (int i = 0; i < 6; i++)
                if (Hit(x, y, M.ShadeTransport[i].X, 2, M.ShadeTransport[i].W, 10)) return Part.ShadeT0 + i;
            if (Hit(x, y, M.ShadePosX, M.ShadePosY, M.ShadePosBg.W, M.ShadePosBg.H)) return Part.ShadePos;
            return Part.None;
        }
        if (Hit(x, y, M.ClutterX, M.ClutterY, 8, 43))
        {
            for (int i = 0; i < 5; i++)
            {
                var (s, top) = M.ClutterPressed[i];
                if (y >= M.ClutterY + top && y < M.ClutterY + top + s.H) return Part.Clutter0 + i;
            }
            return Part.None;
        }
        for (int i = 0; i < 6; i++)
        {
            var (tx, ty) = M.TransportPos[i];
            if (Hit(x, y, tx, ty, M.Transport[i].W, M.Transport[i].H)) return Part.Prev + i;
        }
        if (Hit(x, y, M.ShuffleX, M.ShufRepY, 47, 15)) return Part.Shuffle;
        if (Hit(x, y, M.RepeatX, M.ShufRepY, 28, 15)) return Part.Repeat;
        if (Hit(x, y, M.EqBtnX, M.EqPlY, 23, 12)) return Part.EqBtn;
        if (Hit(x, y, M.PlBtnX, M.EqPlY, 23, 12)) return Part.PlBtn;
        if (Hit(x, y, M.VolumeX, M.SliderY, M.VolumeWidth, 13)) return Part.Volume;
        if (Hit(x, y, M.BalanceX, M.SliderY, M.BalanceWidth, 13)) return Part.Balance;
        if (Hit(x, y, M.PosX, M.PosY, M.PosWidth, 10)) return Part.Position;
        if (Hit(x, y, M.MarqueeX - 2, M.MarqueeY - 3, M.MarqueeWidth + 2, 12)) return Part.Marquee;
        if (Hit(x, y, M.AboutArea.X, M.AboutArea.Y, M.AboutArea.W, M.AboutArea.H)) return Part.About;
        return Part.None;
    }

    protected override void OnSkinDown(int x, int y)
    {
        var part = HitTest(x, y);
        if (!Shaded && part == Part.None)
        {
            if (Hit(x, y, 36, 26, 64, 13)) { ToggleTimeMode(); return; }
            if (Hit(x, y, M.VisX, M.VisY, M.VisWidth, M.VisHeight)) { CycleVis(); return; }
        }
        if (Shaded && part == Part.None && Hit(x, y, M.ShadeTimeX - 2, 2, 30, 10)) { ToggleTimeMode(); return; }

        if (part == Part.None)
        {
            BeginWindowDrag();
            return;
        }
        _pressed = part;
        _pressedInside = true;
        switch (part)
        {
            case Part.Volume or Part.Balance or Part.Position or Part.ShadePos:
                OnSkinMove(x, y, true);
                break;
            case Part.Marquee:
                _marqueeDragStart = x;
                _marqueeDragOffset = _marqueeOffset;
                break;
        }
    }

    protected override void OnSkinMove(int x, int y, bool leftDown)
    {
        if (!leftDown || _pressed == Part.None) return;
        switch (_pressed)
        {
            case Part.Volume:
                C.Volume = (int)Math.Round(Math.Clamp((x - M.VolumeX - 7) / 51.0, 0, 1) * 100);
                break;
            case Part.Balance:
                C.Balance = (int)Math.Round(Math.Clamp((x - M.BalanceX - 7) / 24.0, 0, 1) * 200 - 100);
                break;
            case Part.Position:
                _seekFraction = Math.Clamp((x - M.PosX - M.PosThumbWidth / 2.0) / (M.PosWidth - M.PosThumbWidth), 0, 1);
                ShowSeekMessage();
                break;
            case Part.ShadePos:
                _seekFraction = Math.Clamp((x - M.ShadePosX - 1) / (double)(M.ShadePosBg.W - 3), 0, 1);
                ShowSeekMessage();
                break;
            case Part.Marquee:
                _marqueeOffset = _marqueeDragOffset - (x - _marqueeDragStart);
                break;
            default:
                _pressedInside = HitTest(x, y) == _pressed;
                break;
        }
        Invalidate();
    }

    private void ShowSeekMessage()
    {
        if (C.Engine.Duration is not { } d) return;
        var t = d * _seekFraction;
        C.ShowMessage($"SEEK TO: {SkinDraw.FormatTime(t)}/{SkinDraw.FormatTime(d)} ({(int)(_seekFraction * 100)}%)");
    }

    protected override async void OnSkinUp(int x, int y)
    {
        var part = _pressed;
        bool inside = _pressedInside && HitTest(x, y) == part;
        _pressed = Part.None;
        _pressedInside = false;
        var shell = AppShell.Instance;

        if (part is Part.Position or Part.ShadePos)
        {
            if (C.Engine.Duration is { } d && C.State != PlayerState.Stopped) C.Seek(d * _seekFraction);
            C.ClearMessage();
            return;
        }
        if (part is Part.Volume or Part.Balance or Part.Marquee || !inside) return;

        switch (part)
        {
            case Part.Options: Menus.ShowMainMenu(this); break;
            case Part.Minimize: shell.Minimize(); break;
            case Part.Shade: Shaded = !Shaded; break;
            case Part.Close: shell.Exit(); break;
            case Part.Clutter0: Menus.ShowOptionsMenu(this); break;
            case Part.Clutter1: shell.ToggleAlwaysOnTop(); break;
            case Part.Clutter2: shell.ShowFileInfo(); break;
            case Part.Clutter3: shell.ToggleDoubleSize(); break;
            case Part.Clutter4: Menus.ShowVisMenu(this); break;
            case Part.Prev or Part.ShadeT0: await C.PreviousAsync(); break;
            case Part.Play or Part.ShadeT1: await C.PlayAsync(); break;
            case Part.Pause or Part.ShadeT2: C.PauseToggle(); break;
            case Part.Stop or Part.ShadeT3: C.Stop(); break;
            case Part.Next or Part.ShadeT4: await C.NextAsync(); break;
            case Part.Eject or Part.ShadeT5: await shell.OpenFilesDialog(replace: true); break;
            case Part.Shuffle: C.ToggleShuffle(); break;
            case Part.Repeat: C.ToggleRepeat(); break;
            case Part.EqBtn: shell.ToggleEqualizer(); break;
            case Part.PlBtn: shell.TogglePlaylist(); break;
            case Part.About: shell.ShowAbout(); break;
        }
    }

    protected override bool OnSkinDoubleClick(int x, int y)
    {
        if (y < 14 && HitTest(x, y) == Part.None)
        {
            Shaded = !Shaded;
            return true;
        }
        return false;
    }

    protected override void OnSkinRightClick(int x, int y)
    {
        if (!Shaded && Hit(x, y, M.VisX, M.VisY, M.VisWidth, M.VisHeight)) Menus.ShowVisMenu(this);
        else base.OnSkinRightClick(x, y);
    }

    private void ToggleTimeMode()
    {
        C.Settings.TimeMode = C.Settings.TimeMode == TimeMode.Elapsed ? TimeMode.Remaining : TimeMode.Elapsed;
        Invalidate();
    }

    private void CycleVis()
    {
        C.Settings.VisMode = (VisMode)(((int)C.Settings.VisMode + 1) % 3);
        Invalidate();
    }
}

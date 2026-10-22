using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Skins;
using P = WinRetroAmp.Core.Skins.SpriteMap.Pl;
using Sheets = WinRetroAmp.Core.Skins.SpriteMap.Sheets;

namespace WinRetroAmp.Ui;

/// <summary>The resizable playlist editor with its pop-up button menus.</summary>
public sealed class PlaylistWindow : SkinnedWindow
{
    private enum Part { None, Close, Shade, List, Scroll, Resize, MenuButton, Mini0, Mini1, Mini2, Mini3, Mini4, Mini5, ScrollUp, ScrollDown }

    private readonly PlaylistTextLayer _text;
    private Part _pressed;
    private int _scroll;
    private int _anchor = -1;
    private int _dragRow = -1;
    private bool _dragMoved;
    private int _scrollGrabOffset;
    private (int X, int Y) _resizeStart;
    private (int W, int H) _resizeStartSize;
    private int _openMenu = -1, _menuHover = -1;
    private bool _menuSticky, _menuLeftButton;

    public PlaylistWindow(PlayerController c, WindowDocking d) : base(c, d)
    {
        Title = "Playlist Editor";
        ShowInTaskbar = false;
        AllowDrop = true;
        _text = new PlaylistTextLayer(this);
        Overlay.Children.Add(_text);
        Drop += async (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            var (x, y) = ToSkin(e.GetPosition(Root));
            int row = RowAt(y);
            await C.OpenAsync(files, replace: false, insertAt: row < 0 ? -1 : Math.Min(row, C.Playlist.Count));
        };
    }

    public bool Shaded
    {
        get => C.Settings.Playlist.Shaded;
        set
        {
            if (Shaded == value) return;
            Docking.Resize(this, () =>
            {
                C.Settings.Playlist.Shaded = value;
                UpdateSize();
            });
        }
    }

    private int W => C.Settings.PlaylistWidth;
    private int H => C.Settings.PlaylistHeight;

    protected override (int W, int H) LogicalSize => (W, Shaded ? SpriteMap.ShadeHeight : H);

    public override bool IsAnimated => C.State != PlayerState.Stopped;

    internal int VisibleRows => Math.Max(1, (H - P.ListTop - P.BottomBorder) / P.RowHeight);
    internal int ScrollTop => _scroll;
    private int MaxScroll => Math.Max(0, C.Playlist.Count - VisibleRows);
    private Rect ListRect => new(P.ListLeft, P.ListTop, W - P.ListLeft - P.RightBorder, H - P.ListTop - P.BottomBorder);

    public void ScrollTo(int row)
    {
        if (row < _scroll) _scroll = row;
        else if (row >= _scroll + VisibleRows) _scroll = row - VisibleRows + 1;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        Invalidate();
    }

    private int RowAt(int y)
    {
        if (y < P.ListTop || y >= P.ListTop + VisibleRows * P.RowHeight) return -1;
        int row = _scroll + (y - P.ListTop) / P.RowHeight;
        return row < C.Playlist.Count ? row : C.Playlist.Count;
    }

    private (int X, int Y) MenuButtonPos(int menu) => (menu < 4 ? P.MenuButtonX[menu] : W - P.ListButtonFromRight, H - P.MenuButtonFromBottom);

    // ================================================================== rendering

    protected override void Render(PixelImage g)
    {
        var sheet = Skin[Sheets.PlEdit];
        bool active = IsTitleActive;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);

        if (Shaded)
        {
            RenderShaded(g, sheet, active);
            return;
        }

        // Top bar: corners, tiles and the centred title.
        g.Blit(sheet, active ? P.TopLeft : P.TopLeftInactive, 0, 0);
        g.Tile(sheet, active ? P.TopTile : P.TopTileInactive, 25, 0, W - 50, 20);
        g.Blit(sheet, active ? P.Title : P.TitleInactive, (W - 100) / 2, 0);
        g.Blit(sheet, active ? P.TopRight : P.TopRightInactive, W - 25, 0);
        if (_pressed == Part.Close) g.Blit(sheet, P.CloseDown, W - 11, 3);
        if (_pressed == Part.Shade) g.Blit(sheet, P.ShadeDown, W - 20, 3);

        int sideH = H - P.ListTop - P.BottomBorder;
        g.Tile(sheet, P.LeftTile, 0, P.ListTop, 12, sideH);
        g.Tile(sheet, P.RightTile, W - 20, P.ListTop, 20, sideH);

        g.Blit(sheet, P.BottomLeft, 0, H - 38);
        g.Tile(sheet, P.BottomTile, 125, H - 38, W - 275, 38);
        g.Blit(sheet, P.BottomRight, W - 150, H - 38);

        // List background and selection bars (the text itself is drawn by the WPF overlay).
        var style = Skin.Playlist;
        var list = ListRect;
        g.Fill((int)list.X, (int)list.Y, (int)list.Width, (int)list.Height, style.NormalBg);
        for (int r = 0; r < VisibleRows; r++)
        {
            int i = _scroll + r;
            if (i >= C.Playlist.Count) break;
            if (C.Playlist[i].Selected) g.Fill((int)list.X, P.ListTop + r * P.RowHeight, (int)list.Width, P.RowHeight, style.SelectedBg);
        }

        // Scrollbar thumb.
        int trackH = sideH - P.ScrollThumb.H;
        int thumbY = P.ListTop + (MaxScroll == 0 ? 0 : (int)Math.Round(_scroll / (double)MaxScroll * trackH));
        g.Blit(sheet, _pressed == Part.Scroll ? P.ScrollThumbDown : P.ScrollThumb, W - 15, thumbY);

        // Running time "selected/total" and the mini time.
        int cx = W - 150, cy = H - 38;
        string Total(TimeSpan t) => SkinDraw.FormatTime(t, hours: true);
        string running = $"{Total(C.Playlist.SelectedDuration)}/{Total(C.Playlist.TotalDuration)}{(C.Playlist.HasUnknownDurations ? "+" : "")}";
        SkinDraw.TextBackground(g, Skin, cx + 7, cy + 10, 90, 6);
        SkinDraw.Text(g, Skin, running, cx + 7, cy + 10, 90);
        SkinDraw.TextBackground(g, Skin, cx + 66, cy + 23, 30, 6);
        if (C.State != PlayerState.Stopped)
        {
            var pos = C.Engine.Position;
            bool remaining = C.Settings.TimeMode == Core.Settings.TimeMode.Remaining && C.Engine.Duration is not null;
            string t = remaining ? "-" + SkinDraw.FormatTime(C.Engine.Duration!.Value - pos) : SkinDraw.FormatTime(pos);
            SkinDraw.Text(g, Skin, t.PadLeft(6), cx + 66, cy + 23, 30);
        }

        if (_openMenu >= 0) RenderMenu(g, sheet);
    }

    private void RenderMenu(PixelImage g, PixelImage sheet)
    {
        var (bar, items) = P.Menus[_openMenu];
        var (bx, by) = MenuButtonPos(_openMenu);
        int top = by - (items.Length - 1) * 18;
        g.Blit(sheet, bar.X, bar.Y, bar.W, items.Length * 18, bx - 3, top);
        for (int i = 0; i < items.Length; i++)
            g.Blit(sheet, i == _menuHover ? items[i].Pressed : items[i].Normal, bx, by - i * 18);
    }

    private void RenderShaded(PixelImage g, PixelImage sheet, bool active)
    {
        g.Blit(sheet, P.ShadeLeft, 0, 0);
        g.Tile(sheet, P.ShadeTile, 25, 0, W - 75, 14);
        g.Blit(sheet, active ? P.ShadeRightActive : P.ShadeRight, W - 50, 0);
        if (_pressed == Part.Shade) g.Blit(sheet, P.UnshadeDown, W - 19, 3);
        if (_pressed == Part.Close) g.Blit(sheet, P.CloseDown, W - 10, 3);
        string time = C.Playlist.Current?.Duration is { } d ? SkinDraw.FormatTime(d) : "";
        int maxChars = (W - 60) / SpriteMap.CharW;
        string title = C.Playlist.Current is { } cur ? $"{C.Playlist.CurrentIndex + 1}. {cur.DisplayTitle}" : "";
        int room = Math.Max(0, maxChars - time.Length - 1);
        if (title.Length > room) title = title[..Math.Max(0, room - 1)] + "…";
        SkinDraw.Text(g, Skin, title.PadRight(room) + " " + time, 5, 4, W - 60);
    }

    protected override void AfterRender()
    {
        if (Shaded)
        {
            _text.Visibility = Visibility.Collapsed;
            return;
        }
        double s = Scale;
        var r = ListRect;
        _text.Visibility = Visibility.Visible;
        System.Windows.Controls.Canvas.SetLeft(_text, r.X * s);
        System.Windows.Controls.Canvas.SetTop(_text, r.Y * s);
        _text.Width = r.Width * s;
        _text.Height = r.Height * s;
        _text.InvalidateVisual();
    }

    // ================================================================== input

    private Part HitTest(int x, int y, out int menu)
    {
        menu = -1;
        if (Shaded)
        {
            if (Hit(x, y, W - 19, 3, 9, 9)) return Part.Shade;
            if (Hit(x, y, W - 10, 3, 9, 9)) return Part.Close;
            return Part.None;
        }
        if (Hit(x, y, W - 11, 3, 9, 9)) return Part.Close;
        if (Hit(x, y, W - 20, 3, 9, 9)) return Part.Shade;
        if (ListRect.Contains(x, y)) return Part.List;
        if (Hit(x, y, W - 15, P.ListTop, 8, H - P.ListTop - P.BottomBorder)) return Part.Scroll;
        if (Hit(x, y, W - 20, H - 20, 20, 20)) return Part.Resize;
        for (int m = 0; m < 5; m++)
        {
            var (bx, by) = MenuButtonPos(m);
            if (Hit(x, y, bx, by, 22, 18))
            {
                menu = m;
                return Part.MenuButton;
            }
        }
        int cx = W - 150, cy = H - 38;
        int[] mini = [6, 15, 24, 32, 40, 49];
        for (int i = 0; i < 6; i++)
            if (Hit(x, y, cx + mini[i], cy + 21, 8, 9)) return Part.Mini0 + i;
        if (Hit(x, y, cx + 99, cy + 21, 7, 4)) return Part.ScrollUp;
        if (Hit(x, y, cx + 99, cy + 26, 7, 4)) return Part.ScrollDown;
        return Part.None;
    }

    private int MenuItemAt(int x, int y)
    {
        if (_openMenu < 0) return -1;
        var (bx, by) = MenuButtonPos(_openMenu);
        int n = P.Menus[_openMenu].Items.Length;
        for (int i = 0; i < n; i++)
            if (Hit(x, y, bx, by - i * 18, 22, 18)) return i;
        return -1;
    }

    protected override void OnSkinDown(int x, int y)
    {
        if (_openMenu >= 0 && _menuSticky)
        {
            int item = MenuItemAt(x, y);
            int menu = _openMenu;
            CloseMenu();
            if (item >= 0) RunMenuItem(menu, item);
            return;
        }

        var part = HitTest(x, y, out int menuIndex);
        _pressed = part;
        switch (part)
        {
            case Part.None:
                BeginWindowDrag();
                break;
            case Part.MenuButton:
                _openMenu = menuIndex;
                _menuHover = 0;
                _menuSticky = false;
                _menuLeftButton = false;
                break;
            case Part.List:
                ListDown(y);
                break;
            case Part.Scroll:
                int trackH = H - P.ListTop - P.BottomBorder - P.ScrollThumb.H;
                int thumbY = P.ListTop + (MaxScroll == 0 ? 0 : (int)Math.Round(_scroll / (double)MaxScroll * trackH));
                if (y < thumbY) { _scroll -= VisibleRows; _pressed = Part.None; }
                else if (y >= thumbY + P.ScrollThumb.H) { _scroll += VisibleRows; _pressed = Part.None; }
                else _scrollGrabOffset = y - thumbY;
                break;
            case Part.Resize:
                _resizeStart = (x, y);
                _resizeStartSize = (W, H);
                break;
        }
        Invalidate();
    }

    private void ListDown(int y)
    {
        int row = RowAt(y);
        var pl = C.Playlist;
        var mods = System.Windows.Input.Keyboard.Modifiers;
        _dragRow = -1;
        _dragMoved = false;
        if (row < 0 || row >= pl.Count)
        {
            if (!mods.HasFlag(System.Windows.Input.ModifierKeys.Control)) pl.SelectAll(false);
            return;
        }
        if (mods.HasFlag(System.Windows.Input.ModifierKeys.Shift) && _anchor >= 0)
            pl.SelectRange(_anchor, row, mods.HasFlag(System.Windows.Input.ModifierKeys.Control));
        else if (mods.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            pl.Toggle(row);
            _anchor = row;
        }
        else
        {
            if (!pl[row].Selected) pl.SelectOnly(row);
            _anchor = row;
            _dragRow = row;
        }
    }

    protected override void OnSkinMove(int x, int y, bool leftDown)
    {
        if (_openMenu >= 0)
        {
            int item = MenuItemAt(x, y);
            if (item != 0) _menuLeftButton = true;
            if (item != _menuHover)
            {
                _menuHover = item;
                Invalidate();
            }
            return;
        }
        if (!leftDown) return;
        switch (_pressed)
        {
            case Part.List when _dragRow >= 0:
                int row = Math.Clamp(_scroll + (y - P.ListTop) / P.RowHeight - (y < P.ListTop ? 1 : 0), 0, C.Playlist.Count - 1);
                if (row != _dragRow)
                {
                    int before = C.Playlist.Items.ToList().FindIndex(i => i.Selected);
                    C.Playlist.MoveSelected(row - _dragRow);
                    int after = C.Playlist.Items.ToList().FindIndex(i => i.Selected);
                    _dragRow += after - before;
                    _dragMoved = true;
                    ScrollTo(Math.Clamp(row, 0, C.Playlist.Count - 1));
                }
                break;
            case Part.Scroll:
                int trackH = H - P.ListTop - P.BottomBorder - P.ScrollThumb.H;
                if (trackH > 0) _scroll = (int)Math.Round((y - _scrollGrabOffset - P.ListTop) / (double)trackH * MaxScroll);
                break;
            case Part.Resize:
                int w = Math.Max(P.MinWidth, _resizeStartSize.W + (int)Math.Round((x - _resizeStart.X) / (double)P.StepW) * P.StepW);
                int h = Math.Max(P.MinHeight, _resizeStartSize.H + (int)Math.Round((y - _resizeStart.Y) / (double)P.StepH) * P.StepH);
                if (w != W || h != H)
                {
                    Docking.Resize(this, () =>
                    {
                        C.Settings.PlaylistWidth = w;
                        C.Settings.PlaylistHeight = h;
                        UpdateSize();
                    });
                    // Keep the grab point relative to the new size.
                    _resizeStart = (_resizeStart.X + (w - _resizeStartSize.W), _resizeStart.Y + (h - _resizeStartSize.H));
                    _resizeStartSize = (w, h);
                }
                break;
        }
        Invalidate();
    }

    protected override async void OnSkinUp(int x, int y)
    {
        var part = _pressed;
        _pressed = Part.None;
        if (_openMenu >= 0)
        {
            int item = MenuItemAt(x, y);
            int menu = _openMenu;
            if (!_menuLeftButton && item == 0)
            {
                _menuSticky = true; // a plain click leaves the menu open until the next click
            }
            else
            {
                CloseMenu();
                if (item >= 0) RunMenuItem(menu, item);
            }
            Invalidate();
            return;
        }

        if (part == Part.List && _dragRow >= 0 && !_dragMoved)
        {
            int row = RowAt(y);
            if (row >= 0 && row < C.Playlist.Count) C.Playlist.SelectOnly(row);
        }
        _dragRow = -1;

        if (HitTest(x, y, out _) != part) return;
        switch (part)
        {
            case Part.Close: AppShell.Instance.TogglePlaylist(); break;
            case Part.Shade: Shaded = !Shaded; break;
            case Part.Mini0: await C.PreviousAsync(); break;
            case Part.Mini1: await C.PlayAsync(); break;
            case Part.Mini2: C.PauseToggle(); break;
            case Part.Mini3: C.Stop(); break;
            case Part.Mini4: await C.NextAsync(); break;
            case Part.Mini5: await AppShell.Instance.OpenFilesDialog(replace: true); break;
            case Part.ScrollUp: _scroll -= 1; break;
            case Part.ScrollDown: _scroll += 1; break;
        }
        Invalidate();
    }

    private void CloseMenu()
    {
        _openMenu = -1;
        _menuHover = -1;
        _menuSticky = false;
        Invalidate();
    }

    private async void RunMenuItem(int menu, int item)
    {
        var shell = AppShell.Instance;
        var pl = C.Playlist;
        switch (menu, item)
        {
            case (0, 0): await shell.OpenFilesDialog(replace: false); break;
            case (0, 1): await shell.OpenFolderDialog(replace: false); break;
            case (0, 2): await shell.OpenUrlDialog(replace: false); break;
            case (1, 0): Menus.ShowRemoveMiscMenu(this); break;
            case (1, 1): pl.RemoveSelected(); break;
            case (1, 2): pl.Crop(); break;
            case (1, 3): shell.StopIfRemoved(pl.Clear); break;
            case (2, 0): pl.SelectAll(); break;
            case (2, 1): pl.SelectAll(false); break;
            case (2, 2): pl.InvertSelection(); break;
            case (3, 0): Menus.ShowPlaylistOptionsMenu(this); break;
            case (3, 1): shell.ShowFileInfo(pl.SelectedItems.FirstOrDefault() ?? pl.Current); break;
            case (3, 2): Menus.ShowSortMenu(this); break;
            case (4, 0): await shell.LoadPlaylistDialog(); break;
            case (4, 1): shell.SavePlaylistDialog(); break;
            case (4, 2): shell.StopIfRemoved(pl.Clear); break;
        }
        Invalidate();
    }

    protected override bool OnSkinDoubleClick(int x, int y)
    {
        var part = HitTest(x, y, out _);
        if (part == Part.List)
        {
            int row = RowAt(y);
            if (row >= 0 && row < C.Playlist.Count) _ = C.PlayIndexAsync(row);
            return true;
        }
        if (y < (Shaded ? 14 : 20) && part == Part.None)
        {
            Shaded = !Shaded;
            return true;
        }
        return false;
    }

    protected override void OnSkinWheel(int x, int y, int delta)
    {
        _scroll -= Math.Sign(delta) * 3;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        Invalidate();
    }

    protected override void OnSkinRightClick(int x, int y)
    {
        if (!Shaded && ListRect.Contains(x, y))
        {
            int row = RowAt(y);
            if (row >= 0 && row < C.Playlist.Count && !C.Playlist[row].Selected) C.Playlist.SelectOnly(row);
            Menus.ShowPlaylistItemMenu(this);
        }
        else
        {
            base.OnSkinRightClick(x, y);
        }
    }

    /// <summary>Keyboard handling specific to the playlist (selection, delete, enter...).</summary>
    public bool HandleKey(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys mods)
    {
        var pl = C.Playlist;
        int cursor = _anchor >= 0 ? _anchor : pl.CurrentIndex;
        switch (key)
        {
            case System.Windows.Input.Key.Delete:
                pl.RemoveSelected();
                return true;
            case System.Windows.Input.Key.Enter:
                int sel = pl.Items.ToList().FindIndex(i => i.Selected);
                if (sel >= 0) _ = C.PlayIndexAsync(sel);
                return true;
            case System.Windows.Input.Key.Up or System.Windows.Input.Key.Down or System.Windows.Input.Key.PageUp or System.Windows.Input.Key.PageDown
                or System.Windows.Input.Key.Home or System.Windows.Input.Key.End when pl.Count > 0:
                if (mods.HasFlag(System.Windows.Input.ModifierKeys.Alt) && key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)
                {
                    pl.MoveSelected(key == System.Windows.Input.Key.Up ? -1 : 1);
                    return true;
                }
                int next = key switch
                {
                    System.Windows.Input.Key.Up => cursor - 1,
                    System.Windows.Input.Key.Down => cursor + 1,
                    System.Windows.Input.Key.PageUp => cursor - VisibleRows,
                    System.Windows.Input.Key.PageDown => cursor + VisibleRows,
                    System.Windows.Input.Key.Home => 0,
                    _ => pl.Count - 1,
                };
                next = Math.Clamp(next, 0, pl.Count - 1);
                pl.SelectOnly(next);
                _anchor = next;
                ScrollTo(next);
                return true;
        }
        return false;
    }

    /// <summary>Draws the playlist rows with the skin's font and colours, on top of the bitmap.</summary>
    private sealed class PlaylistTextLayer(PlaylistWindow owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var c = owner.C;
            var style = c.Skin.Playlist;
            double s = owner.Scale;
            var typeface = new Typeface(new FontFamily(style.Font + ", Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double width = ActualWidth / s;
            var normal = Freeze(new SolidColorBrush(ToColor(style.Normal)));
            var current = Freeze(new SolidColorBrush(ToColor(style.Current)));
            dc.PushTransform(new ScaleTransform(s, s));
            for (int r = 0; r < owner.VisibleRows; r++)
            {
                int i = owner.ScrollTop + r;
                if (i >= c.Playlist.Count) break;
                var item = c.Playlist[i];
                var brush = i == c.Playlist.CurrentIndex ? current : normal;
                double y = r * P.RowHeight;
                string dur = item.Duration is { } d ? SkinDraw.FormatTime(d, hours: true) : "";
                var durText = new FormattedText(dur, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 9, brush, pixelsPerDip * s);
                var title = new FormattedText($"{i + 1}. {item.DisplayTitle}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 9, brush, pixelsPerDip * s)
                {
                    MaxTextWidth = Math.Max(1, width - durText.Width - 6),
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                double ty = y + (P.RowHeight - title.Height) / 2;
                dc.DrawText(title, new Point(2, ty));
                dc.DrawText(durText, new Point(width - durText.Width - 2, ty));
            }
            dc.Pop();
        }

        private static SolidColorBrush Freeze(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        private static Color ToColor(uint c) => Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c);
    }
}

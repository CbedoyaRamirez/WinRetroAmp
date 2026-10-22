using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinRetroAmp.Controller;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Skins;

namespace WinRetroAmp.Ui;

/// <summary>
/// A borderless window drawn entirely from skin sprites. Subclasses render into a
/// <see cref="PixelImage"/> in skin pixels; this class scales it by a whole number of device
/// pixels (crisp at any DPI, ×2 in double-size mode) and translates mouse input back.
/// </summary>
public abstract class SkinnedWindow : Window
{
    private readonly Image _image = new() { Stretch = Stretch.Fill, SnapsToDevicePixels = true };
    private WriteableBitmap? _bitmap;
    private PixelImage? _canvas;
    private bool _dirty = true;
    private bool _dragging;
    private Point _dragOffset;
    private DateTime _lastClick;
    private Point _lastClickPos;

    protected SkinnedWindow(PlayerController controller, WindowDocking docking)
    {
        C = controller;
        Docking = docking;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual;
        UseLayoutRounding = true;
        Background = Brushes.Black;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(_image, EdgeMode.Aliased);

        Root = new Grid();
        Root.Children.Add(_image);
        Overlay = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        Root.Children.Add(Overlay);
        Content = Root;

        Root.MouseLeftButtonDown += (_, e) => HandleDown(e, MouseButton.Left);
        Root.MouseRightButtonDown += (_, e) => HandleDown(e, MouseButton.Right);
        Root.MouseMove += HandleMove;
        Root.MouseLeftButtonUp += HandleUp;
        Root.MouseWheel += (_, e) =>
        {
            var p = ToSkin(e.GetPosition(Root));
            OnSkinWheel(p.X, p.Y, e.Delta);
            e.Handled = true;
        };
        Root.LostMouseCapture += (_, _) => _dragging = false;

        Activated += (_, _) => Invalidate();
        Deactivated += (_, _) => Invalidate();
        DpiChanged += (_, _) => UpdateSize();
        Loaded += (_, _) => UpdateSize();
        KeyDown += (_, e) =>
        {
            if (Shortcuts.Handle(this, e)) e.Handled = true;
        };
    }

    protected PlayerController C { get; }
    protected WindowDocking Docking { get; }
    protected Skin Skin => C.Skin;
    protected Grid Root { get; }

    /// <summary>WPF content positioned in skin pixels on top of the bitmap (e.g. playlist text).</summary>
    protected Canvas Overlay { get; }

    /// <summary>Size in skin pixels.</summary>
    protected abstract (int W, int H) LogicalSize { get; }

    protected abstract void Render(PixelImage g);

    /// <summary>True while the window animates (visualizer, scrolling title...).</summary>
    public virtual bool IsAnimated => false;

    public void Invalidate() => _dirty = true;

    /// <summary>Device pixels per skin pixel.</summary>
    public int Zoom
    {
        get
        {
            double dpi = PresentationSource.FromVisual(this) is { } ? VisualTreeHelper.GetDpi(this).DpiScaleX : 1;
            int baseZoom = Math.Max(1, (int)Math.Round(dpi));
            return baseZoom * (C.Settings.DoubleSize ? 2 : 1);
        }
    }

    /// <summary>DIPs per skin pixel.</summary>
    public double Scale
    {
        get
        {
            double dpi = PresentationSource.FromVisual(this) is { } ? VisualTreeHelper.GetDpi(this).DpiScaleX : 1;
            return Zoom / dpi;
        }
    }

    public double ScaledWidth => LogicalSize.W * Scale;
    public double ScaledHeight => LogicalSize.H * Scale;

    /// <summary>Resizes the window to its logical size × scale. Call after shade/double-size/resize.</summary>
    public virtual void UpdateSize()
    {
        var (w, h) = LogicalSize;
        Width = w * Scale;
        Height = h * Scale;
        _image.Width = Width;
        _image.Height = Height;
        Overlay.Width = Width;
        Overlay.Height = Height;
        Invalidate();
        Tick();
    }

    /// <summary>Called by the app-wide frame timer.</summary>
    public void Tick()
    {
        if (!IsVisible || WindowState == WindowState.Minimized) return;
        if (!_dirty && !IsAnimated) return;
        _dirty = false;
        var (w, h) = LogicalSize;
        if (_canvas is null || _canvas.Width != w || _canvas.Height != h)
        {
            _canvas = new PixelImage(w, h);
            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            _image.Source = _bitmap;
        }
        Render(_canvas);
        _bitmap!.WritePixels(new Int32Rect(0, 0, w, h), _canvas.Pixels, w * 4, 0);
        AfterRender();
    }

    protected virtual void AfterRender() { }

    protected (int X, int Y) ToSkin(Point p)
    {
        double s = Scale;
        return ((int)Math.Floor(p.X / s), (int)Math.Floor(p.Y / s));
    }

    protected static bool Hit(int x, int y, int rx, int ry, int rw, int rh) => x >= rx && y >= ry && x < rx + rw && y < ry + rh;

    // ------------------------------------------------------------------ input plumbing

    private void HandleDown(MouseButtonEventArgs e, MouseButton button)
    {
        var pos = e.GetPosition(Root);
        var (x, y) = ToSkin(pos);
        if (button == MouseButton.Left)
        {
            bool dbl = (DateTime.UtcNow - _lastClick).TotalMilliseconds < NativeDoubleClickMs && (pos - _lastClickPos).Length < 4;
            _lastClick = dbl ? DateTime.MinValue : DateTime.UtcNow;
            _lastClickPos = pos;
            Root.CaptureMouse();
            if (dbl && OnSkinDoubleClick(x, y))
            {
                e.Handled = true;
                return;
            }
            OnSkinDown(x, y);
        }
        else
        {
            OnSkinRightClick(x, y);
        }
        e.Handled = true;
        Invalidate();
    }

    private static readonly int NativeDoubleClickMs = System.Windows.Forms.SystemInformation.DoubleClickTime;

    private void HandleMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            var screen = ScreenDip(e);
            Docking.Move(this, screen.X - _dragOffset.X, screen.Y - _dragOffset.Y);
            return;
        }
        var (x, y) = ToSkin(e.GetPosition(Root));
        OnSkinMove(x, y, e.LeftButton == MouseButtonState.Pressed);
    }

    private void HandleUp(object sender, MouseButtonEventArgs e)
    {
        Root.ReleaseMouseCapture();
        if (_dragging)
        {
            _dragging = false;
            Docking.EndMove();
            return;
        }
        var (x, y) = ToSkin(e.GetPosition(Root));
        OnSkinUp(x, y);
        Invalidate();
    }

    private Point ScreenDip(MouseEventArgs e)
    {
        var device = Root.PointToScreen(e.GetPosition(Root));
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget?.TransformFromDevice.Transform(device) ?? device;
    }

    /// <summary>Starts moving the window with the mouse (snapping and dragging docked windows).</summary>
    protected void BeginWindowDrag()
    {
        var p = Mouse.GetPosition(this);
        _dragOffset = p;
        _dragging = true;
        Docking.BeginMove(this);
    }

    protected virtual void OnSkinDown(int x, int y) { }
    protected virtual void OnSkinMove(int x, int y, bool leftDown) { }
    protected virtual void OnSkinUp(int x, int y) { }
    protected virtual void OnSkinWheel(int x, int y, int delta) => C.Volume += delta > 0 ? 2 : -2;
    protected virtual bool OnSkinDoubleClick(int x, int y) => false;
    protected virtual void OnSkinRightClick(int x, int y) => Menus.ShowMainMenu(this);

    protected bool IsTitleActive => IsActive;
}

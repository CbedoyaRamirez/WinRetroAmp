using System.Windows;

namespace WinRetroAmp.Ui;

/// <summary>
/// Classic window docking: windows snap to each other and to screen edges within a few pixels,
/// and moving the main window drags every window that is docked to it (directly or in a chain).
/// </summary>
public sealed class WindowDocking
{
    private const double SnapDistance = 10;
    private readonly List<SkinnedWindow> _windows = [];
    private List<SkinnedWindow> _group = [];

    public SkinnedWindow? Main { get; set; }

    public void Register(SkinnedWindow w) => _windows.Add(w);

    private IEnumerable<SkinnedWindow> Visible => _windows.Where(w => w.IsVisible);

    private static Rect R(SkinnedWindow w) => new(w.Left, w.Top, w.Width, w.Height);

    private static bool Touching(Rect a, Rect b)
    {
        const double eps = 1.5;
        bool vOverlap = a.Top < b.Bottom - eps && b.Top < a.Bottom - eps;
        bool hOverlap = a.Left < b.Right - eps && b.Left < a.Right - eps;
        return (vOverlap && (Math.Abs(a.Right - b.Left) <= eps || Math.Abs(b.Right - a.Left) <= eps))
            || (hOverlap && (Math.Abs(a.Bottom - b.Top) <= eps || Math.Abs(b.Bottom - a.Top) <= eps));
    }

    /// <summary>All visible windows connected to <paramref name="root"/> through shared edges.</summary>
    public List<SkinnedWindow> ConnectedTo(SkinnedWindow root)
    {
        var result = new List<SkinnedWindow>();
        var queue = new Queue<SkinnedWindow>([root]);
        var seen = new HashSet<SkinnedWindow> { root };
        while (queue.Count > 0)
        {
            var w = queue.Dequeue();
            foreach (var o in Visible)
            {
                if (seen.Contains(o) || !Touching(R(w), R(o))) continue;
                seen.Add(o);
                result.Add(o);
                queue.Enqueue(o);
            }
        }
        return result;
    }

    public void BeginMove(SkinnedWindow w) => _group = w == Main ? ConnectedTo(w) : [];

    public void EndMove() => _group = [];

    public void Move(SkinnedWindow w, double left, double top)
    {
        var moving = R(w);
        var group = _group;
        // The bounding box of everything that moves together.
        var box = new Rect(left, top, moving.Width, moving.Height);
        foreach (var g in group)
        {
            var r = R(g);
            box.Union(new Rect(r.Left - moving.Left + left, r.Top - moving.Top + top, r.Width, r.Height));
        }

        double bestDx = SnapDistance + 1, bestDy = SnapDistance + 1;
        void Consider(double from, double to, ref double best)
        {
            double d = to - from;
            if (Math.Abs(d) <= SnapDistance && Math.Abs(d) < Math.Abs(best)) best = d;
        }

        var self = new Rect(left, top, moving.Width, moving.Height);
        foreach (var o in Visible)
        {
            if (o == w || group.Contains(o)) continue;
            var r = R(o);
            bool vNear = self.Top < r.Bottom + SnapDistance && r.Top < self.Bottom + SnapDistance;
            bool hNear = self.Left < r.Right + SnapDistance && r.Left < self.Right + SnapDistance;
            if (vNear)
            {
                Consider(self.Left, r.Right, ref bestDx);
                Consider(self.Right, r.Left, ref bestDx);
                Consider(self.Left, r.Left, ref bestDx);
                Consider(self.Right, r.Right, ref bestDx);
            }
            if (hNear)
            {
                Consider(self.Top, r.Bottom, ref bestDy);
                Consider(self.Bottom, r.Top, ref bestDy);
                Consider(self.Top, r.Top, ref bestDy);
                Consider(self.Bottom, r.Bottom, ref bestDy);
            }
        }

        // Screen edges of whichever monitor the window is on.
        var area = WorkAreaFor(box);
        Consider(box.Left, area.Left, ref bestDx);
        Consider(box.Right, area.Right, ref bestDx);
        Consider(box.Top, area.Top, ref bestDy);
        Consider(box.Bottom, area.Bottom, ref bestDy);

        if (Math.Abs(bestDx) <= SnapDistance) left += bestDx;
        if (Math.Abs(bestDy) <= SnapDistance) top += bestDy;

        double dx = left - w.Left, dy = top - w.Top;
        w.Left = left;
        w.Top = top;
        foreach (var g in group)
        {
            g.Left += dx;
            g.Top += dy;
        }
    }

    private static Rect WorkAreaFor(Rect box)
    {
        var center = new System.Drawing.Point((int)(box.Left + box.Width / 2), (int)(box.Top + box.Height / 2));
        var wa = System.Windows.Forms.Screen.FromPoint(center).WorkingArea;
        // Screen coordinates are device pixels; convert with the primary DPI (good enough for snapping).
        double s = SystemParameters.WorkArea.Width / System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea.Width;
        return new Rect(wa.Left * s, wa.Top * s, wa.Width * s, wa.Height * s);
    }

    /// <summary>
    /// Wraps a size change of <paramref name="w"/> (shade/unshade, playlist resize): windows docked
    /// to its bottom or right edge follow the edge so the stack stays together.
    /// </summary>
    public void Resize(SkinnedWindow w, Action resize)
    {
        var before = R(w);
        var below = ConnectedTo(w).Where(o => R(o).Top >= before.Bottom - 1.5).ToList();
        var right = ConnectedTo(w).Where(o => R(o).Left >= before.Right - 1.5 && R(o).Top < before.Bottom - 1.5).ToList();
        resize();
        var after = R(w);
        double dy = after.Height - before.Height, dx = after.Width - before.Width;
        foreach (var o in below) o.Top += dy;
        foreach (var o in right) o.Left += dx;
    }

    /// <summary>
    /// Wraps a change of scale (double size): every window keeps its position relative to the
    /// main window measured in skin pixels.
    /// </summary>
    public void Rescale(Action change)
    {
        if (Main is null)
        {
            change();
            return;
        }
        double oldScale = Main.Scale;
        var offsets = _windows.Where(w => w != Main).Select(w => (w, dx: (w.Left - Main.Left) / oldScale, dy: (w.Top - Main.Top) / oldScale)).ToList();
        change();
        double s = Main.Scale;
        foreach (var (w, ox, oy) in offsets)
        {
            w.Left = Main.Left + ox * s;
            w.Top = Main.Top + oy * s;
        }
    }
}

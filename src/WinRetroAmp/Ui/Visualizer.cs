using WinRetroAmp.Controller;
using WinRetroAmp.Core.Audio;
using WinRetroAmp.Core.Imaging;
using WinRetroAmp.Core.Settings;

namespace WinRetroAmp.Ui;

/// <summary>Spectrum analyzer (thick or thin bars, falling peaks) and oscilloscope in skin colours.</summary>
internal sealed class Visualizer
{
    private double[] _bars = new double[75];
    private double[] _peaks = new double[75];
    private double[] _peakSpeed = new double[75];

    private void Update(PlayerController c, int count)
    {
        if (_bars.Length != count)
        {
            _bars = new double[count];
            _peaks = new double[count];
            _peakSpeed = new double[count];
        }
        double[] target = c.State == PlayerState.Playing ? c.Engine.Analyzer.GetSpectrum(count) : new double[count];
        double fall = 0.02 + 0.015 * Math.Clamp(c.Settings.VisFalloff, 0, 4);
        for (int i = 0; i < count; i++)
        {
            _bars[i] = Math.Max(target[i], _bars[i] - fall);
            if (_bars[i] >= _peaks[i])
            {
                _peaks[i] = _bars[i];
                _peakSpeed[i] = 0;
            }
            else
            {
                _peakSpeed[i] += 0.0025;
                _peaks[i] = Math.Max(0, _peaks[i] - _peakSpeed[i]);
            }
        }
    }

    public void Render(PixelImage g, PlayerController c, int x, int y, int w, int h, VisMode mode)
    {
        var colors = c.Skin.VisColors;
        g.Fill(x, y, w, h, colors[0]);
        for (int yy = 1; yy < h; yy += 2)
            for (int xx = 1; xx < w; xx += 2)
                g[x + xx, y + yy] = colors[1];

        if (mode == VisMode.Off) return;
        if (mode == VisMode.Oscilloscope)
        {
            if (c.State != PlayerState.Playing) return;
            var wave = c.Engine.Analyzer.GetWaveform(w);
            int prev = -1;
            for (int i = 0; i < w; i++)
            {
                int yy = Math.Clamp((int)Math.Round(h / 2.0 - wave[i] * h * 0.9), 0, h - 1);
                int from = prev < 0 ? yy : Math.Min(prev, yy), to = prev < 0 ? yy : Math.Max(prev, yy);
                for (int k = from; k <= to; k++)
                {
                    int dist = Math.Abs(k - h / 2);
                    g[x + i, y + k] = colors[18 + Math.Min(4, dist * 5 / (h / 2 + 1))];
                }
                prev = yy;
            }
            return;
        }

        bool thin = c.Settings.ThinBars;
        int count = thin ? 75 : 19, bw = thin ? 1 : 3, step = thin ? 1 : 4;
        Update(c, count);
        for (int i = 0; i < count; i++)
        {
            int bh = (int)Math.Round(_bars[i] * h);
            int bx = x + i * step;
            for (int r = h - bh; r < h; r++)
                g.Fill(bx, y + r, bw, 1, colors[2 + Math.Clamp(r * 16 / h, 0, 15)]);
            if (c.Settings.VisPeaks)
            {
                int py = (int)Math.Round(_peaks[i] * h);
                if (py > 0) g.Fill(bx, y + Math.Clamp(h - py, 0, h - 1), bw, 1, colors[23]);
            }
        }
    }

    /// <summary>The tiny analyzer in the shaded title bar.</summary>
    public void RenderMini(PixelImage g, PlayerController c, int x, int y, int w, int h)
    {
        var colors = c.Skin.VisColors;
        g.Fill(x, y, w, h, colors[0]);
        if (c.Settings.VisMode == VisMode.Off) return;
        Update(c, 19);
        for (int i = 0; i < 19; i++)
        {
            int bh = (int)Math.Round(_bars[i] * h);
            for (int r = h - bh; r < h; r++)
                g.Fill(x + i * 2, y + r, 1, 1, colors[2 + Math.Clamp(r * 16 / h, 0, 15)]);
        }
    }
}

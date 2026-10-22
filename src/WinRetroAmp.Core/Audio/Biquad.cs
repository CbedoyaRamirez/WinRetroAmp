namespace WinRetroAmp.Core.Audio;

/// <summary>A peaking-EQ biquad (RBJ audio EQ cookbook), direct form I, one instance per channel.</summary>
internal sealed class Biquad
{
    private double _b0, _b1, _b2, _a1, _a2;
    private double _x1, _x2, _y1, _y2;

    public bool IsBypassed { get; private set; } = true;

    public void SetPeaking(double sampleRate, double frequency, double q, double gainDb)
    {
        // Bands above ~Nyquist (e.g. 16 kHz on 22 kHz audio) are simply skipped.
        if (Math.Abs(gainDb) < 0.01 || frequency >= sampleRate * 0.45)
        {
            IsBypassed = true;
            return;
        }
        double a = Math.Pow(10, gainDb / 40);
        double w0 = 2 * Math.PI * frequency / sampleRate;
        double cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * q);
        double a0 = 1 + alpha / a;
        _b0 = (1 + alpha * a) / a0;
        _b1 = -2 * cos / a0;
        _b2 = (1 - alpha * a) / a0;
        _a1 = -2 * cos / a0;
        _a2 = (1 - alpha / a) / a0;
        IsBypassed = false;
    }

    public float Process(float input)
    {
        if (IsBypassed) return input;
        double y = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = y;
        return (float)y;
    }

    public void Reset() => _x1 = _x2 = _y1 = _y2 = 0;
}

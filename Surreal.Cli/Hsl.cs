namespace Surreal.Cli;

public struct Hsl
{
    public double Hue { get; }
    
    public double Saturation { get; }

    public double Lightness { get; }

    public Hsl()
    {
        Hue = 0;
        Saturation = 0;
        Lightness = 100;
    }

    public Hsl(double hue, double saturation, double lightness)
    {
        Hue = hue;
        Saturation = saturation;
        Lightness = lightness;
    }

    public (byte r, byte g, byte b) ToRGB()
    {
        var (h, s, l) = (Hue, Saturation, Lightness);

        var c = s * (1d - Math.Abs(2d * l - 1d));
        var x = c * (1d - Math.Abs(h / 60d % 2d - 1d));
        var m = l - c / 2d;
        var (r, g, b) = h switch
        {
            >= 0d and < 60d => (c, x, 0d),
            >= 60d and < 120d => (x, c, 0d),
            >= 120d and < 180d => (0d, c, x),
            >= 180d and < 240d => (0d, x, c),
            >= 240d and < 300d => (x, 0d, c),
            >= 300d and <= 360d => (c, 0d, x),
            _ => throw new ArgumentOutOfRangeException(nameof(h), "The given hue was outside the allowed range of [0, 360]")
        };
        r += m;
        g += m;
        b += m;
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}


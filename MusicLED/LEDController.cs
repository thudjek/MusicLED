using Iot.Device.Ws28xx;
using System.Device.Spi;
using System.Drawing;

namespace MusicLED;

public class LEDController : IDisposable
{
    private readonly Random _random = new Random();
    private readonly Ws2812b _ledStrip;
    private readonly SpiDevice _spiDevice;
    private readonly double _globalBrightness;
    private readonly List<Color> _smoothColors = [Color.Red, Color.Purple, Color.Yellow, Color.Beige, Color.Blue, Color.RosyBrown];

    public LEDController(int ledCount = 150)
    {
        _globalBrightness = 0.23;

        var settings = new SpiConnectionSettings(0, 0)
        {
            ClockFrequency = 2_400_000,
            Mode = SpiMode.Mode0,
            DataBitLength = 8
        };

        _spiDevice = SpiDevice.Create(settings);
        _ledStrip = new Ws2812b(_spiDevice, ledCount);
    }

    public void ClearNoUpdate()
    {
        _ledStrip.Image.Clear(Color.Black);
    }

    public void Clear()
    {
        _ledStrip.Image.Clear(Color.Black);
        _ledStrip.Update();
    }

    public void SetAllLEDs(Color color)
    {
        _ledStrip.Image.Clear(GetAdjustedColor(color));
        _ledStrip.Update();
    }

    public void SetFixedColors(List<string> hexValues)
    {
        if (hexValues == null || hexValues.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _ledStrip.Image.Width; i++)
        {
            var hexValue = hexValues[i % hexValues.Count];
            _ledStrip.Image.SetPixel(i, 0, GetAdjustedColor(ColorTranslator.FromHtml(hexValue)));
        }

        _ledStrip.Update();
    }

    public void RunningAnimation(int numberOfLeds, int delay, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var currentColor = GetRandomColor();

            for (var i = 0; i < _ledStrip.Image.Width; i += numberOfLeds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ClearNoUpdate();
                for (var j = i; j < i + numberOfLeds; j++)
                {
                    if (j < _ledStrip.Image.Width)
                    {
                        _ledStrip.Image.SetPixel(j, 0, currentColor);
                    }
                }

                _ledStrip.Update();
                Thread.Sleep(delay);
            }
        }
    }

    public void SmoothAnimation(int transitionSteps, int delay, CancellationToken cancellationToken)
    {
        if (transitionSteps <= 0)
        {
            throw new OperationCanceledException();
        }

        var colorIndexes = new int[_ledStrip.Image.Width];

        for (var i = 0; i < _ledStrip.Image.Width; i++)
        {
            colorIndexes[i] = i % _smoothColors.Count;
            _ledStrip.Image.SetPixel(i, 0, GetAdjustedColor(_smoothColors[colorIndexes[i]]));
        }

        _ledStrip.Update();

        while (!cancellationToken.IsCancellationRequested)
        {
            for (var step = 0; step <= transitionSteps; step++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var t = (float)step / transitionSteps;

                for (var i = 0; i < _ledStrip.Image.Width; i++)
                {
                    var currentColor = _smoothColors[colorIndexes[i]];
                    var nextColor = _smoothColors[(colorIndexes[i] + 1) % _smoothColors.Count];
                    var color = LerpColor(currentColor, nextColor, t);
                    _ledStrip.Image.SetPixel(i, 0, GetAdjustedColor(color));
                }

                _ledStrip.Update();
                Thread.Sleep(delay);
            }

            for (var i = 0; i < _ledStrip.Image.Width; i++)
            {
                colorIndexes[i] = (colorIndexes[i] + 1) % _smoothColors.Count;
            }
        }
    }

    public void SetSingleLED(int index, Color color)
    {
        _ledStrip.Image.SetPixel(index, 0, GetAdjustedColor(color));
        _ledStrip.Update();
    }

    public void UpdateFromFrequencies(FrequencyBands bands)
    {
        _ledStrip.Image.Clear(Color.Black);

        if (bands.RawBass < 0.01f && bands.RawMid < 0.01f && bands.RawTreble < 0.01f)
        {
            return;
        }

        _ledStrip.Image.Clear(Color.Black);

        // Calculate total energy for dynamic color shifts
        float totalEnergy = (bands.RawBass + bands.RawMid + bands.RawTreble) / 3f;

        for (int i = 0; i < _ledStrip.Image.Width; i++)
        {
            float intensity;
            Color baseColor;

            int pattern = i % 3;
            float brightnessMultiplier;

            switch (pattern)
            {
                case 0: // Bass - Red to Orange gradient based on energy
                    intensity = bands.RawBass;
                    brightnessMultiplier = 0.35f; // Bass at 40% brightness
                    // Use RawBass directly for color, not totalEnergy - keeps it snappy
                    baseColor = Color.FromArgb(
                        255,
                        (int)(140 * bands.RawBass), // Add orange when bass is high
                        0
                    );
                    break;
                case 1: // Mid - Green to Yellow gradient based on energy (vocals/sustain)
                    intensity = bands.RawMid;
                    brightnessMultiplier = 0.25f; // Mid at 30% brightness
                    baseColor = Color.FromArgb(
                        (int)(200 * totalEnergy), // Add yellow when energetic
                        255,
                        0
                    );
                    break;
                case 2: // Treble - Blue to Purple/Cyan gradient based on energy
                    // Amplify treble to make it more visible
                    intensity = bands.RawTreble * 1.5f;
                    intensity = Math.Min(intensity, 1.0f); // Clamp to max 1.0
                    brightnessMultiplier = 0.25f;
                    baseColor = Color.FromArgb(
                        (int)(100 * bands.RawTreble), // Add purple tint when treble is high
                        (int)(100 * bands.RawTreble), // Add cyan tint when treble is high
                        255
                    );
                    break;
                default:
                    throw new InvalidOperationException();
            }

            // Apply intensity to the color
            Color finalColor = Color.FromArgb(
                (int)(baseColor.R * intensity),
                (int)(baseColor.G * intensity),
                (int)(baseColor.B * intensity)
            );

            // Apply per-pattern brightness adjustment
            Color adjustedColor = Color.FromArgb(
                (int)(finalColor.R * brightnessMultiplier),
                (int)(finalColor.G * brightnessMultiplier),
                (int)(finalColor.B * brightnessMultiplier)
            );

            _ledStrip.Image.SetPixel(i, 0, adjustedColor);
        }


        Console.WriteLine($"Bass: {bands.RawBass}, Mid: {bands.RawMid}, Treble: {bands.RawTreble}");

        _ledStrip.Update();
    }

    private Color GetRandomColor()
    {
        int r = 0;
        int g = 0;
        int b = 0;

        while (r == 0 && g == 0 && b == 0)
        {
            r = _random.Next(256);
            g = _random.Next(256);
            b = _random.Next(256);
        }

        return Color.FromArgb(r, g, b);
    }

    private Color GetAdjustedColor(Color color)
    {
        return Color.FromArgb(
            (int)(color.R * _globalBrightness),
            (int)(color.G * _globalBrightness),
            (int)(color.B * _globalBrightness)
        );
    }

    private static Color LerpColor(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        int r = (int)(from.R + (to.R - from.R) * t);
        int g = (int)(from.G + (to.G - from.G) * t);
        int b = (int)(from.B + (to.B - from.B) * t);

        return Color.FromArgb(r, g, b);
    }

    public void Dispose()
    {
        Console.WriteLine("LED Controller disposed");
        GC.SuppressFinalize(this);
        Clear();
        _spiDevice.Dispose();
    }
}
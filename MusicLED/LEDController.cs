using Iot.Device.Ws28xx;
using System.Device.Spi;
using System.Drawing;

namespace MusicLED;

public class LEDController : IDisposable
{
    private readonly Ws2812b _ledStrip;
    private readonly SpiDevice _spiDevice;
    private readonly double _globalBrightness;

    public LEDController(int ledCount = 50)
    {
        _globalBrightness = 0.5;

        var settings = new SpiConnectionSettings(0, 0)
        {
            ClockFrequency = 2_400_000,
            Mode = SpiMode.Mode0,
            DataBitLength = 8
        };

        _spiDevice = SpiDevice.Create(settings);
        _ledStrip = new Ws2812b(_spiDevice, ledCount);
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

    public void SetSingleLED(int index, Color color)
    {
        _ledStrip.Image.SetPixel(index, 0, GetAdjustedColor(color));
        _ledStrip.Update();
    }

    public void UpdateFromFrequencies(FrequencyBands bands, bool isBeat, bool isSustained)
    {
        if (bands.RawBass < 0.01f && bands.RawMid < 0.01f && bands.RawTreble < 0.01f)
        {
            return;
        }

        _ledStrip.Image.Clear(Color.Black);

        // Beat flash - bright cyan/white flash on all LEDs
        if (isBeat)
        {
            for (int i = 0; i < _ledStrip.Image.Width; i += 4)
            {
                _ledStrip.Image.SetPixel(i, 0, GetAdjustedColor(Color.Cyan));
            }
        }
        else
        {
            // Calculate total energy for dynamic color shifts
            float totalEnergy = (bands.RawBass + bands.RawMid + bands.RawTreble) / 3f;

            for (int i = 0; i < _ledStrip.Image.Width; i++)
            {
                float intensity;
                Color baseColor;

                int pattern = i % 3;
                switch (pattern)
                {
                    case 0: // Bass - Red to Orange gradient based on energy
                        intensity = bands.RawBass;
                        baseColor = Color.FromArgb(
                            255,
                            (int)(140 * totalEnergy), // Add orange/yellow when energetic
                            0
                        );
                        break;
                    case 1: // Mid - Green to Yellow gradient based on energy (vocals/sustain)
                        intensity = bands.RawMid;

                        // When sustained note detected, add golden/white tint
                        if (isSustained)
                        {
                            baseColor = Color.FromArgb(
                                255, // Full red for golden
                                215, // Gold component
                                (int)(100 * intensity) // Some blue for white tint
                            );
                        }
                        else
                        {
                            baseColor = Color.FromArgb(
                                (int)(200 * totalEnergy), // Add yellow when energetic
                                255,
                                0
                            );
                        }
                        break;
                    case 2: // Treble - Blue to Purple/Cyan gradient based on energy
                        intensity = bands.RawTreble;
                        baseColor = Color.FromArgb(
                            (int)(100 * totalEnergy), // Add purple tint when energetic
                            (int)(100 * totalEnergy), // Add cyan tint when energetic
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

                _ledStrip.Image.SetPixel(i, 0, GetAdjustedColor(finalColor));
            }
        }

        _ledStrip.Update();
    }

    private Color GetAdjustedColor(Color color)
    {
        return Color.FromArgb(
            (int)(color.R * _globalBrightness),
            (int)(color.G * _globalBrightness),
            (int)(color.B * _globalBrightness)
        );
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Clear();
        _spiDevice.Dispose();
    }
}
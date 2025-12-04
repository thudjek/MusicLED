using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

namespace MusicLED;

public class FFTAnalyzer
{
    private readonly int _sampleRate;
    private readonly int _fftSize;
    private readonly Complex[] _complexBuffer;
    private readonly double[] _window;

    public FFTAnalyzer()
    {
        _sampleRate = 48000;
        _fftSize = 2048;
        _complexBuffer = new Complex[_fftSize];

        _window = CreateHammingWindow(_fftSize);
    }

    public void Analyze(short[] samples, FrequencyBands frequencyBands)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Length < _fftSize)
            throw new ArgumentException($"Need at least {_fftSize} samples, got {samples.Length}");

        PrepareComplexBuffer(samples);

        Fourier.Forward(_complexBuffer, FourierOptions.Matlab);

        CalculateAndUpdateFrequencyBands(frequencyBands);
    }

    private static double[] CreateHammingWindow(int size)
    {
        var window = new double[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (size - 1));
        }
        return window;
    }

    private void PrepareComplexBuffer(short[] samples)
    {
        for (int i = 0; i < _fftSize; i++)
        {
            double normalized = (samples[i] / 32768.0) * _window[i];
            _complexBuffer[i] = new Complex(normalized, 0);
        }
    }

    private void CalculateAndUpdateFrequencyBands(FrequencyBands frequencyBands)
    {
        var binWidth = (double)_sampleRate / _fftSize;

        var bassEnd = (int)(450 / binWidth);
        var midEnd = (int)(4000 / binWidth);
        var nyquist = _fftSize / 2;

        double bassEnergy = 0;
        double midEnergy = 0;
        double trebleEnergy = 0;

        for (var i = 1; i < nyquist; i++)
        {
            var magnitude = _complexBuffer[i].Magnitude / _fftSize;
            var power = magnitude * magnitude;

            if (i <= bassEnd)
            {
                bassEnergy += power;
            }
            else if (i <= midEnd)
            {
                midEnergy += power;
            }
            else
            {
                trebleEnergy += power;
            }
        }

        var bass = (float)Math.Sqrt(bassEnergy / bassEnd);
        var mid = (float)Math.Sqrt(midEnergy / (midEnd - bassEnd));
        var treble = (float)Math.Sqrt(trebleEnergy / (nyquist - midEnd));

        bass *= 50.0f;  // Reduced to prevent constant saturation at 1.0
        mid *= 120.0f;
        treble *= 235.0f;

        bass = Math.Clamp(bass, 0, 1);
        mid = Math.Clamp(mid, 0, 1);
        treble = Math.Clamp(treble, 0, 1);

        frequencyBands.UpdateValues(bass, mid, treble);
    }
}
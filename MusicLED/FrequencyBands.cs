namespace MusicLED;

public class FrequencyBands
{
    private const float BassSmoothFactor = 0.1f;  // Very responsive
    private const float MidSmoothFactor = 0.3f;   // Moderate smoothing
    private const float TrebleSmoothFactor = 0.3f; // Moderate smoothing

    private const float BassDecayRate = 0.92f;  // How fast peaks decay (0.92 = slow decay, more sustain)
    private const float MidDecayRate = 0.90f;   // Slightly faster decay for mids
    private const float TrebleDecayRate = 0.88f; // Fastest decay for treble
    private const float PeakThreshold = 1.05f;  // Multiplier - new value must be this much higher to be a new peak (lowered from 1.1)
    private const float MinDecayAmount = 0.015f; // Minimum amount to decay each frame to prevent getting stuck at max

    private float smoothedBass;
    private float smoothedMid;
    private float smoothedTreble;
    private float bassPeak;    // Tracks the recent peak bass value
    private float midPeak;     // Tracks the recent peak mid value
    private float treblePeak;  // Tracks the recent peak treble value

    public float RawBass { get; private set; }
    public float RawMid { get; private set; }
    public float RawTreble { get; private set; }

    public float SmoothedBass => smoothedBass;
    public float SmoothedMid => smoothedMid;
    public float SmoothedTreble => smoothedTreble;

    public void UpdateValues(float bass, float mid, float treble)
    {
        Console.WriteLine($"Incoming - Bass: {bass}, Mid: {mid}, Treble: {treble}");

        // Peak detection with decay for bass
        if (bass > bassPeak * PeakThreshold)
        {
            bassPeak = bass;
        }
        else
        {
            // Apply both multiplicative decay and minimum decay to prevent getting stuck
            float decayedValue = bassPeak * BassDecayRate;
            bassPeak = Math.Max(decayedValue - MinDecayAmount, 0);

            // Only update if new value is significantly higher than decayed peak
            if (bass > bassPeak * 1.02f)  // Require 2% higher to update
            {
                bassPeak = bass;
            }
        }

        // Peak detection with decay for mid
        if (mid > midPeak * PeakThreshold)
        {
            midPeak = mid;
        }
        else
        {
            // Apply both multiplicative decay and minimum decay to prevent getting stuck
            float decayedValue = midPeak * MidDecayRate;
            midPeak = Math.Max(decayedValue - MinDecayAmount, 0);

            // Only update if new value is significantly higher than decayed peak
            if (mid > midPeak * 1.02f)  // Require 2% higher to update
            {
                midPeak = mid;
            }
        }

        // Peak detection with decay for treble
        if (treble > treblePeak * PeakThreshold)
        {
            treblePeak = treble;
        }
        else
        {
            // Apply both multiplicative decay and minimum decay to prevent getting stuck
            float decayedValue = treblePeak * TrebleDecayRate;
            treblePeak = Math.Max(decayedValue - MinDecayAmount, 0);

            // Only update if new value is significantly higher than decayed peak
            if (treble > treblePeak * 1.02f)  // Require 2% higher to update
            {
                treblePeak = treble;
            }
        }

        // Use the peak values for more consistent response
        RawBass = bassPeak;
        RawMid = midPeak;
        RawTreble = treblePeak;

        // Smoothing using peak values
        smoothedBass = BassSmoothFactor * smoothedBass + (1 - BassSmoothFactor) * bassPeak;
        smoothedMid = MidSmoothFactor * smoothedMid + (1 - MidSmoothFactor) * midPeak;
        smoothedTreble = TrebleSmoothFactor * smoothedTreble + (1 - TrebleSmoothFactor) * treblePeak;
    }
}
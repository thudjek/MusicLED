namespace MusicLED;

public class FrequencyBands
{
    private const float SmoothFactor = 0.5f;  // Reduced from 0.7 for faster response

    private float smoothedBass;
    private float smoothedMid;
    private float smoothedTreble;

    public float RawBass { get; private set; }
    public float RawMid { get; private set; }
    public float RawTreble { get; private set; }

    public float SmoothedBass => smoothedBass;
    public float SmoothedMid => smoothedMid;
    public float SmoothedTreble => smoothedTreble;

    public void UpdateValues(float bass, float mid, float treble)
    {
        RawBass = bass;
        RawMid = mid;
        RawTreble = treble;

        smoothedBass = SmoothFactor * smoothedBass + (1 - SmoothFactor) * bass;
        smoothedMid = SmoothFactor * smoothedMid + (1 - SmoothFactor) * mid;
        smoothedTreble = SmoothFactor * smoothedTreble + (1 - SmoothFactor) * treble;
    }
}
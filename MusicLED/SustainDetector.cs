namespace MusicLED;

public class SustainDetector
{
    private readonly Queue<float> _midEnergyHistory;
    private const int HistorySize = 15; // Track last 15 frames (~150ms)
    private const float SustainThreshold = 0.3f; // Minimum energy to consider sustained
    private const float VarianceThreshold = 0.08f; // How stable the energy must be

    public SustainDetector()
    {
        _midEnergyHistory = new Queue<float>(HistorySize);
    }

    public bool IsSustained(float currentMidEnergy)
    {
        // Add current energy to history
        _midEnergyHistory.Enqueue(currentMidEnergy);

        // Keep only recent history
        if (_midEnergyHistory.Count > HistorySize)
        {
            _midEnergyHistory.Dequeue();
        }

        // Need enough samples
        if (_midEnergyHistory.Count < HistorySize)
        {
            return false;
        }

        // Calculate average and check if sustained
        float average = _midEnergyHistory.Average();

        // Must have minimum energy level (not silence)
        if (average < SustainThreshold)
        {
            return false;
        }

        // Calculate variance (how much energy fluctuates)
        float variance = _midEnergyHistory.Select(e => Math.Abs(e - average)).Average();

        // Sustained note = high energy + low variance (stable)
        return variance < VarianceThreshold;
    }

    public void Reset()
    {
        _midEnergyHistory.Clear();
    }
}

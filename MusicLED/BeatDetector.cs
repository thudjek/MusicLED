namespace MusicLED;

public class BeatDetector
{
    private readonly int _historySize;
    private readonly Queue<float> _energyHistory;
    private float _averageEnergy;
    private int _cooldownFrames;
    private const int CooldownDuration = 20; // Increased from 10 - more time between beats
    private const float BeatThreshold = 2.2f; // Increased from 1.5 - require bigger spike

    public BeatDetector(int historySize = 43)
    {
        _historySize = historySize;
        _energyHistory = new Queue<float>(historySize);
        _averageEnergy = 0;
        _cooldownFrames = 0;
    }

    public bool DetectBeat(float currentEnergy)
    {
        // Add current energy to history
        _energyHistory.Enqueue(currentEnergy);

        // Keep only the last N frames
        if (_energyHistory.Count > _historySize)
        {
            _energyHistory.Dequeue();
        }

        // Calculate average energy from history
        if (_energyHistory.Count > 0)
        {
            _averageEnergy = _energyHistory.Average();
        }

        // Decrease cooldown
        if (_cooldownFrames > 0)
        {
            _cooldownFrames--;
        }

        // Beat detection: current energy is significantly higher than average
        // and we're not in cooldown period
        bool isBeat = false;
        if (_cooldownFrames == 0 &&
            currentEnergy > (_averageEnergy * BeatThreshold) &&
            _averageEnergy > 0.05f && // Increased from 0.01 - need minimum baseline energy
            currentEnergy > 0.3f) // Require absolute minimum energy level for beat
        {
            isBeat = true;
            _cooldownFrames = CooldownDuration; // Start cooldown
        }

        return isBeat;
    }

    public void Reset()
    {
        _energyHistory.Clear();
        _averageEnergy = 0;
        _cooldownFrames = 0;
    }
}

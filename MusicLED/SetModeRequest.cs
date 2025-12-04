namespace MusicLED;

public class SetModeRequest
{
    public SetModeRequest()
    {
        FixedColors = [];
    }

    public LedMode Mode { get; set; }
    public List<string> FixedColors { get; set; }
    public int NumOfRunningLeds { get; set; }
    public int RunningDelay { get; set; }
    public int TransitionSteps { get; set; }
    public int TransitionDelay { get; set; }
}
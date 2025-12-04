using System.Diagnostics;

namespace MusicLED;

public static class ChmodHelper
{
    public static void MakeFileExecutable(string filePath)
    {
        using var chmodProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/chmod",
            Arguments = $"+x {filePath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (chmodProcess != null)
        {
            chmodProcess.WaitForExit();

            if (chmodProcess.ExitCode == 0)
            {
                Console.WriteLine($"Script {filePath} is now exectuable");
            }
            else
            {
                Console.WriteLine($"Chmod +x for {filePath} failed");
            }
        }
    }
}
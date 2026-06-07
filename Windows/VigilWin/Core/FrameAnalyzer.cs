namespace VigilWin.Core;

public sealed class FrameAnalyzer
{
    public bool ShouldAnalyze(byte[] currentScreenshot)
    {
        ArgumentNullException.ThrowIfNull(currentScreenshot);

        // TODO: Compare perceptual hashes later to avoid repeated AI calls for nearly identical screenshots.
        return true;
    }
}

namespace VigilWin.Core;

public static class ElapsedTimeFormatter
{
    public static string Format(TimeSpan time)
    {
        var safeTime = time < TimeSpan.Zero ? TimeSpan.Zero : time;
        return safeTime.TotalHours >= 1
            ? safeTime.ToString(@"hh\:mm\:ss")
            : safeTime.ToString(@"mm\:ss");
    }
}

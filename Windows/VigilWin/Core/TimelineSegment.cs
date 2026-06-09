namespace VigilWin.Core;

public sealed class TimelineSegment
{
    public FocusStatus Status { get; init; }

    public TimeSpan Start { get; init; }

    public TimeSpan Duration { get; set; }

    public TimeSpan End => Start + Duration;
}

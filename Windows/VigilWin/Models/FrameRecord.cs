using VigilWin.Core;

namespace VigilWin.Models;

public sealed class FrameRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.Now;

    public FocusStatus Status { get; set; } = FocusStatus.Focused;

    public double Confidence { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? ScreenshotPath { get; set; }
}

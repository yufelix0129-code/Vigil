using VigilWin.Core;

namespace VigilWin.Models;

public sealed class FrameRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;

    public FocusStatus Status { get; set; } = FocusStatus.Focused;

    public string? ImagePath { get; set; }
}

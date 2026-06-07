using VigilWin.Core;

namespace VigilWin.Models;

public sealed class FocusSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset? EndedAt { get; set; }

    public FocusStatus Status { get; set; } = FocusStatus.Focused;
}

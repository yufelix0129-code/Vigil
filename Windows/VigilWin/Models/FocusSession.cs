namespace VigilWin.Models;

public sealed class FocusSession
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Goal { get; set; } = string.Empty;

    public DateTime StartTime { get; set; } = DateTime.Now;

    public DateTime? EndTime { get; set; }

    public int PlannedDurationSeconds { get; set; }

    public int FocusedSeconds { get; set; }

    public int WanderingSeconds { get; set; }

    public int DistractedSeconds { get; set; }

    public int IdleSeconds { get; set; }

    public int DistractionCount { get; set; }

    public string? Summary { get; set; }
}

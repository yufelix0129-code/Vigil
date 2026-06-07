using VigilWin.Core;

namespace VigilWin.Models;

public sealed class AIAnalysisResult
{
    public FocusStatus Status { get; set; } = FocusStatus.Unknown;

    public double Confidence { get; set; }

    public string Reason { get; set; } = string.Empty;
}

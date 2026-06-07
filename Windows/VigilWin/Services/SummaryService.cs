using VigilWin.Models;
using VigilWin.Core;

namespace VigilWin.Services;

public static class SummaryService
{
    public static string BuildLocalSummary(FocusSession session, IReadOnlyList<FrameRecord> records)
    {
        var topReasons = records
            .Where(record => record.Status == FocusStatus.Distracted && !string.IsNullOrWhiteSpace(record.Reason))
            .Select(record => record.Reason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var reasonText = topReasons.Count == 0
            ? "本地记录中没有明显分心原因。"
            : string.Join("；", topReasons);

        return $"""
            本次目标：{session.Goal}
            专注情况：专注 {session.FocusedSeconds} 秒，轻微偏离 {session.WanderingSeconds} 秒，分心 {session.DistractedSeconds} 秒，空闲 {session.IdleSeconds} 秒。
            主要分心原因：{reasonText}
            简短建议：下次可以把目标拆得更小，并在开始前关闭明显无关的应用或网页。
            """;
    }
}

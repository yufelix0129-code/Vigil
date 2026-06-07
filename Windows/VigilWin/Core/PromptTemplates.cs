using System.Text;
using VigilWin.Models;

namespace VigilWin.Core;

public static class PromptTemplates
{
    public static string BuildAnalysisPrompt(string goal)
    {
        return $$"""
            你是一个专注监督助手。
            用户当前的专注目标是：
            {{goal}}

            请根据截图判断用户当前屏幕是否服务于这个目标。

            只返回 JSON，不要返回其他文字：

            {
              "status": "focused | wandering | distracted | idle",
              "confidence": 0.0,
              "reason": "简短原因"
            }

            判断标准：
            - focused：屏幕内容直接服务于目标
            - wandering：屏幕内容和目标弱相关，或者短暂偏离
            - distracted：屏幕内容明显无关，比如娱乐、购物、社交媒体、游戏、无关网页
            - idle：没有明显操作、空白桌面、锁屏、用户似乎离开电脑
            """;
    }

    public static string BuildSummaryPrompt(FocusSession session, IReadOnlyList<FrameRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("请根据这次 Windows Vigil 专注会话生成中文总结。");
        builder.AppendLine("输出简洁自然，包含：本次目标、专注情况、主要分心原因、简短建议。");
        builder.AppendLine();
        builder.AppendLine($"目标：{session.Goal}");
        builder.AppendLine($"计划时长：{session.PlannedDurationSeconds} 秒");
        builder.AppendLine($"实际开始：{session.StartTime:O}");
        builder.AppendLine($"实际结束：{session.EndTime:O}");
        builder.AppendLine($"focused seconds：{session.FocusedSeconds}");
        builder.AppendLine($"wandering seconds：{session.WanderingSeconds}");
        builder.AppendLine($"distracted seconds：{session.DistractedSeconds}");
        builder.AppendLine($"idle seconds：{session.IdleSeconds}");
        builder.AppendLine($"distraction count：{session.DistractionCount}");
        builder.AppendLine();
        builder.AppendLine("最近分析记录：");

        foreach (var record in records.TakeLast(60))
        {
            builder.AppendLine($"- {record.Timestamp:HH:mm:ss} {record.Status} {record.Confidence:0.00} {record.Reason}");
        }

        return builder.ToString();
    }
}

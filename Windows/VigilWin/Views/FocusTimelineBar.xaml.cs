using System.Windows;
using System.Windows.Controls;
using VigilWin.Core;
using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUserControl = System.Windows.Controls.UserControl;

namespace VigilWin.Views;

public partial class FocusTimelineBar : WpfUserControl
{
    private static readonly WpfBrush FutureBrush = CreateBrush(56, 61, 72, 0.75);
    private static readonly WpfBrush UnknownBrush = CreateBrush(139, 147, 163, 0.78);
    private static readonly WpfBrush FocusedBrush = CreateBrush(53, 211, 174);
    private static readonly WpfBrush WanderingBrush = CreateBrush(245, 184, 66);
    private static readonly WpfBrush DistractedBrush = CreateBrush(255, 104, 87);
    private static readonly WpfBrush IdleBrush = CreateBrush(193, 174, 104);

    public FocusTimelineBar()
    {
        InitializeComponent();
    }

    public bool IsMini { get; set; }

    public void UpdateTimeline(
        IReadOnlyList<TimelineSegment> segments,
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        FocusStatus currentStatus,
        bool completed = false)
    {
        var count = IsMini ? 34 : 58;
        var safePlannedSeconds = Math.Max(1, plannedDuration.TotalSeconds);
        var elapsedRatio = completed ? 1 : Math.Clamp(elapsed.TotalSeconds / safePlannedSeconds, 0, 1);
        var grid = new UniformGrid
        {
            Columns = count,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };

        for (var index = 0; index < count; index++)
        {
            var ratio = (index + 0.5) / count;
            FocusStatus? status = ratio <= elapsedRatio
                ? FindStatus(segments, TimeSpan.FromSeconds(ratio * safePlannedSeconds), currentStatus)
                : null;
            var height = IsMini ? 2d : 8d + ((index * 7) % 4);
            var bar = new Border
            {
                Height = height,
                Margin = new Thickness(IsMini ? 1 : 2, 0, IsMini ? 1 : 2, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = status.HasValue ? GetBrush(status.Value) : FutureBrush,
                CornerRadius = new CornerRadius(IsMini ? 1 : 3),
                Opacity = status.HasValue ? 1 : 0.72
            };

            if (status.HasValue && index == Math.Max(0, (int)Math.Ceiling(elapsedRatio * count) - 1))
            {
                bar.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = IsMini ? 4 : 8,
                    Color = GetColor(status.Value),
                    Opacity = 0.45,
                    ShadowDepth = 0
                };
            }

            grid.Children.Add(bar);
        }

        TimelineRoot.Children.Clear();
        TimelineRoot.Children.Add(grid);
    }

    private static FocusStatus FindStatus(
        IReadOnlyList<TimelineSegment> segments,
        TimeSpan position,
        FocusStatus fallback)
    {
        for (var index = segments.Count - 1; index >= 0; index--)
        {
            var segment = segments[index];
            if (position >= segment.Start && position <= segment.End)
            {
                return segment.Status;
            }
        }

        return fallback;
    }

    private static WpfBrush GetBrush(FocusStatus status)
    {
        return status switch
        {
            FocusStatus.Focused => FocusedBrush,
            FocusStatus.Wandering => WanderingBrush,
            FocusStatus.Distracted => DistractedBrush,
            FocusStatus.Idle => IdleBrush,
            _ => UnknownBrush
        };
    }

    private static WpfColor GetColor(FocusStatus status)
    {
        return status switch
        {
            FocusStatus.Focused => WpfColor.FromRgb(53, 211, 174),
            FocusStatus.Wandering => WpfColor.FromRgb(245, 184, 66),
            FocusStatus.Distracted => WpfColor.FromRgb(255, 104, 87),
            FocusStatus.Idle => WpfColor.FromRgb(193, 174, 104),
            _ => WpfColor.FromRgb(139, 147, 163)
        };
    }

    private static WpfBrush CreateBrush(byte red, byte green, byte blue, double opacity = 1)
    {
        var brush = new WpfSolidColorBrush(WpfColor.FromRgb(red, green, blue))
        {
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }
}

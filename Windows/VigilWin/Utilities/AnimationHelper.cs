using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VigilWin.Utilities;

public static class AnimationHelper
{
    public static void FadeInUp(FrameworkElement element, double fromY = 8, int durationMs = 300, int delayMs = 0)
    {
        EnsureTranslate(element);
        element.Opacity = 0;
        if (element.RenderTransform is TranslateTransform translate)
        {
            translate.Y = fromY;
            AnimateDouble(translate, TranslateTransform.YProperty, fromY, 0, durationMs, delayMs, EasingMode.EaseOut);
        }

        AnimateDouble(element, UIElement.OpacityProperty, 0, 1, durationMs, delayMs, EasingMode.EaseOut);
    }

    public static void FadeInScale(FrameworkElement element, double fromScale = 0.96, int durationMs = 260, int delayMs = 0)
    {
        EnsureScale(element);
        element.Opacity = 0;
        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

        if (element.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = fromScale;
            scale.ScaleY = fromScale;
            AnimateDouble(scale, ScaleTransform.ScaleXProperty, fromScale, 1, durationMs, delayMs, EasingMode.EaseOut);
            AnimateDouble(scale, ScaleTransform.ScaleYProperty, fromScale, 1, durationMs, delayMs, EasingMode.EaseOut);
        }

        AnimateDouble(element, UIElement.OpacityProperty, 0, 1, durationMs, delayMs, EasingMode.EaseOut);
    }

    public static void FadeOutUp(FrameworkElement element, Action? completed = null, double toY = -8, int durationMs = 220)
    {
        EnsureTranslate(element);
        var opacity = CreateAnimation(element.Opacity, 0, durationMs, 0, EasingMode.EaseIn);
        opacity.Completed += (_, _) => completed?.Invoke();
        element.BeginAnimation(UIElement.OpacityProperty, opacity);

        if (element.RenderTransform is TranslateTransform translate)
        {
            AnimateDouble(translate, TranslateTransform.YProperty, translate.Y, toY, durationMs, 0, EasingMode.EaseIn);
        }
    }

    public static void AnimateDouble(
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        int durationMs,
        int delayMs = 0,
        EasingMode easingMode = EasingMode.EaseOut)
    {
        var animation = CreateAnimation(from, to, durationMs, delayMs, easingMode);
        switch (target)
        {
            case UIElement element:
                element.BeginAnimation(property, animation);
                break;
            case Animatable animatable:
                animatable.BeginAnimation(property, animation);
                break;
        }
    }

    public static void RunAfterDelay(FrameworkElement element, int delayMs, Action action)
    {
        var animation = new DoubleAnimation(element.Opacity, element.Opacity, TimeSpan.FromMilliseconds(1))
        {
            BeginTime = TimeSpan.FromMilliseconds(Math.Max(0, delayMs))
        };
        animation.Completed += (_, _) => action();
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private static DoubleAnimation CreateAnimation(double from, double to, int durationMs, int delayMs, EasingMode easingMode)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(Math.Max(1, durationMs)))
        {
            BeginTime = TimeSpan.FromMilliseconds(Math.Max(0, delayMs)),
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };
    }

    private static void EnsureTranslate(FrameworkElement element)
    {
        if (element.RenderTransform is not TranslateTransform)
        {
            element.RenderTransform = new TranslateTransform();
        }
    }

    private static void EnsureScale(FrameworkElement element)
    {
        if (element.RenderTransform is not ScaleTransform)
        {
            element.RenderTransform = new ScaleTransform(1, 1);
        }
    }
}

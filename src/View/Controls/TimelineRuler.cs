using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ViewModel;

namespace View;

// Time ruler above the keyframe lanes: beat grid, second labels, and click-or-drag to scrub.
// Time maps linearly across the full width — the timeline always fits, so there is no scroll
// offset to reconcile between the ruler and the lanes below it.
public sealed class TimelineRuler : Control
{
    public static readonly StyledProperty<double> DurationSecondsProperty =
        AvaloniaProperty.Register<TimelineRuler, double>(nameof(DurationSeconds), 10);

    public static readonly StyledProperty<double> GridSecondsProperty =
        AvaloniaProperty.Register<TimelineRuler, double>(nameof(GridSeconds));

    public static readonly StyledProperty<KeyframeEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<TimelineRuler, KeyframeEditorViewModel?>(nameof(Editor));

    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#141417"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#6B6B78"));
    private static readonly IPen BeatPen = new Pen(new SolidColorBrush(Color.Parse("#2E2E38")), 1);
    private static readonly IPen BarPen = new Pen(new SolidColorBrush(Color.Parse("#3E3E4A")), 1);
    private static readonly IPen BasePen = new Pen(new SolidColorBrush(Color.Parse("#2E2E38")), 1);

    static TimelineRuler()
    {
        AffectsRender<TimelineRuler>(DurationSecondsProperty, GridSecondsProperty);
    }

    public double DurationSeconds
    {
        get => GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    public double GridSeconds
    {
        get => GetValue(GridSecondsProperty);
        set => SetValue(GridSecondsProperty, value);
    }

    public KeyframeEditorViewModel? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var duration = DurationSeconds;
        if (width <= 0 || duration <= 0) return;

        context.FillRectangle(Background, new Rect(0, 0, width, height));

        // Beat grid, with every fourth line drawn as a bar. Skipped when the lines would be too
        // dense to read.
        var grid = GridSeconds;
        if (grid > 0 && width / (duration / grid) >= 4)
        {
            var index = 0;
            for (var t = 0.0; t <= duration; t += grid, index++)
            {
                var x = Math.Round(t / duration * width) + 0.5;
                var isBar = index % 4 == 0;
                context.DrawLine(isBar ? BarPen : BeatPen,
                    new Point(x, isBar ? height * 0.35 : height * 0.6), new Point(x, height));
            }
        }

        // Second labels, thinned so they never overlap.
        var step = Math.Max(1, Math.Ceiling(duration / Math.Max(1, width / 56)));
        for (var t = 0.0; t <= duration; t += step)
        {
            var x = t / duration * width;
            var text = new FormattedText($"{t:0.#}s", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 9, LabelBrush);
            context.DrawText(text, new Point(Math.Min(x + 3, width - text.Width - 2), 2));
        }

        context.DrawLine(BasePen, new Point(0, height - 0.5), new Point(width, height - 0.5));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Pointer.Capture(this);
        ScrubTo(e.GetPosition(this).X);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Equals(e.Pointer.Captured, this)) ScrubTo(e.GetPosition(this).X);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
    }

    private void ScrubTo(double x)
    {
        if (Editor is not { } editor || Bounds.Width <= 0) return;
        editor.SeekToFraction(Math.Clamp(x / Bounds.Width, 0, 1));
    }
}

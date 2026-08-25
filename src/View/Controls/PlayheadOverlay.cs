using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace View;

// The playhead line, drawn over the ruler and every lane. It lives outside the scrolling track
// area and is positioned by fraction rather than pixels, so it needs no knowledge of the timeline's
// contents — and moving it at frame rate redraws one thin control instead of every row.
public sealed class PlayheadOverlay : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<PlayheadOverlay, double>(nameof(Fraction));

    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#E0574B"));
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.Parse("#E0574B")), 1);

    static PlayheadOverlay()
    {
        AffectsRender<PlayheadOverlay>(FractionProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0) return;

        var x = Math.Round(Math.Clamp(Fraction, 0, 1) * width) + 0.5;
        context.DrawLine(Line, new Point(x, 0), new Point(x, height));

        // A small head so the playhead is findable when the line sits over a bright band.
        var head = new StreamGeometry();
        using (var draw = head.Open())
        {
            draw.BeginFigure(new Point(x - 4, 0), isFilled: true);
            draw.LineTo(new Point(x + 4, 0));
            draw.LineTo(new Point(x, 6));
            draw.EndFigure(isClosed: true);
        }

        context.DrawGeometry(Accent, null, head);
    }
}

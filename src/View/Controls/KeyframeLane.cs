using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Model;
using ViewModel;

namespace View;

// One track's row of the timeline: the value it holds across the sequence, drawn as a band, with
// its keys as draggable diamonds. Custom-drawn rather than built from controls because a track can
// carry hundreds of keys, and a visual per key does not scale.
//
// The band is filled from the keys' own colours, so a colour track reads as the colour ramp it is
// and a dimmer track as the fade it is.
public sealed class KeyframeLane : Control
{
    public static readonly StyledProperty<KeyframeTrackViewModel?> TrackProperty =
        AvaloniaProperty.Register<KeyframeLane, KeyframeTrackViewModel?>(nameof(Track));

    public static readonly StyledProperty<double> DurationSecondsProperty =
        AvaloniaProperty.Register<KeyframeLane, double>(nameof(DurationSeconds), 10);

    public static readonly StyledProperty<double> GridSecondsProperty =
        AvaloniaProperty.Register<KeyframeLane, double>(nameof(GridSeconds));

    // Bumped by the track view model on any change to its keys. Re-rendering on one integer is far
    // simpler than subscribing to every key's every property.
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<KeyframeLane, int>(nameof(Revision));

    private const double HitRadius = 7;

    private static readonly IBrush LaneBrush = new SolidColorBrush(Color.Parse("#1C1C21"));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#242429")), 1);
    private static readonly IPen HoldPen =
        new Pen(new SolidColorBrush(Color.Parse("#6B6B78")), 1, new DashStyle([3, 3], 0));
    // A key is filled with its own value, so a fixed outline colour disappears whenever the value
    // approaches it — a black outline vanished on a dimmer at zero. Both are kept and chosen per
    // key by the fill's brightness, so the marker has an edge against anything it can be filled with.
    private static readonly IPen OutlineOnDark = new Pen(new SolidColorBrush(Color.Parse("#E8E8EE")), 1.5);
    private static readonly IPen OutlineOnLight = new Pen(new SolidColorBrush(Color.Parse("#101014")), 1.5);

    // Selection reads as a ring outside the marker, so it stays legible whatever the fill is.
    private static readonly IPen SelectionRing = new Pen(new SolidColorBrush(Color.Parse("#9B7BD4")), 1.5);

    private KeyframeViewModel? _dragging;

    static KeyframeLane()
    {
        AffectsRender<KeyframeLane>(
            TrackProperty, DurationSecondsProperty, GridSecondsProperty, RevisionProperty);
    }

    public KeyframeTrackViewModel? Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
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

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var duration = DurationSeconds;
        if (width <= 0 || height <= 0 || duration <= 0) return;

        context.FillRectangle(LaneBrush, new Rect(0, 0, width, height));

        var grid = GridSeconds;
        if (grid > 0 && width / (duration / grid) >= 4)
            for (var t = 0.0; t <= duration; t += grid)
            {
                var x = Math.Round(t / duration * width) + 0.5;
                context.DrawLine(GridPen, new Point(x, 0), new Point(x, height));
            }

        if (Track is not { } track || track.Keys.Count == 0) return;

        var keys = track.Keys;
        var top = height * 0.28;
        var bandHeight = height * 0.44;
        var mid = height / 2;

        // Before the first key and after the last one the track holds that key's value, so the
        // band runs the full width — the row shows everything the track contributes, not just the
        // span between its outermost keys.
        var firstX = X(keys[0], duration, width);
        var lastX = X(keys[^1], duration, width);

        if (firstX > 0)
            context.FillRectangle(Flat(keys[0]), new Rect(0, top, firstX, bandHeight));

        if (lastX < width)
            context.FillRectangle(Flat(keys[^1]), new Rect(lastX, top, width - lastX, bandHeight));

        for (var i = 0; i < keys.Count - 1; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            var x1 = X(a, duration, width);
            var x2 = X(b, duration, width);
            if (x2 <= x1) continue;

            var rect = new Rect(x1, top, x2 - x1, bandHeight);

            if (a.Interpolation == Interpolation.Hold)
            {
                context.FillRectangle(Flat(a), rect);
                context.DrawLine(HoldPen, new Point(x1, mid), new Point(x2, mid));
            }
            else
            {
                context.FillRectangle(Ramp(a, b), rect);
            }
        }

        foreach (var key in keys)
            DrawKey(context, X(key, duration, width), mid, key);
    }

    private static void DrawKey(DrawingContext context, double x, double y, KeyframeViewModel key)
    {
        if (key.IsSelected)
            context.DrawGeometry(null, SelectionRing, Diamond(x, y, 8));

        context.DrawGeometry(new SolidColorBrush(key.Swatch), Outline(key.Swatch), Diamond(x, y, 5));
    }

    private static StreamGeometry Diamond(double x, double y, double r)
    {
        var diamond = new StreamGeometry();

        using (var draw = diamond.Open())
        {
            draw.BeginFigure(new Point(x, y - r), isFilled: true);
            draw.LineTo(new Point(x + r, y));
            draw.LineTo(new Point(x, y + r));
            draw.LineTo(new Point(x - r, y));
            draw.EndFigure(isClosed: true);
        }

        return diamond;
    }

    // Perceived brightness, so a mid-green counts as light and a mid-blue as dark.
    private static IPen Outline(Color fill) =>
        0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B < 128 ? OutlineOnDark : OutlineOnLight;

    private static double X(KeyframeViewModel key, double duration, double width) =>
        Math.Clamp(key.TimeSeconds / duration, 0, 1) * width;

    private static IBrush Flat(KeyframeViewModel key) =>
        new SolidColorBrush(key.Swatch, 0.75);

    private static IBrush Ramp(KeyframeViewModel from, KeyframeViewModel to) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        Opacity = 0.75,
        GradientStops =
        {
            new GradientStop(from.Swatch, 0),
            new GradientStop(to.Swatch, 1)
        }
    };

    // Clicking a key selects it and starts a drag; clicking anywhere else clears the selection and
    // parks the playhead there, so the row doubles as a scrub target.
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Track is not { } track || Bounds.Width <= 0 || DurationSeconds <= 0) return;

        var x = e.GetPosition(this).X;
        var hit = track.Keys
            .Select(k => (Key: k, Distance: Math.Abs(X(k, DurationSeconds, Bounds.Width) - x)))
            .Where(candidate => candidate.Distance <= HitRadius)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Key)
            .FirstOrDefault();

        track.SelectKey(hit);

        if (hit is null)
        {
            track.ScrubTo(TimeSpan.FromSeconds(x / Bounds.Width * DurationSeconds));
            return;
        }

        _dragging = hit;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging is null || Track is not { } track || Bounds.Width <= 0) return;

        track.DragKeyTo(_dragging, e.GetPosition(this).X / Bounds.Width * DurationSeconds);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = null;
        e.Pointer.Capture(null);
    }
}

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ViewModel;

namespace View;

// A vertical fader bound to a CapabilityParameter (its DataContext). The thumb/fill show the
// manual layer; the green indicator shows the cue (playback) value being fed in.
public partial class LevelFader : UserControl
{
    private CapabilityParameter? _param;

    // Optional label drawn inside the track (e.g. "R").
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<LevelFader, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // Optional fill colour for the manual level (defaults to the neutral theme fill when unset).
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<LevelFader, IBrush?>(nameof(Accent));

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public LevelFader()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Track.SizeChanged += (_, _) => UpdateVisuals();
        Track.PointerPressed += OnPointerPressed;
        Track.PointerMoved += OnPointerMoved;

        // Applied after attach so the fill brush isn't re-clobbered by theme resource resolution.
        Loaded += (_, _) => ApplyAppearance();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (IsLoaded && (change.Property == LabelProperty || change.Property == AccentProperty))
            ApplyAppearance();
    }

    // Label plus, only when the fader has an Accent, its colours: the fill (below the handle) is the
    // full colour and the track (above the handle) a dimmed version. Applied after the theme's
    // DynamicResource brushes have resolved, so it overrides them. Default faders are left untouched.
    private void ApplyAppearance()
    {
        LabelText.Text = Label;

        if (Accent is ISolidColorBrush accent)
        {
            Fill.Fill = accent;
            Track.Background = Dim(accent.Color, 0.28);
        }
    }

    private static SolidColorBrush Dim(Color c, double factor) =>
        new(Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor)));

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_param is not null) _param.PropertyChanged -= OnParamChanged;
        _param = DataContext as CapabilityParameter;
        if (_param is not null) _param.PropertyChanged += OnParamChanged;
        UpdateVisuals();
    }

    private void OnParamChanged(object? sender, PropertyChangedEventArgs e) => UpdateVisuals();

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        SetFromPointer(e.GetPosition(Track).Y);

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(Track).Properties.IsLeftButtonPressed)
            SetFromPointer(e.GetPosition(Track).Y);
    }

    private void SetFromPointer(double y)
    {
        if (_param is null) return;
        var height = Track.Bounds.Height;
        if (height <= 0) return;

        var fraction = Math.Clamp(1 - y / height, 0, 1);
        _param.Manual = (int)Math.Round(fraction * _param.Max);
    }

    private void UpdateVisuals()
    {
        if (_param is null) return;

        var height = Track.Bounds.Height;
        var width = Track.Bounds.Width;

        Fill.Width = width;
        Thumb.Width = width;
        Indicator.Width = width;

        var manualY = (1 - _param.ManualFraction) * height;
        Canvas.SetTop(Fill, manualY);
        Fill.Height = Math.Max(0, height - manualY);
        Canvas.SetTop(Thumb, Clamp(manualY - Thumb.Height / 2, height, Thumb.Height));

        Indicator.IsVisible = _param.PlaybackActive;
        var playbackY = (1 - _param.PlaybackFraction) * height;
        Canvas.SetTop(Indicator, Clamp(playbackY - Indicator.Height / 2, height, Indicator.Height));
    }

    // Keeps a marker of the given thickness fully within the track.
    private static double Clamp(double top, double trackHeight, double markHeight) =>
        Math.Clamp(top, 0, Math.Max(0, trackHeight - markHeight));
}

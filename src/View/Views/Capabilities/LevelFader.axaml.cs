using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using ViewModel;

namespace View;

// A vertical fader bound to a CapabilityParameter (its DataContext). The thumb/fill show the
// manual layer; the green indicator shows the cue (playback) value being fed in.
public partial class LevelFader : UserControl
{
    private CapabilityParameter? _param;

    public LevelFader()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Track.SizeChanged += (_, _) => UpdateVisuals();
        Track.PointerPressed += OnPointerPressed;
        Track.PointerMoved += OnPointerMoved;
    }

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

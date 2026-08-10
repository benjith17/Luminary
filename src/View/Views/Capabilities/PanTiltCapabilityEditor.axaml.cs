using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ViewModel;

namespace View;

public partial class PanTiltCapabilityEditor : UserControl
{
    public PanTiltCapabilityEditor()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is PanTiltCapabilityViewModel vm)
            {
                vm.Pan.PropertyChanged  += (_, _) => UpdateDots();
                vm.Tilt.PropertyChanged += (_, _) => UpdateDots();
                UpdateDots();
            }
        };

        Pad.SizeChanged += (_, _) => UpdateDots();
        Pad.PointerPressed += OnPointerPressed;
        Pad.PointerMoved += OnPointerMoved;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        UpdateFromPointer(e.GetPosition(Pad));

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(Pad).Properties.IsLeftButtonPressed)
            UpdateFromPointer(e.GetPosition(Pad));
    }

    private void UpdateFromPointer(Point pos)
    {
        if (DataContext is not PanTiltCapabilityViewModel vm) return;
        vm.Pan.Manual  = (int)Math.Clamp(pos.X / Pad.Bounds.Width  * vm.Pan.Max,  0, vm.Pan.Max);
        vm.Tilt.Manual = (int)Math.Clamp(pos.Y / Pad.Bounds.Height * vm.Tilt.Max, 0, vm.Tilt.Max);
    }

    private void UpdateDots()
    {
        if (DataContext is not PanTiltCapabilityViewModel vm) return;
        var w = Pad.Bounds.Width  - Dot.Width;
        var h = Pad.Bounds.Height - Dot.Height;

        Canvas.SetLeft(Dot, vm.Pan.ManualFraction  * w);
        Canvas.SetTop(Dot,  vm.Tilt.ManualFraction * h);

        // Cue value being fed in.
        Ghost.IsVisible = vm.Pan.PlaybackActive || vm.Tilt.PlaybackActive;
        Canvas.SetLeft(Ghost, vm.Pan.PlaybackFraction  * w);
        Canvas.SetTop(Ghost,  vm.Tilt.PlaybackFraction * h);
    }
}

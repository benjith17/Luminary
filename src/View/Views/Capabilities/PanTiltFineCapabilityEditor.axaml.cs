using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ViewModel;

namespace View;

public partial class PanTiltFineCapabilityEditor : UserControl
{
    public PanTiltFineCapabilityEditor()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is PanTiltFineCapabilityViewModel vm)
            {
                vm.PropertyChanged += (_, _) => UpdateDot();
                UpdateDot();
            }
        };

        Pad.SizeChanged += (_, _) => UpdateDot();
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
        if (DataContext is not PanTiltFineCapabilityViewModel vm) return;
        vm.Pan  = (ushort)Math.Clamp(pos.X / Pad.Bounds.Width  * 65535, 0, 65535);
        vm.Tilt = (ushort)Math.Clamp(pos.Y / Pad.Bounds.Height * 65535, 0, 65535);
    }

    private void UpdateDot()
    {
        if (DataContext is not PanTiltFineCapabilityViewModel vm) return;
        var w = Pad.Bounds.Width  - Dot.Width;
        var h = Pad.Bounds.Height - Dot.Height;
        Canvas.SetLeft(Dot, vm.Pan  / 65535.0 * w);
        Canvas.SetTop(Dot,  vm.Tilt / 65535.0 * h);
    }
}

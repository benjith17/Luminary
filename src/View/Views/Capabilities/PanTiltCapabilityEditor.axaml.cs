using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
        if (DataContext is not PanTiltCapabilityViewModel vm) return;
        vm.Pan  = (byte)Math.Clamp(pos.X / Pad.Bounds.Width  * 255, 0, 255);
        vm.Tilt = (byte)Math.Clamp(pos.Y / Pad.Bounds.Height * 255, 0, 255);
    }

    private void UpdateDot()
    {
        if (DataContext is not PanTiltCapabilityViewModel vm) return;
        var w = Pad.Bounds.Width  - Dot.Width;
        var h = Pad.Bounds.Height - Dot.Height;
        Canvas.SetLeft(Dot, vm.Pan  / 255.0 * w);
        Canvas.SetTop(Dot,  vm.Tilt / 255.0 * h);
    }
}

using Avalonia.Controls;
using ViewModel;

namespace View;

public partial class MidiMonitorWindow : Window
{
    public MidiMonitorWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        // Devices open asynchronously at startup, so refresh the list when the window appears.
        (DataContext as MidiMonitorViewModel)?.RefreshDevices();
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using ViewModel;

namespace View;

public partial class MainWindow : Window
{
    private ConfigWindow? _configWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnPatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Reuse the existing window if it's already open.
        if (_configWindow is not null)
        {
            _configWindow.Activate();
            return;
        }

        _configWindow = new ConfigWindow { DataContext = vm.FixturesListPanel };
        _configWindow.Closed += (_, _) => _configWindow = null;
        _configWindow.Show(this);
    }
}

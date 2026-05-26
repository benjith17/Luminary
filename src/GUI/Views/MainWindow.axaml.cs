using Avalonia.Controls;
using Avalonia.Input;
using GUI.ViewModels;

namespace GUI.Views;

public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNewItemKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
            vm.AddItemCommand.Execute(null);
    }
}

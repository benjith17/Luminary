using Avalonia.Controls;
using Avalonia.Interactivity;
using ViewModel;

namespace View;

// Shown after a show loads when fixtures could not be resolved, or a library failed to read.
public partial class FixtureIssuesDialog : Window
{
    public FixtureIssuesDialog()
    {
        InitializeComponent();
    }

    public FixtureIssuesDialog(FixtureIssuesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}

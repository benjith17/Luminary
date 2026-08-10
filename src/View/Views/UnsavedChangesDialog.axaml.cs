using Avalonia.Controls;
using Avalonia.Interactivity;

namespace View;

// Cancel is first so closing the dialog via the title bar (default result) is treated as Cancel.
public enum UnsavedChangesResult
{
    Cancel,
    Save,
    Discard
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Cancel);
    private void OnDiscard(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Discard);
    private void OnSave(object? sender, RoutedEventArgs e) => Close(UnsavedChangesResult.Save);
}

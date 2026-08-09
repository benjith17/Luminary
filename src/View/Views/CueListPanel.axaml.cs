using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ViewModel;

namespace View;

public partial class CueListPanel : UserControl
{
    public CueListPanel()
    {
        InitializeComponent();
    }

    private void OnCueDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Handled at the ListBox level so a double-tap anywhere on the row triggers rename.
        if (sender is not ListBox { SelectedItem: CueViewModel cue } listBox) return;

        cue.IsEditing = true;

        // The TextBox is only realised once IsEditing flips it visible, so focus after layout.
        Dispatcher.UIThread.Post(() =>
        {
            var container = listBox.ContainerFromItem(cue);
            var textBox = container?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (textBox is null) return;
            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape && sender is TextBox tb)
        {
            EndRename(tb);
            e.Handled = true;
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) EndRename(tb);
    }

    private static void EndRename(TextBox tb)
    {
        if (tb.DataContext is CueViewModel cue) cue.IsEditing = false;
    }
}

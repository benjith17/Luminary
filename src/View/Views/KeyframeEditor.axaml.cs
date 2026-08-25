using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ViewModel;

namespace View;

public partial class KeyframeEditor : UserControl
{
    public KeyframeEditor()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // Clicking a track's name block selects that track. The add-key button inside the header marks
    // the event handled, so pressing it doesn't also run this.
    private void OnTrackHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: KeyframeTrackViewModel track })
            track.SelectSelfCommand.Execute(null);
    }
}

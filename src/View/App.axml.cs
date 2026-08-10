using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ViewModel;

namespace View;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterFocusBehaviours();
    }

    // Text boxes don't drop focus on their own, so a field stays "in edit" after you're done with it.
    // These global handlers make Enter and clicking elsewhere behave the way people expect.
    private static void RegisterFocusBehaviours()
    {
        // Enter unfocuses a single-line text box (committing its binding).
        InputElement.KeyDownEvent.AddClassHandler<TextBox>((box, e) =>
        {
            if (e.Key == Key.Enter && !box.AcceptsReturn)
            {
                TopLevel.GetTopLevel(box)?.FocusManager?.Focus(null);
                e.Handled = true;
            }
        });

        // Pressing anywhere outside the focused text box drops its focus.
        InputElement.PointerPressedEvent.AddClassHandler<TopLevel>((top, e) =>
        {
            if (top.FocusManager?.GetFocusedElement() is TextBox box &&
                e.Source is Visual source && box != source && !box.IsVisualAncestorOf(source))
            {
                top.FocusManager.Focus(null);
            }
        }, RoutingStrategies.Tunnel);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
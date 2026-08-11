using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Settings;
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
            // Show the splash immediately, then build the (heavier) main window at a lower priority
            // so the splash paints first. The splash closes once the main window is loaded AND a
            // minimum display time has elapsed, whichever is later — so it never just flashes.
            const long minSplashMs = 1000;
            var clock = Stopwatch.StartNew();

            var splash = new SplashWindow();
            splash.Show();

            // Machine-local settings (keybindings, …) — loaded once, shared across every show.
            var settings = new SettingsService();

            Dispatcher.UIThread.Post(() =>
            {
                var window = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                    Settings = settings
                };
                desktop.MainWindow = window;
                window.Loaded += (_, _) =>
                {
                    var remaining = TimeSpan.FromMilliseconds(System.Math.Max(0, minSplashMs - clock.ElapsedMilliseconds));
                    DispatcherTimer.RunOnce(splash.Close, remaining);
                };
                window.Show();
            }, DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
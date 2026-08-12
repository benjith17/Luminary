using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Settings;
using ViewModel;

namespace View;

public partial class MainWindow : Window
{
    private ConfigWindow? _configWindow;
    private MacrosWindow? _macrosWindow;
    private BindingsWindow? _bindingsWindow;
    private MidiMonitorWindow? _midiWindow;
    private bool _forceClose;
    private bool _keybindsAttached;

    // App-level settings, set by the app before the window is shown (null at design time).
    public SettingsService? Settings { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AttachKeybindings();
    }

    // Wires configured gestures to their actions. Tunnelling, so a bound key beats a focused control;
    // the controller itself skips keys while a text field is focused.
    private void AttachKeybindings()
    {
        if (_keybindsAttached || Settings is null || DataContext is not MainWindowViewModel vm) return;
        _keybindsAttached = true;

        // CloseWindow is intentionally absent here: Ctrl/Cmd+W closes the tool windows, not the main one.
        var controller = new KeybindingController(Settings, new Dictionary<Keybind, Action>
        {
            [Keybind.Go]                = () => vm.CueListPanel?.GoSelected(),
            [Keybind.SelectPreviousCue] = () => vm.CueListPanel?.SelectPrevious(),
            [Keybind.SelectNextCue]     = () => vm.CueListPanel?.SelectNext(),
            [Keybind.Blackout]          = () => vm.Blackout = !vm.Blackout,
            [Keybind.Patch]             = OpenPatch,
            [Keybind.Fullscreen]        = ToggleFullscreen,
        });

        AddHandler(KeyDownEvent, controller.HandleKeyDown, RoutingStrategies.Tunnel);
    }

    private void ToggleFullscreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    // Attaches the "close window" gesture (Ctrl/Cmd+W) to a tool window so it closes on the keybind.
    private void AttachCloseKeybind(Window window)
    {
        if (Settings is null) return;

        var controller = new KeybindingController(Settings, new Dictionary<Keybind, Action>
        {
            [Keybind.CloseWindow] = () => window.Close()
        });
        window.AddHandler(KeyDownEvent, controller.HandleKeyDown, RoutingStrategies.Tunnel);
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose) return;
        if (DataContext is not MainWindowViewModel vm || !vm.IsDirty) return;

        // Cancel this close and decide asynchronously; re-close ourselves if the user proceeds.
        e.Cancel = true;
        if (await ConfirmDiscardAsync(vm))
        {
            _forceClose = true;
            Close();
        }
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!await ConfirmDiscardAsync(vm)) return;

        CloseToolWindows();
        vm.New();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!await ConfirmDiscardAsync(vm)) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Show",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Luminary Show") { Patterns = ["*.lum"] }]
        });
        if (files.Count == 0) return;

        CloseToolWindows();
        try
        {
            vm.Open(files[0].Path.LocalPath);
        }
        catch
        {
            // Invalid or unreadable file — keep the current show rather than crashing.
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) await SaveAsync(vm);
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) await SaveAsPickerAsync(vm);
    }

    // Saves to the current path, or prompts for one. Returns false if the user cancelled.
    private async Task<bool> SaveAsync(MainWindowViewModel vm)
    {
        if (vm.CurrentPath is { } path)
        {
            vm.Save(path);
            return true;
        }
        return await SaveAsPickerAsync(vm);
    }

    private async Task<bool> SaveAsPickerAsync(MainWindowViewModel vm)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Show",
            SuggestedFileName = "show",
            DefaultExtension = "lum",
            FileTypeChoices = [new FilePickerFileType("Luminary Show") { Patterns = ["*.lum"] }]
        });
        if (file is null) return false;

        vm.Save(file.Path.LocalPath);
        return true;
    }

    // Prompts about unsaved changes. Returns true if it's safe to proceed (saved or discarded),
    // false if the user cancelled.
    private async Task<bool> ConfirmDiscardAsync(MainWindowViewModel vm)
    {
        if (!vm.IsDirty) return true;

        var result = await new UnsavedChangesDialog().ShowDialog<UnsavedChangesResult>(this);
        return result switch
        {
            UnsavedChangesResult.Save => await SaveAsync(vm),
            UnsavedChangesResult.Discard => true,
            _ => false
        };
    }

    private void OnPatchClick(object? sender, RoutedEventArgs e) => OpenPatch();

    private void OpenPatch()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_configWindow is not null)
        {
            _configWindow.Activate();
            return;
        }

        _configWindow = new ConfigWindow { DataContext = vm.FixturesListPanel };
        _configWindow.Closed += (_, _) => _configWindow = null;
        AttachCloseKeybind(_configWindow);
        _configWindow.Show(this);
    }

    private void OnMacrosClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_macrosWindow is not null)
        {
            _macrosWindow.Activate();
            return;
        }

        _macrosWindow = new MacrosWindow { DataContext = vm.MacrosPanel };
        _macrosWindow.Closed += (_, _) =>
        {
            vm.MacrosPanel?.StopRun(); // don't let a run outlive its window
            _macrosWindow = null;
        };
        AttachCloseKeybind(_macrosWindow);
        _macrosWindow.Show(this);
    }

    private void OnBindingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_bindingsWindow is not null)
        {
            _bindingsWindow.Activate();
            return;
        }

        _bindingsWindow = new BindingsWindow { DataContext = vm.BindingsPanel };
        _bindingsWindow.Closed += (_, _) =>
        {
            vm.BindingsPanel?.StopListening(); // don't keep capturing MIDI after close
            _bindingsWindow = null;
        };
        AttachCloseKeybind(_bindingsWindow);
        _bindingsWindow.Show(this);
    }

    private void OnMidiClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_midiWindow is not null)
        {
            _midiWindow.Activate();
            return;
        }

        _midiWindow = new MidiMonitorWindow { DataContext = vm.MidiMonitor };
        _midiWindow.Closed += (_, _) => _midiWindow = null;
        AttachCloseKeybind(_midiWindow);
        _midiWindow.Show(this);
    }

    // Closes the non-modal tool windows (used before swapping the show on New / Open).
    private void CloseToolWindows()
    {
        _configWindow?.Close();
        _configWindow = null;
        _macrosWindow?.Close();
        _macrosWindow = null;
        _bindingsWindow?.Close();
        _bindingsWindow = null;
    }
}

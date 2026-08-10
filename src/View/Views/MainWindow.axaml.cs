using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ViewModel;

namespace View;

public partial class MainWindow : Window
{
    private ConfigWindow? _configWindow;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
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

        CloseConfigWindow();
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

        CloseConfigWindow();
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

    private void OnPatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (_configWindow is not null)
        {
            _configWindow.Activate();
            return;
        }

        _configWindow = new ConfigWindow { DataContext = vm.FixturesListPanel };
        _configWindow.Closed += (_, _) => _configWindow = null;
        _configWindow.Show(this);
    }

    private void CloseConfigWindow()
    {
        _configWindow?.Close();
        _configWindow = null;
    }
}

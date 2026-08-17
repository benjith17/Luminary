using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Macros;
using Model;

namespace ViewModel;

// Backing for the Macros window: the list of saved macros, an editor for the selected one, and
// Run/Stop over the shared MacroHost. Parse errors block a run; runtime problems are reported after.
public partial class MacrosWindowViewModel : ViewModelBase
{
    private readonly ShowService _show;
    private readonly MacroHost _host;
    private CancellationTokenSource? _cts;

    public ObservableCollection<MacroItemViewModel> Macros { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveMacroCommand))]
    public partial MacroItemViewModel? SelectedMacro { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    // Status / diagnostics shown under the editor.
    [ObservableProperty]
    public partial string Output { get; set; } = "";

    public MacrosWindowViewModel(ShowService show, MacroHost host)
    {
        _show = show;
        _host = host;
        Macros = new ObservableCollection<MacroItemViewModel>(show.Macros.Select(m => new MacroItemViewModel(m, host)));
        SelectedMacro = Macros.FirstOrDefault();
    }

    [RelayCommand]
    private void AddMacro()
    {
        var macro = new Macro { Name = UniqueName("Macro"), Source = "" };
        _show.Macros.Add(macro);
        var vm = new MacroItemViewModel(macro, _host);
        Macros.Add(vm);
        SelectedMacro = vm;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveMacro()
    {
        if (SelectedMacro is null) return;

        var index = Macros.IndexOf(SelectedMacro);
        _show.Macros.Remove(SelectedMacro.Model);
        Macros.RemoveAt(index);
        SelectedMacro = Macros.Count == 0 ? null : Macros[System.Math.Min(index, Macros.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task Run()
    {
        if (SelectedMacro is null) return;

        var program = Parser.Parse(SelectedMacro.Source);
        if (program.HasErrors)
        {
            Output = Format("Not run — fix these first:", program.Diagnostics);
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        Output = "Running…";
        try
        {
            // A manual run has no driving input, so `$` resolves to 0 (with a diagnostic).
            var result = await MacroInterpreter.RunAsync(program, _host, MacroInput.None, _cts.Token);

            if (!result.Completed)
            {
                _host.CancelFades(); // Stop freezes in-progress manual fades where they are
                Output = "Stopped.";
            }
            else
            {
                Output = result.Diagnostics.Count == 0
                    ? "Done."
                    : Format("Completed with issues:", result.Diagnostics);
            }
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _cts?.Cancel();

    // Called when the Macros window closes, so the editor's run can't outlive its window. Panel-
    // triggered macros are intentionally left running — they live in the main window.
    public void StopRun() => _cts?.Cancel();

    // Stop the editor run and every panel-triggered macro (used when the show itself is swapped).
    public void StopAll()
    {
        StopRun();
        foreach (var macro in Macros) macro.StopRun();
    }

    private bool HasSelection() => SelectedMacro is not null;
    private bool CanRun() => !IsRunning && SelectedMacro is not null;

    private static string Format(string header, IReadOnlyList<Diagnostic> diagnostics) =>
        header + "\n" + string.Join("\n", diagnostics.Select(d => "  " + d));

    private string UniqueName(string baseName)
    {
        if (_show.Macros.All(m => m.Name != baseName)) return baseName;
        var n = 2;
        while (_show.Macros.Any(m => m.Name == $"{baseName} {n}")) n++;
        return $"{baseName} {n}";
    }
}

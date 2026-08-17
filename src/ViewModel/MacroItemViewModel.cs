using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Macros;
using Model;

namespace ViewModel;

// One saved macro in the Macros window: an editable name and source. Edits write straight through
// to the model so the show's dirty-tracking (serialization compare) notices them. Also owns the
// per-macro run state used by the triggers panel — its own cancellation token, so each macro runs
// and stops independently of the editor's transport and of the other macros.
public partial class MacroItemViewModel : ViewModelBase
{
    private readonly Macro _macro;
    private readonly MacroHost _host;
    private CancellationTokenSource? _cts;

    public MacroItemViewModel(Macro macro, MacroHost host)
    {
        _macro = macro;
        _host = host;
    }

    public Macro Model => _macro;

    public string Name
    {
        get => _macro.Name;
        set => SetProperty(_macro.Name, value, _macro, (m, v) => m.Name = v);
    }

    public string Source
    {
        get => _macro.Source;
        set
        {
            if (!SetProperty(_macro.Source, value, _macro, (m, v) => m.Source = v)) return;
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(CanTrigger));
        }
    }

    // True when the macro parses cleanly. The triggers panel disables the run button for invalid
    // macros; the Macros editor is where the actual diagnostics are surfaced.
    public bool IsValid => !Parser.Parse(_macro.Source).HasErrors;

    // True while this macro is executing. Flips the triggers panel's button from play to stop.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTrigger))]
    public partial bool IsRunning { get; set; }

    // The button stays clickable while running (to stop it), otherwise only when the macro is valid.
    public bool CanTrigger => IsRunning || IsValid;

    // Triggered from the panel button: starts the macro if idle, stops it if already running. A
    // manual trigger has no driving input, so `$` resolves to 0. Fire-and-forget from the caller's
    // view — it owns its own cancellation token and clears IsRunning when done.
    [RelayCommand]
    private async Task Trigger()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
            return;
        }

        var program = Parser.Parse(_macro.Source);
        if (program.HasErrors) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            var result = await MacroInterpreter.RunAsync(program, _host, MacroInput.None, _cts.Token);
            if (!result.Completed) _host.CancelFades(); // Stop freezes in-progress manual fades where they are
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // Called when the show is swapped or the window closes, so a run can't outlive its context.
    public void StopRun() => _cts?.Cancel();
}

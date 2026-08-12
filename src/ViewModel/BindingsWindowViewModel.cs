using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Midi;
using Model;

namespace ViewModel;

// Backing for the Bindings window: the per-show list of trigger→macro bindings, editing of the
// selected one, and MIDI learn. Any change calls back to rebuild the live dispatcher so edits take
// effect immediately.
public partial class BindingsWindowViewModel : ViewModelBase
{
    private readonly ShowService _show;
    private readonly IMidiInputService _midi;
    private readonly Action _onChanged;

    public ObservableCollection<BindingItemViewModel> Bindings { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveBindingCommand))]
    [NotifyCanExecuteChangedFor(nameof(LearnCommand))]
    public partial BindingItemViewModel? SelectedBinding { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LearnLabel))]
    public partial bool IsLearning { get; set; }

    public string LearnLabel => IsLearning ? "Listening…  move a control" : "MIDI Learn";

    public BindingsWindowViewModel(ShowService show, IMidiInputService midi, Action onChanged)
    {
        _show = show;
        _midi = midi;
        _onChanged = onChanged;
        Bindings = new ObservableCollection<BindingItemViewModel>(show.Bindings.Select(Wrap));
        SelectedBinding = Bindings.FirstOrDefault();
    }

    private BindingItemViewModel Wrap(Binding binding)
    {
        var vm = new BindingItemViewModel(binding);
        vm.Changed += _onChanged;
        return vm;
    }

    [RelayCommand]
    private void AddBinding()
    {
        // Default to a MIDI trigger so Learn is right there; switch Type to "key" for a keyboard binding.
        var binding = new Binding { Name = "Binding", Action = "", Trigger = new BindingTrigger { Kind = "midi" } };
        _show.Bindings.Add(binding);
        var vm = Wrap(binding);
        Bindings.Add(vm);
        SelectedBinding = vm;
        _onChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveBinding()
    {
        if (SelectedBinding is null) return;

        var index = Bindings.IndexOf(SelectedBinding);
        _show.Bindings.Remove(SelectedBinding.Model);
        Bindings.RemoveAt(index);
        SelectedBinding = Bindings.Count == 0 ? null : Bindings[Math.Min(index, Bindings.Count - 1)];
        _onChanged();
    }

    // Toggle MIDI learn: the next inbound message fills the selected binding's trigger.
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Learn()
    {
        if (IsLearning) { StopListening(); return; }
        if (SelectedBinding is null) return;

        IsLearning = true;
        _midi.MessageReceived += OnLearnMessage;
    }

    // Stop listening for a learn capture (also called when the window closes / selection changes).
    public void StopListening()
    {
        if (!IsLearning) return;
        IsLearning = false;
        _midi.MessageReceived -= OnLearnMessage;
    }

    private void OnLearnMessage(MidiMessage m)
    {
        // Arrives on the MIDI thread — marshal before touching VM state.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsLearning || SelectedBinding is null) return;
            SelectedBinding.CaptureMidi(m.Device, m.Channel, m.Kind == MidiMessageKind.NoteOn ? "note" : "cc", m.Number);
            StopListening();
        });
    }

    private bool HasSelection() => SelectedBinding is not null;

    partial void OnSelectedBindingChanged(BindingItemViewModel? value) => StopListening();
}

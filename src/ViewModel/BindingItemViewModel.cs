using System;
using System.Linq;
using Macros;
using Model;

namespace ViewModel;

// One per-show binding in the Bindings window: its trigger (key or MIDI) and macro action. Edits
// write through to the model and raise Changed so the owner can rebuild the live dispatcher.
public partial class BindingItemViewModel : ViewModelBase
{
    private readonly Binding _binding;

    public event Action? Changed;

    public BindingItemViewModel(Binding binding) => _binding = binding;

    public Binding Model => _binding;

    public string[] Kinds { get; } = ["key", "midi"];
    public string[] MessageKinds { get; } = ["cc", "note"];

    public string Name
    {
        get => _binding.Name;
        set { if (SetProperty(_binding.Name, value, _binding, (b, v) => b.Name = v)) Touched(); }
    }

    public string Kind
    {
        get => _binding.Trigger.Kind;
        set
        {
            if (!SetProperty(_binding.Trigger.Kind, value, _binding.Trigger, (t, v) => t.Kind = v)) return;
            OnPropertyChanged(nameof(IsKey));
            OnPropertyChanged(nameof(IsMidi));
            Touched();
        }
    }

    public bool IsKey => Kind == "key";
    public bool IsMidi => Kind == "midi";

    public string Gesture
    {
        get => _binding.Trigger.Gesture ?? "";
        set { if (SetProperty(_binding.Trigger.Gesture ?? "", value, _binding.Trigger, (t, v) => t.Gesture = v)) Touched(); }
    }

    public string Device
    {
        get => _binding.Trigger.Device ?? "";
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (SetProperty(_binding.Trigger.Device, normalized, _binding.Trigger, (t, v) => t.Device = v)) Touched();
        }
    }

    public int Channel
    {
        get => _binding.Trigger.Channel ?? 1;
        set { if (SetProperty(_binding.Trigger.Channel ?? 1, value, _binding.Trigger, (t, v) => t.Channel = v)) Touched(); }
    }

    public string Message
    {
        get => _binding.Trigger.Message ?? "cc";
        set { if (SetProperty(_binding.Trigger.Message ?? "cc", value, _binding.Trigger, (t, v) => t.Message = v)) Touched(); }
    }

    public int Number
    {
        get => _binding.Trigger.Number ?? 0;
        set { if (SetProperty(_binding.Trigger.Number ?? 0, value, _binding.Trigger, (t, v) => t.Number = v)) Touched(); }
    }

    public bool IgnoreZero
    {
        get => _binding.Trigger.IgnoreZero;
        set { if (SetProperty(_binding.Trigger.IgnoreZero, value, _binding.Trigger, (t, v) => t.IgnoreZero = v)) Touched(); }
    }

    public string Action
    {
        get => _binding.Action;
        set
        {
            if (!SetProperty(_binding.Action, value, _binding, (b, v) => b.Action = v)) return;
            OnPropertyChanged(nameof(ActionStatus));
            Touched();
        }
    }

    // Parse feedback for the action editor (empty when the macro is valid).
    public string ActionStatus
    {
        get
        {
            // Bindings disallow unbounded loops (a binding must finish), so parse in that context.
            var program = Parser.Parse(_binding.Action, allowLoops: false);
            var error = program.Diagnostics.FirstOrDefault(d => d.Severity == Severity.Error);
            return error?.ToString() ?? "";
        }
    }

    // One-line description for the list.
    public string Summary
    {
        get
        {
            var trigger = IsMidi
                ? $"{(_binding.Trigger.Message ?? "cc").ToUpperInvariant()} {Number} · ch {Channel}"
                : string.IsNullOrWhiteSpace(Gesture) ? "(unset key)" : Gesture;

            var action = _binding.Action.Replace("\n", " ").Trim();
            if (action.Length > 26) action = action[..26] + "…";
            return action.Length == 0 ? trigger : $"{trigger}  →  {action}";
        }
    }

    // Applies a captured MIDI message (MIDI learn) in one shot, so the dispatcher rebuilds once.
    public void CaptureMidi(string device, int channel, string message, int number)
    {
        _binding.Trigger.Kind = "midi";
        _binding.Trigger.Device = device;
        _binding.Trigger.Channel = channel;
        _binding.Trigger.Message = message;
        _binding.Trigger.Number = number;

        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsKey));
        OnPropertyChanged(nameof(IsMidi));
        OnPropertyChanged(nameof(Device));
        OnPropertyChanged(nameof(Channel));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(Number));
        Touched();
    }

    private void Touched()
    {
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke();
    }
}

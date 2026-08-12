using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Midi;

namespace ViewModel;

// A live view of incoming MIDI, so you can confirm a controller is reaching Luminary and read off the
// channel / note / CC numbers to author bindings. App-level (the MIDI service is app-wide).
public partial class MidiMonitorViewModel : ViewModelBase
{
    private const int MaxRows = 200;
    private readonly IMidiInputService _midi;

    public ObservableCollection<string> Messages { get; } = [];

    // Comma-separated list of opened input devices (refreshed when the window opens).
    [ObservableProperty]
    public partial string Devices { get; set; } = "";

    public MidiMonitorViewModel(IMidiInputService midi)
    {
        _midi = midi;
        _midi.MessageReceived += OnMessage;
        RefreshDevices();
    }

    // Devices open asynchronously after startup, so re-read them when the monitor is shown.
    public void RefreshDevices() =>
        Devices = _midi.DeviceNames.Count == 0 ? "No MIDI input devices found" : string.Join(", ", _midi.DeviceNames);

    private void OnMessage(MidiMessage m)
    {
        // Raised on the MIDI thread — marshal to the UI thread to touch the collection.
        Dispatcher.UIThread.Post(() =>
        {
            var kind = m.Kind == MidiMessageKind.NoteOn ? "Note" : "CC  ";
            Messages.Insert(0, $"{m.Device,-24}  ch {m.Channel,2}   {kind} {m.Number,3}   = {m.Value,3}");
            while (Messages.Count > MaxRows) Messages.RemoveAt(Messages.Count - 1);
        });
    }
}

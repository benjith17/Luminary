using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

// One automated capability, as one row of the timeline. Wraps the model track and keeps its key
// list sorted — playback binary-searches it, so order is a correctness requirement, not a display
// preference.
public partial class KeyframeTrackViewModel : ViewModelBase
{
    private readonly KeyframeEditorViewModel _editor;

    public KeyframeTrackViewModel(
        KeyframeTrack model,
        FixtureListItemViewModel fixture,
        CapabilityViewModelBase capability,
        KeyframeEditorViewModel editor)
    {
        Model = model;
        Fixture = fixture;
        Capability = capability;
        _editor = editor;

        Keys = new ObservableCollection<KeyframeViewModel>(
            model.Keys.Select(k => new KeyframeViewModel(k, capability)));
    }

    public KeyframeTrack Model { get; }
    public FixtureListItemViewModel Fixture { get; }
    public CapabilityViewModelBase Capability { get; }

    public ObservableCollection<KeyframeViewModel> Keys { get; }

    public string FixtureLabel => $"{Fixture.Number} · {Fixture.Name}";
    public string CapabilityLabel => Capability.Name;

    // The lane control needs the timebase, and reaching the editor's from inside a data template is
    // far more awkward than surfacing it here.
    public double DurationSeconds => _editor.DurationSeconds;
    public double GridSeconds => _editor.GridSeconds;

    public void NotifyTimebaseChanged()
    {
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(GridSeconds));
    }

    // Bumped on any change to the keys. The lane control re-renders when this changes, which is a
    // far simpler invalidation signal than watching every key for every property.
    [ObservableProperty]
    public partial int Revision { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void NotifyChanged() => Revision++;

    // Records the capability's current live output as a key at this time, replacing any key already
    // sitting there — dropping a second key on the same instant would make playback ambiguous.
    public KeyframeViewModel SetKeyAt(TimeSpan time)
    {
        var values = Capability.Capture();
        var existing = Keys.FirstOrDefault(k => k.Model.Time == time);

        if (existing is not null)
        {
            existing.Model.Values = values;
            existing.NotifyValueChanged();
            NotifyChanged();
            return existing;
        }

        var model = new Keyframe { Time = time, Values = values };
        var vm = new KeyframeViewModel(model, Capability);

        Model.Keys.Add(model);
        Keys.Add(vm);
        Resort();
        return vm;
    }

    public void Remove(KeyframeViewModel key)
    {
        Model.Keys.Remove(key.Model);
        Keys.Remove(key);
        NotifyChanged();
    }

    // Moves a key in time, refusing to land it exactly on another key for the same reason SetKeyAt
    // replaces rather than stacks.
    public void MoveKey(KeyframeViewModel key, TimeSpan time)
    {
        time = _editor.ClampToTimeline(time);
        if (key.Model.Time == time) return;
        if (Keys.Any(k => k != key && k.Model.Time == time)) return;

        key.Model.Time = time;
        key.NotifyTimeChanged();
        Resort();
    }

    // Keeps the model list and the display collection in the same order.
    private void Resort()
    {
        Model.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));

        var ordered = Keys.OrderBy(k => k.Model.Time).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var from = Keys.IndexOf(ordered[i]);
            if (from != i) Keys.Move(from, i);
        }

        NotifyChanged();
    }

    // Called by the lane control while a key is being dragged. Snapping lives here rather than in
    // the control so the grid is defined in one place.
    public void DragKeyTo(KeyframeViewModel key, double seconds)
    {
        MoveKey(key, _editor.SnapTime(TimeSpan.FromSeconds(seconds)));
        _editor.NotifyKeyDragged();
    }

    // Records a key on this row alone at the playhead, without having to select the row first.
    [RelayCommand]
    private void AddKey() => _editor.AddKeyTo(this);

    // Called when the operator clicks this row's header.
    [RelayCommand]
    private void SelectSelf() => _editor.SelectTrack(this);

    // Called by the lane control when the operator clicks a key or an empty part of the row.
    public void SelectKey(KeyframeViewModel? key) => _editor.Select(this, key);

    public void ScrubTo(TimeSpan time) => _editor.Seek(time);
}

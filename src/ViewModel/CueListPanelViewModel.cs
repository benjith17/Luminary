using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

public partial class CueListPanelViewModel(ShowService showService, FixturesListPanelViewModel fixtures) : ViewModelBase
{
    public ObservableCollection<CueViewModel> Cues { get; } = new(
        showService.CueList.Cues.Select(c => new CueViewModel(c))
    );

    [ObservableProperty]
    public partial CueViewModel? ActiveCue { get; set; }

    [RelayCommand]
    private void Record()
    {
        var nextMajor = showService.CueList.Cues.Count == 0
            ? 1
            : showService.CueList.Cues.Max(c => c.CueMajor) + 1;

        var cue = new Cue { CueMajor = nextMajor };

        foreach (var fixture in fixtures.Fixtures)
        {
            var snapshot = new CueFixtureSnapshot
            {
                FixtureName = fixture.Name,
                CapabilityValues = fixture.Capabilities.Select(c => c.Capture()).ToList()
            };
            cue.Fixtures.Add(snapshot);
        }

        showService.CueList.Cues.Add(cue);
        Cues.Add(new CueViewModel(cue));
    }

    [RelayCommand]
    private void Go()
    {
        if (showService.CueList.Cues.Count == 0) return;
        showService.CueList.ActiveIndex = Math.Min(
            showService.CueList.ActiveIndex + 1,
            showService.CueList.Cues.Count - 1);
        Recall(showService.CueList.Cues[showService.CueList.ActiveIndex]);
    }

    [RelayCommand]
    private void Back()
    {
        if (showService.CueList.Cues.Count == 0) return;
        showService.CueList.ActiveIndex = Math.Max(showService.CueList.ActiveIndex - 1, 0);
        Recall(showService.CueList.Cues[showService.CueList.ActiveIndex]);
    }

    private void Recall(Cue cue)
    {
        foreach (var snapshot in cue.Fixtures)
        {
            var fixtureVm = fixtures.Fixtures.FirstOrDefault(f => f.Name == snapshot.FixtureName);
            if (fixtureVm is null) continue;

            foreach (var (capability, values) in fixtureVm.Capabilities.Zip(snapshot.CapabilityValues))
                capability.Restore(values);
        }

        foreach (var vm in Cues) vm.IsActive = false;
        var activeVm = Cues.FirstOrDefault(v => v.Model == cue);
        if (activeVm is not null)
        {
            activeVm.IsActive = true;
            ActiveCue = activeVm;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;

namespace ViewModel;

public partial class FixturesListPanelViewModel : ViewModelBase
{
    private readonly ShowService _showService;
    private readonly FixtureLibrary _library;

    public FixturesListPanelViewModel(ShowService showService, FixtureLibrary library)
    {
        _showService = showService;
        _library = library;

        Fixtures = new ObservableCollection<FixtureListItemViewModel>(
            showService.Fixtures.Select(Wrap));

        Universes = new ObservableCollection<UniverseItemViewModel>(
            showService.Universes.Select(u => new UniverseItemViewModel(u)));

        UniverseNumbers = new ObservableCollection<byte>(
            showService.Universes.Select(u => u.Number));

        SelectedUniverse = Universes.FirstOrDefault();
        SelectedPersonality = library.Definitions.FirstOrDefault();
    }

    public ObservableCollection<FixtureListItemViewModel> Fixtures { get; }

    public ObservableCollection<UniverseItemViewModel> Universes { get; }

    // Universe numbers available in the patch combo.
    public ObservableCollection<byte> UniverseNumbers { get; }

    // Personalities available to patch, from the library.
    public IReadOnlyList<FixtureDefinition> Personalities => _library.Definitions;

    [ObservableProperty]
    public partial FixtureListItemViewModel? SelectedFixture { get; set; }

    [ObservableProperty]
    public partial UniverseItemViewModel? SelectedUniverse { get; set; }

    // The personality used when adding a new fixture.
    [ObservableProperty]
    public partial FixtureDefinition? SelectedPersonality { get; set; }

    [RelayCommand]
    private void AddUniverse()
    {
        var universe = new Universe(NextUniverseNumber());

        // Copy-on-write so the ArtNet transmit thread only ever sees a complete list.
        _showService.Universes = [.. _showService.Universes, universe];

        var vm = new UniverseItemViewModel(universe);
        Universes.Add(vm);
        UniverseNumbers.Add(universe.Number);
        SelectedUniverse = vm;
    }

    [RelayCommand]
    private void RemoveUniverse()
    {
        if (SelectedUniverse is null) return;

        var number = SelectedUniverse.Number;
        _showService.Universes = _showService.Universes.Where(u => u.Number != number).ToList();

        var index = Universes.IndexOf(SelectedUniverse);
        Universes.Remove(SelectedUniverse);
        UniverseNumbers.Remove(number);
        SelectedUniverse = Universes.Count == 0 ? null : Universes[Math.Min(index, Universes.Count - 1)];
    }

    private byte NextUniverseNumber()
    {
        for (byte n = 1; n < byte.MaxValue; n++)
            if (_showService.Universes.All(u => u.Number != n))
                return n;
        return 1;
    }

    [RelayCommand]
    private void AddFixture()
    {
        var def = SelectedPersonality ?? _library.Definitions.FirstOrDefault();
        if (def is null) return;

        var universeNumber = _showService.Universes.FirstOrDefault()?.Number ?? (byte)1;
        var fixture = new Fixture(UniqueName(def.Name), NextFreeAddress(universeNumber, def), def)
        {
            UniverseNumber = universeNumber
        };

        _showService.Fixtures.Add(fixture);
        var vm = Wrap(fixture);
        Fixtures.Add(vm);
        SelectedFixture = vm;
        RefreshOutput();
    }

    [RelayCommand]
    private void RemoveFixture()
    {
        if (SelectedFixture is null) return;

        var index = Fixtures.IndexOf(SelectedFixture);
        _showService.Fixtures.Remove(SelectedFixture.Fixture);
        Fixtures.Remove(SelectedFixture);
        SelectedFixture = Fixtures.Count == 0 ? null : Fixtures[Math.Min(index, Fixtures.Count - 1)];
        RefreshOutput();
    }

    private FixtureListItemViewModel Wrap(Fixture fixture)
    {
        var vm = new FixtureListItemViewModel(fixture, _showService);
        vm.PatchChanged += RefreshOutput;
        return vm;
    }

    // Clears every universe and re-emits all fixtures' current output, so re-addressing or
    // removing a fixture never leaves stale channels driving hardware.
    private void RefreshOutput()
    {
        foreach (var universe in _showService.Universes) universe.Clear();
        foreach (var fixture in Fixtures) fixture.PushOutput();
    }

    // Places a new fixture directly after the highest channel currently used in its universe.
    private int NextFreeAddress(byte universeNumber, FixtureDefinition def)
    {
        var end = _showService.Fixtures
            .Where(f => f.UniverseNumber == universeNumber)
            .Select(f => f.Channel + f.FixtureType.ChannelCount)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Min(end, Math.Max(0, 512 - def.ChannelCount));
    }

    private string UniqueName(string baseName)
    {
        if (Fixtures.All(f => f.Name != baseName)) return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (Fixtures.All(f => f.Name != candidate)) return candidate;
        }
    }
}

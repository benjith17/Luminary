using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Model;

namespace ViewModel;

// A manufacturer and its models, for the first picker column.
public sealed class ManufacturerItem
{
    public required string Name { get; init; }
    public required IReadOnlyList<ModelItem> Models { get; init; }
}

// A fixture model and its available modes (personalities), for the second column.
public sealed class ModelItem
{
    public required string Name { get; init; }
    public required IReadOnlyList<FixtureDefinition> Modes { get; init; }
}

public partial class AddFixtureDialogViewModel : ViewModelBase
{
    private readonly IReadOnlyList<FixtureDefinition> _all;

    public AddFixtureDialogViewModel(IReadOnlyList<FixtureDefinition> definitions)
    {
        _all = definitions;
        RebuildManufacturers();
    }

    public ObservableCollection<ManufacturerItem> Manufacturers { get; } = [];
    public ObservableCollection<ModelItem> Models { get; } = [];
    public ObservableCollection<FixtureDefinition> Modes { get; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ManufacturerItem? SelectedManufacturer { get; set; }

    [ObservableProperty]
    public partial ModelItem? SelectedModel { get; set; }

    [ObservableProperty]
    public partial FixtureDefinition? SelectedMode { get; set; }

    // Detail pane (third column).
    public bool HasSelection => SelectedModel is not null;
    public string DetailModel => SelectedModel?.Name ?? string.Empty;
    public string DetailManufacturer => SelectedManufacturer?.Name ?? string.Empty;
    public string DetailChannels => SelectedMode is { } d ? $"{d.ChannelCount} channels" : string.Empty;
    public IReadOnlyList<string> DetailCapabilities => SelectedMode?.Capabilities.Select(c => c.Name).ToList() ?? [];

    public bool CanAdd => SelectedMode is not null;
    public FixtureDefinition? Result => SelectedMode;

    partial void OnSearchChanged(string value) => RebuildManufacturers();

    partial void OnSelectedManufacturerChanged(ManufacturerItem? value)
    {
        Models.Clear();
        if (value is not null)
            foreach (var model in value.Models) Models.Add(model);
        SelectedModel = Models.FirstOrDefault();
    }

    partial void OnSelectedModelChanged(ModelItem? value)
    {
        Modes.Clear();
        if (value is not null)
            foreach (var mode in value.Modes) Modes.Add(mode);
        SelectedMode = Modes.FirstOrDefault();

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(DetailModel));
        OnPropertyChanged(nameof(DetailManufacturer));
    }

    partial void OnSelectedModeChanged(FixtureDefinition? value)
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(DetailChannels));
        OnPropertyChanged(nameof(DetailCapabilities));
    }

    // Rebuilds the manufacturer list (and its nested models) for the current search, preserving the
    // manufacturer/model selection where it still exists.
    private void RebuildManufacturers()
    {
        var query = Search.Trim();
        bool Match(FixtureDefinition d) => query.Length == 0 ||
            $"{d.Manufacturer} {d.Model} {d.Mode} {d.Name}".Contains(query, StringComparison.OrdinalIgnoreCase);

        var previousManufacturer = SelectedManufacturer?.Name;
        var previousModel = SelectedModel?.Name;

        Manufacturers.Clear();
        foreach (var manufacturer in Group(_all.Where(Match), d => Or(d.Manufacturer, "Other")))
        {
            var models = Group(manufacturer.Items, d => Or(d.Model, d.Name))
                .Select(m => new ModelItem { Name = m.Key, Modes = m.Items })
                .ToList();
            Manufacturers.Add(new ManufacturerItem { Name = manufacturer.Key, Models = models });
        }

        // Restore selection (setting SelectedManufacturer repopulates Models + picks a model).
        SelectedManufacturer = Manufacturers.FirstOrDefault(m => m.Name == previousManufacturer)
                               ?? Manufacturers.FirstOrDefault();
        if (previousModel is not null && Models.FirstOrDefault(m => m.Name == previousModel) is { } restored)
            SelectedModel = restored;
    }

    private static string Or(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    // Order-preserving grouping (keeps the library's declared order rather than re-sorting).
    private static IEnumerable<(string Key, List<FixtureDefinition> Items)> Group(
        IEnumerable<FixtureDefinition> defs, Func<FixtureDefinition, string> key)
    {
        var order = new List<string>();
        var map = new Dictionary<string, List<FixtureDefinition>>();
        foreach (var d in defs)
        {
            var k = key(d);
            if (!map.TryGetValue(k, out var list))
            {
                map[k] = list = [];
                order.Add(k);
            }
            list.Add(d);
        }
        foreach (var k in order) yield return (k, map[k]);
    }
}

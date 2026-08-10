using Avalonia.Controls;
using Avalonia.Interactivity;
using Model;
using ViewModel;

namespace View;

public partial class ConfigWindow : Window
{
    public ConfigWindow()
    {
        InitializeComponent();
    }

    private async void OnAddFixtureClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FixturesListPanelViewModel vm) return;

        var def = await new AddFixtureDialog(vm.Personalities).ShowDialog<FixtureDefinition?>(this);
        if (def is not null) vm.AddFixture(def);
    }
}

using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Model;
using ViewModel;

namespace View;

public partial class AddFixtureDialog : Window
{
    public AddFixtureDialog()
    {
        InitializeComponent();
    }

    public AddFixtureDialog(IReadOnlyList<FixtureDefinition> definitions) : this()
    {
        DataContext = new AddFixtureDialogViewModel(definitions);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AddFixtureDialogViewModel vm) Close(vm.Result);
    }

    // Double-clicking a fixture patches its currently-selected mode immediately.
    private void OnModelDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AddFixtureDialogViewModel { Result: { } def }) Close(def);
    }
}

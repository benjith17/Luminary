using System;
using System.Collections.ObjectModel;
using System.Timers;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ViewModel;

public partial class MainWindowViewModel : ViewModelBase
{

    public EffectsPanelViewModel EffectsPanel { get; } = new();
    public FixturesListPanelViewModel FixturesListPanel { get; } = new();
}

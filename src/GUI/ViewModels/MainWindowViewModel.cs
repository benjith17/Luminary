using System;
using System.Collections.ObjectModel;
using System.Timers;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // --- Dashboard ---
    [ObservableProperty] private int _userCount = 1_042;
    [ObservableProperty] private int _eventCount = 38_761;
    [ObservableProperty] private string _uptime = "99.9%";
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    public ObservableCollection<ActivityItem> ActivityItems { get; } = new()
    {
        new("User alice signed in",  "2m ago",  "#a6e3a1"),
        new("Config updated",        "5m ago",  "#89dceb"),
        new("Deploy #47 succeeded",  "12m ago", "#a6e3a1"),
        new("Warning: high memory",  "18m ago", "#f9e2af"),
        new("User bob signed out",   "34m ago", "#6c7086"),
    };

    // --- Controls ---
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private int _selectedPriority = 1;
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _autoSave = false;
    [ObservableProperty] private bool _showAdvanced = false;
    [ObservableProperty] private bool _isDarkMode = false;
    [ObservableProperty] private double _volume = 65;
    [ObservableProperty] private string _statusMessage = string.Empty;

    partial void OnVolumeChanged(double value) =>
        VolumeLabel = $"Volume: {(int)value}%";

    [ObservableProperty] private string _volumeLabel = "Volume: 65%";

    [RelayCommand]
    private void Save()
    {
        StatusMessage = $"Saved at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private void Reset()
    {
        Username = string.Empty;
        Notes = string.Empty;
        Volume = 65;
        SelectedPriority = 1;
        NotificationsEnabled = true;
        AutoSave = false;
        StatusMessage = "Reset to defaults.";
    }

    // --- Todo list ---
    [ObservableProperty] private string _newItemText = string.Empty;
    [ObservableProperty] private TodoItem? _selectedTodo;
    [ObservableProperty] private string _itemCountLabel = "0 items";

    public ObservableCollection<TodoItem> TodoItems { get; } = new();

    public MainWindowViewModel()
    {
        TodoItems.Add(new TodoItem("Buy groceries"));
        TodoItems.Add(new TodoItem("Read Avalonia docs") { IsDone = true });
        TodoItems.Add(new TodoItem("Build something cool"));
        UpdateItemCount();

        var timer = new System.Timers.Timer(1000);
        timer.Elapsed += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        timer.Start();
    }

    [RelayCommand]
    private void AddItem()
    {
        var text = NewItemText.Trim();
        if (string.IsNullOrEmpty(text)) return;
        TodoItems.Add(new TodoItem(text));
        NewItemText = string.Empty;
        UpdateItemCount();
    }

    [RelayCommand]
    private void RemoveItem(TodoItem item)
    {
        TodoItems.Remove(item);
        UpdateItemCount();
    }

    [RelayCommand]
    private void ClearItems()
    {
        TodoItems.Clear();
        UpdateItemCount();
    }

    private void UpdateItemCount() =>
        ItemCountLabel = TodoItems.Count == 1 ? "1 item" : $"{TodoItems.Count} items";
}

public partial class TodoItem : ObservableObject
{
    [ObservableProperty] private bool _isDone;
    public string Text { get; }

    public TextDecorationCollection? Decoration =>
        IsDone ? TextDecorations.Strikethrough : null;

    public TodoItem(string text) => Text = text;

    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(Decoration));
}

public record ActivityItem(string Message, string Time, string DotColor);

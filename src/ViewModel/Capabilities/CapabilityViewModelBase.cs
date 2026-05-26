namespace ViewModel;

public abstract class CapabilityViewModelBase(string name) : ViewModelBase
{
    public string Name { get; } = name;
}

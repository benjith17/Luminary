namespace ViewModel;

public abstract class CapabilityViewModelBase(string name) : ViewModelBase
{
    public string Name { get; } = name;

    public abstract byte[] Capture();
    public abstract void Restore(byte[] values);
}

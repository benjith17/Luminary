using Model;

namespace ViewModel;

// One saved macro in the Macros window: an editable name and source. Edits write straight through
// to the model so the show's dirty-tracking (serialization compare) notices them.
public partial class MacroItemViewModel(Macro macro) : ViewModelBase
{
    public Macro Model => macro;

    public string Name
    {
        get => macro.Name;
        set => SetProperty(macro.Name, value, macro, (m, v) => m.Name = v);
    }

    public string Source
    {
        get => macro.Source;
        set => SetProperty(macro.Source, value, macro, (m, v) => m.Source = v);
    }
}

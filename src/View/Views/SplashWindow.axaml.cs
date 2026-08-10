using System.Reflection;
using Avalonia.Controls;

namespace View;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = BuildLabel();
    }

    private static string BuildLabel()
    {
#if DEBUG
        return "dev";
#else
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "dev" : $"v{v.Major}.{v.Minor}.{v.Build}";
#endif
    }
}

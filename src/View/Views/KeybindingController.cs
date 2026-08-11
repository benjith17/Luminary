using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Settings;

namespace View;

// Maps configured key gestures to actions on the main window. Attached as a tunnelling KeyDown handler
// so a bound key (e.g. Space = GO) wins over a focused button — except while a text field is focused,
// where keys are left alone so typing (and the macro editor) work normally.
public sealed class KeybindingController
{
    private readonly List<(KeyGesture Gesture, Action Invoke)> _bindings = [];

    public KeybindingController(SettingsService settings, IReadOnlyDictionary<Keybind, Action> actions)
    {
        foreach (var (bind, action) in actions)
        {
            var text = settings.GestureFor(bind);
            if (text is null) continue;
            try
            {
                _bindings.Add((KeyGesture.Parse(ResolveMod(text)), action));
            }
            catch
            {
                // Ignore an unparseable gesture rather than failing startup.
            }
        }
    }

    public void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        var typing = TopLevel.GetTopLevel(sender as Visual)?.FocusManager?.GetFocusedElement() is TextBox;

        foreach (var (gesture, invoke) in _bindings)
        {
            if (!gesture.Matches(e)) continue;

            // While typing, let the text field keep bare keys (Space, arrows) it needs; modified
            // accelerators (Alt+P, Ctrl+W, …) and function keys still fire.
            if (typing && SuppressedWhileTyping(gesture)) return;

            invoke();
            e.Handled = true;
            return;
        }
    }

    private static bool SuppressedWhileTyping(KeyGesture gesture) =>
        gesture.KeyModifiers == KeyModifiers.None && !IsFunctionKey(gesture.Key);

    private static bool IsFunctionKey(Key key) => key is >= Key.F1 and <= Key.F24;

    // Expands the platform-neutral "Mod" modifier to Cmd (Meta) on macOS, Ctrl elsewhere.
    private static string ResolveMod(string gesture) =>
        gesture.Replace("Mod+", OperatingSystem.IsMacOS() ? "Meta+" : "Ctrl+", StringComparison.OrdinalIgnoreCase);
}

using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>One shortcut: how it is written, what it does, and the key itself.</summary>
/// <param name="Also">
/// A second key that means the same thing. The backtick carries the tilde on the
/// same physical key, and a shortcut that stopped working because Shift was down
/// would look broken rather than particular.
/// </param>
internal sealed record Shortcut(string Name, string Does, Key Binding, Key? Also = null)
{
    public bool Matches(Key pressed) => pressed == Binding || pressed == Also;
}

/// <summary>
/// The keys, in one table. Both the bindings and the panel are built from it, so a
/// shortcut cannot be listed without being bound, or changed while the panel goes
/// on describing the old one.
/// </summary>
internal static class Keys
{
    /// <summary>
    /// Whether the panel can be held open. SharpOS reports real key releases, so
    /// there the tilde works like a held key; System.Console reports presses only
    /// and the driver synthesises the release immediately after, which would make
    /// the panel flash rather than stay. It toggles there instead.
    /// </summary>
    /// <remarks>
    /// A property rather than a const: as a const, the branch the host does not take
    /// becomes unreachable code and the build refuses it.
    /// </remarks>
#if SHARPOS
    public static bool HoldToShow => true;
#else
    public static bool HoldToShow => false;
#endif

    public static Shortcut Panel { get; } = new(
        "`",
        HoldToShow ? "hold for this panel" : "show or hide this panel",
        (Key)'`',
        (Key)'~');

    public static Shortcut Help { get; } = new("F1", "the same panel, for keyboards without a tilde", Key.F1);

    public static Shortcut Scan { get; } = new("F5", "scan now, without waiting for the sweep", Key.F5);

    public static Shortcut Save { get; } = new("Ctrl+S", "write the wallet file now", Key.CtrlMask | Key.S);

    public static Shortcut Quit { get; } = new("Ctrl+Q", "save and leave", Key.CtrlMask | Key.Q);

    public static Shortcut[] All => [Panel, Help, Scan, Save, Quit];

    /// <summary>
    /// Bound elsewhere: Alt+digit in Program, the rest by the library. Written down
    /// because it was wrong once — Tab was listed as switching tabs, when TabView
    /// answers only to the arrows and only while the strip has focus.
    /// </summary>
    /// <remarks>
    /// There is deliberately no key for the next and previous tab. Ctrl+arrow was
    /// the obvious pair, and it is what a text field uses to move by words: taking
    /// it would have cost a working editor for a second way to do what Alt+digit
    /// already does.
    /// </remarks>
    private static readonly (string Name, string Does)[] Builtin =
    [
        ("Alt+1…6", "jump straight to a tab"),
        ("← →", "move between tabs, while the strip has focus"),
        ("Tab", "move focus between the strip and the page"),
        ("F9", "open the menu"),
    ];

    private const int Width = 62;

    /// <summary>
    /// An overlay rather than a dialog. A dialog runs its own loop, and this is
    /// meant to come and go under one key without stopping what is behind it.
    /// </summary>
    public static View Build()
    {
        int height = All.Length + Builtin.Length + 5;

        FrameView panel = new("Keys")
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Width,
            Height = height,
        };

        int y = 0;

        foreach (Shortcut shortcut in All)
        {
            panel.Add(new Label(shortcut.Name) { X = 2, Y = y, Width = 11 });
            panel.Add(new Label(shortcut.Does) { X = 13, Y = y, ColorScheme = Colors.Dialog });
            y++;
        }

        y++;

        foreach ((string name, string does) in Builtin)
        {
            panel.Add(new Label(name) { X = 2, Y = y, Width = 11 });
            panel.Add(new Label(does) { X = 13, Y = y, ColorScheme = Colors.Dialog });
            y++;
        }

        panel.Add(new Label(HoldToShow ? "release ` to close" : "` or Esc to close")
        {
            X = 2,
            Y = y + 1,
            ColorScheme = Colors.Dialog,
        });

        return panel;
    }
}

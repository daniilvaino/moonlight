using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The bottom line: what the wallet is doing on the left, what it holds and where
/// it is connected on the right. Feather keeps these in view on every screen, and
/// they are the two things worth never having to go looking for.
/// </summary>
internal sealed class Status : View
{
    private string state = "not started";
    private string balance = "";
    private string node = "";
    private string? note;

    public Status() => CanFocus = false;

    public void Report(string activity, string held, string daemon)
    {
        state = activity;
        balance = held;
        node = daemon;
        SetNeedsDisplay();
    }

    /// <summary>
    /// A word about something that just happened, in place of the activity until it
    /// is taken back. An action with no visible result reads as one that did not
    /// happen — saving especially, since its whole point is that nothing changes.
    /// </summary>
    public void Note(string? text)
    {
        note = text;
        SetNeedsDisplay();
    }

    public override void Redraw(Rect bounds)
    {
        Driver.SetAttribute(ColorScheme.Normal);

        // Painted rather than cleared: without it the line keeps whatever the
        // screen under it last drew.
        Move(0, 0);
        for (int i = 0; i < Bounds.Width; i++) Driver.AddRune(' ');

        string left = note ?? state;

        Move(1, 0);
        Driver.AddStr(Trim(left, Bounds.Width - 2));

        string right = node.Length == 0 ? balance : $"{balance}   {node}";
        int at = Bounds.Width - right.Length - 1;

        // Only when the two would not collide; a narrow terminal keeps the left.
        if (at > left.Length + 3)
        {
            Move(at, 0);
            Driver.AddStr(right);
        }
    }

    private static string Trim(string text, int width)
        => width <= 0 ? "" : text.Length <= width ? text : text[..width];
}

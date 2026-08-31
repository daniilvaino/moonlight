using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The application's colours, set once. Terminal.Gui's schemes are global, so this
/// has to happen straight after the driver exists and before any screen is built —
/// a view created earlier keeps whatever was in force at the time.
/// </summary>
internal static class Theme
{
    /// <summary>Black on white, for the QR only.</summary>
    /// <remarks>
    /// The specification wants dark modules on a light field. In the application's
    /// own colours a code comes out inverted — bright modules on dark — and some
    /// readers refuse that, which is a payment that quietly does not arrive.
    /// </remarks>
    public static ColorScheme Qr { get; private set; } = new();

    public static void Apply()
    {
        Terminal.Gui.Attribute Make(Color foreground, Color background)
            => Application.Driver.MakeAttribute(foreground, background);

        // Purple is the brand's, and the only accent here.
        Colors.TopLevel = new ColorScheme
        {
            Normal = Make(Color.White, Color.Black),
            Focus = Make(Color.Black, Color.BrightMagenta),
            HotNormal = Make(Color.BrightMagenta, Color.Black),
            HotFocus = Make(Color.Black, Color.BrightMagenta),
            Disabled = Make(Color.DarkGray, Color.Black),
        };

        Colors.Base = Colors.TopLevel;

        Colors.Menu = new ColorScheme
        {
            Normal = Make(Color.White, Color.DarkGray),
            Focus = Make(Color.Black, Color.BrightMagenta),
            HotNormal = Make(Color.BrightMagenta, Color.DarkGray),
            HotFocus = Make(Color.Black, Color.BrightMagenta),
            Disabled = Make(Color.Gray, Color.DarkGray),
        };

        Colors.Dialog = new ColorScheme
        {
            Normal = Make(Color.Gray, Color.Black),
            Focus = Make(Color.Black, Color.BrightMagenta),
            HotNormal = Make(Color.BrightMagenta, Color.Black),
            HotFocus = Make(Color.Black, Color.BrightMagenta),
            Disabled = Make(Color.DarkGray, Color.Black),
        };

        Colors.Error = new ColorScheme
        {
            Normal = Make(Color.White, Color.Red),
            Focus = Make(Color.Black, Color.BrightRed),
            HotNormal = Make(Color.BrightYellow, Color.Red),
            HotFocus = Make(Color.BrightYellow, Color.BrightRed),
            Disabled = Make(Color.Gray, Color.Red),
        };

        Terminal.Gui.Attribute onWhite = Make(Color.Black, Color.White);
        Qr = new ColorScheme { Normal = onWhite, Focus = onWhite, HotNormal = onWhite, HotFocus = onWhite, Disabled = onWhite };

        // Application.Top is built inside Init, before this runs, and everything
        // added to it inherits its scheme. Without this line the frames and fields
        // keep Terminal.Gui's defaults while the menu wears ours.
        Application.Top.ColorScheme = Colors.TopLevel;
    }
}

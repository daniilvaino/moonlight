using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The send form, laid out but not wired: the fields are real, the button is not.
/// </summary>
/// <remarks>
/// Spending needs three things this wallet does not have yet — decoy selection,
/// a fee and weight calculation, and the transaction builder. The tab is here
/// because leaving it out would hide how much of the wallet is still missing, and
/// the button says so rather than failing when pressed.
/// </remarks>
internal sealed class Send : View
{
    public Send()
    {
        Add(new Label("pay to") { X = 1, Y = 1, ColorScheme = Colors.Dialog });
        Add(new TextField("") { X = 13, Y = 1, Width = Dim.Fill(2) });

        Add(new Label("description") { X = 1, Y = 3, ColorScheme = Colors.Dialog });
        Add(new TextField("") { X = 13, Y = 3, Width = Dim.Fill(2) });

        Add(new Label("amount") { X = 1, Y = 5, ColorScheme = Colors.Dialog });
        Add(new TextField("") { X = 13, Y = 5, Width = 24 });
        Add(new Label("XMR") { X = 38, Y = 5, ColorScheme = Colors.Dialog });

        Add(new Button("Send") { X = 13, Y = 7, Enabled = false });

        Add(new Label("not yet — this wallet cannot build a transaction.") { X = 22, Y = 7, ColorScheme = Colors.Dialog });
        Add(new Label("decoy selection, fees and the builder are still missing.") { X = 22, Y = 8, ColorScheme = Colors.Dialog });
    }
}

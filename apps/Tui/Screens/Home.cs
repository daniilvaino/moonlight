using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The first tab: what this wallet is, what it holds, and how far it has looked.
/// </summary>
/// <remarks>
/// Feather's home screen also carries fiat prices and a news feed. Neither is here:
/// a price needs a source this wallet does not have yet, and inventing one would
/// put a number on the screen that nothing behind it can stand behind.
/// </remarks>
internal sealed class Home : View
{
    private readonly Label balance;
    private readonly Label unlocked;
    private readonly Label progress;
    private readonly Label owned;

    public Home(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        Add(new Label("address") { X = 1, Y = 0, ColorScheme = Colors.Dialog });
        Add(new TextField(account.Address.Encode())
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            ReadOnly = true,   // selectable, so it can be copied; not editable
        });

        Add(new Label("balance") { X = 1, Y = 3, ColorScheme = Colors.Dialog });
        balance = new Label("—") { X = 11, Y = 3, Width = Dim.Fill(1) };

        Add(new Label("unlocked") { X = 1, Y = 4, ColorScheme = Colors.Dialog });
        unlocked = new Label("—") { X = 11, Y = 4, Width = Dim.Fill(1) };

        Add(new Label("scanned") { X = 1, Y = 6, ColorScheme = Colors.Dialog });
        progress = new Label("not started") { X = 11, Y = 6, Width = Dim.Fill(1) };

        Add(new Label("outputs") { X = 1, Y = 7, ColorScheme = Colors.Dialog });
        owned = new Label("0") { X = 11, Y = 7, Width = Dim.Fill(1) };

        Add(balance, unlocked, progress, owned);
    }

    public void Update(Balance amounts, ulong scanned, ulong height, int outputs)
    {
        balance.Text = $"{Amounts.Format(amounts.Total)} XMR";
        unlocked.Text = amounts.Locked == 0
            ? $"{Amounts.Format(amounts.Unlocked)} XMR"
            : $"{Amounts.Format(amounts.Unlocked)} XMR   ({Amounts.Format(amounts.Locked)} still locked)";

        progress.Text = Amounts.Progress(scanned, height);
        owned.Text = outputs.ToString(CultureInfo.InvariantCulture);
        SetNeedsDisplay();
    }
}

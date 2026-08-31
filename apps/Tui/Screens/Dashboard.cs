using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>What the wallet is, what it holds, and how far it has looked.</summary>
internal sealed class Dashboard : FrameView
{
    private readonly Label balance;
    private readonly Label unlocked;
    private readonly Label progress;
    private readonly Label outputs;

    public Dashboard(Account account)
        : base("Dashboard")
    {
        ArgumentNullException.ThrowIfNull(account);

        Add(new Label("address") { X = 2, Y = 1, ColorScheme = Colors.Dialog });
        Add(new TextField(account.Address.Encode())
        {
            X = 2,
            Y = 2,
            Width = Dim.Fill(2),
            ReadOnly = true,   // selectable, so it can be copied; not editable
        });

        Add(new Label("balance") { X = 2, Y = 4, ColorScheme = Colors.Dialog });
        balance = new Label("—") { X = 12, Y = 4, Width = Dim.Fill(2) };

        Add(new Label("unlocked") { X = 2, Y = 5, ColorScheme = Colors.Dialog });
        unlocked = new Label("—") { X = 12, Y = 5, Width = Dim.Fill(2) };

        Add(new Label("scanned") { X = 2, Y = 7, ColorScheme = Colors.Dialog });
        progress = new Label("not started") { X = 12, Y = 7, Width = Dim.Fill(2) };

        Add(new Label("outputs") { X = 2, Y = 8, ColorScheme = Colors.Dialog });
        outputs = new Label("0") { X = 12, Y = 8, Width = Dim.Fill(2) };

        Add(balance, unlocked, progress, outputs);
    }

    public void Update(Balance amounts, ulong scanned, ulong height, int owned)
    {
        balance.Text = $"{Format(amounts.Total)} XMR";
        unlocked.Text = amounts.Locked == 0
            ? $"{Format(amounts.Unlocked)} XMR"
            : $"{Format(amounts.Unlocked)} XMR   ({Format(amounts.Locked)} still locked)";

        progress.Text = height == 0
            ? $"block {scanned}"
            : $"block {scanned} of {height - 1}   {100.0 * scanned / Math.Max(height - 1, 1):F1}%";

        outputs.Text = owned.ToString(CultureInfo.InvariantCulture);
        SetNeedsDisplay();
    }

    /// <summary>Twelve decimal places, and a wallet that rounds them is lying.</summary>
    public static string Format(ulong atomic)
        => (atomic / 1_000_000_000_000m).ToString("0.############", CultureInfo.InvariantCulture);
}

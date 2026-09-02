using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// Every output this wallet owns, one line each — Feather calls these coins, and
/// they are what a spend will actually be built out of.
/// </summary>
internal sealed class Coins : View
{
    private readonly Label header;
    private readonly ListView list;
    private readonly List<string> rows = [];

    public Coins()
    {
        header = new Label(Row("block", "amount", "account", "state"))
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            ColorScheme = Colors.Dialog,
        };

        list = new ListView(rows) { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill() };
        Add(header, list);
    }

    public void Update(IEnumerable<OwnedOutput> outputs, Func<OwnedOutput, bool> isSpent, ulong height)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(isSpent);

        rows.Clear();

        foreach (OwnedOutput output in outputs.OrderByDescending(o => o.Height))
        {
            rows.Add(Row(
                output.Height.ToString(CultureInfo.InvariantCulture),
                Amounts.Format(output.Amount),
                Account(output),
                State(output, isSpent(output), height)));
        }

        if (rows.Count == 0) rows.Add("nothing yet — the scan has found no outputs");

        list.SetSource(rows);
        SetNeedsDisplay();
    }

    /// <summary>
    /// Spent first: it is the one state that means the coin is gone. A view-only
    /// wallet never sees it, since it cannot compute the key image that proves it.
    /// </summary>
    private static string State(OwnedOutput output, bool spent, ulong height)
        => spent ? "spent"
            : output.KeyImage is null ? "view-only"
            : WalletState.IsUnlocked(output, height) ? "spendable"
            : "locked";

    private static string Account(OwnedOutput output)
        => output.Subaddress.IsZero ? "main" : $"{output.Subaddress.Major}/{output.Subaddress.Minor}";

    private static string Row(string height, string amount, string account, string state)
        => string.Format(CultureInfo.InvariantCulture, "{0,9}  {1,20}  {2,-9} {3}", height, amount, account, state);
}

using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// What happened to this wallet's money, newest first: an output arriving, and
/// later that same output leaving.
/// </summary>
/// <remarks>
/// Feather shows transactions here, with a counterparty and a description. This
/// shows movements instead, because that is all the chain records. Monero does not
/// say who paid whom — an incoming output has no sender, and a spend is recognised
/// only by a key image we made ourselves. A wallet that presented senders here
/// would be inventing them.
/// </remarks>
internal sealed class History : View
{
    private readonly Label header;
    private readonly ListView list;
    private readonly List<string> rows = [];

    public History()
    {
        header = new Label(Row("block", "", "amount", "account"))
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill(1),
            ColorScheme = Colors.Dialog,
        };

        list = new ListView(rows) { X = 1, Y = 1, Width = Dim.Fill(1), Height = Dim.Fill() };
        Add(header, list);
    }

    public void Update(IEnumerable<OwnedOutput> outputs, Func<OwnedOutput, ulong?> spentAt)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(spentAt);

        List<(ulong Height, string Direction, ulong Amount, string Account)> events = [];

        foreach (OwnedOutput output in outputs)
        {
            string account = output.Subaddress.IsZero
                ? "main"
                : $"{output.Subaddress.Major}/{output.Subaddress.Minor}";

            events.Add((output.Height, "in", output.Amount, account));

            if (spentAt(output) is ulong height) events.Add((height, "out", output.Amount, account));
        }

        rows.Clear();

        foreach ((ulong height, string direction, ulong amount, string account) in
                 events.OrderByDescending(e => e.Height).ThenBy(e => e.Direction, StringComparer.Ordinal))
        {
            // The sign is the direction, so a column of numbers reads as a ledger.
            string signed = (direction == "in" ? "+" : "−") + Amounts.Format(amount);

            rows.Add(Row(height.ToString(CultureInfo.InvariantCulture), direction, signed, account));
        }

        if (rows.Count == 0) rows.Add("nothing yet — the scan has found no movements");

        list.SetSource(rows);
        SetNeedsDisplay();
    }

    private static string Row(string height, string direction, string amount, string account)
        => string.Format(CultureInfo.InvariantCulture, "{0,9}  {1,-4} {2,20}  {3}", height, direction, amount, account);
}

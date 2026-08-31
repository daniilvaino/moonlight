using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// The outputs this wallet owns. Not a transaction history: the chain does not
/// record who paid whom, and a wallet that presents one is inventing it.
/// </summary>
internal sealed class History : FrameView
{
    private readonly ListView list;
    private readonly List<string> rows = [];

    public History()
        : base("Outputs")
    {
        list = new ListView(rows) { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        Add(list);
    }

    public void Update(IEnumerable<OwnedOutput> outputs, Func<OwnedOutput, bool> isSpent)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(isSpent);

        rows.Clear();

        foreach (OwnedOutput output in outputs.OrderByDescending(o => o.Height))
        {
            string account = output.Subaddress.IsZero
                ? "main"
                : $"{output.Subaddress.Major}/{output.Subaddress.Minor}";

            rows.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0,9}  {1,20} XMR  {2,-8} {3}",
                output.Height,
                Dashboard.Format(output.Amount),
                account,
                isSpent(output) ? "spent" : ""));
        }

        if (rows.Count == 0) rows.Add("  nothing yet — run a scan");

        list.SetSource(rows);
        SetNeedsDisplay();
    }
}

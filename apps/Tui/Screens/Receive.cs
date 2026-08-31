using System.Globalization;
using Moonlight.Wallet;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>
/// An address to be paid at, as text and as a QR code. Subaddresses are the point
/// of this screen: a fresh one per payer is what keeps two payers from seeing that
/// they paid the same wallet.
/// </summary>
internal sealed class Receive : FrameView
{
    private readonly Account account;
    private readonly TextField text;
    private readonly Label code;
    private readonly TextField major;
    private readonly TextField minor;

    public Receive(Account account)
        : base("Receive")
    {
        ArgumentNullException.ThrowIfNull(account);
        this.account = account;

        Add(new Label("account") { X = 2, Y = 1, ColorScheme = Colors.Dialog });
        major = new TextField("0") { X = 12, Y = 1, Width = 6 };

        Add(new Label("address") { X = 20, Y = 1, ColorScheme = Colors.Dialog });
        minor = new TextField("0") { X = 30, Y = 1, Width = 6 };

        Button show = new("Show") { X = 38, Y = 1 };
        show.Clicked += Refresh;

        text = new TextField("") { X = 2, Y = 3, Width = Dim.Fill(2), ReadOnly = true };
        // Its own scheme, not the frame's: dark modules on a light field.
        code = new Label("")
        {
            X = 2,
            Y = 5,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
            ColorScheme = Theme.Qr,
        };

        Add(major, minor, show, text, code);
        Refresh();
    }

    private void Refresh()
    {
        uint accountIndex = Parse(major.Text?.ToString());
        uint addressIndex = Parse(minor.Text?.ToString());

        Address address = Subaddress.Address(account, new SubaddressIndex(accountIndex, addressIndex));
        text.Text = address.Encode();

        // The QR carries the bare address. A monero: URI would let a payer's wallet
        // prefill an amount, which is not what this screen is for.
        code.Text = string.Join('\n', Qr.Render(address.Encode()));
        SetNeedsDisplay();
    }

    private static uint Parse(string? value)
        => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed) ? parsed : 0;
}

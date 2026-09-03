using Avalonia.Media;

namespace Moonlight.Gui.Demo.Models;

public sealed class TransactionRow
{
    private static readonly Geometry PendingIcon = Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,4h2v4.5l3,2-1,1.5-4-2.5z");
    private static readonly Geometry ConfirmedIcon = Geometry.Parse("M1,8.5 3.2,6.3 6.4,9.5 12.8,3 15,5.2 6.4,14z");
    private static readonly Geometry FailedIcon = Geometry.Parse("M8,1 15,14H1z M7,5h2v5H7z M7,11.5h2v2H7z");
    private static readonly Geometry[] ClockIcons =
    [
        Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,3h2v5l3.5,0v2H7z"),
        Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,3h2v5l3,2-1,1.5L7,9z"),
        Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,3h2v6H7z"),
        Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,3h2v6l-3,2-1-1.5L7,8z"),
        Geometry.Parse("M8,1a7,7 0 1,0 0,14A7,7 0 0,0 8,1z M7,3h2v7H7z M4,7h3v2H4z")
    ];

    public bool Failed { get; init; }
    public bool Pending { get; init; }
    public bool IsOutgoing { get; init; }
    public int Confirmations { get; init; }
    public int ConfirmationsRequired { get; init; } = 10;
    public required DateTime Date { get; init; }
    public required string TransactionId { get; init; }
    public string Address { get; init; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; init; }
    public IBrush AmountBrush => Amount < 0 ? Brush.Parse("#C4574F") : Brush.Parse("#5FA463");
    public string DateText => Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    public string AmountText => Amount.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture);
    public string TransactionIdDisplay => TransactionId.Length <= 16 ? TransactionId : $"{TransactionId[..8]}…{TransactionId[^8..]}";
    public bool CanResend => IsOutgoing && (Failed || Pending);
    public int ConfirmationStage
    {
        get
        {
            if (Failed) return -2;
            if (Pending) return -1;
            if (Confirmations >= ConfirmationsRequired) return 6;
            int required = Math.Max(1, ConfirmationsRequired);
            return Math.Clamp((int)Math.Ceiling(Confirmations * 5d / required), 1, 5);
        }
    }
    public Geometry StatusIcon
    {
        get
        {
            if (Failed) return FailedIcon;
            if (Pending) return PendingIcon;
            if (Confirmations >= ConfirmationsRequired) return ConfirmedIcon;
            return ClockIcons[ConfirmationStage - 1];
        }
    }
    public IBrush StatusBrush => Failed
        ? Brush.Parse("#C4574F")
        : Confirmations >= ConfirmationsRequired
            ? Brush.Parse("#5FA463")
            : Brush.Parse("#D9A441");
    public string StatusToolTip => Failed
        ? "Transaction failed"
        : Pending
            ? "Unconfirmed transaction"
            : Confirmations >= ConfirmationsRequired
                ? $"{Confirmations} confirmations"
                : $"{Confirmations}/{ConfirmationsRequired} confirmations";
}

public sealed class AddressRow
{
    public int Index { get; init; }
    public required string Address { get; init; }
    public string Label { get; set; } = string.Empty;
    public bool IsUsed { get; init; }
    public bool IsPinned { get; set; }
    public bool IsHidden { get; set; }
    public bool IsChange { get; init; }
    public bool ShowFullAddress { get; set; }
    public string IndexText => $"#{Index}";
    public string AddressDisplay
    {
        get
        {
            if (Address.Length <= 31) return Address;
            string first = string.Join(' ', Address[..15].Chunk(5).Select(chars => new string(chars)));
            string last = string.Join(' ', Address[^15..].Chunk(5).Select(chars => new string(chars)));
            return $"{first} … {last}";
        }
    }
    public string AddressText => ShowFullAddress ? Address : AddressDisplay;

    // A used address recedes rather than being flagged with a fill; the Used column carries the fact.
    public IBrush Ink => IsUsed ? Brush.Parse("#767E88") : Brush.Parse("#D7DBE0");
}

public sealed class ContactRow
{
    public required string Name { get; set; }
    public required string Address { get; init; }
    public bool ShowFullAddress { get; set; }
    public string AddressDisplay => Address.Length <= 31 ? Address : $"{Address[..15]}…{Address[^15..]}";
    public string AddressText => ShowFullAddress ? Address : AddressDisplay;
}

public enum CoinState
{
    Available,
    Locked,
    Spent,
    Frozen,
    SelectedForSpend
}

public sealed class CoinRow
{
    public required string PublicKey { get; init; }
    public required string TransactionId { get; init; }
    public required string Address { get; init; }
    public string Label { get; set; } = string.Empty;
    public int BlockHeight { get; init; }
    public int SpentHeight { get; init; }
    public decimal Amount { get; init; }
    public CoinState State { get; set; }
    public string AmountText => Amount.ToString("0.000000000000", CultureInfo.InvariantCulture);
    public bool IsSpent => State == CoinState.Spent;
    public bool IsFrozen => State == CoinState.Frozen;

    public string SpentHeightText => IsSpent ? SpentHeight.ToString(CultureInfo.InvariantCulture) : "0";

    /// <summary>Keys and hashes show their first eight characters; the rest never fits a column.</summary>
    public string PublicKeyDisplay => PublicKey.Length <= 8 ? PublicKey : PublicKey[..8];

    /// <summary>The transaction hash, likewise cut to eight.</summary>
    public string TransactionIdDisplay => TransactionId.Length <= 8 ? TransactionId : TransactionId[..8];

    /// <summary>
    /// An amount carries twelve decimals, and for most outputs all but a few are zero. The
    /// significant digits are set solid and the run of zeros behind them is dimmed, so a
    /// column of amounts can be read by size instead of counted digit by digit.
    /// </summary>
    public string AmountHead => AmountText[..SignificantLength];

    /// <summary>The trailing zeros, drawn faintly.</summary>
    public string AmountTail => AmountText[SignificantLength..];

    private int SignificantLength
    {
        get
        {
            string text = AmountText;
            int point = text.IndexOf('.', StringComparison.Ordinal);
            int end = text.Length;
            while (end > point + 2 && text[end - 1] == '0')
                end--;
            return end;
        }
    }

    /// <summary>The glyph in the leading column, which is what says what state an output is in.</summary>
    public Geometry StateIcon => State switch
    {
        CoinState.Locked => LockedIcon,
        CoinState.Spent => SpentIcon,
        CoinState.Frozen => FrozenIcon,
        CoinState.SelectedForSpend => SelectedIcon,
        _ => AvailableIcon
    };

    public IBrush StateBrush => State switch
    {
        CoinState.Locked => Brush.Parse("#D9A441"),
        CoinState.Spent => Brush.Parse("#C4574F"),
        CoinState.Frozen => Brush.Parse("#6B8FC7"),
        CoinState.SelectedForSpend => Brush.Parse("#5FA463"),
        _ => Brush.Parse("#3B434C")
    };

    // A spent output recedes by taking a lower-contrast ink. Fading the whole row would fade
    // the glyph that says it is spent along with it.
    public IBrush Ink => IsSpent ? Brush.Parse("#767E88") : Brush.Parse("#D7DBE0");
    public IBrush InkDim => IsSpent ? Brush.Parse("#5E666F") : Brush.Parse("#78808A");
    public IBrush AmountBrush => IsSpent ? Brush.Parse("#8A6A4E") : Brush.Parse("#E8873A");
    public double TailOpacity => IsSpent ? 0.45 : 0.3;

    private static readonly Geometry AvailableIcon = Geometry.Parse("M8,5.2a2.8,2.8 0 1,0 0.01,0z");
    private static readonly Geometry LockedIcon = Geometry.Parse("M5,7.4V5.4a3,3 0 0,1 6,0v2h-1.4V5.4a1.6,1.6 0 0,0-3.2,0v2z M3.8,7.4h8.4v6H3.8z");
    private static readonly Geometry SpentIcon = Geometry.Parse("M4.4,3.4 12.6,11.6 11.6,12.6 3.4,4.4z M11.6,3.4 12.6,4.4 4.4,12.6 3.4,11.6z");
    private static readonly Geometry FrozenIcon = Geometry.Parse("M7.4,2.4h1.2v11.2H7.4z M2.9,4.7 12.5,10.3 11.9,11.3 2.3,5.7z M13.1,4.7 13.7,5.7 4.1,11.3 3.5,10.3z");
    private static readonly Geometry SelectedIcon = Geometry.Parse("M3.4,8 6.6,11.2 12.6,4.6 13.6,5.7 6.6,13.4 2.3,9.1z");
}

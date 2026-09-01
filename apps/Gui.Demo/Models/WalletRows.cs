using Avalonia.Media;

namespace Moonlight.Gui.Demo.Models;

public sealed class TransactionRow
{
    public required string Status { get; init; }
    public required IBrush StatusBrush { get; init; }
    public required DateTime Date { get; init; }
    public required string TransactionId { get; init; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal FiatAmount { get; init; }
    public IBrush AmountBrush => Amount < 0 ? Brush.Parse("#C01F2F") : Brush.Parse("#202428");
    public string DateText => Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    public string AmountText => Amount.ToString("+0.000000;-0.000000;0.000000", CultureInfo.InvariantCulture);
    public string FiatText => FiatAmount.ToString("€+0.00;€-0.00;€0.00", CultureInfo.InvariantCulture);
}

public sealed class AddressRow
{
    public int Index { get; init; }
    public required string Address { get; init; }
    public string Label { get; set; } = string.Empty;
    public bool IsUsed { get; init; }
    public bool IsPinned { get; init; }
    public string IndexText => IsPinned ? $"●  #{Index}" : $"#{Index}";
    public IBrush AddressBackground => IsUsed ? Brush.Parse("#FFB6BC") : Brushes.Transparent;
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
    public decimal Amount { get; init; }
    public CoinState State { get; set; }
    public string AmountText => Amount.ToString("0.000000000000", CultureInfo.InvariantCulture);
    public IBrush RowBrush => State switch
    {
        CoinState.Locked => Brush.Parse("#FFF500"),
        CoinState.Spent => Brush.Parse("#F18087"),
        CoinState.Frozen => Brush.Parse("#8CB3F2"),
        CoinState.SelectedForSpend => Brush.Parse("#8AF296"),
        _ => Brushes.Transparent
    };
}

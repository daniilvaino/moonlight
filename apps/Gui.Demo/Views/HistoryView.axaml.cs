using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Moonlight.Gui.Demo.Models;

namespace Moonlight.Gui.Demo.Views;

public sealed partial class HistoryView : UserControl
{
    private readonly List<TransactionRow> rows =
    [
        new() { Failed = true, IsOutgoing = true, Date = new(2025, 4, 1, 9, 43, 0), TransactionId = "1f62ff84e6c532fa8909ad738ae6a063", Address = "88usdUphRq6CGJuQWSvuSyPoVVacreHJRFiFf", Description = "Donation", Amount = -0.010031m },
        new() { Pending = true, IsOutgoing = true, Date = new(2025, 4, 1, 9, 16, 0), TransactionId = "4d02e6c4fba31ed6991e726cd958ce41", Address = "87X7dGWaQB6WoRXcqAeZGkEMGpaVif3rcMHr78DN", Description = "", Amount = 0.010000m },
        new() { Confirmations = 4, Date = new(2025, 4, 1, 9, 13, 0), TransactionId = "30d65635ec46ae411fc26873d04acac0", Address = "82VjQ2LZqkNcWqS7B2drLr4zCsYavfyWfPX6NSP5MC6", Description = "Exchange", Amount = 0.020000m },
        new() { Confirmations = 42, Date = new(2025, 3, 28, 18, 22, 0), TransactionId = "6e2cc99f5489953cf507b59761632f87", Address = "86ymLxZ6tn8CXKcDYQ2E6mJ4EehW4PFnmV3oT", Description = "Mining payout", Amount = 0.000010m }
    ];
    private bool transactionIdVisibleBeforeSearch;
    private bool transactionIdForcedBySearch;
    private readonly DataGridCollectionView view;
    private string query = string.Empty;

    public event EventHandler<string>? StatusChanged;

    public HistoryView()
    {
        InitializeComponent();
        view = new DataGridCollectionView(rows)
        {
            Culture = CultureInfo.CurrentCulture,
            Filter = MatchesFilter
        };
        Table.ItemsSource = view;
        UiState state = UiStateStore.Load();
        if (state.HistoryColumns is { Length: 4 } columns && columns.Any(visible => visible))
        {
            for (int index = 0; index < columns.Length; index++)
                Table.Columns[index].IsVisible = columns[index];
            DateColumnMenu.IsChecked = columns[0];
            TransactionIdColumnMenu.IsChecked = columns[1];
            DescriptionColumnMenu.IsChecked = columns[2];
            AmountColumnMenu.IsChecked = columns[3];
        }
        FullTransactionIdMenu.IsChecked = state.HistoryFullTxid;
        ApplyFullTransactionIds(state.HistoryFullTxid);
        FitToWindow(null, new RoutedEventArgs());
    }

    private void TableLoaded(object? sender, RoutedEventArgs e)
    {
        Table.Columns[0].Sort(ListSortDirection.Descending);
        Table.SelectedIndex = -1;
    }

    private void SearchChanged(object? sender, TextChangedEventArgs e)
    {
        query = SearchBox.Text?.Trim() ?? string.Empty;
        view.Refresh();

        if (query.Length > 0 && !transactionIdForcedBySearch)
        {
            transactionIdVisibleBeforeSearch = Table.Columns[1].IsVisible;
            Table.Columns[1].IsVisible = true;
            transactionIdForcedBySearch = true;
        }
        else if (query.Length == 0 && transactionIdForcedBySearch)
        {
            Table.Columns[1].IsVisible = transactionIdVisibleBeforeSearch;
            transactionIdForcedBySearch = false;
        }
    }

    private bool MatchesFilter(object item)
    {
        if (item is not TransactionRow row || query.Length == 0)
            return true;

        CompareInfo comparer = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return comparer.IndexOf(row.DateText, query, options) >= 0 ||
               comparer.IndexOf(row.TransactionId, query, options) >= 0 ||
               comparer.IndexOf(row.Address, query, options) >= 0 ||
               comparer.IndexOf(row.Description, query, options) >= 0 ||
               comparer.IndexOf(row.AmountText, query, options) >= 0;
    }

    private void ToggleColumn(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string tag } item || !int.TryParse(tag, out int index))
            return;

        int visibleColumns = Table.Columns.Count(column => column.IsVisible);
        if (!item.IsChecked && visibleColumns == 1)
        {
            item.IsChecked = true;
            StatusChanged?.Invoke(this, "At least one column must remain visible");
            return;
        }
        Table.Columns[index].IsVisible = item.IsChecked;
        PersistViewState();
    }

    private void ToggleFullTransactionIds(object? sender, RoutedEventArgs e)
    {
        bool showFull = sender is MenuItem { IsChecked: true };
        ApplyFullTransactionIds(showFull);
        PersistViewState();
    }

    private void ApplyFullTransactionIds(bool showFull)
    {
        if (Table.Columns[1] is DataGridTextColumn column)
            column.Binding = new Binding(showFull ? nameof(TransactionRow.TransactionId) : nameof(TransactionRow.TransactionIdDisplay));
        Table.Columns[1].Width = showFull ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.SizeToCells;
    }

    private void FitToWindow(object? sender, RoutedEventArgs e)
    {
        foreach (DataGridColumn column in Table.Columns.Where(column => column.IsVisible))
            column.Width = DataGridLength.SizeToCells;
        if (Table.Columns[2].IsVisible)
            Table.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
    }

    private void FitToContents(object? sender, RoutedEventArgs e)
    {
        foreach (DataGridColumn column in Table.Columns.Where(column => column.IsVisible))
            column.Width = DataGridLength.SizeToCells;
    }

    private void ResetDefaults(object? sender, RoutedEventArgs e)
    {
        Table.Columns[0].IsVisible = true;
        Table.Columns[1].IsVisible = query.Length > 0;
        Table.Columns[2].IsVisible = true;
        Table.Columns[3].IsVisible = true;
        DateColumnMenu.IsChecked = true;
        TransactionIdColumnMenu.IsChecked = query.Length > 0;
        DescriptionColumnMenu.IsChecked = true;
        AmountColumnMenu.IsChecked = true;
        FullTransactionIdMenu.IsChecked = false;
        if (Table.Columns[1] is DataGridTextColumn txidColumn)
            txidColumn.Binding = new Binding(nameof(TransactionRow.TransactionIdDisplay));
        Table.Columns[0].Width = DataGridLength.SizeToCells;
        Table.Columns[1].Width = new DataGridLength(150);
        Table.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        Table.Columns[3].Width = DataGridLength.SizeToCells;
        view.SortDescriptions.Clear();
        Table.Columns[0].Sort(ListSortDirection.Descending);
        FitToWindow(null, new RoutedEventArgs());
        transactionIdForcedBySearch = query.Length > 0;
        transactionIdVisibleBeforeSearch = false;
        PersistViewState();
        StatusChanged?.Invoke(this, "History columns reset");
    }

    private void PersistViewState() => UiStateStore.Save(state =>
    {
        bool[] columns = Table.Columns.Select(column => column.IsVisible).ToArray();
        if (transactionIdForcedBySearch)
            columns[1] = transactionIdVisibleBeforeSearch;
        state.HistoryColumns = columns;
        state.HistoryFullTxid = FullTransactionIdMenu.IsChecked;
    });

    public void SetSearchText(string text) => SearchBox.Text = text;

    /// <summary>Shows or hides the incomplete-history warning, as the wallet's sync state changes.</summary>
    public void SetSynchronizing(bool synchronizing) => SyncNotice.IsVisible = synchronizing;

    private void ShowSyncInfo(object? sender, RoutedEventArgs e) =>
        StatusChanged?.Invoke(this, "History is filled in as the wallet scans the chain");

    private void CloseSyncNotice(object? sender, RoutedEventArgs e) => SyncNotice.IsVisible = false;

    private async void CopyField(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is not TransactionRow row || sender is not MenuItem { Tag: string field })
            return;

        string text = field switch
        {
            nameof(TransactionRow.DateText) => row.DateText,
            nameof(TransactionRow.Description) => row.Description,
            nameof(TransactionRow.AmountText) => row.AmountText,
            _ => row.TransactionId
        };
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
        StatusChanged?.Invoke(this, $"{field} copied");
    }

    private void BeginDescriptionEdit(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is null)
        {
            StatusChanged?.Invoke(this, "Select a transaction");
            return;
        }
        Table.CurrentColumn = Table.Columns[2];
        Table.BeginEdit();
    }

    private async void ShowDetails(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is not TransactionRow row) return;
        await DemoDialog.ShowInfoAsync(this, "Transaction details", $"Txid: {row.TransactionId}\nDate: {row.DateText}\nDescription: {row.Description}\nAmount: {row.AmountText}\nConfirmations: {row.Confirmations}");
    }

    private void HistoryContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        TransactionRow? row = Table.SelectedItem as TransactionRow;
        ResendTransactionMenu.IsVisible = row?.CanResend == true;
        RemoveFromHistoryMenu.IsVisible = row?.Failed == true;
    }

    private async void ResendTransaction(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is not TransactionRow { CanResend: true } row) return;
        if (await DemoDialog.ConfirmAsync(this, "Resend transaction", $"Resend transaction {row.TransactionIdDisplay}?"))
            StatusChanged?.Invoke(this, "Transaction queued for resend");
    }

    private async void RemoveFromHistory(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is TransactionRow { Failed: true } row)
        {
            if (!await DemoDialog.ConfirmAsync(this, "Remove transaction from history", "Are you sure you want to remove this transaction from the history?"))
                return;
            rows.Remove(row);
            view.Refresh();
            StatusChanged?.Invoke(this, "Failed transaction removed from history");
        }
    }

    private async void ViewOnBlockExplorer(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is TransactionRow row)
            await DemoDialog.ShowInfoAsync(this, "External link", $"The block explorer will open transaction {row.TransactionIdDisplay}. Verify the destination before continuing.");
    }

    private async void CreateTransactionProof(object? sender, RoutedEventArgs e)
    {
        if (Table.SelectedItem is TransactionRow row)
            await DemoDialog.ShowInfoAsync(this, "Create Tx Proof", $"Transaction: {row.TransactionId}\nEnter the recipient address and message in the connected wallet flow.");
    }
}

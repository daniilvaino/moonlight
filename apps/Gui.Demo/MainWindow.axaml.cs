using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Moonlight.Gui.Demo;

public sealed partial class MainWindow : Window
{
    private readonly Control[] pages;
    private readonly Button[] tabs;

    public MainWindow()
    {
        InitializeComponent();

        pages = [HomePage, HistoryPage, SendPage, ReceivePage, CoinsPage, ContactsPage, NotesPage, CalcPage];
        tabs = [HomeTab, HistoryTab, SendTab, ReceiveTab, CoinsTab, ContactsTab, NotesTab, CalcTab];

        HistoryPage.StatusChanged += ShowStatus;
        ReceivePage.StatusChanged += ShowStatus;
        ReceivePage.ShowTransactionsRequested += ShowAddressTransactions;
        CoinsPage.StatusChanged += ShowStatus;
        ContactsPage.StatusChanged += ShowStatus;
        ContactsPage.PayToRequested += PayToContact;
    }

    private void ShowStatus(object? sender, string message) => StatusText.Text = message;

    private void ShowAddressTransactions(object? sender, string address)
    {
        Show(HistoryPage, HistoryTab);
        HistoryPage.SetSearchText(address);
        StatusText.Text = "Transactions for selected address";
    }

    private void PayToContact(object? sender, string address)
    {
        Show(SendPage, SendTab);
        PayToBox.Text = address;
        StatusText.Text = "Contact selected as recipient";
    }

    private void ShowHome(object? sender, RoutedEventArgs e) => Show(HomePage, HomeTab);
    private void ShowHistory(object? sender, RoutedEventArgs e) => Show(HistoryPage, HistoryTab);
    private void ShowSend(object? sender, RoutedEventArgs e) => Show(SendPage, SendTab);
    private void ShowReceive(object? sender, RoutedEventArgs e) => Show(ReceivePage, ReceiveTab);
    private void ShowCoins(object? sender, RoutedEventArgs e) => Show(CoinsPage, CoinsTab);
    private void ShowContacts(object? sender, RoutedEventArgs e) => Show(ContactsPage, ContactsTab);
    private void ShowNotes(object? sender, RoutedEventArgs e) => Show(NotesPage, NotesTab);
    private void ShowCalc(object? sender, RoutedEventArgs e) => Show(CalcPage, CalcTab);

    private void Show(Control selectedPage, Button selectedTab)
    {
        foreach (Control page in pages)
            page.IsVisible = ReferenceEquals(page, selectedPage);

        foreach (Button tab in tabs)
            tab.Classes.Set("active", ReferenceEquals(tab, selectedTab));

        StatusText.Text = "Synchronized";
    }

    private void MenuAction(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
            StatusText.Text = $"{item.Header} · not connected";
    }

    private void ClearSend(object? sender, RoutedEventArgs e)
    {
        PayToBox.Clear();
        DescriptionBox.Clear();
        AmountBox.Clear();
        StatusText.Text = "Payment form cleared";
    }

    private void ScanPayment(object? sender, RoutedEventArgs e) => StatusText.Text = "QR scanner · not connected";

    private void UseMaximum(object? sender, RoutedEventArgs e)
    {
        AmountBox.Text = "0.019969";
        StatusText.Text = "Maximum spendable amount selected";
    }

    private void AmountChanged(object? sender, TextChangedEventArgs e)
    {
        ConversionText.Text = decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)
            ? $"~€{amount * 201.19m:0.00}"
            : string.Empty;
    }

    private void ResolveAlias(object? sender, RoutedEventArgs e) => StatusText.Text = "OpenAlias resolver · not connected";

    private void NewsAction(object? sender, RoutedEventArgs e) =>
        StatusText.Text = sender is Button { Tag: string action } && action == "donation"
            ? "Donation payment prepared"
            : "External link ready to open";

    private void Quit(object? sender, RoutedEventArgs e) => Close();
}

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

        pages = [HomePage, HistoryPage, SendPage, ReceivePage, CoinsPage, NotesPage, CalcPage];
        tabs = [HomeTab, HistoryTab, SendTab, ReceiveTab, CoinsTab, NotesTab, CalcTab];

        HistoryPage.StatusChanged += ShowStatus;
        ReceivePage.StatusChanged += ShowStatus;
        CoinsPage.StatusChanged += ShowStatus;
    }

    private void ShowStatus(object? sender, string message) => StatusText.Text = message;

    private void ShowHome(object? sender, RoutedEventArgs e) => Show(HomePage, HomeTab);
    private void ShowHistory(object? sender, RoutedEventArgs e) => Show(HistoryPage, HistoryTab);
    private void ShowSend(object? sender, RoutedEventArgs e) => Show(SendPage, SendTab);
    private void ShowReceive(object? sender, RoutedEventArgs e) => Show(ReceivePage, ReceiveTab);
    private void ShowCoins(object? sender, RoutedEventArgs e) => Show(CoinsPage, CoinsTab);
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

    private void Quit(object? sender, RoutedEventArgs e) => Close();
}

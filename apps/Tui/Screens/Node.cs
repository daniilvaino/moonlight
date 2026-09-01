using Moonlight.Node;
using Terminal.Gui;

namespace Moonlight.Tui.Screens;

/// <summary>Which daemon we are talking to, and whether it is answering.</summary>
internal sealed class Node : FrameView
{
    private readonly TextField address;
    private readonly Label status;

    public Node(Uri daemon)
        : base("Node")
    {
        ArgumentNullException.ThrowIfNull(daemon);

        Add(new Label("daemon") { X = 2, Y = 1, ColorScheme = Colors.Dialog });
        address = new TextField(daemon.ToString()) { X = 12, Y = 1, Width = Dim.Fill(12) };

        Button check = new("Use") { X = Pos.AnchorEnd(9), Y = 1 };
        check.Clicked += () => _ = Apply();

        Add(new Label("status") { X = 2, Y = 3, ColorScheme = Colors.Dialog });
        status = new Label("not checked") { X = 12, Y = 3, Width = Dim.Fill(2) };

        Add(address, check, status);
    }

    /// <summary>
    /// Raised when the address is accepted. Typing one and having nothing happen is
    /// worse than not offering the field: the wallet has to move to it and remember
    /// it, which is what this tells the application to do.
    /// </summary>
    public event Action<Uri>? Changed;

    public async Task Apply()
    {
        Uri chosen;

        try
        {
            chosen = Address;
        }
        catch (UriFormatException e)
        {
            status.Text = $"not an address — {e.Message}";
            SetNeedsDisplay();
            return;
        }

        await Check().ConfigureAwait(true);
        Changed?.Invoke(chosen);
    }

    public Uri Address => new(address.Text?.ToString() ?? "http://127.0.0.1:18081/");

    public async Task Check()
    {
        status.Text = "asking…";
        SetNeedsDisplay();

        try
        {
            using DaemonClient client = new(Address);
            ulong height = await client.GetHeightAsync().ConfigureAwait(true);

            status.Text = $"height {height}";
        }
        catch (Exception e) when (e is HttpRequestException or DaemonException or UriFormatException or TaskCanceledException)
        {
            // A daemon that is not there is the ordinary case, not a crash.
            status.Text = $"unreachable — {e.Message}";
        }

        SetNeedsDisplay();
    }
}

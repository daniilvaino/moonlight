using Moonlight.Diagnostics;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The log is shared state, so these run one at a time and put it back as they
/// found it.
/// </summary>
[Collection("log")]
public sealed class LogTests : IDisposable
{
    private readonly Level level = Log.Minimum;
    private readonly string? file = Log.File;

    public LogTests()
    {
        Log.Clear();
        Log.File = null;
        Log.Minimum = Level.Info;
    }

    [Fact]
    public void KeepsWhatItIsGiven()
    {
        Log.Info("node", "hello");
        Log.Error("sync", "gone");

        Entry[] lines = Log.Recent();

        Assert.Equal(2, lines.Length);
        Assert.Equal(Level.Info, lines[0].Level);
        Assert.Equal("node", lines[0].Source);
        Assert.Equal("gone", lines[1].Message);
    }

    [Fact]
    public void QuietLevelsAreDropped()
    {
        Log.Debug("sync", "a batch");

        Assert.Empty(Log.Recent());

        Log.Minimum = Level.Debug;
        Log.Debug("sync", "a batch");

        Assert.Single(Log.Recent());
    }

    /// <summary>
    /// The type of an exception is half of what it says — an HttpRequestException
    /// and a FormatException from the same call mean very different things.
    /// </summary>
    [Fact]
    public void AnExceptionKeepsItsType()
    {
        Log.Error("sync", "catch-up failed", new FormatException("bad blob"));

        Entry entry = Assert.Single(Log.Recent());

        Assert.Contains("FormatException", entry.Message, StringComparison.Ordinal);
        Assert.Contains("bad blob", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>A wallet left open for a day must not grow a log until it dies.</summary>
    [Fact]
    public void TheOldestLinesGo()
    {
        for (int i = 0; i < Log.Capacity + 50; i++) Log.Info("test", i.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Entry[] lines = Log.Recent();

        Assert.Equal(Log.Capacity, lines.Length);
        Assert.Equal("50", lines[0].Message);
        Assert.Equal((Log.Capacity + 49).ToString(System.Globalization.CultureInfo.InvariantCulture), lines[^1].Message);
    }

    [Fact]
    public void WritesToItsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "moonlight-log-" + Guid.NewGuid().ToString("N"));
        Log.File = path;

        try
        {
            Log.Info("wallet", "opened");

            Assert.Contains("opened", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Log.File = null;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// A log that cannot be written is not a reason to stop a wallet, and it must
    /// not keep throwing on every line either.
    /// </summary>
    [Fact]
    public void AnUnwritableFileIsGivenUpOn()
    {
        Log.File = Path.Combine(Path.GetTempPath(), "moonlight-missing-" + Guid.NewGuid().ToString("N"), "deep", "log.txt");

        Log.Info("wallet", "opened");

        Assert.Null(Log.File);
        Assert.Single(Log.Recent());
    }

    [Fact]
    public void FollowersAreTold()
    {
        List<Entry> seen = [];
        void Follow(Entry entry) => seen.Add(entry);

        Log.Written += Follow;

        try
        {
            Log.Info("node", "connected");
        }
        finally
        {
            Log.Written -= Follow;
        }

        Assert.Equal("connected", Assert.Single(seen).Message);
    }

    public void Dispose()
    {
        Log.Clear();
        Log.Minimum = level;
        Log.File = file;
    }
}

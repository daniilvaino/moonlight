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
    /// <summary>
    /// A source nobody else uses, so these tests can be told apart from whatever
    /// else is logging. The log is global and the rest of the suite runs beside it,
    /// which made a test that counted lines fail about one run in three.
    /// </summary>
    private readonly string source = "test-" + Guid.NewGuid().ToString("N")[..8];

    private readonly Level level = Log.Minimum;
    private readonly string? file = Log.File;

    private string Source => source;

    private Entry[] Mine() => [.. Log.Recent().Where(e => e.Source == source)];

    public LogTests()
    {
        Log.Clear();
        Log.File = null;
        Log.Minimum = Level.Info;
    }

    [Fact]
    public void KeepsWhatItIsGiven()
    {
        Log.Info(Source, "hello");
        Log.Error(Source, "gone");

        Entry[] lines = Mine();

        Assert.Equal(2, lines.Length);
        Assert.Equal(Level.Info, lines[0].Level);
        Assert.Equal("hello", lines[0].Message);
        Assert.Equal("gone", lines[1].Message);
    }

    [Fact]
    public void QuietLevelsAreDropped()
    {
        Log.Debug(Source, "a batch");

        Assert.Empty(Mine());

        Log.Minimum = Level.Debug;
        Log.Debug(Source, "a batch");

        Assert.Single(Mine());
    }

    /// <summary>
    /// The type of an exception is half of what it says — an HttpRequestException
    /// and a FormatException from the same call mean very different things.
    /// </summary>
    [Fact]
    public void AnExceptionKeepsItsType()
    {
        Log.Error(Source, "catch-up failed", new FormatException("bad blob"));

        Entry entry = Assert.Single(Mine());

        Assert.Contains("FormatException", entry.Message, StringComparison.Ordinal);
        Assert.Contains("bad blob", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>A wallet left open for a day must not grow a log until it dies.</summary>
    [Fact]
    public void TheOldestLinesGo()
    {
        for (int i = 0; i < Log.Capacity + 50; i++) Log.Info(Source, i.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Entry[] lines = Mine();

        // Never more than the capacity, the newest survived, and the first fifty are
        // gone. Not an exact count: the log is global and the rest of the suite is
        // writing to it at the same time, so some of these were evicted by lines
        // belonging to somebody else.
        Assert.True(Log.Recent().Length <= Log.Capacity);
        Assert.Equal((Log.Capacity + 49).ToString(System.Globalization.CultureInfo.InvariantCulture), lines[^1].Message);
        Assert.DoesNotContain(lines, e => e.Message == "0");
    }

    [Fact]
    public void WritesToItsFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "moonlight-log-" + Guid.NewGuid().ToString("N"));
        Log.File = path;

        try
        {
            Log.Info(Source, "opened");

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

        Log.Info(Source, "opened");

        Assert.Null(Log.File);
        Assert.Single(Mine());
    }

    [Fact]
    public void FollowersAreTold()
    {
        List<Entry> seen = [];

        // Only this test's lines: the handler hears everything the suite logs.
        void Follow(Entry entry)
        {
            if (entry.Source == source) lock (seen) seen.Add(entry);
        }

        Log.Written += Follow;

        try
        {
            Log.Info(Source, "connected");
        }
        finally
        {
            Log.Written -= Follow;
        }

        lock (seen) Assert.Equal("connected", Assert.Single(seen).Message);
    }

    public void Dispose()
    {
        Log.Clear();
        Log.Minimum = level;
        Log.File = file;
    }
}

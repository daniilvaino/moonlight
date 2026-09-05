using System.Text;
using Moonlight.Core.Abi;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

/// <summary>
/// The C interface, called the way C calls it: pointers, buffers and status codes.
/// Managed here, but these are the same entry points a shared library exports, so a
/// mistake in the contract shows up as a failing test rather than as a crash in
/// somebody else's process.
/// </summary>
public sealed unsafe class AbiTests : IDisposable
{
    private const int Fast = 1000;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "moonlight-" + Guid.NewGuid().ToString("N"));
    private readonly Account account = Account.Create();
    private readonly string path;

    public AbiTests()
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "w.keys");

        WalletDocument document = new()
        {
            Network = account.Network.ToString(),
            Settings = new WalletSettings { LookaheadAccounts = 1, LookaheadAddresses = 3 },
        };

        Storage.Save(path, document, Storage.Encrypt(
            account, Storage.Seal("p", Fast), 4242, document.Settings.Lookahead, null, null,
            document.SettingsFingerprint()));
    }

    private static byte[] Cstring(string value) => Encoding.UTF8.GetBytes(value + "\0");

    private nint Open(string password = "p")
    {
        byte[] file = Cstring(path);
        byte[] secret = Cstring(password);
        nint handle;

        fixed (byte* p = file)
        fixed (byte* s = secret)
        {
            Assert.Equal(Status.Ok, Api.WalletOpen(p, s, &handle));
        }

        return handle;
    }

    [Fact]
    public void TheVersionIsAnswered() => Assert.Equal(1, Api.AbiVersion());

    [Fact]
    public void OpensAndClosesAWallet()
    {
        nint handle = Open();

        Assert.NotEqual(0, handle);
        Assert.Equal(Status.Ok, Api.WalletClose(handle));
    }

    /// <summary>
    /// A pointer that is not one of ours comes back as an argument error rather than
    /// dereferencing whatever is at that address. This is the difference between a
    /// bug in the caller and a crash in the caller.
    /// </summary>
    [Fact]
    public void AnInventedHandleIsRefused()
    {
        ulong height;

        Assert.Equal(Status.BadArgument, Api.WalletScannedHeight(0x1234, &height));
        Assert.Equal(Status.BadArgument, Api.WalletClose(0x1234));
    }

    /// <summary>Closing twice is a mistake callers make. It is answered, not crashed on.</summary>
    [Fact]
    public void ClosingTwiceIsRefusedRatherThanFatal()
    {
        nint handle = Open();

        Assert.Equal(Status.Ok, Api.WalletClose(handle));
        Assert.Equal(Status.BadArgument, Api.WalletClose(handle));
    }

    /// <summary>The wrong password and a file that is not a wallet are the same answer, deliberately.</summary>
    [Fact]
    public void AWrongPasswordCannotOpen()
    {
        byte[] file = Cstring(path);
        byte[] secret = Cstring("not the password");
        nint handle;

        fixed (byte* p = file)
        fixed (byte* s = secret)
        {
            Assert.Equal(Status.CannotOpen, Api.WalletOpen(p, s, &handle));
        }

        Assert.Equal(0, handle);
    }

    /// <summary>
    /// A buffer too small says so and says how much is wanted, so the caller sizes
    /// it on the second call rather than guessing.
    /// </summary>
    [Fact]
    public void ASmallBufferIsToldTheSizeItNeeds()
    {
        nint handle = Open();

        try
        {
            int needed;
            byte one;

            Assert.Equal(Status.BufferTooSmall, Api.WalletAddress(handle, &one, 1, &needed));
            Assert.Equal(account.Address.Encode().Length + 1, needed);

            byte[] buffer = new byte[needed];

            fixed (byte* b = buffer)
            {
                Assert.Equal(Status.Ok, Api.WalletAddress(handle, b, needed, &needed));
            }

            // Terminated, so it is a C string; the count includes that zero.
            Assert.Equal(0, buffer[^1]);
            Assert.Equal(account.Address.Encode(), Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1));
        }
        finally
        {
            Api.WalletClose(handle);
        }
    }

    [Fact]
    public void ReportsWhereTheScanGotTo()
    {
        nint handle = Open();

        try
        {
            ulong height;

            Assert.Equal(Status.Ok, Api.WalletScannedHeight(handle, &height));
            Assert.Equal(4242UL, height);
        }
        finally
        {
            Api.WalletClose(handle);
        }
    }

    /// <summary>The whole sync contract, driven the way the header describes it.</summary>
    [Fact]
    public void DrivesASweepWithoutTouchingASocket()
    {
        nint handle = Open();

        try
        {
            byte[] path = new byte[64];
            byte[] body = new byte[256];
            int pathNeeded, bodyNeeded;

            fixed (byte* p = path)
            fixed (byte* b = body)
            {
                Assert.Equal(Status.Ok, Api.SyncNext(handle, p, path.Length, &pathNeeded, b, body.Length, &bodyNeeded));

                Assert.Equal("get_height", Encoding.UTF8.GetString(path, 0, pathNeeded - 1));
                Assert.Equal("{}", Encoding.UTF8.GetString(body, 0, bodyNeeded));

                // The wallet is at 4242 and the chain says 4242, so there is nothing
                // to fetch and the sweep is over.
                byte[] answer = Encoding.UTF8.GetBytes("{\"height\":4242,\"status\":\"OK\"}");

                fixed (byte* a = answer)
                {
                    Assert.Equal(Status.Ok, Api.SyncSupply(handle, a, answer.Length));
                }

                Assert.Equal(Status.Done, Api.SyncNext(handle, p, path.Length, &pathNeeded, b, body.Length, &bodyNeeded));

                // And a warm wallet begins again.
                Assert.Equal(Status.Ok, Api.SyncRestart(handle));
                Assert.Equal(Status.Ok, Api.SyncNext(handle, p, path.Length, &pathNeeded, b, body.Length, &bodyNeeded));
            }
        }
        finally
        {
            Api.WalletClose(handle);
        }
    }

    /// <summary>An answer that cannot be read is reported, not carried on from.</summary>
    [Fact]
    public void AnUnreadableAnswerIsReported()
    {
        nint handle = Open();

        try
        {
            byte[] answer = Encoding.UTF8.GetBytes("{\"status\":\"OK\"}");

            fixed (byte* a = answer)
            {
                Assert.Equal(Status.BadResponse, Api.SyncSupply(handle, a, answer.Length));
            }
        }
        finally
        {
            Api.WalletClose(handle);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}

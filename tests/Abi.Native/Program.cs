using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Moonlight.Core;
using Moonlight.Wallet;

namespace Moonlight.Abi.Native;

/// <summary>
/// Checks the shared library the way a foreign application would: load the file,
/// look the symbols up by name, call them.
/// </summary>
/// <remarks>
/// Everything else about the C interface is tested in managed code, which cannot
/// see the door — only the room behind it. A renamed EntryPoint, a header that has
/// drifted from the exports, a build that dropped a symbol as unreachable: all of
/// those leave the managed tests green and every foreign caller broken. This is the
/// only thing that would notice.
///
/// It takes the names from moonlight.h rather than a list of its own, so the header
/// cannot promise a function the library does not have.
/// </remarks>
internal static unsafe class Program
{
    private static int failures;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("moonlight-abi-check <shared library> <moonlight.h>");
            return 2;
        }

        string library = args[0];
        string header = args[1];

        if (!File.Exists(library))
        {
            Console.Error.WriteLine($"no library at {library}");
            return 2;
        }

        nint handle = NativeLibrary.Load(Path.GetFullPath(library));

        Console.WriteLine($"loaded {Path.GetFileName(library)}");

        Dictionary<string, nint> exports = Exports(handle, header);

        CheckVersion(exports);
        CheckWallet(exports);

        Console.WriteLine(failures == 0 ? "ok" : $"{failures} failed");
        return failures == 0 ? 1 - 1 : 1;
    }

    /// <summary>
    /// Every function the header declares, looked up in the library. A name in one
    /// and not the other is the failure this exists to catch.
    /// </summary>
    private static Dictionary<string, nint> Exports(nint library, string header)
    {
        string text = File.ReadAllText(header);

        // Declarations only: "<type> moonlight_x(" at the start of a line, which
        // skips the same names where they appear in the comments above them.
        MatchCollection declared = Regex.Matches(
            text,
            @"^\s*(?:int32_t|moonlight_status)\s+(moonlight_[a-z0-9_]+)\s*\(",
            RegexOptions.Multiline);

        string[] names = [.. declared.Select(m => m.Groups[1].Value).Distinct().Order()];

        if (names.Length == 0)
        {
            Console.Error.WriteLine($"no declarations found in {header} — the check would pass vacuously");
            Environment.Exit(2);
        }

        Dictionary<string, nint> found = [];

        foreach (string name in names)
        {
            if (NativeLibrary.TryGetExport(library, name, out nint address))
            {
                found[name] = address;
            }
            else
            {
                Fail($"{name} is declared in the header and not exported by the library");
            }
        }

        Console.WriteLine($"{found.Count} of {names.Length} declared symbols exported");
        return found;
    }

    private static void CheckVersion(Dictionary<string, nint> exports)
    {
        if (!exports.TryGetValue("moonlight_abi_version", out nint address)) return;

        int version = ((delegate* unmanaged<int>)address)();

        if (version != 1) Fail($"abi version is {version}, expected 1");
    }

    /// <summary>
    /// A wallet written by the managed library, opened and driven through the native
    /// one. The two agreeing on the address is what says they are the same code.
    /// </summary>
    private static void CheckWallet(Dictionary<string, nint> exports)
    {
        foreach (string name in (string[])
            ["moonlight_wallet_open", "moonlight_wallet_close", "moonlight_wallet_address",
             "moonlight_wallet_scanned_height", "moonlight_sync_next", "moonlight_sync_supply"])
        {
            if (!exports.ContainsKey(name)) return;
        }

        string directory = Path.Combine(Path.GetTempPath(), "moonlight-abi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Account account = Account.Create();
            string path = Path.Combine(directory, "w.keys");

            WalletDocument document = new()
            {
                Network = account.Network.ToString(),
                Settings = new WalletSettings { LookaheadAccounts = 1, LookaheadAddresses = 3 },
            };

            Storage.Save(path, document, Storage.Encrypt(
                account, Storage.Seal("p", 1000), 4242, document.Settings.Lookahead, null, null,
                document.SettingsFingerprint()));

            nint wallet = 0;

            fixed (byte* file = Cstring(path))
            fixed (byte* password = Cstring("p"))
            {
                int opened = ((delegate* unmanaged<byte*, byte*, nint*, int>)exports["moonlight_wallet_open"])(
                    file, password, &wallet);

                if (opened != 0)
                {
                    Fail($"moonlight_wallet_open returned {opened}");
                    return;
                }
            }

            CheckAddress(exports, wallet, account.Address.Encode());
            CheckSweep(exports, wallet);

            int closed = ((delegate* unmanaged<nint, int>)exports["moonlight_wallet_close"])(wallet);
            if (closed != 0) Fail($"moonlight_wallet_close returned {closed}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckAddress(Dictionary<string, nint> exports, nint wallet, string expected)
    {
        byte[] buffer = new byte[256];
        int needed;

        fixed (byte* into = buffer)
        {
            int status = ((delegate* unmanaged<nint, byte*, int, int*, int>)exports["moonlight_wallet_address"])(
                wallet, into, buffer.Length, &needed);

            if (status != 0)
            {
                Fail($"moonlight_wallet_address returned {status}");
                return;
            }
        }

        string address = Encoding.UTF8.GetString(buffer, 0, needed - 1);

        if (address != expected)
        {
            Fail($"the library reports a different address than the wallet it opened{Environment.NewLine}" +
                 $"  library: {address}{Environment.NewLine}  managed: {expected}");
        }
        else
        {
            Console.WriteLine($"address agrees: {address[..16]}…");
        }
    }

    /// <summary>
    /// The sweep, driven the way the header describes it: ask, answer, ask again.
    /// The wallet is at the height the answer names, so one exchange finishes it.
    /// </summary>
    private static void CheckSweep(Dictionary<string, nint> exports, nint wallet)
    {
        byte[] path = new byte[64];
        byte[] body = new byte[256];
        int pathNeeded, bodyNeeded;

        var next = (delegate* unmanaged<nint, byte*, int, int*, byte*, int, int*, int>)exports["moonlight_sync_next"];

        fixed (byte* p = path)
        fixed (byte* b = body)
        {
            int status = next(wallet, p, path.Length, &pathNeeded, b, body.Length, &bodyNeeded);

            if (status != 0)
            {
                Fail($"moonlight_sync_next returned {status}");
                return;
            }

            string asked = Encoding.UTF8.GetString(path, 0, pathNeeded - 1);

            if (asked != "get_height") Fail($"the first request is {asked}, expected get_height");

            byte[] answer = Encoding.UTF8.GetBytes("{\"height\":4242,\"status\":\"OK\"}");

            fixed (byte* a = answer)
            {
                int supplied = ((delegate* unmanaged<nint, byte*, int, int>)exports["moonlight_sync_supply"])(
                    wallet, a, answer.Length);

                if (supplied != 0) Fail($"moonlight_sync_supply returned {supplied}");
            }

            // 1 is DONE: the wallet is at the tip the answer named.
            int done = next(wallet, p, path.Length, &pathNeeded, b, body.Length, &bodyNeeded);

            if (done != 1) Fail($"the sweep did not finish; moonlight_sync_next returned {done}");
            else Console.WriteLine("a sweep ran through the C interface");
        }
    }

    private static byte[] Cstring(string value) => Encoding.UTF8.GetBytes(value + "\0");

    private static void Fail(string what)
    {
        Console.Error.WriteLine($"FAIL  {what}");
        failures++;
    }
}

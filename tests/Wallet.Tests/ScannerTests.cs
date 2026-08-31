using System.Text.Json;
using Moonlight.Crypto;
using Moonlight.RingCT;
using Moonlight.Serialization;
using Moonlight.Tests;
using Moonlight.Wallet;
using Xunit;

namespace Moonlight.Wallet.Tests;

public class ScannerTests
{
    // The wallet that created the CLSAG transaction in the corpus. monero-oxide
    // publishes these keys in its own test for exactly this purpose.
    private const string SpendSecret = "25f7339ce03a0206129c0bdd78396f80bf28183ccd16084d4ab1cbaf74f0c204";
    private const string ViewSecret = "9df81dd2e369004d3737850e4f0abaf2111720f270b174acf8e08547e41afb0b";

    private static Account Wallet => Account.FromKeys(
        Scalar.FromCanonical(Convert.FromHexString(SpendSecret)),
        Scalar.FromCanonical(Convert.FromHexString(ViewSecret)));

    private static Transaction ClsagTransaction()
    {
        using JsonDocument document = Corpus.Json("clsag", "clsag_tx.json");
        return TxParser.Parse(Convert.FromHexString(document.RootElement.GetProperty("hex").GetString()!));
    }

    /// <summary>
    /// A real mainnet transaction, scanned with the keys of the wallet that made
    /// it. It finds the change output — and the decoded amount is proved right by
    /// rebuilding the commitment and matching it against the one on chain, which
    /// is the only way to know a decode is correct rather than merely plausible.
    /// </summary>
    [Fact]
    public void FindsTheChangeOutputOfARealTransaction()
    {
        Transaction tx = ClsagTransaction();
        Scanner scanner = new(Wallet);

        IReadOnlyList<OwnedOutput> found = scanner.Scan(tx, height: 0);

        OwnedOutput output = Assert.Single(found);
        Assert.Equal(0, output.OutputIndex);
        Assert.Equal(60_363_387_616_637UL, output.Amount);
        Assert.Equal(new SubaddressIndex(0, 0), output.Subaddress);

        Assert.Equal(
            Point.FromBytes(tx.Rct!.OutPk[output.OutputIndex]),
            Pedersen.Commit(output.Amount, output.Mask));
    }

    /// <summary>
    /// The other output of that transaction went to somebody else, and the view tag
    /// rejected it before any curve arithmetic. That ratio is the whole reason
    /// scanning is affordable.
    /// </summary>
    [Fact]
    public void TheViewTagRejectsWhatIsNotOurs()
    {
        Scanner scanner = new(Wallet);
        scanner.Scan(ClsagTransaction(), height: 0);

        Assert.Equal(2, scanner.Examined);
        Assert.Equal(1, scanner.PassedViewTag);
    }

    [Fact]
    public void AStrangerFindsNothing()
    {
        Scanner scanner = new(Account.Create());

        Assert.Empty(scanner.Scan(ClsagTransaction(), height: 0));
    }

    /// <summary>
    /// The key image is what marks the output spent. A view-only wallet cannot
    /// compute one — it can see the money and cannot tell whether it is still
    /// there, which is exactly the difference between the two kinds of wallet.
    /// </summary>
    [Fact]
    public void OnlyAFullWalletProducesKeyImages()
    {
        Transaction tx = ClsagTransaction();

        OwnedOutput full = Assert.Single(new Scanner(Wallet).Scan(tx, 0));
        OwnedOutput viewOnly = Assert.Single(new Scanner(Wallet.AsViewOnly()).Scan(tx, 0));

        Assert.NotNull(full.KeyImage);
        Assert.Null(viewOnly.KeyImage);

        // Everything else the two agree on.
        Assert.Equal(full.Amount, viewOnly.Amount);
        Assert.Equal(full.Key, viewOnly.Key);
        Assert.Equal(full.Mask, viewOnly.Mask);
    }

    /// <summary>
    /// The key image belongs to the output, not to the transaction it was found in
    /// or the ring it will later hide in. Scanning twice must give the same one.
    /// </summary>
    [Fact]
    public void TheKeyImageIsStable()
    {
        Transaction tx = ClsagTransaction();

        Assert.Equal(
            new Scanner(Wallet).Scan(tx, 0)[0].KeyImage,
            new Scanner(Wallet).Scan(tx, 12345)[0].KeyImage);
    }

    [Fact]
    public void CoinbaseAmountsAreReadInTheClear()
    {
        using JsonDocument document = Corpus.Json("blocks", "transactions.json");

        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            Transaction tx = TxParser.Parse(Convert.FromHexString(entry.GetProperty("hex").GetString()!));
            if (!tx.IsCoinbase) continue;

            // Nobody here owns it, but the amounts are public either way.
            Assert.All(tx.Outputs, o => Assert.True(o.Amount > 0));
            Assert.Empty(new Scanner(Wallet).Scan(tx, 0));
        }
    }
}

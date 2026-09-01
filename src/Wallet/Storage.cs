using System.Buffers.Binary;
using System.Security.Cryptography;
using Moonlight.Crypto;

namespace Moonlight.Wallet;

/// <summary>
/// The wallet file: two secret keys and where scanning got to, under a password.
/// </summary>
/// <remarks>
/// Deliberately our own format rather than wallet2's, which is a boost archive
/// wrapped in ChaCha20 with a key that is one unsalted Keccak of the password.
/// Here the key comes from PBKDF2 with a random salt, and AES-GCM authenticates
/// the whole file — so a modified file is refused rather than decrypted into
/// something wrong.
/// </remarks>
/// <summary>Everything a wallet file holds.</summary>
public sealed record WalletFile(Account Account, WalletSnapshot Snapshot, SubaddressIndex Lookahead, string? Daemon)
{
    /// <summary>The key this file was opened with, so saving it again is cheap.</summary>
    public WalletSeal? Seal { get; init; }

    /// <summary>The readable half of the file.</summary>
    public WalletDocument Document { get; init; } = new();

    /// <summary>The settings this wallet last wrote, as it recorded them.</summary>
    public byte[] Fingerprint { get; init; } = [];

    /// <summary>
    /// The date this wallet was restored from, kept only while the starting height
    /// is still a guess. A wallet restored with no daemon to hand starts from an
    /// offline estimate that deliberately lands weeks early; holding on to the date
    /// lets the first daemon it meets replace that guess with the exact block.
    /// Cleared once that happens, so it is asked at most once.
    /// </summary>
    public DateTimeOffset? PendingRestoreDate { get; init; }

    /// <summary>
    /// True when the settings on disk are not the ones this wallet last wrote.
    /// Editing them by hand is allowed and expected; being redirected to another
    /// node without noticing is not, so the wallet is told rather than stopped.
    /// </summary>
    public bool SettingsChangedOutside { get; init; }
}

/// <summary>
/// A derived key, kept for as long as a wallet is open. Deriving it costs tens of
/// milliseconds by design, and a wallet that saves while it scans would otherwise
/// pay that over and over for no benefit.
/// </summary>
/// <remarks>
/// The salt is fixed for the life of the file; the nonce is fresh on every write,
/// which is what AES-GCM requires of a reused key. Ninety-six random bits make a
/// repeat vanishingly unlikely, and a repeat is the one thing that would matter.
/// </remarks>
public sealed class WalletSeal
{
    internal WalletSeal(byte[] salt, byte[] key, int iterations)
    {
        Salt = salt;
        Key = key;
        Iterations = iterations;
    }

    internal byte[] Salt { get; }

    internal byte[] Key { get; }

    internal int Iterations { get; }
}

public static class Storage
{
    /// <summary>"MOONLIGHT" and a format version. A future format changes the version, not the meaning.</summary>
    private static ReadOnlySpan<byte> Magic => "MOONLIGHT\x01"u8;

    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;

    /// <summary>
    /// PBKDF2 rounds. High enough to cost a guesser real time, low enough that
    /// opening a wallet is not annoying — and stored in the file, so raising it
    /// later does not orphan existing wallets.
    /// </summary>
    public const int DefaultIterations = 500_000;

    /// <summary>
    /// How far ahead of the last used index to look for payments. wallet2 defaults
    /// to 50 accounts of 200 addresses, and the number matters: a payment to a
    /// subaddress outside the range is one the wallet never finds, with nothing
    /// anywhere reporting a problem.
    /// </summary>
    public static readonly SubaddressIndex DefaultLookahead = new(50, 200);

    /// <summary>Derives the key once, for a wallet that will be saved more than once.</summary>
    public static WalletSeal Seal(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);

        return new WalletSeal(salt, DeriveKey(password, salt, iterations), iterations);
    }

    /// <summary>Wraps the encrypted blob in the readable document and writes it whole.</summary>
    public static void Save(string path, WalletDocument document, byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(secret);

        Save(path, (document with { Secret = Convert.ToBase64String(secret) }).ToBytes());
    }

    /// <summary>
    /// Writes the file without ever leaving it half-written. WriteAllBytes truncates
    /// first, so a process that dies mid-write takes the keys with it; this writes
    /// beside the wallet and swaps it in, keeping the previous file as .bak.
    /// </summary>
    public static void Save(string path, byte[] contents)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(contents);

        string temporary = path + ".new";
        System.IO.File.WriteAllBytes(temporary, contents);

        if (System.IO.File.Exists(path))
        {
            System.IO.File.Replace(temporary, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            System.IO.File.Move(temporary, path);
        }
    }

    public static byte[] Encrypt(
        Account account,
        string password,
        ulong scannedHeight = 0,
        int iterations = DefaultIterations,
        SubaddressIndex? lookahead = null,
        WalletSnapshot? snapshot = null,
        string? daemon = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        return Encrypt(account, Seal(password, iterations), scannedHeight, lookahead, snapshot, daemon);
    }

    public static byte[] Encrypt(
        Account account,
        WalletSeal seal,
        ulong scannedHeight = 0,
        SubaddressIndex? lookahead = null,
        WalletSnapshot? snapshot = null,
        string? daemon = null,
        ReadOnlySpan<byte> settingsFingerprint = default,
        DateTimeOffset? pendingRestoreDate = null)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(seal);

        byte[] salt = seal.Salt;
        int iterations = seal.Iterations;
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceLength);

        // The header is authenticated but not encrypted: it says how to derive the
        // key, so it has to be readable before there is one.
        byte[] header = new byte[Magic.Length + 4 + SaltLength + NonceLength + 1];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(Magic.Length), iterations);
        salt.CopyTo(header, Magic.Length + 4);
        nonce.CopyTo(header, Magic.Length + 4 + SaltLength);
        header[^1] = (byte)account.Network;

        SubaddressIndex range = lookahead ?? DefaultLookahead;

        byte[] fixedPart = new byte[BodyLength];
        account.SpendSecret.ToBytes().CopyTo(fixedPart, 0);
        account.ViewSecret.ToBytes().CopyTo(fixedPart, 32);
        BinaryPrimitives.WriteUInt64LittleEndian(fixedPart.AsSpan(64), snapshot?.ScannedHeight ?? scannedHeight);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedPart.AsSpan(72), range.Major);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedPart.AsSpan(76), range.Minor);

        List<byte> body = [.. fixedPart];
        SnapshotFormat.Write(body, snapshot ?? new WalletSnapshot(scannedHeight, [], new Dictionary<string, ulong>()));

        // Everything after the snapshot is a tagged section: a tag, a length, then
        // the bytes. A reader skips what it does not know, so a later version can
        // add a field without the order mattering and without breaking this one.
        WriteSection(body, SectionDaemon, System.Text.Encoding.UTF8.GetBytes(daemon ?? ""));
        WriteSection(body, SectionSettingsFingerprint, settingsFingerprint);

        if (pendingRestoreDate is DateTimeOffset pending)
        {
            byte[] seconds = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(seconds, pending.ToUnixTimeSeconds());
            WriteSection(body, SectionRestoreDate, seconds);
        }

        byte[] plaintext = [.. body];

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagLength];

        using (AesGcm aes = new(seal.Key, TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        }

        CryptographicOperations.ZeroMemory(plaintext);

        return [.. header, .. ciphertext, .. tag];
    }

    /// <summary>
    /// The fixed part: two keys, the scanned height and the lookahead. Everything a
    /// scan found follows it. Files written before the lookahead existed are 72
    /// bytes and open with the default — the tag covers the length either way, so
    /// there is nothing to guess at.
    /// </summary>
    private const int BodyLength = 80;

    private const int BodyLengthWithoutLookahead = 72;

    private const byte SectionDaemon = 1;

    private const byte SectionSettingsFingerprint = 2;

    /// <summary>The date a still-unresolved restore was asked for, in unix seconds.</summary>
    private const byte SectionRestoreDate = 3;

    private static void WriteSection(List<byte> body, byte tag, ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) return;

        body.Add(tag);

        byte[] length = new byte[Crypto.VarInt.MaxLength];
        body.AddRange(length.AsSpan(0, Crypto.VarInt.Write(length, (ulong)payload.Length)).ToArray());
        body.AddRange(payload.ToArray());
    }

    public static (Account Account, ulong ScannedHeight, SubaddressIndex Lookahead) Decrypt(
        ReadOnlySpan<byte> file,
        string password)
    {
        WalletFile opened = Open(file, password);

        return (opened.Account, opened.Snapshot.ScannedHeight, opened.Lookahead);
    }

    /// <summary>The whole file, scan results included.</summary>
    /// <summary>Opens a wallet document: reads the settings, then the blob inside it.</summary>
    public static WalletFile OpenDocument(ReadOnlySpan<byte> file, string password)
    {
        WalletDocument document = WalletDocument.Parse(file);
        WalletFile opened = Open(Convert.FromBase64String(document.Secret), password);

        return opened with
        {
            Document = document,
            Lookahead = document.Settings.Lookahead,
            // Only meaningful within one shape of the file: a wallet written before
            // a setting existed hashes a different object, and that is not tampering.
            SettingsChangedOutside = document.Format == WalletDocument.CurrentFormat
                && opened.Fingerprint.Length == 32
                && !opened.Fingerprint.AsSpan().SequenceEqual(document.SettingsFingerprint()),
        };
    }

    public static WalletFile Open(
        ReadOnlySpan<byte> file,
        string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        int headerLength = Magic.Length + 4 + SaltLength + NonceLength + 1;

        if (file.Length < headerLength + TagLength || !file[..Magic.Length].SequenceEqual(Magic))
        {
            throw new FormatException("not a moonlight wallet file");
        }

        int iterations = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(Magic.Length, 4));
        if (iterations < 1)
        {
            throw new FormatException("wallet file states a nonsensical work factor");
        }

        ReadOnlySpan<byte> header = file[..headerLength];
        ReadOnlySpan<byte> salt = file.Slice(Magic.Length + 4, SaltLength);
        ReadOnlySpan<byte> nonce = file.Slice(Magic.Length + 4 + SaltLength, NonceLength);
        Network network = (Network)file[headerLength - 1];

        ReadOnlySpan<byte> ciphertext = file[headerLength..^TagLength];
        ReadOnlySpan<byte> tag = file[^TagLength..];

        byte[] plaintext = new byte[ciphertext.Length];
        byte[] key = DeriveKey(password, salt, iterations);

        try
        {
            using AesGcm aes = new(key, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
        }
        catch (CryptographicException e)
        {
            // Wrong password and tampering are the same failure here, and saying
            // which would tell an attacker something.
            throw new FormatException("wrong password, or the wallet file has been modified", e);
        }

        if (plaintext.Length < BodyLengthWithoutLookahead)
        {
            throw new FormatException("wallet file has the wrong shape");
        }

        Account account = Account.FromKeys(
            Scalar.FromCanonical(plaintext.AsSpan(0, 32)),
            Scalar.FromCanonical(plaintext.AsSpan(32, 32)),
            network);

        ulong scannedHeight = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.AsSpan(64));

        SubaddressIndex lookahead = plaintext.Length >= BodyLength
            ? new SubaddressIndex(
                BinaryPrimitives.ReadUInt32LittleEndian(plaintext.AsSpan(72)),
                BinaryPrimitives.ReadUInt32LittleEndian(plaintext.AsSpan(76)))
            : DefaultLookahead;

        WalletSnapshot snapshot = new(scannedHeight, [], new Dictionary<string, ulong>());
        string? daemon = null;
        byte[] fingerprint = [];
        DateTimeOffset? pendingRestoreDate = null;

        if (plaintext.Length > BodyLength)
        {
            Serialization.Reader reader = new(plaintext.AsSpan(BodyLength));
            snapshot = SnapshotFormat.Read(ref reader, scannedHeight);

            while (!reader.AtEnd)
            {
                byte section = reader.ReadByte();
                int length = (int)reader.ReadVarInt();
                ReadOnlySpan<byte> payload = reader.ReadBytes(length);

                switch (section)
                {
                    case SectionDaemon when length > 0:
                        daemon = System.Text.Encoding.UTF8.GetString(payload);
                        break;

                    case SectionSettingsFingerprint when length == 32:
                        fingerprint = payload.ToArray();
                        break;

                    case SectionRestoreDate when length == 8:
                        pendingRestoreDate = DateTimeOffset.FromUnixTimeSeconds(
                            BinaryPrimitives.ReadInt64LittleEndian(payload));
                        break;

                    // Anything else was written by a later version. Skipping it is
                    // the point of the length being there.
                }
            }
        }

        CryptographicOperations.ZeroMemory(plaintext);

        // The key comes back with the file so saving it again costs nothing.
        return new WalletFile(account, snapshot, lookahead, daemon)
        {
            Seal = new WalletSeal(salt.ToArray(), key, iterations),
            Fingerprint = fingerprint,
            PendingRestoreDate = pendingRestoreDate,
        };
    }

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
}

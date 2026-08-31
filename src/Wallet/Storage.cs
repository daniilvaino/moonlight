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

    public static byte[] Encrypt(Account account, string password, ulong scannedHeight = 0, int iterations = DefaultIterations)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceLength);

        // The header is authenticated but not encrypted: it says how to derive the
        // key, so it has to be readable before there is one.
        byte[] header = new byte[Magic.Length + 4 + SaltLength + NonceLength + 1];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(Magic.Length), iterations);
        salt.CopyTo(header, Magic.Length + 4);
        nonce.CopyTo(header, Magic.Length + 4 + SaltLength);
        header[^1] = (byte)account.Network;

        byte[] plaintext = new byte[64 + 8];
        account.SpendSecret.ToBytes().CopyTo(plaintext, 0);
        account.ViewSecret.ToBytes().CopyTo(plaintext, 32);
        BinaryPrimitives.WriteUInt64LittleEndian(plaintext.AsSpan(64), scannedHeight);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagLength];

        using (AesGcm aes = new(DeriveKey(password, salt, iterations), TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        }

        CryptographicOperations.ZeroMemory(plaintext);

        return [.. header, .. ciphertext, .. tag];
    }

    public static (Account Account, ulong ScannedHeight) Decrypt(ReadOnlySpan<byte> file, string password)
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

        try
        {
            using AesGcm aes = new(DeriveKey(password, salt, iterations), TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
        }
        catch (CryptographicException e)
        {
            // Wrong password and tampering are the same failure here, and saying
            // which would tell an attacker something.
            throw new FormatException("wrong password, or the wallet file has been modified", e);
        }

        if (plaintext.Length != 72)
        {
            throw new FormatException("wallet file has the wrong shape");
        }

        Account account = Account.FromKeys(
            Scalar.FromCanonical(plaintext.AsSpan(0, 32)),
            Scalar.FromCanonical(plaintext.AsSpan(32, 32)),
            network);

        ulong scannedHeight = BinaryPrimitives.ReadUInt64LittleEndian(plaintext.AsSpan(64));
        CryptographicOperations.ZeroMemory(plaintext);

        return (account, scannedHeight);
    }

    private static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
}

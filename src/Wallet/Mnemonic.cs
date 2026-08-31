using System.Buffers.Binary;
using Moonlight.Crypto;
using Moonlight.Wallet.Vendor;

namespace Moonlight.Wallet;

/// <summary>
/// Monero's 25-word seed. Twenty-four words carry the 32-byte key, three words
/// per four bytes; the twenty-fifth repeats one of them and is the checksum.
/// </summary>
/// <remarks>
/// Ported from <c>src/mnemonics/electrum-words.cpp</c>. English only for now —
/// other languages are the same arithmetic over a different list.
/// </remarks>
public static class Mnemonic
{
    public const int WordCount = 25;

    private const int SeedLength = 32;

    private static readonly string[] Words = EnglishWordList.Words;

    private static readonly Dictionary<string, int> Index = BuildIndex();

    public static string Encode(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedLength)
        {
            throw new ArgumentException($"a seed is {SeedLength} bytes", nameof(seed));
        }

        int n = Words.Length;
        List<string> words = new(WordCount);

        for (int i = 0; i < seed.Length / 4; i++)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(seed.Slice(i * 4, 4));

            int first = (int)(value % n);
            int second = (int)((value / n) + first) % n;
            int third = (int)((value / n / n) + second) % n;

            words.Add(Words[first]);
            words.Add(Words[second]);
            words.Add(Words[third]);
        }

        words.Add(words[ChecksumIndex(words)]);
        return string.Join(' ', words);
    }

    public static bool TryDecode(string phrase, out byte[] seed)
    {
        seed = [];

        if (string.IsNullOrWhiteSpace(phrase)) return false;

        string[] words = phrase.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != WordCount) return false;

        int[] indices = new int[WordCount - 1];
        for (int i = 0; i < indices.Length; i++)
        {
            if (!Index.TryGetValue(Trim(words[i]), out indices[i])) return false;
        }

        // The checksum word must be one of the twenty-four, and the right one.
        if (!Index.ContainsKey(Trim(words[^1]))) return false;

        string[] body = words[..(WordCount - 1)];
        if (Trim(body[ChecksumIndex(body)]) != Trim(words[^1])) return false;

        long n = Words.Length;
        byte[] result = new byte[SeedLength];

        for (int i = 0; i < indices.Length / 3; i++)
        {
            long first = indices[i * 3];
            long second = indices[(i * 3) + 1];
            long third = indices[(i * 3) + 2];

            long value = first
                + (n * ((n - first + second) % n))
                + (n * n * ((n - second + third) % n));

            // A group that does not reduce back to its own first word is not a
            // group this encoder could have produced.
            if (value % n != first || value > uint.MaxValue) return false;

            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4, 4), (uint)value);
        }

        seed = result;
        return true;
    }

    public static byte[] Decode(string phrase)
        => TryDecode(phrase, out byte[] seed) ? seed : throw new FormatException("not a valid Monero seed phrase");

    /// <summary>
    /// CRC-32 over the words' unique prefixes, modulo the number of words. Prefixes,
    /// not whole words, which is why a seed survives a word being mistyped past its
    /// third character.
    /// </summary>
    private static int ChecksumIndex(IReadOnlyList<string> words)
    {
        string trimmed = string.Concat(words.Select(Trim));
        return (int)(Crc32.Compute(System.Text.Encoding.UTF8.GetBytes(trimmed)) % (uint)words.Count);
    }

    private static string Trim(string word)
        => word.Length > EnglishWordList.UniquePrefixLength
            ? word[..EnglishWordList.UniquePrefixLength]
            : word;

    private static Dictionary<string, int> BuildIndex()
    {
        Dictionary<string, int> index = new(Words.Length, StringComparer.Ordinal);

        for (int i = 0; i < Words.Length; i++)
        {
            index.TryAdd(Trim(Words[i]), i);
        }

        return index;
    }
}

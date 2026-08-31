/*
 * MoneroRing, C# .NET implementation of Monero keys, signatures, ring signatures, and key images
 * Github: https://github.com/MystSafe/MoneroRing
 * 
 * Copyright (C) 2024, MystSafe (https://mystsafe.com)
 * Copyright (C) 2024, Author: crypticana <crypticana@proton.me>
 * MystSafe is the only privacy preserving password manager
 *
 * Licensed under MIT (See LICENSE file)
 */

using System.Security.Cryptography;

namespace MoneroRing.Crypto;


public static partial class RingSig
{
    /// <summary>
    /// Where randomness comes from. Replaceable only from inside this assembly and
    /// its tests, so monero's deterministic test generator can be substituted and
    /// the generating half of tests.txt replayed byte for byte. Thread-local, so a
    /// substitution in one test cannot reach another running beside it.
    /// </summary>
#nullable enable
    [ThreadStatic]
    private static Action<byte[]>? randomSource;

    internal static Action<byte[]> RandomSource
    {
        get => randomSource ?? Fill;
        set => randomSource = value;
    }

    private static void Fill(byte[] bytes) => RandomNumberGenerator.Fill(bytes);
#nullable restore

    public static void generate_random_bytes(byte[] random_bytes, int length_bytes)
    {
        if (random_bytes == null || random_bytes.Length == 0 || random_bytes.Length != length_bytes)
            throw new Exception("Incorrect random buffer size");
        RandomSource(random_bytes);
    }

    public static void random_scalar(byte[] data)
    {
        random32_unbiased(data);
    }

    // checks if k0 is less than k1
    static bool less32(byte[] k0, byte[] k1)
    {
        for (int n = 31; n >= 0; --n)
        {
            if (k0[n] < k1[n])
                return true;
            if (k0[n] > k1[n])
                return false;
        }
        return false;
    }

    // l = 2^252 + 27742317777372353535851937790883648493.
    // l fits 15 times in 32 bytes (iow, 15 l is the highest multiple of l that fits in 32 bytes)
    static readonly byte[] limit = new byte[] {
    0xe3, 0x6a, 0x67, 0x72, 0x8b, 0xce, 0x13, 0x29,
    0x8f, 0x30, 0x82, 0x8c, 0x0b, 0xa4, 0x10, 0x39,
    0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0
    };

    static void random32_unbiased(byte[] bytes)
    {
        while (true)
        {
            generate_random_bytes(bytes, 32);
            if (!less32(bytes, limit))
                continue;
            sc_reduce32(bytes);
            if (sc_isnonzero(bytes) != 0)
                break;
        }
    }

}


namespace Moonlight.Crypto.Tests;

/// <summary>
/// Token shape of each operation in tests.txt, transcribed from the reference
/// runner (monero/tests/crypto/main.cpp). Three operations are variable-length:
/// the two ring-signature ones size themselves from a count, and three others
/// append an expected value only when the boolean before it is true.
/// </summary>
public static class VectorGrammar
{
    public const int Scalar = 64;      // ec_scalar, secret_key, key_derivation, chash
    public const int Point = 64;       // public_key, ec_point, key_image, ec_coord
    public const int Signature = 128;
    public const int ViewTag = 2;

    public static readonly string[] Ops =
    [
        "check_scalar", "random_scalar", "hash_to_scalar", "generate_keys", "check_key",
        "secret_key_to_public_key", "generate_key_derivation", "derive_public_key",
        "derive_secret_key", "generate_signature", "check_signature", "hash_to_point",
        "biased_hash_to_ec", "generate_key_image", "generate_ring_signature",
        "check_ring_signature", "check_ge_p3_identity", "derive_view_tag",
        "point_to_wei_x_y", "derive_key_image_generator",
    ];

    /// <summary>Argument count the operation must have, or -1 if the line does not fit the grammar.</summary>
    public static int Arity(string op, string[] args) => op switch
    {
        "check_scalar" or "random_scalar" or "hash_to_scalar" or "generate_keys"
            or "check_key" or "hash_to_point" or "biased_hash_to_ec" => op == "random_scalar" ? 1 : 2,

        // sec, ok, [pub]
        "secret_key_to_public_key" => Conditional(args, 1, 2),
        // pub, sec, ok, [derivation]
        "generate_key_derivation" => Conditional(args, 2, 3),
        // derivation, index, base, ok, [pub]
        "derive_public_key" => Conditional(args, 3, 4),

        "derive_secret_key" or "generate_signature" or "check_signature" => 4,

        "generate_key_image" or "check_ge_p3_identity" or "derive_view_tag"
            or "point_to_wei_x_y" or "derive_key_image_generator" => 3,

        // hash, image, count, pubs..., sec, index, signatures
        "generate_ring_signature" => Ring(args, 3),
        // hash, image, count, pubs..., signatures, ok
        "check_ring_signature" => Ring(args, 2),

        _ => -1,
    };

    private static int Conditional(string[] args, int flagAt, int withoutValue)
        => args.Length > flagAt && args[flagAt] == "true" ? withoutValue + 1 : withoutValue;

    private static int Ring(string[] args, int tail)
        => args.Length > 2 && int.TryParse(args[2], out int count) && count >= 0
            ? 3 + count + tail
            : -1;
}

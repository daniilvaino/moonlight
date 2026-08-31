namespace Moonlight.Crypto.Tests;

/// <summary>
/// monero's deterministic generator, as its own crypto tests seed it: a 200-byte
/// state filled with 0x42 and permuted once. Everything the reference draws while
/// running tests.txt comes out of this, which is what makes the generating half
/// of that file replayable at all.
/// </summary>
/// <remarks>
/// Ported from <c>tests/crypto/random.c</c> and
/// <c>generate_random_bytes_not_thread_safe</c> in <c>src/crypto/random.c</c>.
/// The sponge squeezes from the first 136 bytes, then zeroes them and permutes —
/// forward security, so a leaked state does not reveal what was drawn before.
/// </remarks>
internal sealed class MoneroTestRandom
{
    private const int Rate = 136;

    private readonly byte[] state = new byte[200];

    public MoneroTestRandom()
    {
        state.AsSpan().Fill(42);   // decimal 42, not 0x42 — memset(&state, 42, ...)
        Keccak.Permute(state);
    }

    public void Fill(byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        int offset = 0;
        int remaining = destination.Length;

        while (remaining > Rate)
        {
            state.AsSpan(0, Rate).CopyTo(destination.AsSpan(offset));
            Keccak.Permute(state);

            offset += Rate;
            remaining -= Rate;
        }

        if (remaining > 0)
        {
            state.AsSpan(0, remaining).CopyTo(destination.AsSpan(offset));
            state.AsSpan(0, Rate).Clear();
            Keccak.Permute(state);
        }
    }
}

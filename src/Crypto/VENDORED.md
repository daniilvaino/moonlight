# Vendored: MoneroRing crypto layer

| | |
|---|---|
| repo | https://github.com/NeoSoft99/MoneroRing — `MoneroRing.Crypto/` |
| commit | `642e443` |
| licence | MIT (MystSafe, author crypticana) |
| imported | 2026-08-31 |
| scope | 33 files: derivations, key images, `hash_to_ec`, `hash_to_scalar`, CryptoNote signatures and ring signatures, plus the ref10 extensions Monero adds (`ge_fromfe_frombytes_vartime`, `ge_mul8`, `ge_scalarmult`, `ge_dsm_precomp`, `sc_mulsub`, `sc_reduce32`, `fe_divpowm1`, …) |

The extensions sit here rather than in `Crypto.Ed25519` because upstream declares them in
the same `RingSig` partial class as the crypto layer. Namespace stays `MoneroRing.Crypto`.

**Verified against monero's own vectors**: 3829 lines of `tests.txt` across ten
operations, in `tests/Crypto.Tests/VectorTests.cs`. All pass.

## Differences from upstream

| file | change | reason |
|---|---|---|
| `hash_to_scalar.cs`, `generate_key_image.cs` | `Nethereum.Util.Sha3Keccack` → our `Keccak.Hash` | sterile tree allows 0 packages; behaviour is identical (both are original-padding Keccak-256) |
| `random.cs` | `Org.BouncyCastle.Security.SecureRandom` → `RandomNumberGenerator.Fill` | drops the last package |
| `generate_signature.cs` | `CryptoBytes.Wipe` → `CryptographicOperations.ZeroMemory` | avoids pulling in NaCl's helper class; the BCL version is guaranteed not to be optimised away |
| `check_ring_signature.cs` | dropped `using MoneroSharp.Utils` | unused, and it was the only reference to a namespace we do not vendor |
| `generate_mnemonic_seed.cs` | not copied | mnemonic belongs in `Wallet`, from MoneroSharp |
| `Vendor/.editorconfig` | **added** (Moonlight) | analyzers and nullable warnings off for vendored code |

No bugs found so far, so nothing to send upstream yet.

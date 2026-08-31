# Architecture

Pure managed C#, `net8.0`, AOT-ready. Layers depend downward only.

```
Crypto.Ed25519   ref10 fe_*/ge_*/sc_* (vendored)
Crypto           Scalar, Point; Keccak (legacy pad), CRC32, VarInt, derivations, key images,
                 hash_to_ec/scalar, view tags, CryptoNote signatures
Serialization    tx/block parsers, Epee
RingCT           Pedersen, ECDH, CLSAG, Bulletproofs+, MultiExp
Node             monerod JSON-RPC; /getblocks.bin (stage 2)
Wallet           keys, accounts, addresses, scanner, decoys, fees, tx builder, storage
apps             Cli · Tui · Gui.Demo (outside the gate)
```

| project | source |
|---|---|
| `Crypto.Ed25519` | `Vendor/Ref10` ← MoneroRing (Chaos.NaCl, public domain) |
| `Crypto` | `Vendor/MoneroRing` (Keccak and RNG swapped for ours); `Keccak.cs`, `VarInt.cs`, `ViewTag.cs`, `Scalar.cs`, `Point.cs` ours. Scalar and Point live here, not in Crypto.Ed25519: they need `sc_check`/`sc_reduce32`/`hash_to_ec`, which are Monero's additions to ref10 |
| `Serialization` | ours; ref `monero/src/cryptonote_basic`, Epee cross-checked vs monero-oxide |
| `RingCT` | hand port of `src/ringct/*.cc`; CLSAG first in C#; BP+ transcript verified step by step vs monero-oxide |
| `Node` | ours; RPC models may come from btcpay monero-csharp |
| `Wallet` | `Vendor/MoneroSharp` (mnemonic/Base58/prefixes only); rest ours |

Key material is never `byte[]`: `SecretKey`/`PublicKey`/`KeyImage`/`Commitment` are
distinct types with no implicit conversions.

Vectors before code — `tests/vectors/tests.txt` via `tests/Crypto.Tests/TestVectors.cs`.

See [purity-policy.md](purity-policy.md), [hardfork-policy.md](hardfork-policy.md).

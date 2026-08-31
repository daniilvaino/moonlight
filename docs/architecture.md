# Architecture

Pure managed C#, `net8.0`, AOT-ready. Layers depend downward only.

```
Crypto.Ed25519   ref10 fe_*/ge_*/sc_*; Scalar, Point
Crypto           Keccak (legacy pad), CRC32, derivations, key images, hash_to_ec/scalar,
                 view tags, CryptoNote signatures
Serialization    VarInt, tx/block parsers, Epee
RingCT           Pedersen, ECDH, CLSAG, Bulletproofs+, MultiExp
Node             monerod JSON-RPC; /getblocks.bin (stage 2)
Wallet           keys, accounts, addresses, scanner, decoys, fees, tx builder, storage
apps             Cli · Tui · Gui.Demo (outside the gate)
```

| project | source |
|---|---|
| `Crypto.Ed25519` | `Vendor/Ref10` ← MoneroRing; `Scalar.cs`/`Point.cs` ours |
| `Crypto` | `Vendor/MoneroRing` (RNG rewritten on `RandomNumberGenerator`); `Keccak.cs`, `Crc32.cs`, `ViewTag.cs` ours |
| `Serialization` | ours; ref `monero/src/cryptonote_basic`, Epee cross-checked vs monero-oxide |
| `RingCT` | hand port of `src/ringct/*.cc`; CLSAG first in C#; BP+ transcript verified step by step vs monero-oxide |
| `Node` | ours; RPC models may come from btcpay monero-csharp |
| `Wallet` | `Vendor/MoneroSharp` (mnemonic/Base58/prefixes only); rest ours |

Key material is never `byte[]`: `SecretKey`/`PublicKey`/`KeyImage`/`Commitment` are
distinct types with no implicit conversions.

Vectors before code — `tests/vectors/tests.txt` via `tests/Crypto.Tests/TestVectors.cs`.

See [purity-policy.md](purity-policy.md), [hardfork-policy.md](hardfork-policy.md).

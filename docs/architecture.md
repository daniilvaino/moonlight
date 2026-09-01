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
| `Node` | ours. JSON-RPC over `HttpClient`, with source-generated `System.Text.Json` — reflection-based JSON does not survive trimming or AOT |
| `Wallet` | `Vendor/MoneroSharp` (English word list only); rest ours |

## The wallet file

JSON with one opaque field. Settings — nodes, where to fetch more, where a price
comes from, the subaddress lookahead — are readable and editable by hand. Everything
else lives in `secret`: the two keys, and the outputs a scan found.

The split is not "keys versus the rest". An output says what the wallet holds, and
its key image identifies that wallet's spends on the chain, so the scan results are
as revealing as the keys and stay inside.

Settings are outside the authenticated envelope so they can be edited, which means
they can also be changed by somebody else — a node quietly redirected is a privacy
attack. Their hash is stored inside the blob, so the wallet reports that they
changed rather than either refusing to open or saying nothing.

Key material is never `byte[]`: `SecretKey`/`PublicKey`/`KeyImage`/`Commitment` are
distinct types with no implicit conversions.

Vectors before code — `tests/vectors/tests.txt` via `tests/Crypto.Tests/TestVectors.cs`.

See [purity-policy.md](purity-policy.md), [hardfork-policy.md](hardfork-policy.md).

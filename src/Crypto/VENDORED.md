# Vendored: MoneroRing crypto layer

| | |
|---|---|
| repo | _TBD_ |
| commit | _TBD_ |
| licence | MIT |
| imported | _TBD_ |
| scope | `Vendor/MoneroRing/` — `hash_to_ec`, `generate_key_image`, derivations, `hash_to_scalar`, CryptoNote signatures |

Ours, not vendored: `Keccak.cs`, `Crc32.cs`, `ViewTag.cs`.

## Differences from upstream

| file | change | reason |
|---|---|---|
| `random.cs` | rewritten on `RandomNumberGenerator` | drops BouncyCastle; sterile tree allows 0 packages |

# Test corpus

What we have, where it came from, and what it can prove. Vectors before code.

## In the repo — `tests/vectors/`

| file | source | licence | contents |
|---|---|---|---|
| `tests.txt` | monero `tests/crypto/` | BSD-3 | 5945 lines, 20 operations, 8.7 MB |
| `clsag/clsag_tx.json` | monero-oxide | MIT | one real transaction with two CLSAG signatures, its Bulletproof+ fields and pseudo-outs |
| `clsag/ring_data.json` | monero-oxide | MIT | the two rings that transaction was signed against, 16 members each |
| `blocks/transactions.json` | monero-oxide | MIT | 5 real transactions with ids — 4 v2, 1 v1 |
| `blocks/block_202612_transactions.txt` | monero-oxide | MIT | 514 transaction **ids** from block 202612 — the Merkle root and block id are checked against them |
| `addresses/featured_addresses.json` | monero-oxide | MIT | 24 addresses, mainnet/stagenet/testnet |

### tests.txt coverage

| operation | count | | operation | count |
|---|---:|---|---|---:|
| `check_ring_signature` | 1024 | | `generate_key_image` | 256 |
| `check_signature` | 512 | | `derive_secret_key` | 256 |
| `check_key` | 372 | | `biased_hash_to_ec` | 256 |
| `hash_to_point` | 371 | | `generate_keys` | 256 |
| `check_scalar` | 337 | | `random_scalar` | 245 |
| `secret_key_to_public_key` | 272 | | `derive_key_image_generator` | 200 |
| `derive_public_key` | 272 | | `point_to_wei_x_y` | 200 |
| `generate_key_derivation` | 272 | | `derive_view_tag` | 70 |
| `hash_to_scalar` | 256 | | `check_ge_p3_identity` | 6 |
| `generate_signature` | 256 | | `generate_ring_signature` | 256 |

### Coverage: 4526 of 5945 replayed (76%)

Replayed line by line: `check_ring_signature`, `check_signature`, `check_key`,
`hash_to_point`, `check_scalar`, `secret_key_to_public_key`, `generate_key_derivation`,
`derive_public_key`, `hash_to_scalar`, `generate_key_image`, `derive_secret_key`,
`biased_hash_to_ec`, `derive_view_tag`.

The remaining 1419 are not skipped work but four different reasons:

| operation | lines | why not replayed |
|---|---:|---|
| `generate_signature`, `generate_ring_signature`, `generate_keys`, `random_scalar` | 1013 | **Unreplayable by construction.** The reference seeds a fixed PRNG and compares the bytes it draws; we use a real one. Covered instead by round-trip: what our generators produce must satisfy the verifiers that already pass the corpus, and must fail against a different message. |
| `point_to_wei_x_y`, `derive_key_image_generator` | 400 | FCMP++ groundwork; nothing implemented yet. |
| `check_ge_p3_identity` | 6 | Needs the two identity probes from `crypto-tests.h`, which are test-only helpers, not library functions. |

The grammar (which arguments each operation takes, including the three that append an
expected value only when the preceding boolean is true, and the two ring operations that
size themselves from a count) is transcribed from the reference runner
`monero/tests/crypto/main.cpp` into `tests/Crypto.Tests/VectorGrammar.cs`, and every line
is checked against it.

## Not vectors — reference runners to generate from

No ready-made vector files exist for these layers; the C++ unit tests build their inputs
in code. The plan is to run them and capture the intermediate values.

| source | for |
|---|---|
| `monero/tests/unit_tests/ringct.cpp` | CLSAG sign/verify cases |
| `monero/tests/unit_tests/bulletproofs_plus.cpp` | BP+ prove/verify, batch, edge sizes |
| `monero/tests/unit_tests/serialization.cpp` | tx/block round-trips |
| `monero/tests/unit_tests/epee_serialization.cpp` | Epee portable storage |
| `monero/tests/unit_tests/cryptonote_format_utils.cpp` | format helpers |
| `monero/tests/unit_tests/varint.cpp`, `base58.cpp`, `mnemonics.cpp`, `subaddress.cpp` | serialization and wallet basics |
| `monero-oxide/.../plus/transcript.rs` | BP+ transcript, step by step |
| `monero-oxide/tests/verify-chain` | the integration idea: parse and verify real chain |
| skunkworks `clsag`, `pybullet-plus` | **GPL-3 — read only.** Slow but transparent for edge cases |

## Where the tests live

| project | today |
|---|---|
| `Crypto.Tests` | harness + grammar over all 5945 lines; Keccak; VarInt; 4526 replayed vectors; round-trip for the generators (48 tests) |
| `Serialization.Tests` | corpus integrity; TxParser and TxHash against 5 real transactions; MerkleTree and BlockParser against block 202612 (22) |
| `RingCT.Tests` | Pedersen, ECDH, CLSAG sign/verify, and two real monero-made CLSAG signatures verified against their rings (34) |
| `Wallet.Tests` | address corpus integrity (2) |
| `Integration` | DaemonClient against canned monerod answers, no network (6); plus chain tests that run only when `MOONLIGHT_DAEMON` names a node (2) |

The corpus tests assert the vector files themselves are intact. That is not busywork: a
truncated or reformatted vector file is the one failure mode that makes every later test
pass for free.

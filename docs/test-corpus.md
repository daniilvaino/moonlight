# Test corpus

What we have, where it came from, and what it can prove. Vectors before code.

## In the repo — `tests/vectors/`

| file | source | licence | contents |
|---|---|---|---|
| `tests.txt` | monero `tests/crypto/` | BSD-3 | 5945 lines, 20 operations, 8.3 MB |
| `hash/blake2b.txt` | monero `tests/hash/` | BSD-3 | the official BLAKE2 vector set: 256 keyed digests, inputs of 0 to 255 bytes |
| `hash/keccak.txt` | monero `tests/hash/` | BSD-3 | 321 digests of what monero calls `cn_fast_hash`, inputs from nothing to past twice the rate |
| `hash/tree.txt` | monero `tests/hash/` | BSD-3 | 16 tree-hash roots, one for every count from a single hash to sixteen |
| `clsag/clsag_tx.json` | monero-oxide | MIT | one real transaction with two CLSAG signatures, its Bulletproof+ fields and pseudo-outs |
| `clsag/ring_data.json` | monero-oxide | MIT | the two rings that transaction was signed against, 16 members each |
| `blocks/transactions.json` | monero-oxide | MIT | 5 real transactions with ids — 4 v2, 1 v1 |
| `blocks/block_202612_transactions.txt` | monero-oxide | MIT | 514 transaction **ids** from block 202612 — the Merkle root and block id are checked against them |
| `addresses/monero_addresses.json` | monero + monero-oxide | BSD-3, MIT | 4 real addresses: standard, integrated, subaddress |
| `addresses/featured_addresses.json` | monero-oxide | MIT | 24 **Featured Addresses** — an unofficial extension with a flags byte, not consensus |

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

### Coverage: 5945 of 5945 replayed

Every line of monero's corpus, every operation, performed and compared.

The number is counted, not remembered: `HarnessTests.EveryLineIsReplayed` holds the
list of what is held back — empty — and asserts it against the file. It is written
that way because the hand-kept version drifted, and drifted in the flattering
direction: a hundred lines were recorded as impossible when nobody had tried them.

The generating four — `random_scalar`, `generate_keys`, `generate_signature` and
`generate_ring_signature`, 1013 lines — record the bytes monero's reference drew from a
deterministic generator, so replaying them means reproducing that generator rather than
using a real one. It is `tests/crypto/random.c`: a 200-byte Keccak state filled with 42
and permuted once. `MoneroTestRandom` is that, and the vendored RNG has an
assembly-internal hook so the tests can substitute it.

Because the generator is shared across the file and advances with every draw, those four
have to be replayed together in file order. Drawing a different number of bytes than the
reference did desynchronises everything after it — so this checks the draw pattern as
well as the arithmetic, and our generators produce byte-identical output to monero's.

Three groups were held back for most of the corpus's life. Each turned out to be
smaller than its entry said.

`derive_key_image_generator` was the last of them, and the only one that needed a new
primitive: the unbiased map hashes with **BLAKE2b**, so `src/Crypto/Blake2b.cs` is
RFC 7693. Monero does not call it unpersonalised — `blake2b_monero` writes "Monero"
into the personalisation field of the parameter block, which changes the initial state
and every digest after it. Both forms are pinned in `Blake2bTests`, because a parameter
block assembled wrongly still produces a perfectly stable wrong answer.

The compression function itself answers to the official vector set, `hash/blake2b.txt`
above: 256 keyed digests with inputs from nothing to 255 bytes, one length at a time.
The sweep is what makes it worth having — it crosses every boundary a block-based hash
can get wrong, the empty message, the byte before a block, the block exactly full, the
byte after it, and does it all again shifted by 128 because a key occupies a block of
its own in front of the message. Nothing here passes a key; the branch exists because
that is the branch the vectors exercise, and a covered branch is better attested than
the one the wallet calls.

With the hash in place the map is what the reference does: 64 bytes of digest, each
half through `ge_fromfe_frombytes_vartime` and `ge_mul8`, and the two points added.

`check_ge_p3_identity` was held back for needing test-only helpers from monero's
`crypto-tests.h`. Test-only is where they belong, so they were written there. Monero
builds `((K+K)-K)-K` — the identity for any K — and asks two questions of the result:
a naive check that reads the ten limbs of each coordinate as they sit, and a correct
one that reduces first. The naive check answers **false for three of the six points**,
because the limbs of a genuine identity need not be the reduced ones, and that
disagreement is the whole point of the operation.

It is the only vector in the corpus about representation rather than arithmetic, and
replaying it pins something no other one does: that our group operations leave the same
intermediate representations monero's do, limb for limb, and not merely the same points.

`point_to_wei_x_y` used to be here as FCMP++ groundwork with nothing to implement
against. It turned out to need no new primitive at all — only the field arithmetic
already vendored. Ed25519 and the curve FCMP++ proves over are one curve under two
names, and `Point.TryToWeierstrass` is the map between the forms, from
[draft-ietf-lwig-curve-representations-02 E.2](https://www.ietf.org/archive/id/draft-ietf-lwig-curve-representations-02.pdf):

```
wei_x = (1+y)/(1-y) + A/3
wei_y = c * (1+y) / ((1-y)*x)      A = 486662, c = sqrt(-(A+2))
```

Both coordinates of all 200 lines match. Vectors before code, in the order the phrase
implies: the map is written down here first and the proofs that will use it come later.

`derive_key_image_generator` used to be written off whole, on the reading that its
middle argument said whether the operation had succeeded. It does not. Every input
appears twice in the corpus, once with `true` and once with `false`, and the two give
different points: the flag chooses **which map onto the curve to use**, and it is a
parameter rather than a result. The `true` map is the biased `hash_to_ec` this repository
already has and already replays 256 lines of under `biased_hash_to_ec`, so those hundred
lines were never blocked on anything — they were simply not being run. They are now.

Two readings were checked and discarded on the way to that: the flag is not whether the
input decodes to a point (nearly all of them do under both), and it is not whether the
point is free of torsion (multiplying by the group order does not sort them either).
Monero settles it — the parameter is named `biased` there.

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

| `monero/tests/unit_tests/cryptonote_format_utils.cpp` | format helpers |
| `monero/tests/unit_tests/varint.cpp`, `base58.cpp`, `mnemonics.cpp`, `subaddress.cpp` | serialization and wallet basics |
| `monero-oxide/.../plus/transcript.rs` | BP+ transcript, step by step |
| `monero-oxide/tests/verify-chain` | the integration idea: parse and verify real chain |
| skunkworks `clsag`, `pybullet-plus` | **GPL-3 — read only.** Slow but transparent for edge cases |

## Where the tests live

| project | today |
|---|---|
| `Crypto.Tests` | harness + grammar over all 5945 lines; Keccak; VarInt; all 5945 vectors replayed, generators included; round-trip and typed-API tests on top (61) |
| `Serialization.Tests` | corpus integrity; TxParser, TxHash and TxExtra against 5 real transactions; MerkleTree and BlockParser against block 202612; Epee against monero's own byte vectors (42) |
| `RingCT.Tests` | Pedersen, ECDH, CLSAG sign/verify, two real monero-made CLSAG signatures verified against their rings, the real Bulletproof+ range proof from the same transaction, and our own prover checked against that verifier (61) |
| `Wallet.Tests` | Base58, addresses, mnemonic, key derivation, subaddresses and restore height — anchored on the seed monero's own functional tests restore; the scanner finding the change output of a real mainnet transaction, and the same output again in a pruned one; balance, locking and spend detection; the sync engine driven by hand with no socket open, settling a restore date included; the C interface called the way C calls it; the settings fingerprint pinned to a wallet on disk (151) |
| `Integration` | DaemonClient and getblocks.bin against canned monerod answers, both the full and the pruned response shape, no network (14); plus chain tests that run only when `MOONLIGHT_DAEMON` names a node (3) |
| `Gui.Tests` | the demo's controls driven for real, a type-ramp guard measured off the laid-out tree, and a pixel baseline that skips outside macOS and in CI, where the interface fonts are not installed (13) |

| `Abi.Native` | not a test project: it loads the shared library that was just built, looks up every function `moonlight.h` declares, and drives a sweep through them. Run as a CI step after publishing, because it needs the artifact to exist — under `dotnet test` it would pass by finding nothing |

Both native modes are built in CI now, on linux x64 and arm64 as well as the
platforms the managed build already covered. What is still not exercised anywhere:
the three chain tests, which need `MOONLIGHT_DAEMON` pointed at a node, and the
visual baseline, which needs fonts a runner does not have.

The corpus tests assert the vector files themselves are intact. That is not busywork: a
truncated or reformatted vector file is the one failure mode that makes every later test
pass for free.

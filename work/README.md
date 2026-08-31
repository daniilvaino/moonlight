# work/

Donor and reference clones. Git-ignored, never built. Re-run `pwsh tools/clone-donors.ps1`
to clone or refresh. Code leaves only by being copied into a `Vendor/` dir (with
`VENDORED.md`) or ported by hand.

| dir | licence | use |
|---|---|---|
| `MoneroRing` | MIT | extended ref10, crypto layer, CryptoNote signatures |
| `MoneroSharp` | MIT | mnemonic + wordlists, Base58, network prefixes |
| `QRCoder` | MIT | QR for the TUI — vendored as source, not NuGet |
| `monero-csharp`, `monero-lws-csharp` | MIT | RPC models |
| `monero-oxide` | MIT (per crate, all checked) | Rust reference; BP+ transcript intermediates |
| `p2pool-consensus` | MIT | independent Go consensus reference |
| `ZkpSharp` | see repo | look at how they do Pedersen/BP; not Monero |
| `monero` | BSD-3 | `tests/crypto/tests.txt`, `tests/unit_tests`, `src/ringct` |
| `monero-v0.18.5.1` | BSD-3 | worktree at the release tag — the port target |
| `skunkworks-clsag`, `skunkworks-pybullet-plus` | **GPL-3** | **read only**, Python MRL reference |
| `decentralized-message-queue` | **multi, pending counsel** | **read only** |

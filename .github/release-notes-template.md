<!--
The body of every Release. release.yml takes everything below the rule, fills in
<version>, and puts it above the generated commit list — nobody edits the page by
hand, so the file table has to stay true of what release.yml builds.

The warning block stays first and stays as blunt as it is. Somebody is about to
point a wallet at their money on the strength of this page.
-->

---

> ### Read this first
>
> **Alpha, and not audited.** This is a Monero wallet written from scratch — the
> curve arithmetic, the parsers, the ring signatures and the scanner are all our own
> code, and none of it has been reviewed by anybody outside this project. A bug in
> any of it loses money.
>
> **A seed phrase is the wallet.** Do not give this one a seed that controls funds
> you would mind losing. Make a new wallet, send it a small amount, and treat
> everything it can see as at risk.
>
> **It cannot spend.** There is no transaction builder yet, so nothing here moves
> money — which is also why nothing here can move it by mistake. That changes in a
> later release, and this warning gets heavier when it does.

### What works

Crypto, serialization, the daemon client, RingCT and a scanning wallet. The TUI
syncs to the mainnet tip over pruned blocks and finds real payments to its own
addresses. The CLSAG signatures and the Bulletproof+ range proof of a real
transaction verify against this code, and it reads that transaction's change amount
back. All of it is also a linkable library with a C interface, so a user interface
in any language can drive the same wallet.

### What is not here

- **Spending.** Decoy selection, fees and the transaction builder.
- **Pippenger** for the multiexponentiation — verification is correct, not yet fast.
- **Error messages across the C boundary.** A failure arrives as a bare code with no
  text attached to it.

### Which file

| platform | wallet | smaller build | demo |
|---|---|---|---|
| Linux x64 | `moonlight-<version>-linux-x64.tar.gz` | `…-linux-x64-bflat.tar.gz` | `moonlight-gui-demo-<version>-linux-x64.tar.gz` |
| Linux arm64 | `moonlight-<version>-linux-arm64.tar.gz` | `…-linux-arm64-bflat.tar.gz` | `moonlight-gui-demo-<version>-linux-arm64.tar.gz` |
| Windows x64 | `moonlight-<version>-win-x64.zip` | `…-win-x64-bflat.zip` | `moonlight-gui-demo-<version>-win-x64.zip` |
| Windows arm64 | `moonlight-<version>-win-arm64.zip` | — | `moonlight-gui-demo-<version>-win-arm64.zip` |
| macOS, Apple silicon | `moonlight-<version>-osx-arm64.tar.gz` | — | `moonlight-gui-demo-<version>-osx-arm64.tar.gz` |

Each wallet archive holds `moonlight`, `moonlight-tui`, the shared library
`libmoonlight.*` with its header, and the licences. The `-bflat` archives are the
same wallet from a different compiler and a much smaller one; either is the wallet.
Intel Macs are not built, and bflat has no win-arm64 build — it compiles one that
does not start, so it is not shipped.

Also on this page: `moonlight-<version>-managed.zip`, one build for every platform
on a .NET 8 runtime, carrying the nine libraries and the same nine as a single
`Moonlight.dll`; `moonlight-<version>-nuget.zip`, the packages that go to nuget.org;
`…-symbols`, the debug symbols, apart because most people downloading a wallet do
not want them; and `moonlight-<version>-src.tar.gz`, the tag's tree.

**`moonlight-gui-demo` is a demonstration, not a wallet.** It draws the interface on
made-up data and holds no keys.

### Checking and running

```sh
sha256sum   --ignore-missing -c SHA256SUMS   # Linux
shasum -a 256 --ignore-missing -c SHA256SUMS # macOS
```

Nothing is code-signed, so macOS quarantines a downloaded archive until told
otherwise: `xattr -dr com.apple.quarantine moonlight-<version>-osx-arm64`.

```sh
nix run github:daniilvaino/moonlight/v<version> -- version
dotnet add package Moonlight.Core.Http --version <version>
```

# Moonlight

A Monero wallet in pure managed C#: no packages, no P/Invoke, AOT-ready.

**Status: early.** Scanning, balances and verification work against mainnet;
nothing here spends money yet. Not audited. Do not hand an alpha the seed of a
wallet that holds funds.

Start with `Moonlight.Core.Http` — the wallet driven over HttpClient — or
`Moonlight.Core`, the same engine with no I/O, where your host owns the socket.
The other packages are the layers underneath and arrive as dependencies.

Source, docs and the C interface: https://github.com/daniilvaino/moonlight

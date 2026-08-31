# MOONLIGHT

Monero wallet in pure managed C#. `net8.0`, AOT-ready, no P/Invoke, no packages in the
sterile tree.

```
dotnet build Moonlight.slnx
dotnet test  Moonlight.slnx
dotnet publish apps/Cli -c Release      # NativeAOT
pwsh tools/purity-gate.ps1
```

[architecture](docs/architecture.md) ·
[purity policy](docs/purity-policy.md) ·
[hard forks](docs/hardfork-policy.md) ·
[upstream bugs](docs/known-upstream-bugs.md) ·
[notices](THIRD-PARTY-NOTICES.md)

MIT. `work/` is a git-ignored scratch area for donor clones.

#!/usr/bin/env pwsh
# Clones every donor/reference repo into work/. Idempotent: existing clones are fetched.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$work = Join-Path (Split-Path -Parent $PSScriptRoot) 'work'

$repos = @(
    @{ Dir = 'MoneroRing';                Url = 'https://github.com/NeoSoft99/MoneroRing.git' }
    @{ Dir = 'MoneroSharp';               Url = 'https://github.com/rohanrhu/MoneroSharp.git' }
    @{ Dir = 'QRCoder';                   Url = 'https://github.com/Shane32/QRCoder.git' }
    @{ Dir = 'monero-csharp';             Url = 'https://github.com/btcpay-monero/monero-csharp.git' }
    @{ Dir = 'monero-lws-csharp';         Url = 'https://github.com/btcpay-monero/monero-lws-csharp.git' }
    @{ Dir = 'ZkpSharp';                  Url = 'https://github.com/asagynbaev/Tessera.git' }
    @{ Dir = 'monero';                    Url = 'https://github.com/monero-project/monero.git'; Recursive = $true }
    @{ Dir = 'monero-oxide';              Url = 'https://github.com/monero-oxide/monero-oxide.git' }
    @{ Dir = 'p2pool-consensus';          Url = 'https://github.com/P2Pool-Observer/consensus.git' }
    @{ Dir = 'skunkworks';                Url = 'https://github.com/SarangNoether/skunkworks.git' }
    @{ Dir = 'decentralized-message-queue'; Url = 'https://github.com/StoyanDenev/decentralized-message-queue.git' }
)

foreach ($r in $repos) {
    $path = Join-Path $work $r.Dir
    if (Test-Path (Join-Path $path '.git')) {
        Write-Host "== fetch $($r.Dir)" -ForegroundColor Cyan
        git -C $path fetch --all --tags --prune
        continue
    }
    Write-Host "== clone $($r.Dir)" -ForegroundColor Cyan
    if ($r.ContainsKey('Recursive')) {
        git clone --recursive $r.Url $path
    } else {
        git clone $r.Url $path
    }
}

# SharpOS is only here for vendor/Terminal.Gui and vendor/XtermSharp.
$sharpOs = Join-Path $work 'SharpOS'
if (-not (Test-Path (Join-Path $sharpOs '.git'))) {
    Write-Host '== clone SharpOS (sparse)' -ForegroundColor Cyan
    git clone --filter=blob:none --no-checkout https://github.com/daniilvaino/SharpOS.git $sharpOs
    git -C $sharpOs sparse-checkout set --cone vendor/Terminal.Gui vendor/XtermSharp
    git -C $sharpOs checkout
}

# Pinned reference points as worktrees, so master and the port target coexist.
$worktrees = @(
    @{ Repo = 'monero';     Path = 'monero-v0.18.5.1'; Ref = 'v0.18.5.1' }
    @{ Repo = 'skunkworks'; Path = 'skunkworks-clsag'; Ref = 'origin/clsag' }
    @{ Repo = 'skunkworks'; Path = 'skunkworks-pybullet-plus'; Ref = 'origin/pybullet-plus' }
)

foreach ($w in $worktrees) {
    $src = Join-Path $work $w.Repo
    $dst = Join-Path $work $w.Path
    if ((Test-Path $dst) -or -not (Test-Path $src)) { continue }
    Write-Host "== worktree $($w.Path) @ $($w.Ref)" -ForegroundColor Cyan
    git -C $src worktree add --detach $dst $w.Ref
}

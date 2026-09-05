#!/usr/bin/env pwsh
# Purity gate: src/, apps/Cli and apps/Tui must stay managed-only and package-free.
# See docs/purity-policy.md. Exits non-zero on any violation.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sterile = @('src', 'apps/Cli', 'apps/Tui') |
    ForEach-Object { Join-Path $root ($_ -replace '/', [IO.Path]::DirectorySeparatorChar) } |
    Where-Object { Test-Path $_ }

$violations = [System.Collections.Generic.List[string]]::new()

# 1. Interop that calls out of managed code, anywhere in the sterile sources.
#
#    Direction is the whole of the rule. DllImport, LibraryImport and NativeLibrary
#    reach for native code we would then depend on, and that is what costs us "one
#    artifact that runs wherever the runtime does". UnmanagedCallersOnly is the
#    opposite: it lets somebody else call us, and adds no dependency at all — it is
#    how src/Core.Abi offers the library to a user interface written in anything.
#
#    So exports are allowed, and only there: one project, so the exemption stays
#    small enough to read.
$interop = 'DllImport|LibraryImport|SuppressGCTransition|NativeLibrary'
$exports = 'UnmanagedCallersOnly'
$abi = Join-Path $root ('src/Core.Abi' -replace '/', [IO.Path]::DirectorySeparatorChar)

#    Comments are skipped. The rule is about what the code does, and a file that
#    explains why it does not call out was being reported for saying the word.
$comment = '^\s*(//|\*|/\*)'

foreach ($dir in $sterile) {
    Get-ChildItem -Path $dir -Recurse -Include *.cs -File |
        Select-String -Pattern $interop |
        Where-Object { $_.Line -notmatch $comment } |
        ForEach-Object {
            $violations.Add("interop: $($_.Path):$($_.LineNumber) -> $($_.Line.Trim())")
        }

    Get-ChildItem -Path $dir -Recurse -Include *.cs -File |
        Where-Object { -not $_.FullName.StartsWith($abi, [StringComparison]::Ordinal) } |
        Select-String -Pattern $exports |
        Where-Object { $_.Line -notmatch $comment } |
        ForEach-Object {
            $violations.Add("export outside src/Core.Abi: $($_.Path):$($_.LineNumber) -> $($_.Line.Trim())")
        }
}

# 2. PackageReference in any sterile project file.
foreach ($dir in $sterile) {
    Get-ChildItem -Path $dir -Recurse -Include *.csproj, *.props, *.targets -File |
        Select-String -Pattern '<PackageReference' |
        ForEach-Object {
            $violations.Add("package: $($_.Path):$($_.LineNumber) -> $($_.Line.Trim())")
        }
}

# 3. Native assets in the restore graph. The NativeAOT toolchain (ILCompiler) is ours by
#    definition and exempt; anything else means a native dependency slipped in.
foreach ($dir in $sterile) {
    Get-ChildItem -Path $dir -Recurse -Filter project.assets.json -File |
        Select-String -Pattern 'runtimes/[^"]+/native' |
        Where-Object { $_.Line -notmatch 'ILCompiler|ilcompiler' } |
        ForEach-Object {
            $violations.Add("native asset: $($_.Path):$($_.LineNumber) -> $($_.Line.Trim())")
        }
}

# 4. Every Vendor/ directory must document itself.
foreach ($dir in $sterile) {
    Get-ChildItem -Path $dir -Recurse -Directory -Filter Vendor |
        ForEach-Object {
            # VENDORED.md may sit inside Vendor/ or next to it, in the owning project.
            $vendor = $_.FullName
            $documented = (Get-ChildItem -Path $vendor -Recurse -Filter VENDORED.md -File) -or
                          (Test-Path (Join-Path $_.Parent.FullName 'VENDORED.md'))
            if (-not $documented) {
                $violations.Add("undocumented vendor tree: $vendor (needs a VENDORED.md)")
            }
        }
}

if ($violations.Count -gt 0) {
    Write-Host "Purity gate FAILED ($($violations.Count)):" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Purity gate passed: no interop, no packages, no native assets.' -ForegroundColor Green
exit 0

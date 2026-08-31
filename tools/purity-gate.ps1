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

# 1. Interop attributes anywhere in the sterile sources.
$interop = 'DllImport|LibraryImport|SuppressGCTransition|UnmanagedCallersOnly|NativeLibrary'
foreach ($dir in $sterile) {
    Get-ChildItem -Path $dir -Recurse -Include *.cs -File |
        Select-String -Pattern $interop |
        ForEach-Object {
            $violations.Add("interop: $($_.Path):$($_.LineNumber) -> $($_.Line.Trim())")
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

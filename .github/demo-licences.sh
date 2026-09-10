#!/usr/bin/env bash
# Collects the licence texts of the packages the demo ships binaries from.
#
# The demo is the one application carrying other people's compiled code — Avalonia,
# SkiaSharp and HarfBuzzSharp — so the root THIRD-PARTY-NOTICES.md, which is about
# vendored source, does not cover it.
#
# Taken from the restored packages rather than copied into the repository. The
# native-asset package alone carries some two and a half thousand lines about Skia,
# ANGLE, libpng and zlib; transcribing that would go stale against the version that
# actually shipped, and a notice that no longer matches the binary is worse than a
# long one.
set -euo pipefail

dest="${1:?usage: demo-licences.sh <destination directory>}"
mkdir -p "$dest"

packages="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
[ -d "$packages" ] || { echo "no package cache at $packages" >&2; exit 1; }

count=0
collect() {
  # The newest restored version of the package; a build restores exactly one.
  local id="$1" file="$2" name="$3"
  local found
  found=$(find "$packages/$id" -maxdepth 2 -name "$file" 2>/dev/null | sort | tail -1)

  if [ -n "$found" ]; then
    cp "$found" "$dest/$name"
    count=$((count + 1))
  else
    echo "no $file in $packages/$id" >&2
    return 1
  fi
}

collect skiasharp     LICENSE.txt "SkiaSharp.LICENSE.txt"
collect harfbuzzsharp LICENSE.txt "HarfBuzzSharp.LICENSE.txt"

# One of these per platform, and only the restored one exists. The file inside is
# the same set of notices for the native code either way.
notices=$(find "$packages" -maxdepth 3 -path '*skiasharp.nativeassets*' -name 'THIRD-PARTY-NOTICES.txt' | sort | tail -1)
if [ -n "$notices" ]; then
  cp "$notices" "$dest/SkiaSharp.THIRD-PARTY-NOTICES.txt"
  count=$((count + 1))
else
  echo "no native-asset notices under $packages" >&2
  exit 1
fi

# Avalonia states MIT in its package metadata and ships no licence file, so the text
# travels from here.
cat > "$dest/Avalonia.LICENSE.txt" <<'EOF'
The MIT License (MIT)

Copyright 2013-2026 © The AvaloniaUI Project

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in the
Software without restriction, including without limitation the rights to use, copy,
modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is furnished to do so, subject to the
following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
EOF
count=$((count + 1))

echo "$count demo licences -> $dest"
ls -1 "$dest"

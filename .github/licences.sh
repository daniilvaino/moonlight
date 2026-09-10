#!/usr/bin/env bash
# Collects the vendored third-party licences into one directory of an archive.
#
# Not a plain copy of Vendor/*/LICENSE*. Five of the six texts are named exactly
# LICENSE, so copying them flat leaves two files out of six, and nothing about that
# looks wrong: the archive builds, the step is green, and four notices are gone. The
# directory each one sits in is the name that carries meaning, so it becomes part of
# the file's name.
#
# MIT asks that the notice travels with the copy, which is the whole point of the
# directory this writes.
set -euo pipefail

dest="${1:?usage: licences.sh <destination directory>}"
mkdir -p "$dest"

count=0
while IFS= read -r file; do
  cp "$file" "$dest/$(basename "$(dirname "$file")").$(basename "$file")"
  count=$((count + 1))
done < <(find src apps -path '*/Vendor/*' \( -name 'LICENSE*' -o -name 'COPYING*' \) \
              -not -path '*/obj/*' -not -path '*/bin/*' | sort)

# A vendored tree that arrives without its licence, or one that quietly stops being
# collected, is the failure this number is here to catch.
expected=6
if [ "$count" -ne "$expected" ]; then
  echo "collected $count licences, expected $expected:" >&2
  ls -1 "$dest" >&2
  exit 1
fi

echo "$count licences -> $dest"

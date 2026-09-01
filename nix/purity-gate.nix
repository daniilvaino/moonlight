# tools/purity-gate.ps1 over the source tree.
#
# Three of the script's four checks — interop attributes, PackageReference in a
# sterile project, undocumented Vendor/ trees — read the sources alone, so they
# run here. The fourth scans project.assets.json for native assets and only has
# anything to look at after a restore; the packages themselves cover it, since
# Directory.Build.targets fails their build on the same violation.
{
  lib,
  runCommand,
  powershell,

  src,
  version,
}:

runCommand "moonlight-purity-gate-${version}"
  {
    src = import ./source.nix { inherit lib src; };
    nativeBuildInputs = [ powershell ];

    meta = {
      description = "moonlight purity gate";
      license = lib.licenses.mit;
      platforms = powershell.meta.platforms;
    };
  }
  ''
    cp -r "$src" source
    chmod -R +w source
    cd source
    # pwsh writes to $HOME on startup, and the sandbox has none.
    export HOME=$PWD/.home
    mkdir -p "$HOME"
    pwsh -NoProfile tools/purity-gate.ps1 | tee "$out"
  ''

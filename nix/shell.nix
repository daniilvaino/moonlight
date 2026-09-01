{
  lib,
  mkShell,
  mkShellNoCC,
  stdenv,
  dotnetCorePackages,
  clang,
  openssl,
  powershell,
  zlib,
}:

let
  inherit (stdenv.hostPlatform) isDarwin;

  # Same pair the packages build with: SDK 10 for the C# 14 compiler, SDK 8 for
  # the net8.0 packs, so `dotnet build` and `dotnet test` work out of the box.
  dotnet = dotnetCorePackages.combinePackages [
    dotnetCorePackages.sdk_10_0
    dotnetCorePackages.sdk_8_0
  ];

  # On macOS the shell deliberately carries no C toolchain. ILC shells out to
  # clang, dsymutil and strip by name, and Nixpkgs' cctools strip rejects the
  # -no_code_signature_warning that ILC passes it — so leave /usr/bin (Xcode's
  # command line tools) in front, the way an ordinary `dotnet publish` expects.
  # nix/moonlight.nix builds the same apps hermetically instead.
  shell = if isDarwin then mkShellNoCC else mkShell;
in
shell {
  name = "moonlight";

  packages = [
    dotnet
    powershell # tools/purity-gate.ps1
  ]
  ++ lib.optionals (!isDarwin) [ clang ];

  buildInputs = lib.optionals (!isDarwin) [ zlib ];

  env = {
    DOTNET_ROOT = "${dotnet}/share/dotnet";
    DOTNET_NOLOGO = "1";
    DOTNET_CLI_TELEMETRY_OPTOUT = "1";
    # AES-GCM and PBKDF2 reach for OpenSSL at run time on Linux.
    LD_LIBRARY_PATH = lib.optionalString stdenv.hostPlatform.isLinux "${lib.getLib openssl}/lib";
  };

  shellHook = ''
    echo "moonlight — dotnet $(dotnet --version)"
    echo "  dotnet build   Moonlight.slnx"
    echo "  dotnet test    Moonlight.slnx"
    echo "  dotnet publish apps/Cli -c Release"
    echo "  pwsh tools/purity-gate.ps1"
  '';
}

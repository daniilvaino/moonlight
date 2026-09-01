# `nix flake check` runs the xunit corpus. tests/Integration skips itself unless
# MOONLIGHT_STAGENET_RPC points at a daemon, which a sandboxed build never can.
{
  lib,
  buildDotnetModule,
  dotnetCorePackages,

  src,
  version,
}:

buildDotnetModule {
  pname = "moonlight-tests";
  inherit version;

  src = import ./source.nix { inherit lib src; };

  projectFile = [
    "tests/Crypto.Tests/Crypto.Tests.csproj"
    "tests/RingCT.Tests/RingCT.Tests.csproj"
    "tests/Serialization.Tests/Serialization.Tests.csproj"
    "tests/Wallet.Tests/Wallet.Tests.csproj"
    "tests/Integration/Integration.csproj"
  ];
  nugetDeps = ./deps/tests.json;

  dotnet-sdk = dotnetCorePackages.combinePackages [
    dotnetCorePackages.sdk_10_0
    dotnetCorePackages.sdk_8_0
  ];
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  doCheck = true;

  # Nothing to ship — the result is the exit code.
  dontPublish = true;
  executables = [ ];
  postInstall = ''
    mkdir -p "$out"
    echo "moonlight ${version} test corpus: passed" > "$out/result"
  '';

  meta = {
    description = "moonlight test corpus";
    license = lib.licenses.mit;
    platforms = [
      "x86_64-linux"
      "aarch64-linux"
      "aarch64-darwin"
    ];
  };
}

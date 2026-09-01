# The two NativeAOT apps. `variant` picks which one.
{
  lib,
  stdenv,
  apple-sdk_15,
  buildDotnetModule,
  darwin,
  dotnetCorePackages,
  clang,
  openssl,
  zlib,

  src,
  version,
  variant ? "cli",
}:

let
  app =
    {
      cli = {
        pname = "moonlight";
        projectFile = "apps/Cli/Cli.csproj";
        executable = "moonlight";
        description = "Monero wallet in pure managed C# — command line";
      };
      tui = {
        pname = "moonlight-tui";
        projectFile = "apps/Tui/Tui.csproj";
        executable = "moonlight-tui";
        description = "Monero wallet in pure managed C# — terminal UI";
      };
    }
    .${variant};
in
buildDotnetModule (finalAttrs: {
  inherit (app) pname;
  inherit version;

  src = import ./source.nix { inherit lib src; };

  projectFile = app.projectFile;
  nugetDeps = ./deps/${variant}.json;

  # Targets net8.0 but needs the C# 14 compiler, so both SDKs have to be on
  # hand: 10 to build with, 8 for the targeting and runtime packs.
  dotnet-sdk = dotnetCorePackages.combinePackages [
    dotnetCorePackages.sdk_10_0
    dotnetCorePackages.sdk_8_0
  ];
  dotnet-runtime = dotnetCorePackages.runtime_8_0;

  # PublishAot is set in the csproj; SelfContained follows from it, and ILC
  # needs a C toolchain to link the object file it emits.
  selfContainedBuild = true;
  nativeBuildInputs = [ clang ];
  buildInputs = [
    zlib
  ]
  ++ lib.optionals stdenv.hostPlatform.isDarwin [
    # ILC links -licucore on macOS whatever InvariantGlobalization says, and
    # the Nixpkgs Apple SDK deliberately withholds the system one.
    darwin.ICU
    # libSystem.Security.Cryptography.Native.Apple.a autolinks the Swift
    # Darwin overlay shims (swiftunistd, swift_errno, ...). They first appear
    # in the 15 SDK; the default 14.4 has only libswiftCore and
    # libswiftFoundation, and the link ends in undefined symbols.
    apple-sdk_15
  ];

  # AES-GCM, PBKDF2 and SHA-256 come from OpenSSL on Linux, dlopened at run
  # time rather than linked. macOS uses its own.
  runtimeDeps = lib.optionals stdenv.hostPlatform.isLinux [ openssl ];

  executables = [ app.executable ];

  # Publish leaves the debug symbols beside the native binary — a .dbg on
  # Linux, a .dsym bundle on macOS, plus a .pdb per project. Nothing at run
  # time reads any of them.
  postInstall = ''
    find "$out/lib/${finalAttrs.pname}" \
      \( -name '*.pdb' -o -name '*.dbg' \) -delete
    rm -rf "$out/lib/${finalAttrs.pname}"/*.dsym "$out/lib/${finalAttrs.pname}"/*.dSYM
  '';

  meta = {
    inherit (app) description;
    homepage = "https://github.com/daniilvaino/moonlight";
    license = lib.licenses.mit;
    mainProgram = app.executable;
    platforms = [
      "x86_64-linux"
      "aarch64-linux"
      "aarch64-darwin"
    ];
  };
})

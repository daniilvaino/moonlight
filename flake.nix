{
  description = "moonlight — a Monero wallet in pure managed C#";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs =
    { self, nixpkgs }:
    let
      inherit (nixpkgs) lib;

      # The .NET 10 SDK is not packaged for x86_64-darwin; see
      # dotnetCorePackages.sdk_10_0.meta.platforms.
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "aarch64-darwin"
      ];

      forAllSystems = f: lib.genAttrs systems (system: f nixpkgs.legacyPackages.${system});

      version = "0.0.1";
    in
    {
      overlays.default = final: prev: {
        moonlight = final.callPackage ./nix/moonlight.nix {
          inherit version;
          src = ./.;
        };
        moonlight-tui = final.callPackage ./nix/moonlight.nix {
          inherit version;
          src = ./.;
          variant = "tui";
        };
      };

      packages = forAllSystems (
        pkgs:
        let
          scope = self.overlays.default pkgs pkgs;
        in
        {
          inherit (scope) moonlight moonlight-tui;
          default = scope.moonlight;
        }
      );

      apps = forAllSystems (pkgs: {
        moonlight = {
          type = "app";
          program = lib.getExe self.packages.${pkgs.stdenv.hostPlatform.system}.moonlight;
          meta.description = "moonlight command line wallet";
        };
        moonlight-tui = {
          type = "app";
          program = lib.getExe self.packages.${pkgs.stdenv.hostPlatform.system}.moonlight-tui;
          meta.description = "moonlight terminal user interface";
        };
        default = self.apps.${pkgs.stdenv.hostPlatform.system}.moonlight;
      });

      devShells = forAllSystems (pkgs: {
        default = pkgs.callPackage ./nix/shell.nix { };
      });

      checks = forAllSystems (
        pkgs:
        let
          system = pkgs.stdenv.hostPlatform.system;
        in
        {
          inherit (self.packages.${system}) moonlight moonlight-tui;

          tests = pkgs.callPackage ./nix/tests.nix {
            inherit version;
            src = ./.;
          };

          purity-gate = pkgs.callPackage ./nix/purity-gate.nix {
            inherit version;
            src = ./.;
          };
        }
      );

      formatter = forAllSystems (pkgs: pkgs.nixfmt-tree);
    };
}

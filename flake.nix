{
  #
  # Nuget Dependency lock file is updated with: `nix build .#<package name>.fetch-deps`
  #
  description = "A Simple Radio Standalone Flake";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixpkgs-unstable";
    flake-parts.url = "github:hercules-ci/flake-parts";
  };

  outputs =
    inputs@{ self, ... }:
    let
      revision = "2.3.8.2"; # automatic sould be better, something like: self.shortRev or self.dirtyShortRev or "unknown";
    in
    inputs.flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [
        "x86_64-windows"
        "x86_64-linux"
      ];

      perSystem = { pkgs, lib, ... }: {
        devShells = {
          default = pkgs.mkShell {
            nativeBuildInputs = with pkgs; [
              dotnetCorePackages.sdk_10_0
              stylua
            ];

            DOTNET_BIN = "${pkgs.dotnetCorePackages.sdk_10_0}/bin/dotnet";
          };
        };

        packages =
          let
            common-meta = with lib; {
              homepage = "https://github.com/ciribob/DCS-SimpleRadioStandalone/";
              license = with lib.licenses; [ gpl3Plus ];
            };
          in
          {
            server-cli = pkgs.buildDotnetModule {
              pname = "SRS-Server-Commandline";
              version = revision;

              src = lib.fileset.toSource {
                root = ./.;
                fileset = lib.fileset.unions [
                  ./Common
                  ./SharedAudio
                  ./ServerCommandLine
                ];
              };

              projectFile = "./ServerCommandLine/ServerCommandLine.csproj";
              nugetDeps = ./ServerCommandLine/deps.json;

              dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
              dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;

              meta = common-meta // {
                description = "A platform agnostic CLI for running a Simple Radio Standalone Server.";
              };
            };
          };
      };
    };
}

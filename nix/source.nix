# The subset of the tree that MSBuild actually reads.
#
# Keeping README.md, docs/ and most of media/ out of the source means editing
# them does not invalidate a build. media/favicon.ico stays because both apps
# name it in <ApplicationIcon>.
{ lib, src }:

lib.fileset.toSource {
  root = src;
  fileset = lib.fileset.unions [
    (src + "/Directory.Build.props")
    (src + "/Directory.Build.targets")
    (src + "/Directory.Packages.props")
    (src + "/Moonlight.slnx")
    # Directory.Build.props reads the version from it, and fails to evaluate if the
    # file is not there — even though the Nix build also passes the same number as
    # -p:Version.
    (src + "/VERSION")
    (src + "/global.json")
    (src + "/nuget.config")
    (src + "/media/favicon.ico")
    (src + "/src")
    (src + "/apps")
    (src + "/tests")
    (src + "/tools")
  ];
}

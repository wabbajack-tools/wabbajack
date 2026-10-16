#!/usr/bin/env bash
# Builds and packages the Linux release: the app and the CLI for linux-x64, as one tarball.
#
# The Linux counterpart of release.ps1, without its signing (nothing checks a signature on Linux) and without
# the launcher, which is Windows-only. The app and CLI are published as single files, with the CLI in a cli
# folder beside the app, the layout the app looks for. A tarball rather than a zip, because a zip loses the
# executable bits on the app, the CLI and the bundled 7zz and innoextract.
#
# The GitHub release is created by release.ps1; the release workflow attaches this tarball to it.
#
#   PUBLISH_DIR   where to build and package (default: $TMPDIR/publish-wj-linux)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
publish_dir="${PUBLISH_DIR:-${TMPDIR:-/tmp}/publish-wj-linux}"
runtime="linux-x64"

step() {
    printf '\n========================================\n %s\n========================================\n' "$1"
}

# --- Changelog version check, as release.ps1 does it ---

step "Reading changelog version"
changelog="$repo_root/CHANGELOG.md"
if grep -qE '^#### Version - [0-9.]+ - TBD[[:space:]]*$' "$changelog"; then
    echo "ERROR: CHANGELOG.md still has TBD as the date for the latest version." >&2
    echo "Set the release date in CHANGELOG.md, commit, and push before running this script." >&2
    exit 1
fi
version="$(grep -m1 -oE '^#### Version - [0-9.]+ - ' "$changelog" | sed -E 's/^#### Version - ([0-9.]+) - $/\1/')"
if [[ -z "$version" ]]; then
    echo "ERROR: No version header found in CHANGELOG.md" >&2
    exit 1
fi
echo "Version: $version"

# --- Build ---

step "Building projects"
app_dir="$publish_dir/app"
rm -rf "$app_dir"
mkdir -p "$publish_dir"

common_args=(
    --runtime "$runtime"
    --configuration Release
    --self-contained
    -p:IncludeNativeLibrariesForSelfExtract=true
    -p:DebugType=embedded
    -p:EnableWindowsTargeting=true
    "-p:VERSION=$version"
)

cd "$repo_root"
dotnet clean --configuration Release -p:EnableWindowsTargeting=true

echo "Publishing Wabbajack.App.Avalonia..."
dotnet publish Wabbajack.App.Avalonia/Wabbajack.App.Avalonia.csproj --framework net10.0 "${common_args[@]}" \
    -o "$app_dir"

# The CLI targets plain net10.0 when built off Windows (see its csproj).
echo "Publishing Wabbajack.CLI..."
dotnet publish Wabbajack.CLI/Wabbajack.CLI.csproj --framework net10.0 "${common_args[@]}" -o "$app_dir/cli"

# --- Trim what only runs on other platforms ---

step "Removing other platforms' tools"
for dir in "$app_dir" "$app_dir/cli"; do
    # 7-Zip and innoextract for Windows and macOS, and texconv/texdiag, which Linux replaces with texgen.
    rm -rf "$dir/Extractors/windows-x64" "$dir/Extractors/mac" "$dir/Tools"
    chmod +x "$dir/Extractors/$runtime/7zz" "$dir/Extractors/$runtime/innoextract"
done
chmod +x "$app_dir/Wabbajack" "$app_dir/cli/wabbajack-cli"

# --- Package ---

step "Packaging"
tarball="$publish_dir/$version-$runtime.tar.gz"
rm -f "$tarball"
# One top-level folder, so extracting it does not scatter files into the current one.
tar -C "$publish_dir" --transform "s,^app,Wabbajack-$version," -czf "$tarball" app

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
        echo "version=$version"
        echo "tarball=$tarball"
    } >> "$GITHUB_OUTPUT"
fi

echo "Created:"
echo "  $tarball ($(du -h "$tarball" | cut -f1))"

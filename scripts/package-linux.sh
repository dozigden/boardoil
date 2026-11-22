#!/usr/bin/env bash
set -euo pipefail

# Build on native Debian/Ubuntu with debhelper after npm ci in BoardOil.Web.
channel=release
if [[ ${1:-} == --nightly ]]; then
  channel=nightly
  shift
fi
if [[ $# -ne 1 || ${1:-} == -* ]]; then
  echo "Usage: $0 [--nightly] <output-directory>" >&2
  exit 2
fi

mkdir -p -- "$1"
output_dir=$(cd -- "$1" && pwd)
cd -- "$(dirname -- "$0")/.."
repo=$PWD
commit=$(git rev-parse --verify 'HEAD^{commit}')
project=BoardOil.Api/BoardOil.Api.csproj
properties=$(dotnet msbuild "$project" -getProperty:NETCoreSdkRuntimeIdentifier,Version)
runtime=$(jq -er '.Properties.NETCoreSdkRuntimeIdentifier' <<< "$properties")
version=$(jq -er '.Properties.Version' <<< "$properties")
arch=$(dpkg --print-architecture)
case "$runtime/$arch" in
  linux-x64/amd64|linux-arm64/arm64) ;;
  *) echo "Unsupported SDK and package architecture: $runtime/$arch" >&2; exit 1 ;;
esac

if [[ $channel == nightly ]]; then
  if [[ ! ${BUILD_NUMBER:-} =~ ^[1-9][0-9]*$ ]]; then
    echo "A nightly build requires a positive decimal Jenkins BUILD_NUMBER." >&2
    exit 2
  fi
  if [[ $(git rev-parse --is-shallow-repository) == true ]]; then
    echo "A nightly build requires full Git history and release tags." >&2
    exit 1
  fi
  stable_version=$(git tag --merged "$commit" --sort=version:refname |
    sed -nE 's/^v((0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){2})$/\1/p' | tail -n 1)
  if [[ -z $stable_version ]]; then
    echo "No stable vMAJOR.MINOR.PATCH release tag is reachable from $commit." >&2
    exit 1
  fi
  build=$BUILD_NUMBER
  deb_version="$stable_version-1+nightly.$build"
else
  build="tag-$version"
  # Debian sorts a tilde before the final release (e.g. 1.2.0~rc.1 < 1.2.0).
  deb_version=$version
  if [[ $version =~ ^([0-9]+\.[0-9]+\.[0-9]+)-(.+)$ ]]; then
    deb_version="${BASH_REMATCH[1]}~${BASH_REMATCH[2]}"
  fi
  deb_version+="-1"
fi
package="boardoil_${deb_version}_${arch}.deb"
stage=$(mktemp -d -- "$output_dir/.boardoil-build.XXXXXXXX")
trap 'rm -rf -- "$stage"' EXIT
source_dir="$stage/source"
package_root="$source_dir/debian/boardoil"
publish="$package_root/usr/lib/boardoil"
mkdir -p "$source_dir"
cp -R packaging/debian "$source_dir/"

dotnet restore "$project" --locked-mode -maxcpucount:1 -nodeReuse:false
dotnet publish "$project" -c Release -r "$runtime" --no-restore --no-self-contained \
  -p:UseAppHost=false -o "$publish" -maxcpucount:1 -nodeReuse:false

export VITE_BO_VERSION="$version" VITE_BO_CHANNEL="$channel" VITE_BO_BUILD="$build" VITE_BO_COMMIT="$commit"
npm --prefix BoardOil.Web run build
cp -a BoardOil.Web/dist/. "$publish/wwwroot/"
install -Dm644 LICENSE "$package_root/usr/share/doc/boardoil/copyright"
jq '.BoardOilBuild = {Version: env.VITE_BO_VERSION, Channel: env.VITE_BO_CHANNEL,
                      Build: env.VITE_BO_BUILD, Commit: env.VITE_BO_COMMIT}' \
  "$publish/appsettings.json" > "$stage/settings.json"
mv -- "$stage/settings.json" "$publish/appsettings.json"

cd "$source_dir"
install -Dm640 debian/boardoil.env "$package_root/etc/boardoil/boardoil.env"
cat > debian/changelog <<EOF
boardoil ($deb_version) unstable; urgency=medium

  * Package BoardOil $version.

 -- Luke Easter <dozigden@users.noreply.github.com>  $(git -C "$repo" log -1 --format=%aD "$commit")
EOF
# Let Debian generate service lifecycle scripts and register the conffile.
export DEB_RULES_REQUIRES_ROOT=no
dh_installsystemd --no-restart-after-upgrade
dh_fixperms
chmod 640 "$package_root/etc/boardoil/boardoil.env"
dh_installdeb
dh_gencontrol
dh_md5sums
dpkg-deb --root-owner-group --build "$package_root" "$stage/$package"
(cd "$stage" && sha256sum -- "$package" > "$package.sha256")
mv -- "$stage/$package" "$stage/$package.sha256" "$output_dir/"
echo "Packaged $output_dir/$package"

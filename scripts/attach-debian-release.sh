#!/usr/bin/env bash
set -euo pipefail

# Called by the version-tag workflow after both native runners pass.
tag=${GITHUB_REF_NAME:?}
version=${tag#v}
if [[ ${GITHUB_REF_TYPE:?} != tag || $version == "$tag" || $DRAFT_TAG != "debian-rehearsal-$GITHUB_RUN_ID-"* ]]; then
  echo "Expected a version tag and this run's Debian rehearsal draft." >&2
  exit 1
fi
remote_sha=$(gh api "repos/$GH_REPO/commits/refs/tags/$tag" --jq .sha)
if [[ $remote_sha != "$GITHUB_SHA" ]]; then
  echo "Tag $tag no longer points to the tested commit $GITHUB_SHA." >&2
  exit 1
fi
gh release view "$DRAFT_TAG" --json isDraft,targetCommitish |
  jq -e --arg sha "$GITHUB_SHA" '.isDraft and .targetCommitish == $sha' >/dev/null

work=$(mktemp -d "${RUNNER_TEMP:?}/debian-release.XXXXXXXX")
cd "$work"
assets=()
patterns=()
for arch in amd64 arm64; do
  package="boardoil_${version}-1_${arch}.deb"
  assets+=("$package" "$package.sha256")
  patterns+=(--pattern "$package" --pattern "$package.sha256")
done
gh release download "$DRAFT_TAG" "${patterns[@]}"
deb_version=$(sed -E 's/^([0-9]+\.[0-9]+\.[0-9]+)-/\1~/' <<< "$version")-1
for arch in amd64 arm64; do
  package="boardoil_${version}-1_${arch}.deb"
  tested_checksum="$RUNNER_TEMP/tested-debian-checksums/$package.sha256"
  cmp -- "$tested_checksum" "$package.sha256"
  sha256sum -- "$package" | cmp - "$tested_checksum"
  identity=$(dpkg-deb -W --showformat='${Package} ${Version} ${Architecture}' "$package")
  if [[ $identity != "boardoil $deb_version $arch" ]]; then
    echo "$package contains '$identity', expected 'boardoil $deb_version $arch'." >&2
    exit 1
  fi
done

# Listing with write access includes both published releases and maintainer drafts.
release_id=$(gh api --paginate "repos/$GH_REPO/releases?per_page=100" |
  jq --arg tag "$tag" '.[] | select(.tag_name == $tag) | .id')
if [[ -z $release_id ]]; then
  options=(--draft --verify-tag --title "BoardOil $tag" --notes "")
  if [[ ${version%%+*} == *-* ]]; then
    options+=(--prerelease)
  fi
  gh release create "$tag" "${options[@]}"
  release_id=$(gh release view "$tag" --json databaseId --jq .databaseId)
fi

# An interrupted upload can leave completed files alongside an empty starter asset.
for attempt in 1 2; do
  gh api --paginate "repos/$GH_REPO/releases/$release_id/assets?per_page=100" |
    jq -s 'add // []' > release-assets.json
  missing=()
  starters=()
  for asset in "${assets[@]}"; do
    entry=$(jq -c --arg name "$asset" '.[] | select(.name == $name)' release-assets.json)
    if [[ -z $entry ]]; then
      missing+=("$asset")
    elif jq -e '.state == "uploaded"' <<< "$entry" >/dev/null; then
      gh release download "$tag" --pattern "$asset" --dir "existing-$attempt"
      if ! cmp -s "$asset" "existing-$attempt/$asset"; then
        echo "Release $tag already has a different $asset. Existing assets were preserved." >&2
        exit 1
      fi
    elif jq -e '.state == "starter" and .size == 0' <<< "$entry" >/dev/null; then
      starters+=("$(jq -er '.id | numbers' <<< "$entry")")
      missing+=("$asset")
    else
      echo "Unexpected upload state for $asset on $tag: $entry" >&2
      exit 1
    fi
  done
  for id in "${starters[@]}"; do
    endpoint="repos/$GH_REPO/releases/assets/$id"
    gh api "$endpoint" | jq -e '.state == "starter" and .size == 0' >/dev/null
    gh api --method DELETE "$endpoint"
  done
  if (( ${#missing[@]} == 0 )) || gh release upload "$tag" "${missing[@]}"; then
    break
  fi
  if (( attempt == 2 )); then
    echo "Uploading Debian assets to $tag failed twice." >&2
    exit 1
  fi
done

gh release download "$tag" "${patterns[@]}" --dir verified
for asset in "${assets[@]}"; do
  cmp -- "$asset" "verified/$asset"
done
url=$(gh release view "$tag" --json url --jq .url)
echo "Verified both Debian packages and their checksums on the [GitHub Release for $tag]($url)." >> "$GITHUB_STEP_SUMMARY"
gh release delete "$DRAFT_TAG" --yes

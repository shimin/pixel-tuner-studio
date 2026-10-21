#!/usr/bin/env bash
# Downloads the PixelTuner 2 reference set (draft release "samples-v1", asset samples_with_bin.zip),
# checks its digest, unpacks it into $RUNNER_TEMP/samples and exports PIXELTUNER_SAMPLES.
# Needs GH_TOKEN with contents: write (GitHub lists draft releases only to tokens that can push).
set -euo pipefail
repo="${GITHUB_REPOSITORY:?}"
asset=$(gh api "repos/$repo/releases" --paginate --jq '.[] | select(.name == "samples-v1") | .assets[] | select(.name == "samples_with_bin.zip")' | head -n 1)
if [ -z "$asset" ]; then echo "::error::Release samples-v1 with samples_with_bin.zip not found"; exit 1; fi
id=$(echo "$asset" | jq -r .id)
digest=$(echo "$asset" | jq -r '.digest // empty' | sed 's/^sha256://')
zip="$RUNNER_TEMP/samples.zip"
gh api -H "Accept: application/octet-stream" "repos/$repo/releases/assets/$id" > "$zip"
if [ -n "$digest" ]; then echo "$digest  $zip" | sha256sum -c -; fi
out="$RUNNER_TEMP/samples"
mkdir -p "$out"
# The set has nested archives (sources); they are not needed.
if command -v unzip > /dev/null; then unzip -q "$zip" -d "$out" -x '*.zip'; else 7z x -y -bd "-o$out" "$zip" '-xr!*.zip' > /dev/null; fi
echo "PIXELTUNER_SAMPLES=$out" >> "$GITHUB_ENV"

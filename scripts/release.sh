#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -x -o pipefail

if [ -z "$BUILDKITE" ]; then
  echo "This script is only to be run on Improbable CI."
  echo 0
fi

RELEASE_VERSION="$(buildkite-agent meta-data get \"release-version\")"
GITHUB_SSK_KEY="$(imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=github-personal-access-token --secret-name=ci/improbable/unity-gdk/github-personal-access-token)"

if [ -f "gdk.pinned" ]; then
	GDK_VERSION="$(buildkite-agent meta-data get \"gdk-version\")"
fi

dotnet run -p "$(dirname "$0")../tools/ReleaseTool/ReleaseTool.csproj" -- release \
	--version="$RELEASE_VERSION" \
	--update-gdk="$GDK_VERSION" \
	--github-key="$GITHUB_SSK_KEY" \
	--unattended

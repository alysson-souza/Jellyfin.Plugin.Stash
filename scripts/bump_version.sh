#!/bin/bash
# Version bump helper
# Usage: ./scripts/bump_version.sh <jellyfin|emby> [major|minor|patch|build|<version>]
#
# The two artifacts are versioned independently:
#   jellyfin -> <JellyfinPluginVersion>, which is also the release tag and manifest.json entry
#   emby     -> <EmbyPluginVersion>, which moves only when the Emby build itself changes

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
CSPROJ_PATH="$PROJECT_ROOT/Jellyfin.Plugin.Stash/Stash.csproj"

usage() {
    echo "Usage: $0 <jellyfin|emby> [major|minor|patch|build|<version>]"
    echo ""
    echo "Examples:"
    echo "  $0 jellyfin minor    # 1.3.0.0 -> 1.4.0.0"
    echo "  $0 jellyfin patch    # 1.3.0.0 -> 1.3.1.0"
    echo "  $0 emby build        # 1.3.0.0 -> 1.3.0.1"
    echo "  $0 emby major        # 1.3.0.0 -> 2.0.0.0"
    echo "  $0 jellyfin 1.5.0.0  # Set explicit version"
}

case "$1" in
    jellyfin)
        HOST="Jellyfin"
        PROPERTY="JellyfinPluginVersion"
        ;;
    emby)
        HOST="Emby"
        PROPERTY="EmbyPluginVersion"
        ;;
    *)
        echo -e "${RED}Error: First argument must be 'jellyfin' or 'emby'${NC}"
        usage
        exit 1
        ;;
esac

shift

# Get current version for the selected artifact
CURRENT_VERSION=$(grep -oE "<${PROPERTY}>[^<]+" "$CSPROJ_PATH" | sed "s/<${PROPERTY}>//")

echo -e "${YELLOW}Current ${HOST} version: ${CURRENT_VERSION}${NC}"

if [ $# -lt 1 ]; then
    usage
    exit 1
fi

# Parse current version
IFS='.' read -r MAJOR MINOR PATCH BUILD <<< "$CURRENT_VERSION"

case "$1" in
    major)
        NEW_VERSION="$((MAJOR + 1)).0.0.0"
        ;;
    minor)
        NEW_VERSION="${MAJOR}.$((MINOR + 1)).0.0"
        ;;
    patch)
        NEW_VERSION="${MAJOR}.${MINOR}.$((PATCH + 1)).0"
        ;;
    build)
        NEW_VERSION="${MAJOR}.${MINOR}.${PATCH}.$((BUILD + 1))"
        ;;
    *)
        # Assume it's an explicit version
        if [[ $1 =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            NEW_VERSION="$1"
        else
            echo -e "${RED}Error: Invalid version format. Use X.Y.Z.W${NC}"
            exit 1
        fi
        ;;
esac

echo -e "${GREEN}New ${HOST} version: ${NEW_VERSION}${NC}"

# Update .csproj
sed -i.bak "s|<${PROPERTY}>.*</${PROPERTY}>|<${PROPERTY}>${NEW_VERSION}</${PROPERTY}>|" "$CSPROJ_PATH"
rm -f "${CSPROJ_PATH}.bak"

echo -e "${GREEN}✅ Updated Stash.csproj ${PROPERTY} to ${NEW_VERSION}${NC}"
echo ""

if [ "$PROPERTY" = "JellyfinPluginVersion" ]; then
    echo -e "${YELLOW}Next steps to release:${NC}"
    echo "  1. Commit your changes: git commit -am \"chore: bump Jellyfin plugin version to ${NEW_VERSION}\""
    echo "  2. Push to main: git push origin main"
    echo "  3. Option A - Tag release:"
    echo "       git tag v${NEW_VERSION} && git push origin v${NEW_VERSION}"
    echo "  4. Option B - Manual dispatch:"
    echo "       Go to Actions > Build and Release > Run workflow"
    echo "       Enter version: ${NEW_VERSION}"
    echo "       Enter changelog: <your changelog>"
    echo ""
    echo -e "${YELLOW}The release tag must match <JellyfinPluginVersion>; the workflow fails otherwise.${NC}"
else
    echo -e "${YELLOW}Next steps:${NC}"
    echo "  1. Commit your changes: git commit -am \"chore: bump Emby plugin version to ${NEW_VERSION}\""
    echo "  2. Push to main: git push origin main"
    echo ""
    echo -e "${YELLOW}Emby has no tag of its own: Emby.Plugins.Stash.zip ${NEW_VERSION} ships with the next tagged release.${NC}"
fi

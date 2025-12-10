#!/bin/bash
# Version bump helper
# Usage: ./scripts/bump_version.sh [major|minor|patch|build] or ./scripts/bump_version.sh <version>

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
CSPROJ_PATH="$PROJECT_ROOT/Jellyfin.Plugin.Stash/Stash.csproj"

# Get current version
CURRENT_VERSION=$(grep -oE '<AssemblyVersion>[^<]+' "$CSPROJ_PATH" | sed 's/<AssemblyVersion>//')

echo -e "${YELLOW}Current version: ${CURRENT_VERSION}${NC}"

if [ $# -lt 1 ]; then
    echo "Usage: $0 [major|minor|patch|build|<version>]"
    echo ""
    echo "Examples:"
    echo "  $0 major     # 1.3.0.0 -> 2.0.0.0"
    echo "  $0 minor     # 1.3.0.0 -> 1.4.0.0"
    echo "  $0 patch     # 1.3.0.0 -> 1.3.1.0"
    echo "  $0 build     # 1.3.0.0 -> 1.3.0.1"
    echo "  $0 1.5.0.0   # Set explicit version"
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

echo -e "${GREEN}New version: ${NEW_VERSION}${NC}"

# Update .csproj
sed -i.bak "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>${NEW_VERSION}<\/AssemblyVersion>/" "$CSPROJ_PATH"
sed -i.bak "s/<FileVersion>.*<\/FileVersion>/<FileVersion>${NEW_VERSION}<\/FileVersion>/" "$CSPROJ_PATH"
rm -f "${CSPROJ_PATH}.bak"

echo -e "${GREEN}✅ Updated Stash.csproj to version ${NEW_VERSION}${NC}"
echo ""
echo -e "${YELLOW}Next steps to release:${NC}"
echo "  1. Commit your changes: git commit -am \"chore: bump version to ${NEW_VERSION}\""
echo "  2. Push to main: git push origin main"
echo "  3. Option A - Tag release:"
echo "       git tag v${NEW_VERSION} && git push origin v${NEW_VERSION}"
echo "  4. Option B - Manual dispatch:"
echo "       Go to Actions > Build and Release > Run workflow"
echo "       Enter version: ${NEW_VERSION}"
echo "       Enter changelog: <your changelog>"

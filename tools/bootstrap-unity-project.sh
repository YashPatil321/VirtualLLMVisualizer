#!/usr/bin/env bash
# Turns this repo clone into an openable Unity project.
#
# Unity Hub will not create a project inside a folder that already has files in it, so
# the trick is the other direction: create a throwaway URP project with the Hub, then
# copy the two folders Unity generated into this repo. The repo stays the git repo and
# becomes the Unity project.
#
#   ./tools/bootstrap-unity-project.sh ~/Unity/OCSVisualizer-fresh
#
set -euo pipefail

FRESH="${1:-}"
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -z "$FRESH" ]; then
  cat <<USAGE
usage: $0 <path-to-the-fresh-unity-project>

  1. In Unity Hub, install Unity 6 LTS with the Android Build Support module
     (including the Android SDK/NDK and OpenJDK sub-items).
  2. New project -> Universal 3D -> name it anything, e.g. OCSVisualizer-fresh.
     Let it finish opening once, then close Unity.
  3. Run this script with the path to that project.
  4. Open THIS folder in Unity Hub instead:
       $REPO
USAGE
  exit 1
fi

[ -d "$FRESH/ProjectSettings" ] || { echo "No ProjectSettings/ in $FRESH. Is that a Unity project?"; exit 1; }
[ -d "$FRESH/Packages" ]       || { echo "No Packages/ in $FRESH. Is that a Unity project?"; exit 1; }

echo "Copying the generated project config into the repo..."
cp -R "$FRESH/ProjectSettings" "$REPO/"
cp -R "$FRESH/Packages"        "$REPO/"

# Unity writes an absolute path in here on some versions; it is regenerated on open.
rm -f "$REPO/Packages/packages-lock.json"

echo
echo "Checking every asset still has its .meta ..."
missing=0
while IFS= read -r -d '' f; do
  case "$f" in *.meta) continue;; esac
  [ -e "$f.meta" ] || { echo "  MISSING: $f.meta"; missing=$((missing+1)); }
done < <(find "$REPO/Assets" -type f -print0)
[ "$missing" -eq 0 ] && echo "  all good" || { echo "  $missing missing. Do not commit until this is clean."; exit 1; }

cat <<DONE

Done. Next:

  1. Unity Hub -> Open -> pick:
       $REPO
     First open takes a while; it is building Library/, which is gitignored.

  2. Check the console is clean. A C# error puts the editor in Safe Mode and every
     MCP command then fails with nothing useful.

  3. Window -> Package Manager -> install, in this order:
       Test Framework            (the edit mode tests need it)
       XR Plugin Management
       OpenXR Plugin             (enable the Meta Quest feature group)
       XR Interaction Toolkit    (import the Starter Assets sample)
     Install these through Package Manager, never by editing Packages/manifest.json.

  4. Window -> General -> Test Runner -> EditMode -> Run All.
     40 tests should pass. That is the real check; the headless suite is a stand-in.

  5. git add ProjectSettings Packages && git commit
DONE

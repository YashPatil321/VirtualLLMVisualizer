#!/usr/bin/env bash
# Compile and run the project's C# without Unity. See README.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HERE="$ROOT/tools/headless-tests"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

command -v mcs >/dev/null || { echo "mcs not found. Install it: sudo apt-get install -y mono-mcs"; exit 1; }

SCRIPTS=$(find "$ROOT/Assets/Scripts" -name '*.cs')
TESTS=$(find "$ROOT/Assets/Tests" -name '*.cs')
EDITOR=$(find "$ROOT/Assets/Editor" -name '*.cs' 2>/dev/null || true)

echo "== editor scripts compile =="
if [ -n "$EDITOR" ]; then
  mcs -target:library -out:"$OUT/editor.dll" -langversion:latest "$HERE/UnityShim.cs" "$HERE/EditorShim.cs" $SCRIPTS $EDITOR
  echo "  ok"
else
  echo "  none"
fi

echo
echo "== edit mode tests =="
mcs -out:"$OUT/tests.exe" -langversion:latest "$HERE/UnityShim.cs" "$HERE/RunTests.cs" $SCRIPTS $TESTS
mono "$OUT/tests.exe"

echo
echo "== full arc on the real assets =="
mcs -out:"$OUT/fullarc.exe" -langversion:latest "$HERE/UnityShim.cs" "$HERE/AssetLoader.cs" "$HERE/FullArc.cs" $SCRIPTS
mono "$OUT/fullarc.exe" "$ROOT/Assets/Data"

echo
echo "== request act, frame by frame =="
mcs -out:"$OUT/sim.exe" -langversion:latest "$HERE/UnityShim.cs" "$HERE/Sim.cs" $SCRIPTS
mono "$OUT/sim.exe" "$ROOT/Assets/Data/sample-trace.json"

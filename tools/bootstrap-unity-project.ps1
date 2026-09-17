# Turns this repo clone into an openable Unity project. Windows equivalent of
# bootstrap-unity-project.sh. See that script's header for why it works this way.
#
#   .\tools\bootstrap-unity-project.ps1 -Fresh "$HOME\Unity\OCSVisualizer-fresh"
#
param([Parameter(Mandatory=$true)][string]$Fresh)

$ErrorActionPreference = "Stop"
$Repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not (Test-Path (Join-Path $Fresh "ProjectSettings"))) { throw "No ProjectSettings\ in $Fresh. Is that a Unity project?" }
if (-not (Test-Path (Join-Path $Fresh "Packages")))        { throw "No Packages\ in $Fresh. Is that a Unity project?" }

Write-Host "Copying the generated project config into the repo..."
Copy-Item (Join-Path $Fresh "ProjectSettings") $Repo -Recurse -Force
Copy-Item (Join-Path $Fresh "Packages")        $Repo -Recurse -Force
Remove-Item (Join-Path $Repo "Packages\packages-lock.json") -ErrorAction SilentlyContinue

Write-Host "`nChecking every asset still has its .meta ..."
$missing = 0
Get-ChildItem (Join-Path $Repo "Assets") -Recurse -File | Where-Object { $_.Extension -ne ".meta" } | ForEach-Object {
    if (-not (Test-Path ($_.FullName + ".meta"))) { Write-Host "  MISSING: $($_.FullName).meta"; $missing++ }
}
if ($missing -eq 0) { Write-Host "  all good" }
else { Write-Host "  $missing missing. Do not commit until this is clean."; exit 1 }

Write-Host @"

Done. Next:

  1. Unity Hub -> Open -> pick:
       $Repo
     First open takes a while; it is building Library\, which is gitignored.

  2. Check the console is clean. A C# error puts the editor in Safe Mode and every
     MCP command then fails with nothing useful.

  3. Window -> Package Manager -> install, in this order:
       Test Framework            (the edit mode tests need it)
       XR Plugin Management
       OpenXR Plugin             (enable the Meta Quest feature group)
       XR Interaction Toolkit    (import the Starter Assets sample)
     Install these through Package Manager, never by editing Packages\manifest.json.

  4. Window -> General -> Test Runner -> EditMode -> Run All.
     40 tests should pass.

  5. git add ProjectSettings Packages ; git commit
"@

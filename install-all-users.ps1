# Installs ImageToIcon for all user accounts on this machine.
# Must be run as Administrator (right-click > "Als Administrator ausführen").
#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

$source = Join-Path $env:LOCALAPPDATA 'ImageToIcon-publish'
$target = Join-Path $env:ProgramFiles 'ImageToIcon'
$exe    = Join-Path $target 'ImageToIcon.exe'

if (-not (Test-Path (Join-Path $source 'ImageToIcon.exe'))) {
    throw "Build not found: $source. Run the publish step first."
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force

# All-users Start Menu shortcut (visible to every account).
$shortcutDir = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs'
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $shortcutDir 'ImageToIcon.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $target
$shortcut.Description = 'Convert images to multi-size icons'
$shortcut.Save()

Write-Output "Installed to $target"
Write-Output "Start Menu shortcut: $shortcutDir\ImageToIcon.lnk"

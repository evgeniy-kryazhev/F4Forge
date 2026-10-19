#Requires -Version 5.1
<#
.SYNOPSIS
Builds F4Forge and creates a ZIP ready to install through Mod Organizer 2.
.EXAMPLE
./build-mo2.ps1
.EXAMPLE
./build-mo2.ps1 -Jobs 4
.NOTES
Requires xmake, MSVC with C++23 support, the .NET 10 x64 SDK, and initialized submodules.
The archive contains F4SE/Plugins directly: MO2 installs these paths under Data.
Players still need F4SE and the .NET 10 x64 Runtime installed separately.
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 128)]
    [int]$Jobs = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryDirectory = $PSScriptRoot
$buildDescription = Get-Content -LiteralPath (Join-Path $repositoryDirectory 'xmake.lua') -Raw
$versionMatch = [regex]::Match($buildDescription, '(?m)^set_version\("([0-9]+\.[0-9]+\.[0-9]+)"\)')
if (-not $versionMatch.Success) {
    throw 'Cannot read the framework version from xmake.lua.'
}

foreach ($commandName in @('xmake', 'dotnet')) {
    if (-not (Get-Command $commandName -ErrorAction SilentlyContinue)) {
        throw "Required command is missing from PATH: $commandName"
    }
}

Push-Location -LiteralPath $repositoryDirectory
try {
    # Pass the project directory explicitly so xmake does not rely on directory discovery.
    & xmake f -P $repositoryDirectory -m releasedbg -a x64 -y
    if ($LASTEXITCODE -ne 0) {
        throw "xmake configuration failed with exit code $LASTEXITCODE."
    }

    # Building Core also builds the SDK through its declared dependency.
    & xmake build -P $repositoryDirectory -j $Jobs F4Forge.Core
    if ($LASTEXITCODE -ne 0) {
        throw "Framework build failed with exit code $LASTEXITCODE."
    }

    # Use an explicit payload so test DLLs, symbols and stale binaries stay out.
    $payload = @(
        [pscustomobject]@{
            FileName = 'F4Forge.Core.dll'
            ArchivePath = 'F4SE/Plugins/F4Forge.Core.dll'
        }
        [pscustomobject]@{
            FileName = 'F4Forge.Net.dll'
            ArchivePath = 'F4SE/Plugins/F4Forge/F4Forge.Net.dll'
        }
    )
    $binaryDirectory = Join-Path $repositoryDirectory 'build/bin'
    foreach ($file in $payload) {
        $sourcePath = Join-Path $binaryDirectory $file.FileName
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Build output is missing: $sourcePath"
        }
    }

    $packageDirectory = Join-Path $repositoryDirectory 'build/packages'
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    $archivePath = Join-Path $packageDirectory "F4Forge-$($versionMatch.Groups[1].Value)-MO2.zip"
    $temporaryPath = Join-Path $packageDirectory "$([guid]::NewGuid().ToString('N')).zip"

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    try {
        $archive = [IO.Compression.ZipFile]::Open($temporaryPath, [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($file in $payload) {
                $sourcePath = Join-Path $binaryDirectory $file.FileName
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $sourcePath,
                    $file.ArchivePath,
                    [IO.Compression.CompressionLevel]::Optimal
                ) | Out-Null
            }
        }
        finally {
            $archive.Dispose()
        }

        # Replace the previous package only after the new archive is complete.
        Move-Item -LiteralPath $temporaryPath -Destination $archivePath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath
        }
    }

    Write-Host "MO2 archive created: $archivePath"
}
finally {
    Pop-Location
}

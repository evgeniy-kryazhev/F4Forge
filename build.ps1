[CmdletBinding()]
param(
    [switch]$Clean,
    [string]$DotNetHostDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$nativeRoot = Join-Path $root "native"
$runtimeProject = Join-Path $root "runtimes\dotnet\runtime\F4Forge.DotNet.Runtime.csproj"
$buildRoot = Join-Path $root "build"
$packageRoot = Join-Path $buildRoot "F4SE\Plugins"
$frameworkRoot = Join-Path $packageRoot "F4Forge"

function Invoke-Checked {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $Command $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath $nativeRoot -PathType Container)) {
    throw "Native project directory was not found: $nativeRoot"
}

if ([string]::IsNullOrWhiteSpace($DotNetHostDir)) {
    $hostPackRoot = Join-Path ${env:ProgramFiles} "dotnet\packs\Microsoft.NETCore.App.Host.win-x64"
    $hostDir = Get-ChildItem -LiteralPath $hostPackRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { [version]$_.Name }; Descending = $true } |
        ForEach-Object { Join-Path $_.FullName "runtimes\win-x64\native" } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ "nethost.h") } |
        Select-Object -First 1
    if ($null -eq $hostDir) {
        throw "Could not locate the .NET x64 native host pack. Use -DotNetHostDir."
    }
    $DotNetHostDir = $hostDir
}

if (-not (Test-Path -LiteralPath (Join-Path $DotNetHostDir "nethost.h"))) {
    throw "Invalid .NET host directory: $DotNetHostDir"
}

if ($Clean -and (Test-Path -LiteralPath $buildRoot)) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}

Push-Location $nativeRoot
try {
    Invoke-Checked "xmake" @("f", "-y", "-p", "windows", "-a", "x64", "--dotnet_host_dir=$DotNetHostDir")
    Invoke-Checked "xmake" @("-r", "-y", "F4Forge", "F4Forge.Dotnet")
}
finally {
    Pop-Location
}

Invoke-Checked "dotnet" @("build", $runtimeProject, "--configuration", "Release")

$loader = Join-Path $nativeRoot "build\windows\x64\release\F4Forge.dll"
$provider = Join-Path $root "runtimes\dotnet\native-provider\bin\F4Forge.Dotnet.dll"
$managedRoot = Join-Path $root "runtimes\dotnet\runtime\bin\Release\net10.0"
$managedRuntime = Join-Path $managedRoot "F4Forge.DotNet.Runtime.dll"
$runtimeConfig = Join-Path $managedRoot "F4Forge.DotNet.Runtime.runtimeconfig.json"

foreach ($required in @($loader, $provider, $managedRuntime, $runtimeConfig)) {
    if ($null -eq $required -or -not (Test-Path -LiteralPath $required)) {
        throw "Build artifact was not found: $required"
    }
}

New-Item -ItemType Directory -Path $packageRoot, $frameworkRoot -Force | Out-Null
Copy-Item -LiteralPath $loader -Destination (Join-Path $packageRoot "F4Forge.dll") -Force
Copy-Item -LiteralPath $provider -Destination (Join-Path $frameworkRoot "F4Forge.Dotnet.dll") -Force
Copy-Item -LiteralPath $managedRuntime -Destination (Join-Path $frameworkRoot "F4Forge.DotNet.Runtime.dll") -Force
Copy-Item -LiteralPath $runtimeConfig -Destination (Join-Path $frameworkRoot "F4Forge.DotNet.Runtime.runtimeconfig.json") -Force

Write-Host "F4Forge build completed: $buildRoot"

[CmdletBinding()]
param(
    [switch]$Clean,
    [string]$DotNetHostDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$nativeRoot = Join-Path $root "native"
$runtimeProject = Join-Path $root "runtimes\dotnet\runtime\F4Forge.DotNet.Runtime.csproj"
$sampleProject = Join-Path $root "runtimes\dotnet\F4Forge.DotNet.Sample\F4Forge.DotNet.Sample.csproj"
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

Invoke-Checked "dotnet" @("build", $runtimeProject, "--configuration", "Release")
Invoke-Checked "dotnet" @("build", $sampleProject, "--configuration", "Release")

$managedRoot = Join-Path $root "runtimes\dotnet\runtime\bin\Release\net10.0"
$managedRuntime = Join-Path $managedRoot "F4Forge.DotNet.Runtime.dll"
$managedDeps = Join-Path $managedRoot "F4Forge.DotNet.Runtime.deps.json"
$runtimeConfig = Join-Path $managedRoot "F4Forge.DotNet.Runtime.runtimeconfig.json"
$sdkAssembly = Join-Path $root "runtimes\dotnet\sdk\bin\Release\net10.0\F4Forge.DotNet.Sdk.dll"
$sampleAssembly = Join-Path $root "runtimes\dotnet\F4Forge.DotNet.Sample\bin\Release\net10.0\F4Forge.DotNet.Sample.dll"
$sampleManifest = Join-Path $root "runtimes\dotnet\F4Forge.DotNet.Sample\f4forge.plugin.json"
$resourceDirectory = Join-Path $nativeRoot "generated"
$resourceFile = Join-Path $resourceDirectory "dotnet_runtime.rc"

foreach ($required in @($managedRuntime, $managedDeps, $runtimeConfig, $sdkAssembly, $sampleAssembly, $sampleManifest)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Managed build artifact was not found: $required"
    }
}

New-Item -ItemType Directory -Path $resourceDirectory -Force | Out-Null
$resourceRuntimePath = $managedRuntime.Replace('\', '/')
$resourceDepsPath = $managedDeps.Replace('\', '/')
$resourceConfigPath = $runtimeConfig.Replace('\', '/')
@(
    "101 RCDATA `"$resourceRuntimePath`""
    "102 RCDATA `"$resourceDepsPath`""
    "103 RCDATA `"$resourceConfigPath`""
) | Set-Content -LiteralPath $resourceFile -Encoding ASCII

Push-Location $nativeRoot
try {
    Invoke-Checked "xmake" @("f", "-y", "-p", "windows", "-a", "x64", "--dotnet_host_dir=$DotNetHostDir")
    Invoke-Checked "xmake" @("-r", "-y", "F4Forge", "F4Forge.Dotnet")
}
finally {
    Pop-Location
}

$loader = Join-Path $nativeRoot "build\windows\x64\release\F4Forge.dll"
$provider = Join-Path $root "runtimes\dotnet\native-provider\bin\F4Forge.Dotnet.dll"

foreach ($required in @($loader, $provider, $sdkAssembly)) {
    if ($null -eq $required -or -not (Test-Path -LiteralPath $required)) {
        throw "Build artifact was not found: $required"
    }
}

New-Item -ItemType Directory -Path $packageRoot, $frameworkRoot -Force | Out-Null
Remove-Item -LiteralPath @(
    (Join-Path $frameworkRoot "F4Forge.Runtime.DotNet.dll"),
    (Join-Path $frameworkRoot "F4Forge.DotNet.Runtime.dll"),
    (Join-Path $frameworkRoot "F4Forge.DotNet.Runtime.runtimeconfig.json")
) -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath $loader -Destination (Join-Path $packageRoot "F4Forge.dll") -Force
Copy-Item -LiteralPath $provider -Destination (Join-Path $frameworkRoot "F4Forge.Dotnet.dll") -Force
Copy-Item -LiteralPath $sdkAssembly -Destination (Join-Path $frameworkRoot "F4Forge.DotNet.Sdk.dll") -Force

$sampleRoot = Join-Path $frameworkRoot "Samples\F4Forge.DotNet.Sample"
New-Item -ItemType Directory -Path $sampleRoot -Force | Out-Null
Copy-Item -LiteralPath $sampleAssembly -Destination $sampleRoot -Force
Copy-Item -LiteralPath $sampleManifest -Destination $sampleRoot -Force

Write-Host "F4Forge build completed: $buildRoot"

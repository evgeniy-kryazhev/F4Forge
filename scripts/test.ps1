Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$nativeRoot = Join-Path $root "native"

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

& (Join-Path $root "scripts\build.ps1")

Push-Location $nativeRoot
try {
    Invoke-Checked "xmake" @(
        "-y",
        "F4ForgeAbiTests",
        "F4ForgeAbiCCompile",
        "F4ForgeRegistryTests",
        "F4ForgeModuleTests",
        "F4ForgeLifecycleTests",
        "F4ForgeDependencyTests",
        "F4ForgeHostTests",
        "F4ForgeEventTests",
        "F4ForgeCapabilityTests",
        "F4ForgeRuntimeTests",
        "F4ForgeConfigTests",
        "F4ForgeHostFxrVersionTests"
    )

    $nativeTests = @(
        "F4ForgeAbiTests",
        "F4ForgeAbiCCompile",
        "F4ForgeRegistryTests",
        "F4ForgeModuleTests",
        "F4ForgeLifecycleTests",
        "F4ForgeDependencyTests",
        "F4ForgeHostTests",
        "F4ForgeEventTests",
        "F4ForgeCapabilityTests",
        "F4ForgeRuntimeTests",
        "F4ForgeConfigTests",
        "F4ForgeHostFxrVersionTests"
    )
    foreach ($test in $nativeTests) {
        Invoke-Checked "xmake" @("run", $test)
    }
}
finally {
    Pop-Location
}

$managedTest = Join-Path $root "runtimes\dotnet\tests\F4Forge.DotNet.Tests.csproj"
Invoke-Checked "dotnet" @("build", $managedTest, "--configuration", "Release")
Invoke-Checked "dotnet" @("run", "--project", $managedTest, "--configuration", "Release", "--no-build")
Invoke-Checked "dotnet" @("format", $managedTest, "--verify-no-changes", "--no-restore")

Write-Host "F4Forge tests completed successfully."

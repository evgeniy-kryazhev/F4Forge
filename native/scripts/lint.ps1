param(
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
$clangFormat = Get-Command clang-format -ErrorAction SilentlyContinue
$clangTidy = Get-Command clang-tidy -ErrorAction SilentlyContinue
if ($null -eq $clangFormat) { throw "clang-format is required for native linting." }
if ($null -eq $clangTidy) { throw "clang-tidy is required for native linting." }

Push-Location $nativeRoot
try {
    if (-not $SkipBuild) {
        & xmake project -k compile_commands
        if ($LASTEXITCODE -ne 0) { throw "Failed to generate compile_commands.json." }
    }

    $files = Get-ChildItem -LiteralPath $nativeRoot -Recurse -File -Include *.c, *.cc, *.cpp, *.h, *.hpp |
        Where-Object { $_.FullName -notmatch "\\(lib|build|\.xmake)\\" }

    foreach ($file in $files) {
        & $clangFormat.Source --dry-run --Werror --style=file -- $file.FullName
        if ($LASTEXITCODE -ne 0) { throw "clang-format check failed for $($file.FullName)." }
    }

    $sourceFiles = $files | Where-Object { $_.Extension -in @(".c", ".cc", ".cpp") }
    foreach ($file in $sourceFiles) {
        & $clangTidy.Source -p=. -- $file.FullName
        if ($LASTEXITCODE -ne 0) { throw "clang-tidy failed for $($file.FullName)." }
    }
}
finally {
    Pop-Location
}

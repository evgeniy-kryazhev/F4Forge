Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
$clang = Get-Command clang -ErrorAction SilentlyContinue
if ($null -eq $clang) {
    throw "clang is required for native static analysis."
}

$files = Get-ChildItem -LiteralPath $nativeRoot -Recurse -File -Include *.c, *.cc, *.cpp |
    Where-Object { $_.FullName -notmatch "\\(lib|build|\.xmake)\\" }

foreach ($file in $files) {
    & $clang.Source --analyze -std=c++23 -I (Join-Path $nativeRoot "abi") -I (Join-Path $nativeRoot "core") -- $file.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "clang static analysis failed for $($file.FullName)."
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
$clangFormat = Get-Command clang-format -ErrorAction SilentlyContinue
if ($null -eq $clangFormat) {
    throw "clang-format is required for native formatting."
}

$files = Get-ChildItem -LiteralPath $nativeRoot -Recurse -File -Include *.c, *.cc, *.cpp, *.h, *.hpp |
    Where-Object { $_.FullName -notmatch "\\(lib|build|\.xmake)\\" }

foreach ($file in $files) {
    & $clangFormat.Source -i --style=file -- $file.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "clang-format failed for $($file.FullName)."
    }
}

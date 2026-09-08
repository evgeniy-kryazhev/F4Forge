Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
$providerRoot = (Resolve-Path (Join-Path $nativeRoot "..\runtimes\dotnet\native-provider"))
$clangFormat = Get-Command clang-format -ErrorAction SilentlyContinue
if ($null -eq $clangFormat) {
    throw "clang-format is required for native formatting."
}

$files = @(
    Get-ChildItem -LiteralPath $nativeRoot -Recurse -File -Include *.c, *.cc, *.cpp, *.h, *.hpp |
        Where-Object { $_.FullName -notmatch "\\(lib|build|\.xmake)\\" }
    Get-ChildItem -LiteralPath $providerRoot -Recurse -File -Include *.cc, *.cpp, *.h, *.hpp |
        Where-Object { $_.FullName -notmatch "\\(bin|obj|build)\\" }
)

foreach ($file in $files) {
    & $clangFormat.Source -i --style="file:$nativeRoot\.clang-format" -- $file.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "clang-format failed for $($file.FullName)."
    }
}

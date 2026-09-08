Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$nativeRoot = Join-Path $root "native"

Get-Command cppcheck -ErrorAction Stop | Out-Null

$compileCommands = Join-Path $nativeRoot "compile_commands.json"
if (-not (Test-Path -LiteralPath $compileCommands)) {
    throw "Missing compilation database: $compileCommands"
}

Push-Location $nativeRoot
try {
    & cppcheck `
        "--project=compile_commands.json" `
        "--enable=warning,style,performance,portability" `
        "--error-exitcode=1" `
        "--inline-suppr" `
        "-i" "lib/commonlibf4" `
        "-i" "build" `
        "-i" "generated" `
        "-i" ".xmake"

    if ($LASTEXITCODE -ne 0) {
        throw "cppcheck failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

# Builds the Windows deep link binaries into the parent folder (Unity ignores this Source~ folder):
#   EWova.DeepLink.Win.Core.dll  <- Core\       (referenced by WindowsDeepLinking.cs)
#   DeepLinkLauncher.exe         <- Launcher\   (copied next to the player exe by DeepLinkLauncherPostprocessBuild)
#
# Needs Visual Studio (any edition) for the Roslyn compiler; targets .NET Framework 4.7.2 (built into Windows 10/11).
#   powershell -ExecutionPolicy Bypass -File .\Build.ps1
#
# Sign DeepLinkLauncher.exe after building once a code signing certificate is available.

$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw "vswhere not found: $vswhere (install Visual Studio)" }
$csc = & $vswhere -latest -products * -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.exe not found (install the .NET desktop workload in Visual Studio)' }

$refDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (-not (Test-Path -LiteralPath $refDir)) { throw "Reference assemblies not found: $refDir (install the .NET Framework 4.7.2 targeting pack)" }
$refs = 'mscorlib.dll', 'System.dll', 'System.Core.dll' | ForEach-Object { "/reference:$(Join-Path $refDir $_)" }

$outDir = Split-Path -Parent $PSScriptRoot
$common = @('/nologo', '/noconfig', '/nostdlib+', '/optimize+', '/deterministic+', '/debug-', '/warnaserror+') + $refs

function Invoke-Csc([string[]]$CscArgs) {
    & $csc @common @CscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

Invoke-Csc @('/target:library', "/out:$(Join-Path $outDir 'EWova.DeepLink.Win.Core.dll')",
    (Join-Path $PSScriptRoot 'Core\WindowsDeepLinkingCore.cs'),
    (Join-Path $PSScriptRoot 'Core\Properties\AssemblyInfo.cs'))

Invoke-Csc @('/target:winexe', '/platform:anycpu', "/out:$(Join-Path $outDir 'DeepLinkLauncher.exe')",
    (Join-Path $PSScriptRoot 'Launcher\DeepLinkLauncher.cs'))

Write-Host "Built into $outDir"

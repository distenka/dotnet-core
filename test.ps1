<#
.SYNOPSIS
    DEV validation: builds the whole solution (warnings are errors) and runs every test project in it.

.DESCRIPTION
    1. Checks that the .NET SDK pinned by global.json (8.0 feature band, ADR-005 §2.3) is installed.
    2. Builds Distenka.sln from scratch (--no-incremental, so every warning is reported) with -warnaserror,
       unless -AllowWarnings is given (DCS-010 AC 1: zero warnings).
    3. Runs the solution's test projects without rebuilding: test/Distenka.Tests (processor host) and
       test/Distenka.Client.Tests (client: options, registration, request headers, API surface). Distenka.Tests includes:
       - process-level tests that spawn the built Distenka.Diagnostics.dll / Distenka.Tests.Cli.dll with
         `dotnet <dll> run <config>` the way the platform executor does (DCS-040/042);
       - package-boundary tests that `dotnet pack --no-build` the Distenka, Distenka.Client and
         Distenka.Processor.Testing projects and inspect the generated .nuspec files (DCS-010).
    Needs only the .NET 8 SDK and nuget.org (see nuget.config); no platform infrastructure.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER Filter
    Optional `dotnet test --filter` expression, e.g. 'FullyQualifiedName~Scenarios.Running'.

.PARAMETER AllowWarnings
    Build without -warnaserror (warnings are still printed).

.EXAMPLE
    ./test.ps1
    ./test.ps1 -Filter 'FullyQualifiedName~Package_boundaries'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Filter,

    [switch] $AllowWarnings
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # Windows PowerShell turns native stderr into terminating errors under ErrorAction Stop; relax it for this probe.
    $ErrorActionPreference = 'Continue'
    $sdk = & dotnet --version 2>&1 | Out-String
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) {
        Write-Host $sdk
        Write-Error "No .NET SDK matching global.json (8.0.1xx or a later 8.0 feature band) is installed. Install the .NET 8 SDK, e.g. 'winget install Microsoft.DotNet.SDK.8'."
    }
    Write-Host "Using .NET SDK $($sdk.Trim())"

    $buildArgs = @('build', 'Distenka.sln', '-c', $Configuration, '--no-incremental')
    if (-not $AllowWarnings) { $buildArgs += '-warnaserror' }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $testArgs = @('test', 'Distenka.sln', '-c', $Configuration, '--no-build',
        '--logger', 'console;verbosity=normal')
    if ($Filter) { $testArgs += @('--filter', $Filter) }

    & dotnet @testArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

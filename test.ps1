<#
.SYNOPSIS
    DEV validation: builds the SDK and runs the Distenka.Tests suite (unit + process-level tests).

.DESCRIPTION
    Builds test/Distenka.Tests (which builds src/Distenka and src/Diagnostics) and runs its tests.
    The process-level tests spawn the built Distenka.Diagnostics.dll with `dotnet <dll> run <config>`,
    the way the platform executor does, and check exit codes and stdout against the executor's rules.
    Needs only the .NET 8 SDK and nuget.org (or a configured feed); no platform infrastructure.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER Filter
    Optional `dotnet test --filter` expression, e.g. 'FullyQualifiedName~Scenarios.Running'.

.EXAMPLE
    ./test.ps1
    ./test.ps1 -Filter 'FullyQualifiedName~Run_exit_codes'
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [string] $Filter
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $project = 'test/Distenka.Tests/Distenka.Tests.csproj'

    $testArgs = @('test', $project, '-c', $Configuration, '--logger', 'console;verbosity=normal')
    if ($Filter) { $testArgs += @('--filter', $Filter) }

    & dotnet @testArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

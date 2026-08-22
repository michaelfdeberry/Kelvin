<#
.SYNOPSIS
Starts the Kelvin client, server, and simulator in separate terminal windows.

.DESCRIPTION
Opens new terminal instances for the frontend, backend, and simulator so local development can be launched
from one command. The simulator starts by default and uses the default arguments shown in the README.

.PARAMETER NoSimulator
Skips launching the simulator terminal.

.PARAMETER SimulatorArgs
Additional arguments to forward to the simulator. These are appended to the default dotnet run command.

.EXAMPLE
./dev.start.ps1

.EXAMPLE
./dev.start.ps1 -NoSimulator

.EXAMPLE
./dev.start.ps1 -SimulatorArgs @('--port','COM3','--server-url','http://localhost:5209','--non-interactive')
#>

[CmdletBinding()]
param(
  [switch]$NoSimulator,
  [string[]]$SimulatorArgs = @('--port', 'COM1', '--server-url', 'http://localhost:5209', '--non-interactive')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$clientDir = Join-Path $repoRoot 'src/Kelvin.Client'
$serverDir = Join-Path $repoRoot 'src/Kelvin.Server'
$serverProject = Join-Path $serverDir 'Kelvin.Server.csproj'
$simulatorDir = Join-Path $repoRoot 'src/Kelvin.Simulator'
$simulatorProject = Join-Path $simulatorDir 'Kelvin.Simulator.csproj'

function Resolve-Executable {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  $command = Get-Command $Name -ErrorAction SilentlyContinue
  if ($command) {
    return $command.Source
  }

  throw "Could not find '$Name' on PATH."
}

function Start-DevProcess {
  param(
    [Parameter(Mandatory = $true)]
    [string]$DisplayName,

    [Parameter(Mandatory = $true)]
    [string]$FilePath,

    [Parameter(Mandatory = $false)]
    [string[]]$ArgumentList = @(),

    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory
  )

  $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -WorkingDirectory $WorkingDirectory -PassThru
  Write-Host "Started $DisplayName in a new terminal window (PID $($process.Id))." -ForegroundColor Green
}

$dotnet = Resolve-Executable -Name 'dotnet'
$clientPackageManager = Resolve-Executable -Name 'pn.cmd'

Start-DevProcess -DisplayName 'Kelvin Server' -FilePath $dotnet -ArgumentList @('run', '--project', $serverProject) -WorkingDirectory $serverDir
Start-DevProcess -DisplayName 'Kelvin Client' -FilePath $clientPackageManager -ArgumentList @('dev') -WorkingDirectory $clientDir

if (-not $NoSimulator) {
  $simulatorArguments = @('run', '--project', $simulatorProject, '--') + $SimulatorArgs
  Start-DevProcess -DisplayName 'Kelvin Simulator' -FilePath $dotnet -ArgumentList $simulatorArguments -WorkingDirectory $simulatorDir
}

Write-Host 'Development terminals launched.' -ForegroundColor Cyan

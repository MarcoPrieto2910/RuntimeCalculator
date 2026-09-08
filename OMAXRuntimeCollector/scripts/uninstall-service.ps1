param(
    [Parameter(Mandatory = $true)]
    [string]$InstallPath
)

$ErrorActionPreference = "Stop"

$ServiceName = "OMAXRuntimeCollector"

Write-Host "========================================"
Write-Host "OMAX Runtime Collector - Uninstall"
Write-Host "========================================"
Write-Host ""

# ---------------------------------------------------------
# Check administrator privileges
# ---------------------------------------------------------

$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentUser)

if (-not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator))
{
    Write-Host "ERROR: This script must be run as Administrator."
    exit 1
}

# ---------------------------------------------------------
# Stop and remove service
# ---------------------------------------------------------

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($null -ne $existingService) {

    if ($existingService.Status -ne "Stopped") {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "Removing service..."
    sc.exe delete $ServiceName | Out-Null

    Start-Sleep -Seconds 2

    Write-Host "Service removed."
}
else {
    Write-Host "Service is not installed."
}

# ---------------------------------------------------------
# Remove application files
# ---------------------------------------------------------

if (Test-Path $InstallPath) {
    Write-Host "Removing installation directory..."
    Remove-Item $InstallPath -Recurse -Force

    Write-Host "Installation directory removed."
}
else {
    Write-Host "Installation directory does not exist."
}

Write-Host ""
Write-Host "Uninstallation completed."
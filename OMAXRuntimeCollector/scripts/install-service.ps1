param(
    [Parameter(Mandatory = $true)]
    [string]$InstallPath
)

$ErrorActionPreference = "Stop"

$ServiceName = "OMAXRuntimeCollector"
$DisplayName = "OMAX Runtime Collector"
$ExePath = Join-Path $InstallPath "OMAXRuntimeCollector.exe"

Write-Host "========================================"
Write-Host "OMAX Runtime Collector - Install"
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
# Check application files
# ---------------------------------------------------------

if (-not (Test-Path $InstallPath)) {
    Write-Host "ERROR: Installation directory was not found:"
    Write-Host $InstallPath
    exit 1
}

if (-not (Test-Path $ExePath)) {
    Write-Host "ERROR: OMAXRuntimeCollector.exe was not found:"
    Write-Host $ExePath
    exit 1
}

# ---------------------------------------------------------
# Stop existing service
# ---------------------------------------------------------

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($null -ne $existingService) {

    Write-Host "Existing service found."

    if ($existingService.Status -ne "Stopped") {
        Write-Host "Stopping existing service..."
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    Write-Host "Removing existing service..."
    sc.exe delete $ServiceName | Out-Null

    Start-Sleep -Seconds 2
}

# ---------------------------------------------------------
# Create Windows Service
# ---------------------------------------------------------

Write-Host "Creating Windows Service..."

sc.exe create $ServiceName `
    binpath= "`"$ExePath`"" `
    start= auto `
    displayname= "`"$DisplayName`"" | Out-Null

# ---------------------------------------------------------
# Configure recovery
# ---------------------------------------------------------

Write-Host "Configuring service recovery..."

sc.exe failure $ServiceName `
    reset= 86400 `
    actions= restart/5000/restart/5000/restart/5000 | Out-Null

# ---------------------------------------------------------
# Start service
# ---------------------------------------------------------

Write-Host "Starting service..."

Start-Service -Name $ServiceName

Start-Sleep -Seconds 2

# ---------------------------------------------------------
# Show status
# ---------------------------------------------------------

Write-Host ""
Write-Host "Installation completed."
Write-Host ""

Get-Service -Name $ServiceName

Write-Host ""
Write-Host "Installation path:"
Write-Host $InstallPath
Write-Host ""
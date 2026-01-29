<#
.SYNOPSIS
    Builds and installs IPCT (IP Change Tool).

.DESCRIPTION
    This script:
    1. Uninstalls any existing version of IPCT
    2. Builds a fresh version of IPCT
    3. Installs the newly built MSI

.PARAMETER Arch
    Target architecture: x64 or arm64. Defaults to x64.

.PARAMETER SkipBuild
    Skip the build step and only perform uninstall/install.

.EXAMPLE
    .\build-and-install.ps1
    .\build-and-install.ps1 -Arch arm64
#>

param(
    [ValidateSet("x64", "arm64")]
    [string]$Arch = "x64",
    
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Check for admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This script requires Administrator privileges. Please run as Administrator."
    exit 1
}

# Check for .NET SDK (required for building)
if (-not $SkipBuild) {
    $dotnetVersion = $null
    try {
        $dotnetVersion = & dotnet --version 2>$null
    } catch {
        # dotnet not found
    }
    
    if (-not $dotnetVersion) {
        Write-Error ".NET SDK is not installed or not in PATH. Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download"
        exit 1
    }
    Write-Host "Found .NET SDK: $dotnetVersion" -ForegroundColor Gray
    
    # Check for WiX toolset
    $wixInstalled = $null
    try {
        $wixInstalled = & dotnet tool list --global 2>$null | Select-String "wix"
    } catch {
        # wix not found
    }
    
    if (-not $wixInstalled) {
        Write-Host "WiX toolset not found. Installing..." -ForegroundColor Yellow
        & dotnet tool install --global wix
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to install WiX toolset. Please run: dotnet tool install --global wix"
            exit 1
        }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IPCT Build and Install Script" -ForegroundColor Cyan
Write-Host "Architecture: $Arch" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Uninstall existing IPCT
Write-Host "[1/3] Checking for existing IPCT installation..." -ForegroundColor Yellow

# Method 1: Try to find IPCT using Get-Package (faster than WMI)
$existingPackage = Get-Package -Name "IPCT" -ErrorAction SilentlyContinue

if ($existingPackage) {
    Write-Host "  Found existing IPCT installation. Uninstalling..." -ForegroundColor Yellow
    
    # Stop the UI application if it's running
    $uiProcess = Get-Process -Name "IpChanger.UI" -ErrorAction SilentlyContinue
    if ($uiProcess) {
        Write-Host "  Stopping IpChanger.UI..." -ForegroundColor Yellow
        Stop-Process -Name "IpChanger.UI" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
    
    # Stop the service if it's running
    $service = Get-Service -Name "IpChangerService" -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Write-Host "  Stopping IpChangerService..." -ForegroundColor Yellow
            Stop-Service -Name "IpChangerService" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
    }
    
    # Uninstall using msiexec
    $uninstallString = $existingPackage.Meta.Attributes["UninstallString"]
    if ($uninstallString) {
        # Extract the product code from the uninstall string
        if ($uninstallString -match "\{[A-F0-9-]+\}") {
            $productCode = $Matches[0]
            Write-Host "  Uninstalling product code: $productCode" -ForegroundColor Yellow
            $process = Start-Process -FilePath "msiexec.exe" -ArgumentList "/x", $productCode, "/quiet", "/norestart" -Wait -PassThru
            if ($process.ExitCode -eq 0) {
                Write-Host "  Uninstall completed successfully." -ForegroundColor Green
            } else {
                Write-Warning "  Uninstall returned exit code: $($process.ExitCode)"
            }
        }
    } else {
        # Try direct uninstall via Get-Package
        $existingPackage | Uninstall-Package -Force -ErrorAction SilentlyContinue
        Write-Host "  Uninstall completed." -ForegroundColor Green
    }
} else {
    # Method 2: Try using registry to find MSI product
    $uninstallKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    
    $ipctProduct = Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue | 
        Where-Object { $_.DisplayName -eq "IPCT" }
    
    if ($ipctProduct) {
        Write-Host "  Found IPCT in registry. Uninstalling..." -ForegroundColor Yellow
        
        # Stop the UI application if it's running
        $uiProcess = Get-Process -Name "IpChanger.UI" -ErrorAction SilentlyContinue
        if ($uiProcess) {
            Write-Host "  Stopping IpChanger.UI..." -ForegroundColor Yellow
            Stop-Process -Name "IpChanger.UI" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }
        
        # Stop the service first
        $service = Get-Service -Name "IpChangerService" -ErrorAction SilentlyContinue
        if ($service -and $service.Status -eq "Running") {
            Write-Host "  Stopping IpChangerService..." -ForegroundColor Yellow
            Stop-Service -Name "IpChangerService" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
        }
        
        $productCode = $ipctProduct.PSChildName
        Write-Host "  Uninstalling product code: $productCode" -ForegroundColor Yellow
        $process = Start-Process -FilePath "msiexec.exe" -ArgumentList "/x", $productCode, "/quiet", "/norestart" -Wait -PassThru
        if ($process.ExitCode -eq 0) {
            Write-Host "  Uninstall completed successfully." -ForegroundColor Green
        } else {
            Write-Warning "  Uninstall returned exit code: $($process.ExitCode)"
        }
    } else {
        Write-Host "  No existing IPCT installation found." -ForegroundColor Green
    }
}

# Give the system a moment to clean up
Start-Sleep -Seconds 2

# Step 2: Build IPCT
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[2/3] Building IPCT..." -ForegroundColor Yellow
    
    $buildScript = Join-Path $PSScriptRoot "build.ps1"
    if (-not (Test-Path $buildScript)) {
        Write-Error "Build script not found at: $buildScript"
        exit 1
    }
    
    # Run the build script - use try/catch since build.ps1 uses $ErrorActionPreference = "Stop"
    try {
        & $buildScript -Arch $Arch
        $buildExitCode = $LASTEXITCODE
    } catch {
        Write-Error "Build failed: $_"
        exit 1
    }
    
    if ($buildExitCode -ne 0 -and $buildExitCode -ne $null) {
        Write-Error "Build failed with exit code: $buildExitCode"
        exit 1
    }
    
    Write-Host "  Build completed successfully." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/3] Skipping build (SkipBuild flag set)." -ForegroundColor Yellow
}

# Step 3: Install the new MSI
Write-Host ""
Write-Host "[3/3] Installing IPCT..." -ForegroundColor Yellow

$msiPath = Join-Path $PSScriptRoot "installer\bin\$Arch\Release\IPCTInstaller.msi"
if (-not (Test-Path $msiPath)) {
    Write-Error "MSI not found at: $msiPath"
    exit 1
}

Write-Host "  Installing from: $msiPath" -ForegroundColor Yellow

$process = Start-Process -FilePath "msiexec.exe" -ArgumentList "/i", "`"$msiPath`"", "/quiet", "/norestart" -Wait -PassThru

if ($process.ExitCode -eq 0) {
    Write-Host "  Installation completed successfully." -ForegroundColor Green
} else {
    Write-Error "Installation failed with exit code: $($process.ExitCode)"
    exit 1
}

# Verify installation
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Verifying installation..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$installPath = "C:\Program Files\IPCT"
$serviceExe = Join-Path $installPath "IpChanger.Service.exe"
$uiExe = Join-Path $installPath "IpChanger.UI.exe"

$success = $true

if (Test-Path $serviceExe) {
    Write-Host "  [OK] Service executable found" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Service executable not found" -ForegroundColor Red
    $success = $false
}

if (Test-Path $uiExe) {
    Write-Host "  [OK] UI executable found" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] UI executable not found" -ForegroundColor Red
    $success = $false
}

$service = Get-Service -Name "IpChangerService" -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "  [OK] Service registered: $($service.Status)" -ForegroundColor Green
    
    # Start the service if it's not running
    if ($service.Status -ne "Running") {
        Write-Host "  Starting IpChangerService..." -ForegroundColor Yellow
        Start-Service -Name "IpChangerService" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $service = Get-Service -Name "IpChangerService"
        Write-Host "  Service status: $($service.Status)" -ForegroundColor Green
    }
} else {
    Write-Host "  [FAIL] Service not registered" -ForegroundColor Red
    $success = $false
}

Write-Host ""
if ($success) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "IPCT installation completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    
    # Launch the UI application
    Write-Host ""
    Write-Host "Launching IpChanger.UI..." -ForegroundColor Yellow
    Start-Process -FilePath $uiExe
    Write-Host "  UI started." -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "IPCT installation completed with errors." -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}

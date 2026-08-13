param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey
)

# Prioritize the custom .NET 10 SDK path if it exists on the machine
$dotnetPath = "dotnet"
if (Test-Path "C:\Users\Root\opt\dotnet\dotnet.exe") {
    $dotnetPath = "C:\Users\Root\opt\dotnet\dotnet.exe"
}

Write-Host "Using dotnet at: $dotnetPath" -ForegroundColor Cyan

# Clean up old packages directory to avoid pushing old versions
if (Test-Path "./nuget_packages") {
    Write-Host "Cleaning up old ./nuget_packages folder..." -ForegroundColor Gray
    Remove-Item -Recurse -Force "./nuget_packages"
}

# Build and pack the solution
Write-Host "Packing projects (Release mode)..." -ForegroundColor Cyan
& $dotnetPath pack -c Release -o ./nuget_packages

if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed! Aborting publish." -ForegroundColor Red
    exit 1
}

# Find all generated NuGet packages and push them
$packages = Get-ChildItem -Path "./nuget_packages" -Filter "*.nupkg"

if ($packages.Count -eq 0) {
    Write-Host "No .nupkg files found to publish!" -ForegroundColor Red
    exit 1
}

Write-Host "Pushing $($packages.Count) packages to GitHub Packages..." -ForegroundColor Cyan

foreach ($package in $packages) {
    Write-Host "Pushing $($package.Name)..." -ForegroundColor Yellow
    & $dotnetPath nuget push $package.FullName `
        --api-key $ApiKey `
        --source "https://nuget.pkg.github.com/esgaltur/index.json" `
        --skip-duplicate
}

Write-Host "Publish complete!" -ForegroundColor Green

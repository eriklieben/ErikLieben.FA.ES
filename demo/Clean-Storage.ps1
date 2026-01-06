# Clean-Storage.ps1
# Cleans Azurite persistent storage to start from scratch

Write-Host "=== TaskFlow Storage Cleanup ===" -ForegroundColor Cyan
Write-Host ""

# Detect container runtime (Podman or Docker)
$containerCmd = $null
if (Get-Command podman -ErrorAction SilentlyContinue) {
    $containerCmd = "podman"
    Write-Host "Detected Podman container runtime" -ForegroundColor Gray
} elseif (Get-Command docker -ErrorAction SilentlyContinue) {
    $containerCmd = "docker"
    Write-Host "Detected Docker container runtime" -ForegroundColor Gray
}

if ($containerCmd) {
    Write-Host ""

    # Stop any running Aspire containers
    Write-Host "Stopping Aspire containers..." -ForegroundColor Yellow
    & $containerCmd ps --filter "name=storage" --format "{{.ID}}" | ForEach-Object {
        & $containerCmd stop $_ 2>$null
        Write-Host "  Stopped container: $_" -ForegroundColor Green
    }

    & $containerCmd ps --filter "name=azurite" --format "{{.ID}}" | ForEach-Object {
        & $containerCmd stop $_ 2>$null
        Write-Host "  Stopped container: $_" -ForegroundColor Green
    }

    Write-Host ""

    # Remove volumes for Azurite
    Write-Host "Removing container volumes..." -ForegroundColor Yellow
    & $containerCmd volume ls --filter "name=azurite" --format "{{.Name}}" | ForEach-Object {
        & $containerCmd volume rm $_ 2>$null
        Write-Host "  Removed volume: $_" -ForegroundColor Green
    }

    & $containerCmd volume ls --filter "name=storage" --format "{{.Name}}" | ForEach-Object {
        & $containerCmd volume rm $_ 2>$null
        Write-Host "  Removed volume: $_" -ForegroundColor Green
    }
} else {
    Write-Host "No container runtime detected (Docker/Podman). Skipping container cleanup." -ForegroundColor Yellow
}

Write-Host ""

# Clean local bind mount data (if exists)
$bindMountPaths = @(
    (Join-Path $PSScriptRoot "azurite-data"),
    (Join-Path $PSScriptRoot "src\TaskFlow.AppHost\azurite-data")
)

foreach ($bindMountPath in $bindMountPaths) {
    if (Test-Path $bindMountPath) {
        Write-Host "Removing local storage directory..." -ForegroundColor Yellow
        Remove-Item -Path $bindMountPath -Recurse -Force
        Write-Host "  Removed: $bindMountPath" -ForegroundColor Green
    }
}

Write-Host ""

# Clean Aspire local storage cache
$aspirePath = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet-aspire"
if (Test-Path $aspirePath) {
    Write-Host "Cleaning Aspire cache..." -ForegroundColor Yellow
    Get-ChildItem -Path $aspirePath -Filter "*storage*" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  Removed: $($_.Name)" -ForegroundColor Green
    }
}

Write-Host ""

# Clean local Azurite data (when running Azurite directly, not in Docker)
$localAzuritePath = Join-Path $PSScriptRoot "..\azurite"
if (Test-Path $localAzuritePath) {
    Write-Host "Removing local Azurite data..." -ForegroundColor Yellow
    Remove-Item -Path $localAzuritePath -Recurse -Force
    Write-Host "  Removed: $localAzuritePath" -ForegroundColor Green
}

Write-Host ""

# Clean local projection JSON files
$projectionsPath = Join-Path $PSScriptRoot "src\TaskFlow.Api\projections"
if (Test-Path $projectionsPath) {
    Write-Host "Removing local projection JSON files..." -ForegroundColor Yellow
    Remove-Item -Path $projectionsPath -Recurse -Force
    Write-Host "  Removed: $projectionsPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Cleanup Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Storage has been reset. You can now start Aspire with fresh storage." -ForegroundColor Cyan
Write-Host "Run the AppHost with either:" -ForegroundColor White
Write-Host "  - 'Fresh Storage (HTTPS)' profile for ephemeral storage" -ForegroundColor Gray
Write-Host "  - 'Persistent Storage (HTTPS)' profile for persistent storage" -ForegroundColor Gray
Write-Host ""
Write-Host "Or if running standalone API + Azurite:" -ForegroundColor White
Write-Host "  - Restart Azurite: npx azurite --silent --location azurite" -ForegroundColor Gray
Write-Host "  - Restart API: dotnet run --project src/TaskFlow.Api/TaskFlow.Api.csproj" -ForegroundColor Gray

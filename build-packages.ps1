param(
    [Parameter(Mandatory=$true)]
    [string]$PackageVersion
)

$ErrorActionPreference = "Stop"

Write-Host "Building packages with version: $PackageVersion" -ForegroundColor Cyan

# Create output directory
$outputDir = Join-Path $PSScriptRoot "release-artifacts"
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# List of projects to pack (relative to solution root)
$projects = @(
    "src/ErikLieben.FA.ES/ErikLieben.FA.ES.csproj",
    "src/ErikLieben.FA.ES.Analyzers/ErikLieben.FA.ES.Analyzers.csproj",
    "src/ErikLieben.FA.ES.AspNetCore.MinimalApis/ErikLieben.FA.ES.AspNetCore.MinimalApis.csproj",
    "src/ErikLieben.FA.ES.Azure.Functions.Worker.Extensions/ErikLieben.FA.ES.Azure.Functions.Worker.Extensions.csproj",
    "src/ErikLieben.FA.ES.AzureStorage/ErikLieben.FA.ES.AzureStorage.csproj",
    "src/ErikLieben.FA.ES.CLI/ErikLieben.FA.ES.CLI.csproj",
    "src/ErikLieben.FA.ES.CodeAnalysis/ErikLieben.FA.ES.CodeAnalysis.csproj",
    "src/ErikLieben.FA.ES.CosmosDb/ErikLieben.FA.ES.CosmosDb.csproj",
    "src/ErikLieben.FA.ES.EventStreamManagement/ErikLieben.FA.ES.EventStreamManagement.csproj",
    "src/ErikLieben.FA.ES.Testing/ErikLieben.FA.ES.Testing.csproj",
    "src/ErikLieben.FA.ES.WebJobs.Isolated.Extensions/ErikLieben.FA.ES.WebJobs.Isolated.Extensions.csproj"
)

# Restore and build solution first
Write-Host "Restoring solution..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

Write-Host "Building solution in Release mode..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Pack each project
foreach ($project in $projects) {
    $projectPath = Join-Path $PSScriptRoot $project
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)

    Write-Host "Packing $projectName..." -ForegroundColor Yellow

    dotnet pack $projectPath `
        --configuration Release `
        --no-build `
        --output $outputDir `
        -p:PackageVersion=$PackageVersion `
        -p:Version=$PackageVersion

    if ($LASTEXITCODE -ne 0) { throw "Pack failed for $projectName" }
}

Write-Host ""
Write-Host "Packages created in $outputDir" -ForegroundColor Green
Get-ChildItem $outputDir | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Gray }

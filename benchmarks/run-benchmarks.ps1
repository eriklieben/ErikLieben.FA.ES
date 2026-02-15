#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs ErikLieben.FA.ES benchmarks with configurable options.

.DESCRIPTION
    This script provides a convenient wrapper around BenchmarkDotNet for running
    performance benchmarks on the event sourcing library.

.PARAMETER Filter
    Filter pattern for benchmark selection (e.g., "*Json*", "*Registry*").
    Default: runs interactive menu.

.PARAMETER All
    Run all benchmarks without interactive selection.

.PARAMETER Category
    Run benchmarks by category: Core, Serialization, Folding, Registry, Upcasting, Parsing, Storage

.PARAMETER List
    List available benchmarks without running them.

.PARAMETER OutputDir
    Custom output directory for results. Default: BenchmarkDotNet.Artifacts

.PARAMETER Quick
    Run benchmarks with reduced iterations for faster feedback (development mode).

.EXAMPLE
    ./run-benchmarks.ps1 -All
    Runs all benchmarks.

.EXAMPLE
    ./run-benchmarks.ps1 -Filter "*Json*"
    Runs only JSON-related benchmarks.

.EXAMPLE
    ./run-benchmarks.ps1 -Category Serialization
    Runs all serialization benchmarks.

.EXAMPLE
    ./run-benchmarks.ps1 -List
    Lists all available benchmarks.

.EXAMPLE
    ./run-benchmarks.ps1 -Quick -Filter "*Session*"
    Runs session benchmarks with reduced iterations.
#>

param(
    [string]$Filter,
    [switch]$All,
    [ValidateSet("Core", "Serialization", "Folding", "Registry", "Upcasting", "Parsing", "Storage")]
    [string]$Category,
    [switch]$List,
    [string]$OutputDir,
    [switch]$Quick
)

$ErrorActionPreference = "Stop"

# Navigate to benchmark project directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$benchmarkProject = Join-Path $scriptDir "ErikLieben.FA.ES.Benchmarks"

Push-Location $benchmarkProject
try {
    # Build arguments
    $args = @()

    if ($List) {
        $args += "--list", "flat"
    }
    elseif ($All) {
        $args += "--all"
    }
    elseif ($Category) {
        # Map category to filter pattern
        $categoryFilters = @{
            "Core"          = "*EventStream*,*Session*"
            "Serialization" = "*Json*"
            "Folding"       = "*Fold*"
            "Registry"      = "*Registry*"
            "Upcasting"     = "*Upcaster*"
            "Parsing"       = "*Token*"
            "Storage"       = "*DataStore*"
        }
        $args += "--filter", $categoryFilters[$Category]
    }
    elseif ($Filter) {
        $args += "--filter", $Filter
    }

    if ($Quick -and -not $List) {
        # Use ShortRun job for quick feedback
        $args += "--job", "short"
    }

    if ($OutputDir) {
        $args += "--artifacts", $OutputDir
    }

    # Display configuration
    Write-Host "ErikLieben.FA.ES Benchmark Runner" -ForegroundColor Cyan
    Write-Host "==================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Configuration:" -ForegroundColor Yellow
    Write-Host "  Project: $benchmarkProject"
    if ($Quick) {
        Write-Host "  Mode: Quick (ShortRun)" -ForegroundColor Yellow
    } else {
        Write-Host "  Mode: Full benchmark"
    }
    if ($Filter) { Write-Host "  Filter: $Filter" }
    if ($Category) { Write-Host "  Category: $Category" }
    if ($All) { Write-Host "  Running: All benchmarks" }
    Write-Host ""

    # Run benchmarks
    Write-Host "Running: dotnet run -c Release -- $($args -join ' ')" -ForegroundColor Gray
    Write-Host ""

    & dotnet run -c Release -- @args

    if ($LASTEXITCODE -eq 0 -and -not $List) {
        Write-Host ""
        Write-Host "Benchmark completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Results saved to:" -ForegroundColor Cyan
        $artifactsDir = if ($OutputDir) { $OutputDir } else { "BenchmarkDotNet.Artifacts/results" }
        Write-Host "  HTML: $artifactsDir/*.html"
        Write-Host "  CSV:  $artifactsDir/*.csv"
        Write-Host "  MD:   $artifactsDir/*-github.md"
    }
}
finally {
    Pop-Location
}

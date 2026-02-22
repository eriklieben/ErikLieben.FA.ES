<#
.SYNOPSIS
    Runs benchmarks on both .NET 9 and .NET 10 and generates a comparison report.

.DESCRIPTION
    This script runs the specified benchmarks on both target frameworks,
    then combines the results into a single HTML comparison report.

.PARAMETER Filter
    BenchmarkDotNet filter pattern (e.g., "*Json*", "*Serialize*")

.PARAMETER Config
    Benchmark configuration to use (quick, default, ci)

.EXAMPLE
    .\Compare-Runtimes.ps1 -Filter "*JsonSerializationComparison*" -Config quick
#>

param(
    [string]$Filter = "*JsonSerializationComparison*",
    [string]$Config = "quick"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Runtime Comparison Benchmark" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

# Run .NET 9 benchmarks
Write-Host "Running benchmarks on .NET 9..." -ForegroundColor Yellow
$net9Start = Get-Date
dotnet run -c Release -f net9.0 --project $scriptDir -- --filter $Filter --config $Config
$net9Duration = (Get-Date) - $net9Start
Write-Host "  Completed in $($net9Duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

# Find the .NET 9 JSON result
$resultsDir = Join-Path $scriptDir "BenchmarkDotNet.Artifacts\results"
$net9Json = Get-ChildItem $resultsDir -Filter "*-report-full.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $net9Json) {
    Write-Host "  Warning: No JSON result found. Using markdown results." -ForegroundColor Yellow
    $net9MarkedPath = $null
} else {
    $net9JsonPath = $net9Json.FullName
    Write-Host "  Result: $($net9Json.Name)" -ForegroundColor Gray

    # Rename to mark as .NET 9
    $net9MarkedPath = $net9JsonPath -replace "-report-full\.json$", "-net9-report-full.json"
    if (Test-Path $net9MarkedPath) { Remove-Item $net9MarkedPath }
    Copy-Item $net9JsonPath $net9MarkedPath
}

Write-Host ""

# Run .NET 10 benchmarks
Write-Host "Running benchmarks on .NET 10..." -ForegroundColor Yellow
$net10Start = Get-Date
dotnet run -c Release -f net10.0 --project $scriptDir -- --filter $Filter --config $Config
$net10Duration = (Get-Date) - $net10Start
Write-Host "  Completed in $($net10Duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

# Find the .NET 10 JSON result
$net10Json = Get-ChildItem $resultsDir -Filter "*-report-full.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $net10Json) {
    Write-Host "  Warning: No JSON result found." -ForegroundColor Yellow
    $net10MarkedPath = $null
} else {
    $net10JsonPath = $net10Json.FullName
    Write-Host "  Result: $($net10Json.Name)" -ForegroundColor Gray

    # Rename to mark as .NET 10
    $net10MarkedPath = $net10JsonPath -replace "-report-full\.json$", "-net10-report-full.json"
    if (Test-Path $net10MarkedPath) { Remove-Item $net10MarkedPath }
    Copy-Item $net10JsonPath $net10MarkedPath
}

Write-Host ""

# Generate comparison report
Write-Host "Generating comparison report..." -ForegroundColor Yellow

# Check if we have both JSON files
if (-not $net9MarkedPath -or -not $net10MarkedPath) {
    Write-Host "  Error: Could not find JSON results for both runtimes." -ForegroundColor Red
    Write-Host "  Make sure the 'quickmulti' config is being used (includes JSON exporter)." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $net9MarkedPath) -or -not (Test-Path $net10MarkedPath)) {
    Write-Host "  Error: JSON result files not found." -ForegroundColor Red
    exit 1
}

# Read both JSON files
$net9Data = Get-Content $net9MarkedPath | ConvertFrom-Json
$net10Data = Get-Content $net10MarkedPath | ConvertFrom-Json

# Create comparison HTML
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$comparisonHtmlPath = Join-Path $resultsDir "RuntimeComparison-$timestamp.html"

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>.NET 9 vs .NET 10 - Runtime Comparison</title>
  <style>
    :root {
      --ctp-rosewater: #f5e0dc;
      --ctp-flamingo: #f2cdcd;
      --ctp-pink: #f5c2e7;
      --ctp-mauve: #cba6f7;
      --ctp-red: #f38ba8;
      --ctp-maroon: #eba0ac;
      --ctp-peach: #fab387;
      --ctp-yellow: #f9e2af;
      --ctp-green: #a6e3a1;
      --ctp-teal: #94e2d5;
      --ctp-sky: #89dceb;
      --ctp-sapphire: #74c7ec;
      --ctp-blue: #89b4fa;
      --ctp-lavender: #b4befe;
      --ctp-text: #cdd6f4;
      --ctp-subtext1: #bac2de;
      --ctp-subtext0: #a6adc8;
      --ctp-overlay2: #9399b2;
      --ctp-overlay1: #7f849c;
      --ctp-overlay0: #6c7086;
      --ctp-surface2: #585b70;
      --ctp-surface1: #45475a;
      --ctp-surface0: #313244;
      --ctp-base: #1e1e2e;
      --ctp-mantle: #181825;
      --ctp-crust: #11111b;
    }
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: 'Segoe UI', system-ui, sans-serif;
      background: var(--ctp-base);
      color: var(--ctp-text);
      line-height: 1.6;
      min-height: 100vh;
    }
    header {
      background: linear-gradient(135deg, var(--ctp-surface0), var(--ctp-mantle));
      padding: 2.5rem 2rem;
      text-align: center;
      border-bottom: 2px solid var(--ctp-mauve);
    }
    header h1 {
      font-size: 2.25rem;
      color: var(--ctp-lavender);
      margin-bottom: 0.5rem;
    }
    header .subtitle { color: var(--ctp-subtext0); font-size: 1.1rem; }
    main {
      max-width: 1400px;
      margin: 0 auto;
      padding: 2rem;
      display: flex;
      flex-direction: column;
      gap: 1.5rem;
    }
    .card {
      background: var(--ctp-surface0);
      border-radius: 12px;
      padding: 1.5rem;
      border: 1px solid var(--ctp-surface1);
      box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
    }
    .card h2 {
      color: var(--ctp-mauve);
      font-size: 1.25rem;
      margin-bottom: 1rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid var(--ctp-surface1);
    }
    .overview {
      background: linear-gradient(135deg, var(--ctp-surface0), rgba(203, 166, 247, 0.1));
      border-left: 4px solid var(--ctp-mauve);
    }
    .runtime-badges {
      display: flex;
      gap: 1rem;
      margin: 1rem 0;
    }
    .runtime-badge {
      padding: 0.5rem 1rem;
      border-radius: 8px;
      font-weight: 600;
      font-family: 'Cascadia Code', monospace;
    }
    .runtime-badge.net9 {
      background: rgba(137, 180, 250, 0.2);
      color: var(--ctp-blue);
      border: 1px solid var(--ctp-blue);
    }
    .runtime-badge.net10 {
      background: rgba(166, 227, 161, 0.2);
      color: var(--ctp-green);
      border: 1px solid var(--ctp-green);
    }
    .comparison-table { border-left: 4px solid var(--ctp-sapphire); }
    table {
      width: 100%;
      border-collapse: collapse;
      font-family: 'Cascadia Code', monospace;
      font-size: 0.9rem;
    }
    th, td {
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid var(--ctp-surface1);
    }
    th {
      background: var(--ctp-surface1);
      color: var(--ctp-lavender);
      font-weight: 600;
      text-transform: uppercase;
      font-size: 0.75rem;
    }
    th.numeric, td.numeric { text-align: right; }
    tr:hover { background: var(--ctp-surface1); }
    .net9-row { background: rgba(137, 180, 250, 0.1); }
    .net10-row { background: rgba(166, 227, 161, 0.1); }
    .faster { color: var(--ctp-green); font-weight: bold; }
    .slower { color: var(--ctp-red); }
    .same { color: var(--ctp-subtext0); }
    .diff-badge {
      display: inline-block;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .diff-badge.faster {
      background: rgba(166, 227, 161, 0.2);
      color: var(--ctp-green);
    }
    .diff-badge.slower {
      background: rgba(243, 139, 168, 0.2);
      color: var(--ctp-red);
    }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
      margin-top: 1rem;
    }
    .summary-item {
      background: var(--ctp-surface1);
      padding: 1rem;
      border-radius: 8px;
      text-align: center;
    }
    .summary-value {
      font-size: 1.5rem;
      font-weight: bold;
      color: var(--ctp-green);
    }
    .summary-label {
      color: var(--ctp-subtext0);
      font-size: 0.85rem;
    }
    footer {
      text-align: center;
      padding: 2rem;
      color: var(--ctp-overlay1);
      font-size: 0.85rem;
      border-top: 1px solid var(--ctp-surface0);
      margin-top: 2rem;
    }
  </style>
</head>
<body>
  <header>
    <h1>.NET 9 vs .NET 10</h1>
    <p class="subtitle">Runtime Performance Comparison</p>
  </header>
  <main>
    <section class="card overview">
      <h2>Runtime Comparison</h2>
      <p>This report compares benchmark results between .NET 9 and .NET 10 to measure runtime performance improvements.</p>
      <div class="runtime-badges">
        <span class="runtime-badge net9">.NET 9.0</span>
        <span class="runtime-badge net10">.NET 10.0</span>
      </div>
    </section>

    <section class="card comparison-table">
      <h2>Benchmark Results</h2>
      <div style="overflow-x: auto;">
        <table>
          <thead>
            <tr>
              <th>Method</th>
              <th>Runtime</th>
              <th class="numeric">Mean</th>
              <th class="numeric">Allocated</th>
              <th class="numeric">Difference</th>
            </tr>
          </thead>
          <tbody>
"@

# Process benchmarks and create comparison rows
$benchmarks = @{}

# Collect .NET 9 results
foreach ($benchmark in $net9Data.Benchmarks) {
    $method = $benchmark.Method
    $mean = $benchmark.Statistics.Mean
    $allocated = $benchmark.Memory.BytesAllocatedPerOperation

    if (-not $benchmarks.ContainsKey($method)) {
        $benchmarks[$method] = @{}
    }
    $benchmarks[$method]["net9"] = @{
        Mean = $mean
        Allocated = $allocated
        MeanFormatted = if ($mean -lt 1000) { "{0:F1} ns" -f $mean } elseif ($mean -lt 1000000) { "{0:F2} us" -f ($mean / 1000) } else { "{0:F2} ms" -f ($mean / 1000000) }
        AllocatedFormatted = if ($allocated -lt 1024) { "{0} B" -f $allocated } else { "{0:F2} KB" -f ($allocated / 1024) }
    }
}

# Collect .NET 10 results
foreach ($benchmark in $net10Data.Benchmarks) {
    $method = $benchmark.Method
    $mean = $benchmark.Statistics.Mean
    $allocated = $benchmark.Memory.BytesAllocatedPerOperation

    if (-not $benchmarks.ContainsKey($method)) {
        $benchmarks[$method] = @{}
    }
    $benchmarks[$method]["net10"] = @{
        Mean = $mean
        Allocated = $allocated
        MeanFormatted = if ($mean -lt 1000) { "{0:F1} ns" -f $mean } elseif ($mean -lt 1000000) { "{0:F2} us" -f ($mean / 1000) } else { "{0:F2} ms" -f ($mean / 1000000) }
        AllocatedFormatted = if ($allocated -lt 1024) { "{0} B" -f $allocated } else { "{0:F2} KB" -f ($allocated / 1024) }
    }
}

# Generate table rows
$totalNet9Faster = 0
$totalNet10Faster = 0

foreach ($method in $benchmarks.Keys | Sort-Object) {
    $data = $benchmarks[$method]

    if ($data.ContainsKey("net9")) {
        $net9 = $data["net9"]
        $diffHtml = ""

        if ($data.ContainsKey("net10")) {
            $net10 = $data["net10"]
            $ratio = $net9.Mean / $net10.Mean

            if ($ratio -gt 1.05) {
                $diffHtml = "<span class='diff-badge faster'>.NET 10 is {0:F1}x faster</span>" -f $ratio
                $totalNet10Faster++
            } elseif ($ratio -lt 0.95) {
                $diffHtml = "<span class='diff-badge slower'>.NET 9 is {0:F1}x faster</span>" -f (1/$ratio)
                $totalNet9Faster++
            } else {
                $diffHtml = "<span class='same'>~same</span>"
            }
        }

        $html += @"
            <tr class="net9-row">
              <td>$method</td>
              <td><span class="runtime-badge net9" style="padding: 0.2rem 0.5rem; font-size: 0.8rem;">.NET 9</span></td>
              <td class="numeric">$($net9.MeanFormatted)</td>
              <td class="numeric">$($net9.AllocatedFormatted)</td>
              <td class="numeric">$diffHtml</td>
            </tr>
"@
    }

    if ($data.ContainsKey("net10")) {
        $net10 = $data["net10"]
        $html += @"
            <tr class="net10-row">
              <td>$method</td>
              <td><span class="runtime-badge net10" style="padding: 0.2rem 0.5rem; font-size: 0.8rem;">.NET 10</span></td>
              <td class="numeric">$($net10.MeanFormatted)</td>
              <td class="numeric">$($net10.AllocatedFormatted)</td>
              <td class="numeric"></td>
            </tr>
"@
    }
}

$html += @"
          </tbody>
        </table>
      </div>
    </section>

    <section class="card">
      <h2>Summary</h2>
      <div class="summary-grid">
        <div class="summary-item">
          <div class="summary-value">$totalNet10Faster</div>
          <div class="summary-label">benchmarks faster on .NET 10</div>
        </div>
        <div class="summary-item">
          <div class="summary-value">$totalNet9Faster</div>
          <div class="summary-label">benchmarks faster on .NET 9</div>
        </div>
        <div class="summary-item">
          <div class="summary-value">$($benchmarks.Count - $totalNet10Faster - $totalNet9Faster)</div>
          <div class="summary-label">benchmarks with similar performance</div>
        </div>
      </div>
    </section>
  </main>
  <footer>
    <p>Generated at $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
    <p>ErikLieben.FA.ES Event Sourcing Library</p>
  </footer>
</body>
</html>
"@

$html | Out-File -FilePath $comparisonHtmlPath -Encoding utf8

Write-Host "  Saved to: $comparisonHtmlPath" -ForegroundColor Green
Write-Host ""
Write-Host "Opening comparison report..." -ForegroundColor Cyan
Start-Process $comparisonHtmlPath

Write-Host ""
Write-Host "Done!" -ForegroundColor Green

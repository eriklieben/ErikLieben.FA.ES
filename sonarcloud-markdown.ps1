param(
    [Parameter(Mandatory=$true)]
    [string]$Token,

    [Parameter(Mandatory=$true)]
    [string]$ProjectKey,

    [Parameter(Mandatory=$false)]
    [string]$Organization,

    [Parameter(Mandatory=$false)]
    [string]$Branch = "main",

    [Parameter(Mandatory=$false)]
    [string]$PullRequest,

    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "sonarcloud-report.md",

    [Parameter(Mandatory=$false)]
    [switch]$IncludeResolved
)

# Determine whether to use branch or pullRequest parameter
$branchOrPr = if ($PullRequest) { @{ pullRequest = $PullRequest } } else { @{ branch = $Branch } }

# Base URL for SonarCloud API
$baseUrl = "https://sonarcloud.io/api"

# Create authentication header
$headers = @{
    Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${Token}:"))
}

function Get-SonarCloudData {
    param(
        [string]$Endpoint,
        [hashtable]$Params
    )
    
    $uri = "${baseUrl}${Endpoint}"
    $queryString = ($Params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
    
    if ($queryString) {
        $uri = "${uri}?${queryString}"
    }
    
    try {
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        return $response
    }
    catch {
        Write-Error "Failed to fetch data from ${Endpoint}: $_"
        return $null
    }
}

function Get-AllSonarCloudIssues {
    param(
        [string]$ProjectKey,
        [string]$Branch,
        [bool]$IncludeResolved
    )

    $allIssues = @()
    $pageIndex = 1
    $pageSize = 500
    $totalFetched = 0

    do {
        Write-Host "Fetching issues page $pageIndex..." -ForegroundColor Yellow

        $issuesParams = @{
            componentKeys = $ProjectKey
            resolved = if ($IncludeResolved) { "true" } else { "false" }
            ps = $pageSize
            p = $pageIndex
        } + $branchOrPr
        
        $response = Get-SonarCloudData -Endpoint "/issues/search" -Params $issuesParams
        
        if ($response -and $response.issues) {
            $allIssues += $response.issues
            $totalFetched += $response.issues.Count
            Write-Host "  Fetched $($response.issues.Count) issues (Total: $totalFetched of $($response.total))" -ForegroundColor Gray
        }
        
        $pageIndex++
        
    } while ($response -and $response.issues.Count -eq $pageSize -and $totalFetched -lt $response.total)
    
    return @{
        issues = $allIssues
        total = $response.total
    }
}

function Get-AllSonarCloudHotspots {
    param(
        [string]$ProjectKey,
        [string]$Branch
    )

    $allHotspots = @()
    $pageIndex = 1
    $pageSize = 500
    $totalFetched = 0

    do {
        Write-Host "Fetching hotspots page $pageIndex..." -ForegroundColor Yellow

        $hotspotsParams = @{
            projectKey = $ProjectKey
            ps = $pageSize
            p = $pageIndex
        } + $branchOrPr
        
        $response = Get-SonarCloudData -Endpoint "/hotspots/search" -Params $hotspotsParams
        
        if ($response -and $response.hotspots) {
            $allHotspots += $response.hotspots
            $totalFetched += $response.hotspots.Count
            Write-Host "  Fetched $($response.hotspots.Count) hotspots (Total: $totalFetched of $($response.paging.total))" -ForegroundColor Gray
        }
        
        $pageIndex++
        
    } while ($response -and $response.hotspots.Count -eq $pageSize -and $totalFetched -lt $response.paging.total)
    
    return @{
        hotspots = $allHotspots
        total = if ($response) { $response.paging.total } else { 0 }
    }
}

$targetLabel = if ($PullRequest) { "PR #$PullRequest" } else { "Branch: $Branch" }
Write-Host "Fetching SonarCloud data for project: $ProjectKey ($targetLabel)" -ForegroundColor Cyan

# Get project measures
$measuresParams = @{
    component = $ProjectKey
    metricKeys = "alert_status,bugs,vulnerabilities,code_smells,security_hotspots,coverage,duplicated_lines_density,ncloc,sqale_index,reliability_rating,security_rating,sqale_rating"
} + $branchOrPr

$measures = Get-SonarCloudData -Endpoint "/measures/component" -Params $measuresParams

# Get all issues (paginated)
$issuesResult = Get-AllSonarCloudIssues -ProjectKey $ProjectKey -Branch $Branch -IncludeResolved $IncludeResolved.IsPresent


# Start building markdown
$markdown = @"
# SonarCloud Analysis Report

**Project:** ``$ProjectKey``
**Branch/PR:** ``$targetLabel``
**Generated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Organization:** $Organization

---

## Overview

"@

# Add measures
if ($measures -and $measures.component.measures) {
    $measureMap = @{}
    foreach ($measure in $measures.component.measures) {
        $measureMap[$measure.metric] = $measure.value
    }
    
    $qualityGate = if ($measureMap['alert_status'] -eq 'OK') { '✅ Passed' } else { '❌ Failed' }
    
    $markdown += @"

| Metric | Value |
|--------|-------|
| **Quality Gate** | $qualityGate |
| **Lines of Code** | $($measureMap['ncloc'] ?? 'N/A') |
| **Coverage** | $($measureMap['coverage'] ?? 'N/A')% |
| **Duplications** | $($measureMap['duplicated_lines_density'] ?? 'N/A')% |
| **Technical Debt** | $($measureMap['sqale_index'] ?? 'N/A') min |

---

## Issues Summary

| Type | Count |
|------|-------|
| 🐛 **Bugs** | $($measureMap['bugs'] ?? '0') |
| 🔒 **Vulnerabilities** | $($measureMap['vulnerabilities'] ?? '0') |
| 🔥 **Security Hotspots** | $($measureMap['security_hotspots'] ?? '0') |
| 💡 **Code Smells** | $($measureMap['code_smells'] ?? '0') |

"@
}

# Add detailed issues
if ($issuesResult -and $issuesResult.issues) {
    $markdown += @"

---

## Detailed Issues

**Total Issues:** $($issuesResult.total)

"@

    # Group issues by severity
    $groupedIssues = $issuesResult.issues | Group-Object -Property severity
    
    foreach ($severityGroup in ($groupedIssues | Sort-Object { 
        switch ($_.Name) {
            'BLOCKER' { 0 }
            'CRITICAL' { 1 }
            'MAJOR' { 2 }
            'MINOR' { 3 }
            'INFO' { 4 }
        }
    })) {
        $severity = $severityGroup.Name
        $count = $severityGroup.Count
        
        $icon = switch ($severity) {
            'BLOCKER' { '🔴' }
            'CRITICAL' { '🟠' }
            'MAJOR' { '🟡' }
            'MINOR' { '🔵' }
            'INFO' { '⚪' }
            default { '⚫' }
        }
        
        $markdown += @"

### $icon $severity ($count)

"@
        
        # Show ALL issues for this severity
        foreach ($issue in $severityGroup.Group) {
            $type = $issue.type
            $component = $issue.component -replace "^${ProjectKey}:", ""
            $line = if ($issue.line) { ":$($issue.line)" } else { "" }
            $message = $issue.message
            $rule = $issue.rule
            $effort = if ($issue.effort) { " | Effort: $($issue.effort)" } else { "" }
            
            $markdown += @"
- **[$type]** ``$component$line``  
  *$message*  
  Rule: ``$rule``$effort

"@
        }
    }
}
else {
    $markdown += @"

---

## Issues

No issues found or unable to retrieve issues.

"@
}

# Add all hotspots
$hotspotsResult = Get-AllSonarCloudHotspots -ProjectKey $ProjectKey -Branch $Branch

if ($hotspotsResult -and $hotspotsResult.hotspots -and $hotspotsResult.hotspots.Count -gt 0) {
    $markdown += @"

---

## Security Hotspots

**Total Hotspots:** $($hotspotsResult.total)

"@
    
    # Show ALL hotspots
    foreach ($hotspot in $hotspotsResult.hotspots) {
        $component = $hotspot.component -replace "^${ProjectKey}:", ""
        $line = if ($hotspot.line) { ":$($hotspot.line)" } else { "" }
        $message = $hotspot.message
        $status = $hotspot.status
        $vulnerabilityProbability = $hotspot.vulnerabilityProbability
        
        $markdown += @"
- **[$status | $vulnerabilityProbability]** ``$component$line``  
  *$message*  
  Rule: ``$($hotspot.rule)``

"@
    }
}

# Footer
$markdown += @"

---

*Report generated from SonarCloud API*  
*View full results at: https://sonarcloud.io/dashboard?id=$ProjectKey&$(if ($PullRequest) { "pullRequest=$PullRequest" } else { "branch=$Branch" })*

"@

# Write to file
$markdown | Out-File -FilePath $OutputFile -Encoding UTF8

Write-Host "`n✅ Report generated successfully: $OutputFile" -ForegroundColor Green
Write-Host "📊 Total Issues: $($issuesResult.total)" -ForegroundColor Cyan
Write-Host "🔥 Total Hotspots: $($hotspotsResult.total)" -ForegroundColor Cyan
Write-Host "`nYou can now share this markdown file with your coding agent!" -ForegroundColor Cyan
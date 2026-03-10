#!/usr/bin/env pwsh
# Collects GitHub PR authors for all Starlight PRs in the license range.
# Requires: gh CLI authenticated (gh auth login)
# Usage: .\Tools\get-pr-authors.ps1

$repo = "ss14Starlight/space-station-14"
$startCommit = "84205e3857"
$endCommit = "HEAD"
$outputFile = "Tools/pr-authors.csv"

Write-Host "Collecting PR numbers from git log ($startCommit..$endCommit)..."

$prNumbers = git log --first-parent "$startCommit..$endCommit" --oneline |
    Select-String "Merge pull request #(\d+)|PR #(\d+)|\(#(\d+)\)" |
    ForEach-Object {
        $m = $_.Matches[0]
        $g = $m.Groups | Where-Object { $_.Success -and $_.Name -ne '0' } | Select-Object -First 1
        [int]$g.Value
    } |
    Where-Object { $_ -lt 10000 } |  # Exclude upstream PRs (high numbers)
    Sort-Object |
    Get-Unique

Write-Host "Found $($prNumbers.Count) Starlight PRs. Fetching authors from GitHub..."
Write-Host ""

$authors = @{}
$results = @()
$i = 0

foreach ($pr in $prNumbers) {
    $i++
    if ($i % 50 -eq 0) {
        Write-Host "  Progress: $i / $($prNumbers.Count)..."
    }

    try {
        $json = gh api "repos/$repo/pulls/$pr" --jq '{login: .user.login, id: .user.id, pr: .number, title: .title}' 2>$null
        if ($json) {
            $data = $json | ConvertFrom-Json
            $login = $data.login
            $userId = $data.id
            $title = $data.title

            if (-not $authors.ContainsKey($login)) {
                $authors[$login] = @{
                    id = $userId
                    prs = [System.Collections.Generic.List[int]]::new()
                }
            }
            $authors[$login].prs.Add($pr)

            $results += [PSCustomObject]@{
                PR     = $pr
                Author = $login
                UserID = $userId
                Title  = $title
            }
        }
    }
    catch {
        Write-Warning "Failed to fetch PR #$pr : $_"
    }
}

# Export full PR list to CSV
$results | Export-Csv -Path $outputFile -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "Full PR list saved to $outputFile"
Write-Host ""

# Summary: unique authors sorted by PR count
Write-Host "=== Unique Authors (by PR count) ==="
Write-Host ""
$authors.GetEnumerator() |
    Sort-Object { $_.Value.prs.Count } -Descending |
    ForEach-Object {
        $login = $_.Key
        $count = $_.Value.prs.Count
        $id = $_.Value.id
        Write-Host ("  {0,-25} (ID: {1,-12}) PRs: {2}" -f $login, $id, $count)
    }

Write-Host ""
Write-Host "Total unique authors: $($authors.Count)"

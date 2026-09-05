[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$AsObject
)

$ErrorActionPreference = 'Stop'
$resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)

if (-not (Test-Path -LiteralPath $resolvedProjectRoot -PathType Container)) {
    throw "Project directory does not exist: $resolvedProjectRoot"
}

function Get-GitBranchNameUtf8 {
    param([string]$RepositoryRoot)

    if ($RepositoryRoot.Contains('"')) {
        throw "Repository path cannot contain a double quote: $RepositoryRoot"
    }

    $gitPath = (Get-Command git -ErrorAction Stop).Source
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $gitPath
    $startInfo.Arguments = "-C `"$RepositoryRoot`" branch --show-current"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Unable to start Git: $gitPath"
    }

    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Unable to read the Git branch: $RepositoryRoot. $($standardError.Trim())"
    }

    return $standardOutput.Trim()
}

$branchName = Get-GitBranchNameUtf8 -RepositoryRoot $resolvedProjectRoot
$branchResolution = if ([string]::IsNullOrWhiteSpace($branchName)) { 'detached' } else { 'git' }

# Codex desktop worktrees intentionally use detached HEAD and keep their logical
# branch binding beside the worktree gitdir. Treat that binding as the branch
# identity so each Codex worktree still resolves its own Harness task list.
if ([string]::IsNullOrWhiteSpace($branchName)) {
    $gitMarker = Join-Path $resolvedProjectRoot '.git'
    if (Test-Path -LiteralPath $gitMarker -PathType Leaf) {
        $gitDirLine = (Get-Content -Raw -LiteralPath $gitMarker).Trim()
        if ($gitDirLine -match '^gitdir:\s*(.+)$') {
            $gitDirectoryPath = $Matches[1].Trim()
            if (-not [System.IO.Path]::IsPathRooted($gitDirectoryPath)) {
                $gitDirectoryPath = Join-Path $resolvedProjectRoot $gitDirectoryPath
            }
            $gitDirectory = [System.IO.Path]::GetFullPath($gitDirectoryPath)
            $codexBranchFile = Join-Path $gitDirectory 'codex-synced-branch.json'
            if (Test-Path -LiteralPath $codexBranchFile -PathType Leaf) {
                $codexBranch = Get-Content -Raw -Encoding UTF8 -LiteralPath $codexBranchFile | ConvertFrom-Json
                if ($codexBranch.branch -match '^refs/heads/(.+)$') {
                    $branchName = $Matches[1]
                    $branchResolution = 'codex-synced'
                }
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($branchName)) {
    throw "Unable to resolve a task branch for detached HEAD: $resolvedProjectRoot. No Codex logical branch mapping was found, and the Harness no longer falls back to a global feature_list.json."
}

$branchDirectory = Join-Path $PSScriptRoot '.harness\worktree'
foreach ($segment in ($branchName -split '/')) {
    if ([string]::IsNullOrWhiteSpace($segment) -or $segment -in @('.', '..')) {
        throw "The branch name cannot be mapped safely to a task directory: $branchName"
    }
    $branchDirectory = Join-Path $branchDirectory ($segment -replace '[<>:"|?*]', '_')
}

$branchFeatureList = Join-Path $branchDirectory 'feature_list.json'
$created = $false
if (-not (Test-Path -LiteralPath $branchFeatureList -PathType Leaf)) {
    New-Item -ItemType Directory -Force -Path $branchDirectory | Out-Null
    & (Join-Path $PSScriptRoot 'reset-feature-list.ps1') `
        -Path $branchFeatureList `
        -BranchName $branchName `
        -NoBackup `
        -Confirm:$false
    $created = $true
}

if (-not (Test-Path -LiteralPath $branchFeatureList -PathType Leaf)) {
    throw "Failed to create the branch task list: $branchFeatureList"
}

$result = [pscustomobject]@{
    Branch = $branchName
    BranchResolution = $branchResolution
    Source = 'branch'
    Created = $created
    Path = [System.IO.Path]::GetFullPath($branchFeatureList)
}

if ($AsObject) { $result } else { $result.Path }

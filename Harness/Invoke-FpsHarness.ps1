[CmdletBinding()]
param(
    [ValidateSet('status', 'validate-feature', 'compare-snapshots')]
    [string]$Mode = 'status',
    [string]$FeatureId,
    [string]$ClientALog,
    [string]$ClientBLog,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$harnessRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $harnessRoot

switch ($Mode) {
    'status' {
        $selection = & (Join-Path $harnessRoot 'resolve-feature-list.ps1') -ProjectRoot $projectRoot -AsObject
        $status = git -C $projectRoot status --short --branch
        [pscustomobject]@{
            ProjectRoot = $projectRoot
            Branch = $selection.Branch
            FeatureList = $selection.Path
            GitStatus = $status
        } | Format-List
    }
    'validate-feature' {
        if ([string]::IsNullOrWhiteSpace($FeatureId)) {
            throw 'validate-feature requires -FeatureId.'
        }
        & (Join-Path $harnessRoot 'validate-feature-evidence.ps1') -ProjectRoot $projectRoot -FeatureId $FeatureId
    }
    'compare-snapshots' {
        foreach ($parameterName in @('ClientALog', 'ClientBLog', 'OutputPath')) {
            if ([string]::IsNullOrWhiteSpace((Get-Variable -Name $parameterName -ValueOnly))) {
                throw "compare-snapshots requires -$parameterName."
            }
        }
        & (Join-Path $harnessRoot 'compare-authority-snapshots.ps1') `
            -ClientALog $ClientALog -ClientBLog $ClientBLog -OutputPath $OutputPath
    }
}

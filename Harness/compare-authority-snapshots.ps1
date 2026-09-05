[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ClientALog,
    [Parameter(Mandatory = $true)] [string] $ClientBLog,
    [Parameter(Mandatory = $true)] [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$tracePattern = 'AuthoritySnapshotReceived MatchId=(?<MatchId>\d+) PawnId=(?<PawnId>\d+) ServerTick=(?<ServerTick>\d+) Role=\S+ Position=(?<Position>\S+) Rotation=(?<Rotation>\S+) BaseVelocity=(?<BaseVelocity>\S+) MovementState=(?<MovementState>\d+) Grounded=(?<Grounded>True|False) GroundNormal=(?<GroundNormal>\S+) AttachedBaseId=(?<AttachedBaseId>-?\d+)'
$fields = @('Position', 'Rotation', 'BaseVelocity', 'MovementState', 'Grounded', 'GroundNormal', 'AttachedBaseId')

function Read-SnapshotTrace([string] $Path) {
    $entries = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $match = [regex]::Match($line, $tracePattern)
        if (-not $match.Success) { continue }
        $key = '{0}:{1}:{2}' -f $match.Groups['MatchId'].Value, $match.Groups['PawnId'].Value, $match.Groups['ServerTick'].Value
        $value = [ordered]@{}
        foreach ($field in $fields) { $value[$field] = $match.Groups[$field].Value }
        $entries[$key] = $value
    }
    return $entries
}

$clientA = Read-SnapshotTrace $ClientALog
$clientB = Read-SnapshotTrace $ClientBLog
$commonKeys = @($clientA.Keys | Where-Object { $clientB.ContainsKey($_) } | Sort-Object)
$mismatches = @()
foreach ($key in $commonKeys) {
    foreach ($field in $fields) {
        if ($clientA[$key][$field] -ceq $clientB[$key][$field]) { continue }
        $mismatches += [ordered]@{ key = $key; field = $field; client_a = $clientA[$key][$field]; client_b = $clientB[$key][$field] }
        Write-Host "StateDigestMismatch Key=$key Field=$field ClientA=$($clientA[$key][$field]) ClientB=$($clientB[$key][$field])"
        break
    }
}

$result = [ordered]@{
    status = if ($commonKeys.Count -gt 0 -and $mismatches.Count -eq 0) { 'passing' } else { 'failing' }
    join_key = 'MatchId+PawnId+ServerTick'
    compared_fields = $fields
    client_a_snapshot_count = $clientA.Count
    client_b_snapshot_count = $clientB.Count
    joined_snapshot_count = $commonKeys.Count
    mismatch_count = $mismatches.Count
    mismatches = $mismatches
}

$parent = Split-Path -Parent $OutputPath
if ($parent -and -not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
if ($result.status -ne 'passing') { throw "Authority snapshot comparison failed. Joined=$($commonKeys.Count) Mismatches=$($mismatches.Count)" }
Write-Host "Authority snapshot comparison passed: joined=$($commonKeys.Count), exact fields=$($fields.Count)."

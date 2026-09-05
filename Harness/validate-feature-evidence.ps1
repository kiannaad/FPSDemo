[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FeatureListPath,
    [string]$FeatureId,
    [switch]$AllPassing,
    [switch]$RequireBranchClosure
)

$ErrorActionPreference = 'Stop'

$harnessRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$runsRoot = [System.IO.Path]::GetFullPath((Join-Path $harnessRoot '.harness\runs'))
$allCapabilities = @(
    'production_integration',
    'playmode_mainline',
    'visual_result',
    'simulated_input'
)
$profileRequirements = @{
    production_logic = @('production_integration')
    runtime_mainline = @('production_integration', 'playmode_mainline')
    interactive_mainline = $allCapabilities
}
$errors = New-Object System.Collections.Generic.List[string]

function Get-PropertyValue {
    param(
        $InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Test-PathWithinRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    if ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $fullPath.StartsWith(
        $fullRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-EvidenceFile {
    param([Parameter(Mandatory = $true)][string]$EvidencePath)

    $candidate = if ([System.IO.Path]::IsPathRooted($EvidencePath)) {
        $EvidencePath
    }
    else {
        Join-Path $harnessRoot $EvidencePath
    }
    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    Assert-Condition (Test-PathWithinRoot -Path $fullPath -Root $runsRoot) `
        "Evidence must stay under .harness/runs: $EvidencePath"
    Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) `
        "Evidence file does not exist: $EvidencePath"
    Assert-Condition ([System.IO.Path]::GetFileName($fullPath) -eq 'result.json') `
        "Feature evidence must reference result.json: $EvidencePath"
    return $fullPath
}

function Resolve-ProjectFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $normalized = $Path.Replace('/', '\')
    $candidate = if ([System.IO.Path]::IsPathRooted($normalized)) {
        $normalized
    }
    elseif ($normalized.StartsWith('FPSResearch\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Join-Path $harnessRoot $normalized
    }
    else {
        Join-Path $ProjectRoot $normalized
    }
    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    Assert-Condition (Test-PathWithinRoot -Path $fullPath -Root $ProjectRoot) `
        "$Description must stay inside FPSResearch: $Path"
    Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) `
        "$Description does not exist: $Path"
    return $fullPath
}

function Resolve-ProductionFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $normalized = $Path.Replace('/', '\')
    if ($normalized -in @('init.ps1', 'init.sh')) {
        $candidate = Join-Path $harnessRoot $normalized
        $allowedRoot = $harnessRoot
    }
    else {
        return Resolve-ProjectFile -Path $Path -Description $Description
    }

    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    Assert-Condition (Test-PathWithinRoot -Path $fullPath -Root $allowedRoot) `
        "$Description must stay inside its approved production root: $Path"
    Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) `
        "$Description does not exist: $Path"
    return $fullPath
}

function Test-TestRoleName {
    param([Parameter(Mandatory = $true)][string]$Value)

    return $Value -cmatch '(Test|Fake|Mock)(Bootstrap|Controller|Pawn|GameMode|Flow|Installer|Driver|Entry|Runtime)' `
        -or $Value -cmatch '(^|[.\\/])(?:Test|Fake|Mock)[A-Z_]'
}

function Assert-StringList {
    param(
        $Value,
        [Parameter(Mandatory = $true)][string]$Description,
        [int]$MinimumCount = 1
    )

    $items = @($Value)
    Assert-Condition ($items.Count -ge $MinimumCount) `
        "$Description requires at least $MinimumCount item(s)."
    foreach ($item in $items) {
        Assert-Condition ($item -is [string] -and -not [string]::IsNullOrWhiteSpace($item)) `
            "$Description contains an empty or non-string item."
    }
    return $items
}

function Test-ProductionIntegration {
    param(
        [Parameter(Mandatory = $true)]$Block,
        [Parameter(Mandatory = $true)][string]$Context
    )

    Assert-Condition ((Get-PropertyValue $Block 'status') -eq 'passed') `
        "$Context production_integration.status must be passed."
    $productionFiles = Assert-StringList `
        -Value (Get-PropertyValue $Block 'production_files') `
        -Description "$Context production_integration.production_files"
    foreach ($productionFile in $productionFiles) {
        $resolved = Resolve-ProductionFile -Path $productionFile -Description 'Production file'
        $normalized = $resolved.Replace('\', '/')
        Assert-Condition ($normalized -notmatch '(?i)/Assets/Tests?(/|$)') `
            "$Context production file points into a test directory: $productionFile"
        Assert-Condition (-not (Test-TestRoleName -Value ([System.IO.Path]::GetFileName($resolved)))) `
            "$Context production file uses a Test/Fake/Mock flow role name: $productionFile"
    }

    $entryPoints = Assert-StringList `
        -Value (Get-PropertyValue $Block 'runtime_entry_points') `
        -Description "$Context production_integration.runtime_entry_points"
    foreach ($entryPoint in $entryPoints) {
        Assert-Condition (-not (Test-TestRoleName -Value $entryPoint)) `
            "$Context runtime entry point is a Test/Fake/Mock flow role: $entryPoint"
    }

    $checks = @((Get-PropertyValue $Block 'integration_checks'))
    Assert-Condition ($checks.Count -gt 0) `
        "$Context production_integration.integration_checks must record at least one real integration check."
    foreach ($check in $checks) {
        $command = [string](Get-PropertyValue $check 'command')
        $result = [string](Get-PropertyValue $check 'result')
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($command)) `
            "$Context integration check is missing its command or operation."
        Assert-Condition ($result -eq 'passed') `
            "$Context integration check did not pass: $command"
    }
}

function Test-PlayModeMainline {
    param(
        [Parameter(Mandatory = $true)]$Block,
        [Parameter(Mandatory = $true)][string]$Context
    )

    Assert-Condition ((Get-PropertyValue $Block 'status') -eq 'passed') `
        "$Context playmode_mainline.status must be passed."
    $scene = [string](Get-PropertyValue $Block 'scene')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($scene)) `
        "$Context playmode_mainline.scene is required."
    $null = Resolve-ProjectFile -Path $scene -Description 'PlayMode scene'
    $command = [string](Get-PropertyValue $Block 'command')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($command)) `
        "$Context playmode_mainline.command is required."
    $entryPoint = [string](Get-PropertyValue $Block 'entry_point')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($entryPoint)) `
        "$Context playmode_mainline.entry_point is required."
    Assert-Condition (-not (Test-TestRoleName -Value $entryPoint)) `
        "$Context PlayMode entry point is a Test/Fake/Mock flow role: $entryPoint"
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'trace') `
        -Description "$Context playmode_mainline.trace" `
        -MinimumCount 3
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'observations') `
        -Description "$Context playmode_mainline.observations" `
        -MinimumCount 2
    $consoleErrors = Get-PropertyValue $Block 'console_errors'
    Assert-Condition ($null -ne $consoleErrors -and [int]$consoleErrors -eq 0) `
        "$Context playmode_mainline.console_errors must be 0."
}

function Resolve-ArtifactFile {
    param(
        [Parameter(Mandatory = $true)][string]$ArtifactPath,
        [Parameter(Mandatory = $true)][string]$ResultDirectory
    )

    $normalized = $ArtifactPath.Replace('/', '\')
    $candidate = if ([System.IO.Path]::IsPathRooted($normalized)) {
        $normalized
    }
    elseif ($normalized.StartsWith('.harness\', [System.StringComparison]::OrdinalIgnoreCase)) {
        Join-Path $harnessRoot $normalized
    }
    else {
        Join-Path $ResultDirectory $normalized
    }
    $fullPath = [System.IO.Path]::GetFullPath($candidate)
    Assert-Condition (Test-PathWithinRoot -Path $fullPath -Root $runsRoot) `
        "Visual artifact must stay under .harness/runs: $ArtifactPath"
    Assert-Condition (Test-Path -LiteralPath $fullPath -PathType Leaf) `
        "Visual artifact does not exist: $ArtifactPath"
    $extension = [System.IO.Path]::GetExtension($fullPath).ToLowerInvariant()
    Assert-Condition ($extension -in @('.png', '.jpg', '.jpeg', '.gif', '.mp4', '.webm')) `
        "Visual artifact must be an image or video: $ArtifactPath"
    return $fullPath
}

function Test-VisualResult {
    param(
        [Parameter(Mandatory = $true)]$Block,
        [Parameter(Mandatory = $true)][string]$Context,
        [Parameter(Mandatory = $true)][string]$ResultDirectory
    )

    Assert-Condition ((Get-PropertyValue $Block 'status') -eq 'passed') `
        "$Context visual_result.status must be passed."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $Block 'scene'))) `
        "$Context visual_result.scene is required."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $Block 'resolution'))) `
        "$Context visual_result.resolution is required."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $Block 'capture_method'))) `
        "$Context visual_result.capture_method is required."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-PropertyValue $Block 'comparison'))) `
        "$Context visual_result.comparison is required."
    $reviewStatus = [string](Get-PropertyValue $Block 'review_status')
    Assert-Condition ($reviewStatus -in @('confirmed', 'objective_passed')) `
        "$Context visual_result.review_status must be confirmed or objective_passed."
    $artifacts = Assert-StringList `
        -Value (Get-PropertyValue $Block 'artifacts') `
        -Description "$Context visual_result.artifacts"
    foreach ($artifact in $artifacts) {
        $null = Resolve-ArtifactFile -ArtifactPath $artifact -ResultDirectory $ResultDirectory
    }
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'observations') `
        -Description "$Context visual_result.observations"
}

function Test-SimulatedInput {
    param(
        [Parameter(Mandatory = $true)]$Block,
        [Parameter(Mandatory = $true)][string]$Context
    )

    Assert-Condition ((Get-PropertyValue $Block 'status') -eq 'passed') `
        "$Context simulated_input.status must be passed."
    $method = [string](Get-PropertyValue $Block 'method')
    Assert-Condition ($method -match '(?i)QueueStateEvent|InputTestFixture') `
        "$Context simulated_input.method must use Input System simulation such as QueueStateEvent or InputTestFixture."
    $testFile = [string](Get-PropertyValue $Block 'test_file')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($testFile)) `
        "$Context simulated_input.test_file is required."
    $resolvedTestFile = Resolve-ProjectFile -Path $testFile -Description 'Simulated-input test file'
    Assert-Condition ($resolvedTestFile.Replace('\', '/') -match '(?i)/Assets/Tests?(/|$)') `
        "$Context simulated-input test must live under Assets/Tests: $testFile"
    $productionEntryPoint = [string](Get-PropertyValue $Block 'production_entry_point')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($productionEntryPoint)) `
        "$Context simulated_input.production_entry_point is required."
    Assert-Condition (-not (Test-TestRoleName -Value $productionEntryPoint)) `
        "$Context simulated input bypasses production through a Test/Fake/Mock entry: $productionEntryPoint"
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'devices') `
        -Description "$Context simulated_input.devices"
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'actions') `
        -Description "$Context simulated_input.actions"
    $null = Assert-StringList `
        -Value (Get-PropertyValue $Block 'observations') `
        -Description "$Context simulated_input.observations" `
        -MinimumCount 2
}

if ([string]::IsNullOrWhiteSpace($FeatureListPath)) {
    $selection = & (Join-Path $harnessRoot 'resolve-feature-list.ps1') `
        -ProjectRoot $ProjectRoot `
        -AsObject
    $FeatureListPath = $selection.Path
}
$FeatureListPath = [System.IO.Path]::GetFullPath($FeatureListPath)
Assert-Condition (Test-Path -LiteralPath $FeatureListPath -PathType Leaf) `
    "Feature list does not exist: $FeatureListPath"
Assert-Condition (Test-Path -LiteralPath $ProjectRoot -PathType Container) `
    "Project root does not exist: $ProjectRoot"

$featureList = Get-Content -Raw -Encoding UTF8 -LiteralPath $FeatureListPath | ConvertFrom-Json
$features = @($featureList.features)
$featureIds = @($features | ForEach-Object { [string]$_.id })
$gate = Get-PropertyValue $featureList 'acceptance_gate'

if ($features.Count -gt 0) {
    if ($null -eq $gate) {
        $errors.Add('feature_list.json is missing acceptance_gate.')
    }
    else {
        if ([int](Get-PropertyValue $gate 'schema_version') -ne 2) {
            $errors.Add('acceptance_gate.schema_version must be 2.')
        }
        $finalFeatureId = [string](Get-PropertyValue $gate 'final_feature_id')
        if ([string]::IsNullOrWhiteSpace($finalFeatureId) -or $finalFeatureId -notin $featureIds) {
            $errors.Add("acceptance_gate.final_feature_id must reference a real feature: $finalFeatureId")
        }
        $gateCapabilities = @((Get-PropertyValue $gate 'required_capabilities'))
        foreach ($capability in $allCapabilities) {
            if ($capability -notin $gateCapabilities) {
                $errors.Add("acceptance_gate.required_capabilities is missing $capability.")
            }
        }
    }
}

foreach ($feature in $features) {
    $id = [string]$feature.id
    $acceptance = Get-PropertyValue $feature 'acceptance'
    if ($null -eq $acceptance) {
        $errors.Add("Feature $id is missing acceptance.")
        continue
    }
    $profile = [string](Get-PropertyValue $acceptance 'profile')
    if (-not $profileRequirements.ContainsKey($profile)) {
        $errors.Add("Feature $id has unknown acceptance profile: $profile")
        continue
    }
    $requiredCapabilities = @(
        $profileRequirements[$profile]
        (Get-PropertyValue $acceptance 'required_capabilities')
    ) | Where-Object { $_ -in $allCapabilities } | Select-Object -Unique
    $waivers = Get-PropertyValue $acceptance 'waivers'
    foreach ($capability in $allCapabilities) {
        if ($capability -in $requiredCapabilities) {
            continue
        }
        $reason = [string](Get-PropertyValue $waivers $capability)
        if ([string]::IsNullOrWhiteSpace($reason)) {
            $errors.Add("Feature $id must explain why $capability is waived for profile $profile.")
        }
    }
}

if ($null -ne $gate) {
    $finalFeatureId = [string](Get-PropertyValue $gate 'final_feature_id')
    $finalFeature = @($features | Where-Object { $_.id -eq $finalFeatureId }) | Select-Object -First 1
    if ($null -ne $finalFeature) {
        $finalAcceptance = Get-PropertyValue $finalFeature 'acceptance'
        $finalProfile = [string](Get-PropertyValue $finalAcceptance 'profile')
        if ($finalProfile -ne 'interactive_mainline') {
            $errors.Add("Final feature $finalFeatureId must use interactive_mainline, found: $finalProfile")
        }
    }
}

$featuresToValidate = @(if (-not [string]::IsNullOrWhiteSpace($FeatureId)) {
    $features | Where-Object { $_.id -eq $FeatureId }
}
else {
    $features | Where-Object { $_.status -eq 'passing' }
})
if (-not [string]::IsNullOrWhiteSpace($FeatureId) -and $featuresToValidate.Count -ne 1) {
    $errors.Add("FeatureId must match exactly one feature: $FeatureId")
}

foreach ($feature in $featuresToValidate) {
    $id = [string]$feature.id
    $acceptance = Get-PropertyValue $feature 'acceptance'
    $profile = [string](Get-PropertyValue $acceptance 'profile')
    if (-not $profileRequirements.ContainsKey($profile)) {
        continue
    }
    $requiredCapabilities = @(
        $profileRequirements[$profile]
        (Get-PropertyValue $acceptance 'required_capabilities')
    ) | Where-Object { $_ -in $allCapabilities } | Select-Object -Unique
    $capabilityPassed = @{}
    foreach ($capability in $requiredCapabilities) {
        $capabilityPassed[$capability] = $false
    }

    $evidencePaths = @($feature.evidence)
    if ($evidencePaths.Count -eq 0) {
        $errors.Add("Feature $id has no result.json evidence for profile $profile.")
        continue
    }

    foreach ($evidencePath in $evidencePaths) {
        try {
            $resultPath = Resolve-EvidenceFile -EvidencePath ([string]$evidencePath)
            $result = Get-Content -Raw -Encoding UTF8 -LiteralPath $resultPath | ConvertFrom-Json
            $context = "Feature $id evidence $evidencePath"
            Assert-Condition ([int](Get-PropertyValue $result 'schema_version') -eq 2) `
                "$context schema_version must be 2."
            Assert-Condition (([string](Get-PropertyValue $result 'task_id')) -eq $id) `
                "$context task_id does not match the feature."
            Assert-Condition (([string](Get-PropertyValue $result 'status')) -eq 'passing') `
                "$context status must be passing."
            $resultAcceptance = Get-PropertyValue $result 'acceptance'
            Assert-Condition ($null -ne $resultAcceptance) `
                "$context is missing acceptance evidence."

            foreach ($capability in $requiredCapabilities) {
                $block = Get-PropertyValue $resultAcceptance $capability
                if ($null -eq $block) {
                    continue
                }
                switch ($capability) {
                    'production_integration' {
                        Test-ProductionIntegration -Block $block -Context $context
                    }
                    'playmode_mainline' {
                        Test-PlayModeMainline -Block $block -Context $context
                    }
                    'visual_result' {
                        Test-VisualResult `
                            -Block $block `
                            -Context $context `
                            -ResultDirectory (Split-Path -Parent $resultPath)
                    }
                    'simulated_input' {
                        Test-SimulatedInput -Block $block -Context $context
                    }
                }
                $capabilityPassed[$capability] = $true
            }
        }
        catch {
            $errors.Add($_.Exception.Message)
        }
    }

    foreach ($capability in $requiredCapabilities) {
        if (-not $capabilityPassed[$capability]) {
            $errors.Add("Feature $id lacks passing $capability evidence required by $profile.")
        }
    }
}

$allFeaturesPassing = $features.Count -gt 0 `
    -and @($features | Where-Object { $_.status -ne 'passing' }).Count -eq 0
if ($RequireBranchClosure -or $allFeaturesPassing) {
    if (-not $allFeaturesPassing) {
        $notPassing = @($features | Where-Object { $_.status -ne 'passing' } | ForEach-Object { "$($_.id)=$($_.status)" })
        $errors.Add("Branch closure requires every feature passing: $($notPassing -join ', ')")
    }
    elseif ($null -ne $gate) {
        $finalFeatureId = [string](Get-PropertyValue $gate 'final_feature_id')
        $finalFeature = @($features | Where-Object { $_.id -eq $finalFeatureId }) | Select-Object -First 1
        if ($null -eq $finalFeature -or $finalFeature.status -ne 'passing') {
            $errors.Add("Branch closure final feature is not passing: $finalFeatureId")
        }
    }
}

if ($errors.Count -gt 0) {
    $message = "Harness acceptance validation failed:`n - " + ($errors -join "`n - ")
    throw $message
}

$validatedIds = @($featuresToValidate | ForEach-Object { $_.id })
Write-Host "Harness acceptance contract verified: $($features.Count) feature(s), $($validatedIds.Count) evidence-gated feature(s)."
if ($validatedIds.Count -gt 0) {
    Write-Host "Validated: $($validatedIds -join ', ')"
}
if ($allFeaturesPassing) {
    Write-Host 'Branch closure verified: all features and the interactive mainline final feature are passing.'
}

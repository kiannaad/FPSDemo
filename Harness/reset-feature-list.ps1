[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$Path,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BranchName,
    [switch]$NoBackup,
    [switch]$EscapeBackslashes
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Path)) {
    $selection = & (Join-Path $PSScriptRoot 'resolve-feature-list.ps1') -ProjectRoot $ProjectRoot -AsObject
    $Path = $selection.Path
    if ([string]::IsNullOrWhiteSpace($BranchName)) {
        $BranchName = $selection.Branch
    }
}

$targetPath = [System.IO.Path]::GetFullPath($Path)
$targetDirectory = Split-Path -Parent $targetPath
if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
    throw "目标目录不存在：$targetDirectory"
}

if ($EscapeBackslashes) {
    if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        throw "目标文件不存在：$targetPath"
    }

    $originalContent = Get-Content -Raw -Encoding UTF8 -LiteralPath $targetPath
    # 保留 JSON 标准转义；只补全会导致 JSON 解析失败的裸反斜杠。
    $escapedContent = [regex]::Replace(
        $originalContent,
        '(?<!\\)\\(?!["\\/bfnrt]|u[0-9A-Fa-f]{4})',
        '\\')

    try {
        $null = $escapedContent | ConvertFrom-Json
    }
    catch {
        throw "补全单反斜杠后 JSON 仍无法解析，未写入文件：$($_.Exception.Message)"
    }

    if ($escapedContent -eq $originalContent) {
        Write-Host "无需刷新：$targetPath 中没有未转义的单反斜杠，JSON 校验通过。"
        return
    }

    if (-not $PSCmdlet.ShouldProcess($targetPath, '备份并把未转义的单反斜杠替换为双反斜杠')) {
        return
    }

    if (-not $NoBackup) {
        $backupDirectory = Join-Path $PSScriptRoot '.harness\feature-list-backups'
        New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $backupPath = Join-Path $backupDirectory "feature-list-$timestamp.json"
        Copy-Item -LiteralPath $targetPath -Destination $backupPath
        Write-Host "已备份：$backupPath"
    }

    [System.IO.File]::WriteAllText(
        $targetPath,
        $escapedContent,
        [System.Text.UTF8Encoding]::new($false))

    Write-Host "已刷新：$targetPath"
    Write-Host '未转义的单反斜杠已替换为双反斜杠，JSON 校验通过。'
    return
}

$template = [ordered]@{
    project = 'FPSResearch'
    last_updated = Get-Date -Format 'yyyy-MM-dd'
    source_of_truth = $true
    rules = [ordered]@{
        single_active_feature = $true
        respect_dependencies = $true
        passing_requires_evidence = $true
        passing_requires_acceptance_gate = $true
        do_not_skip_verification = $true
        run_evidence_directory = '.harness/runs'
    }
    acceptance_gate = [ordered]@{
        schema_version = 2
        final_feature_id = ''
        required_capabilities = @(
            'production_integration',
            'playmode_mainline',
            'visual_result',
            'simulated_input'
        )
    }
    status_legend = [ordered]@{
        not_started = '功能还没开始做。'
        in_progress = '这个功能是当前唯一正在进行的任务。'
        blocked = '因为已记录的阻塞问题，当前无法继续推进。'
        editmode_verified = '相关 EditMode 测试已通过，但运行时行为尚未验收。'
        playmode_verified = '相关 PlayMode 或场景运行验证已通过，但最终验收尚未完成。'
        passing = '要求的验证已经通过，并且证据已经记录。'
    }
    field_guide = [ordered]@{
        id = '功能的唯一标识。建议使用稳定、可搜索的格式，例如 PhysicsWalking-001；创建后不要随意修改。'
        priority = '任务优先级。数字越小越优先；依赖满足后，优先处理数值最小的未完成任务。'
        area = '修改范围。填写涉及的模块、目录或关键文件，并明确不应修改的边界。'
        title = '简短任务名称，用一句话说明要实现或修复什么。'
        user_visible_behavior = '用户或测试者最终能观察到的行为，不要只写内部实现方式。'
        status = '当前状态。空白任务使用 not_started；只有真实验证和证据齐全后才能写 passing。'
        acceptance = '验收画像。production_logic 强制正式代码接入；runtime_mainline 还强制 PlayMode 主线；interactive_mainline 进一步强制视觉结果和模拟输入。未要求的能力必须在 waivers 写明原因。'
        dependencies = '前置功能 ID 数组。没有依赖时写 []；依赖未通过时不能开始当前功能。'
        verification = '必须实际执行的验收项数组，例如 EditMode 测试、PlayMode 场景操作、预期结果和人工检查。'
        evidence = '已经生成的证据路径数组，通常引用 .harness/runs/<task-id>-<timestamp>/result.json；不要填写尚不存在的证据。'
        blockers = '当前无法继续的具体阻塞数组。写清失败命令、错误现象、影响范围和解除条件；没有阻塞时写 []。'
        next_action = '下一次可直接执行的具体动作，例如“修复 X 后重跑 Y 测试”；避免只写“继续处理”。'
        notes = '补充约束、设计决定、参考资料、已知风险和交接信息。一次性长日志应放进运行证据，不要堆在这里。'
    }
    known_facts = @(
        'Harness root is E:\UnityProgram\FPS; it is not a Git repository.',
        'Unity and Git project root is E:\UnityProgram\FPS\FPSResearch.',
        'Harness files must not be copied into or committed with FPSResearch.',
        'Harness execution is serial: only one active branch, one working thread, one Unity Editor, and one in_progress feature are allowed.',
        'The Harness root feature_list.json is not a task source; task state is stored only in the current branch list under .harness/worktree.',
        'Required Unity version is read from FPSResearch/ProjectSettings/ProjectVersion.txt and is currently 2022.3.62f1c1.',
        'Stable project rules live in Shared; task state and blockers live in this file; per-run evidence lives in .harness/runs.',
        'Before a feature becomes passing, add schema_version 2 result.json evidence and run validate-feature-evidence.ps1 -FeatureId <id>.',
        'A closed branch must name an interactive_mainline final feature that proves production integration, PlayMode mainline, visual result, and simulated input.',
        'Unity MCP SkillSync is optional; MCP availability is determined by handshake, connected instance, and core tool verification.'
    )
    features = @()
}

if (-not [string]::IsNullOrWhiteSpace($BranchName)) {
    $template.Insert(1, 'branch', $BranchName)
}

$json = $template | ConvertTo-Json -Depth 20
$null = $json | ConvertFrom-Json

if (-not $PSCmdlet.ShouldProcess($targetPath, '备份并重置 feature_list.json')) {
    return
}

if ((Test-Path -LiteralPath $targetPath -PathType Leaf) -and -not $NoBackup) {
    $backupDirectory = Join-Path $PSScriptRoot '.harness\feature-list-backups'
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupPath = Join-Path $backupDirectory "feature_list-$timestamp.json"
    Copy-Item -LiteralPath $targetPath -Destination $backupPath
    Write-Host "已备份：$backupPath"
}

[System.IO.File]::WriteAllText(
    $targetPath,
    $json + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))

$written = Get-Content -Raw -Encoding UTF8 -LiteralPath $targetPath | ConvertFrom-Json
if (@($written.features).Count -ne 0) {
    throw "重置后的文件未通过结构校验：$targetPath"
}

Write-Host "已重置：$targetPath"
Write-Host '任务数组已清空；新增任务必须从 not_started 开始，并按 field_guide 填写。'

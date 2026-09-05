# Harness

这是一套随项目分发的精简验证工具，不是游戏运行时模块。

它保留了联网射击项目中最关键的可复现检查能力：按当前 Git 分支定位 feature 清单、核验 `result.json` 证据契约，以及比较两个客户端接收的权威快照。运行产生的任务状态、证据、报告、日志和备份都会写入 `Harness/.harness/`，并已由 Git 忽略。

## 常用入口

```powershell
# 查看项目、当前分支和对应 feature 清单
.\Harness\Invoke-FpsHarness.ps1 -Mode status

# 检查指定 feature 的证据是否符合 schema-v2 和验收配置
.\Harness\Invoke-FpsHarness.ps1 -Mode validate-feature -FeatureId <feature-id>

# 对比两个客户端日志中相同 Match/Pawn/Tick 的权威快照
.\Harness\Invoke-FpsHarness.ps1 -Mode compare-snapshots `
  -ClientALog <client-a.log> `
  -ClientBLog <client-b.log> `
  -OutputPath <comparison.json>
```

## 边界

- 只从仓库根目录推导项目路径；不依赖开发机外置工作区。
- 不包含 Unity 构建产物、截图、录屏、运行证据、历史任务清单或交接文档。
- `validate-feature-evidence.ps1` 只读取 `Harness/.harness/runs/` 内的 `result.json`，避免将任意文件误当作验收证据。
- 该目录仅服务于开发和验证；游戏客户端、Unity Dedicated Server 与 `Server/` 的独立服务端都不依赖它。

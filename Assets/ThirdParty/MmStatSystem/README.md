# Mm-StatSystem 学习副本

本目录是从公开仓库 [Haki-sheep/Mm-StatSystem](https://github.com/Haki-sheep/Mm-StatSystem) 导入的隔离学习副本。

- 固定提交：`ff25c8c216642a5cd00bc649ce06e4194d22bcb9`
- 上游许可证：MIT，见 `LICENSE.md`
- 上游 Unity：`6000.4.10f1`
- 当前项目 Unity：`2022.3.62f1c1`

## 导入范围

保留：

- `Core/`：Stat、Tag、GameplayEffect、Ability 的数据和运行时；
- `Editor/`：不依赖 Odin Editor 的 GAS 浏览与编辑工具；
- `Demo/`：独立学习场景和操作说明；
- `Resources/GAS/GameplayTagDatabase.asset`：标签数据库；
- 原始 Unity `.meta`，用于保持上游资产引用。

没有导入：

- 上游 `Packages/`、`ProjectSettings/`；
- 上游捆绑的 Odin Inspector、DOTween Pro、UniTask、TextMesh Pro 和 URP 资源；
- 五个直接继承 Odin Editor 类型的自定义 Inspector/Drawer。

## 兼容说明

`Compatibility/OdinRuntimeCompatibility.cs` 只为上游核心代码提供无行为的
Inspector 属性和 `SerializedScriptableObject` 表面兼容。它不复刻 Odin 的
Inspector 布局或自定义序列化能力。当前上游 GAS 数据字段均可由 Unity
标准序列化表达，但研究上游设计时仍应记住这一差异。

该目录不接入 FPS 的 Manager、角色、武器或动画主链路；需要实验时应优先
从 `Demo/GasDemoScene.unity` 开始。

# 武器持握晃动 Bug 排查与修复记录

## 范围

记录 AK12、MK18 与 Knife 在第一人称持握状态下晃动表现不一致的问题，包括运行时逐层诊断、根因、配置修复、验证结果与回滚方式。

## 问题

理想表现是持枪或持刀时都有轻微晃动。实际表现为 AK12 会持续轻微晃动，而 MK18 与 Knife 的手臂和武器基本静止。曾尝试替换 AK12 武器 Prefab 的 Idle 动画为 Static 动画，但晃动仍然存在。

## 环境

- Unity：`2022.3.62f3`
- 项目：`E:\UnityProgram\FPS\FPSResearch`
- 角色 Animator Controller：`FPSAnimator_Generic_Project`
- 程序动画入口：`CharacterBoneController -> AdditiveLayerJob -> AdditiveJob`
- 涉及 Profile：`AK12ProceduralBoneProfile`、`MK18BoneProfile`、`KnifeBoneProfile`

## 现象

- AK12：腰射静止持枪时可以观察到持续轻微晃动。
- MK18：修改前腰射时 Additive 基本被 `AimingWeight` 抑制，表现静止。
- Knife：修改前同样由 `AimingWeight` 抑制，表现静止。
- 替换 AK12 武器自身 Idle 动画无效，说明晃动来源不在武器 Animator。

## 诊断

### 逐层定位

在 `CharacterBoneController` 的动画流中，对 `InputPose`、`PoseSampler`、`IkMotion`、`AttachHand`、`View`、`Ads`、`Additive`、`Look`、`Turn`、`IK` 与 `ProfileBlend` 逐层采样。

AK12 稳定持握时，`View` 之前的目标骨骼基本保持稳定；第一个持续产生逐帧变化的节点是 `Additive`。例如 `IK WeaponBone` local yaw 在连续帧中出现：

- frame 55814：`-74.11°`
- frame 55815：`-73.85°`
- frame 55816：`-73.69°`
- frame 55817：`-73.54°`

`Look`、`Turn`、`IK` 与 `ProfileBlend` 只是继续传递该结果，因此它们不是首个异常写入者。

### 数据来源

`AdditiveLayerJob.UpdatePlayableJobData()` 对曲线权重的处理为：

```csharp
float curve = string.IsNullOrWhiteSpace(settings.AimingCurve)
    ? 1f
    : owner.GetCurveValue(settings.AimingCurve);
```

`AdditiveJob` 随后读取 `WeaponBoneAdditive`，把该姿势以 Component Space Additive 方式作用到 `IK WeaponBone`。角色 Animator Controller 中包含下列带 `WeaponBoneAdditive` 曲线的动画：

- `C_CurveIdle.anim`
- `C_CurveRun.anim`
- `C_Rifle_InspectStart.anim`
- `C_Rifle_InspectEnd.anim`

因此，晃动来自角色 Animator 的 `WeaponBoneAdditive` 曲线经过程序动画 Additive 层的应用，而不是 AK12 武器 Prefab 的 Idle 动画。

### Profile 差异

修改前的关键配置为：

| Profile | `AimingCurve` | 非瞄准时结果 |
| --- | --- | --- |
| AK12 | 空字符串 | `curve = 1`，持续应用 Additive |
| MK18 | `AimingWeight` | 腰射时曲线接近 0，Additive 被抑制 |
| Knife | `AimingWeight` | 腰射时曲线接近 0，Additive 被抑制 |

这解释了为什么同一角色动画曲线只在 AK12 上形成明显晃动。

## 修改

仅修改两个武器 Profile 中 Additive 子资产的 `AimingCurve`：

- `Assets/Settings/Gameplay/Weapon/WeaponProfile/MK18/MK18BoneProfile.asset`
  - `AimingCurve: "AimingWeight" -> ""`
- `Assets/Settings/Gameplay/Weapon/WeaponProfile/Knife/KnifeBoneProfile.asset`
  - `AimingCurve: "AimingWeight" -> ""`

没有修改 AK12、共享动画代码、角色 Animator Controller 或武器 Idle 动画。

## 验证

### MK18

PlayMode 中直接选择 MK18 后，运行时成功链接 `MK18BoneProfile`。frame 686 中：

- `Ads` 后 `IK WeaponBone` local yaw：`-55.64°`
- `Additive` 后 `IK WeaponBone` local yaw：`-55.82°`

后续帧继续产生新的 Additive 输出，证明空 `AimingCurve` 已进入运行时动画链。

### Knife

PlayMode 默认装备 Knife 后，运行时成功链接 `KnifeBoneProfile`。frame 10 中：

- `IkMotion` 后 `IK WeaponBone` local yaw：`-102.50°`
- `Additive` 后 `IK WeaponBone` local yaw：`-103.18°`

frame 11 同样产生新的 Additive 输出，证明 Knife 的配置也已生效。

### 资产与编译

- 强制刷新并重新编译后，两份 Profile 的 `AimingCurve` 均确认为空字符串。
- 修改后由用户在 Game View 中确认 MK18 晃动效果符合预期。
- Knife 已完成运行态数据链验证，最终视觉幅度由 Game View 人工观察确认。

## 回滚与恢复

两个武器可以独立回滚，不需要修改代码：

- MK18：把 Additive 的 `AimingCurve` 恢复为 `AimingWeight`。
- Knife：把 Additive 的 `AimingCurve` 恢复为 `AimingWeight`。

恢复后，腰射状态会重新由 `AimingWeight` 抑制 `WeaponBoneAdditive`，ADS 相关权重行为回到修改前状态。

## 后续关注

- 观察 Knife 的晃动幅度是否适合近战持握姿态。
- 分别检查 Idle、移动和 ADS 三种状态，避免放开 Additive 后出现幅度过强。
- 如果不同武器需要不同强度，应优先增加武器级强度配置，而不是修改共享角色曲线。

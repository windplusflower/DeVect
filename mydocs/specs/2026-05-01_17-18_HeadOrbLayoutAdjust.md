# SDD Spec: 头顶球排布调整

## 0. Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- Goal: 调整小骑士头顶球槽的排布，让球整体更靠近小骑士、圆弧更明显，并让相邻球之间的视觉间隔更开。
- In-Scope:
  - 调整 `3` 槽 / `4` 槽布局的展开角度常量。
  - 保持圆弧半径不变，通过下移圆弧圆心来实现“更靠近小骑士”。
  - 保持当前“围绕小骑士圆心的扇形布局”与共圆弧运动语义不变，只修改排布几何参数。
  - 保证虚线槽位与实体球一起跟随新的槽位坐标变化。
- Out-of-Scope:
  - 不修改球的战斗逻辑、触发逻辑、生成逻辑。
  - 不修改球尺寸、材质、虚线环样式、飞出轨迹时长等非本次排布需求。
  - 不重构当前运行时结构。

## 1.5 Code Map (Project Topology)
- Core Logic:
  - `src/Orbs/Runtime/OrbRuntime.cs`: 头顶球排布的核心文件。当前通过 `RootOffset`、`SlotFanRadius`、`SlotFanCenterAngleDeg`、`ThreeSlotSpreadDeg`、`FourSlotSpreadDeg` 控制圆心相对小骑士的位置、圆弧半径与总展开角度。
  - `src/Orbs/Runtime/OrbRuntime.cs`: `BuildSlotLocalPositions(int capacity)` 按槽位容量生成每个槽位的局部坐标。
  - `src/Orbs/Runtime/OrbRuntime.cs`: `GetSlotAngleDeg(int slotIndex, int capacity)` 决定每个槽位在圆弧上的角度分布。
  - `src/Orbs/Runtime/OrbRuntime.cs`: `EvaluateArcPoint(float angleDeg, float radius)` 把角度和半径转换成局部坐标，是当前排布的几何底层。
  - `src/Orbs/Runtime/OrbRuntime.cs`: `TickFollow()` 让整组球根节点跟随小骑士位置；`RootOffset` 控制整组球相对小骑士的整体偏移。
- Entry Points:
  - `src/Orbs/OrbSystem.cs`: `OnHeroUpdate(HeroController hero, float deltaTime)` 每帧驱动 `_runtime.TickFollow()` 与 `_runtime.TickAnimations(deltaTime)`。
  - `src/Orbs/OrbSystem.cs`: `RestoreRuntimeIfNeeded(HeroController hero)` 调用 `_runtime.EnsureBuilt(...)`，在运行时恢复或重建槽位布局。
- Runtime State:
  - `src/Orbs/Runtime/OrbSlotRuntime.cs`: 每个槽位缓存 `CurrentAngleDeg`、`TargetAngleDeg`、`MotionRadius`，说明排布和换位动画都依赖同一套圆弧参数。
- Visual Coupling:
  - `src/Visual/OrbVisualService.cs`: `BuildDashedRing(Transform parent)` 在槽位锚点下构建虚线环，因此槽位坐标变化会同时影响空槽虚线与实体球落点。
- Product/Design Context:
  - `README.md`: 当前默认槽位为 `3` 格，装备 `吸虫之巢` 后为 `4` 格，因此本次排布参数不能只考虑 `3` 槽场景。
  - `mydocs/specs/2026-03-15_10-21_OrbVisualRescaleAndMaterial.md`: 之前已经把槽位布局收敛到“半径 + 角度”的扇形模型；当前实装常量为 `RootOffset = new(0f, 0.1f, 0f)`、`SlotFanRadius = 1.62f`、`ThreeSlotSpreadDeg = 76f`、`FourSlotSpreadDeg = 102f`。

## 2. Architecture (Optional - Populated in INNOVATE)
- 本任务不进入 `INNOVATE`。
- 继续沿用当前“以小骑士为圆心的极坐标圆弧布局”方案，只修改几何参数，不变更构造方式。
- 为保证 `3` 槽与 `4` 槽视觉语言一致，本轮统一采用：
  - 保持 `SlotFanRadius` 不变。
  - 通过下移 `RootOffset.y` 让整条圆弧压向小骑士。
  - 将 `ThreeSlotSpreadDeg` 调整为 `120f`。
  - 将 `FourSlotSpreadDeg` 也调整为 `120f`，让四槽状态保持同一条大圆弧语义，只是单槽间距略小于三槽。
- 保持 `SlotFanCenterAngleDeg = 90f` 不变，继续以角色正上方为扇形中心方向，避免引入左右偏斜。
- 用户已明确：`更靠近` 的含义不是减小半径，而是保持半径、下移圆心。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Orbs/Runtime/OrbRuntime.cs`
  - 保留现有方法签名，不新增或删除任何 public / private 方法：
    - `public void EnsureBuilt(Transform heroTransform, int capacity, IReadOnlyList<OrbInstanceSnapshot> persistedOrbs, OrbDefinitionRegistry definitions)`
    - `private static Vector3[] BuildSlotLocalPositions(int capacity)`
    - `private static float GetSlotAngleDeg(int slotIndex, int capacity)`
    - `private static Vector3 EvaluateArcPoint(float angleDeg, float radius)`
  - 仅调整以下常量值：
    - `private static readonly Vector3 RootOffset = new(0f, -0.15f, 0f)`
    - `private const float SlotFanRadius = 1.62f`
    - `private const float ThreeSlotSpreadDeg = 120f`
    - `private const float FourSlotSpreadDeg = 120f`
  - 保持以下常量不变：
    - `private const float SlotFanCenterAngleDeg = 90f`

### 3.2 Expected Geometry
- `3` 槽布局目标角度：`150° / 90° / 30°`
- `4` 槽布局目标角度：`150° / 110° / 70° / 30°`
- 以 `SlotFanRadius = 1.62f` 推导出的预期局部坐标近似为：
  - `3` 槽：
    - 左：`(-1.40f, 0.81f, 0f)`
    - 中：`(0f, 1.62f, 0f)`
    - 右：`(1.40f, 0.81f, 0f)`
  - `4` 槽：
    - 左 1：`(-1.40f, 0.81f, 0f)`
    - 左 2：`(-0.55f, 1.52f, 0f)`
    - 右 2：`(0.55f, 1.52f, 0f)`
    - 右 1：`(1.40f, 0.81f, 0f)`
- 若采用确认值 `RootOffset.y = -0.15f`，则相对小骑士的世界偏移近似为：
  - `3` 槽：
    - 左：`(-1.40f, 0.66f, 0f)`
    - 中：`(0f, 1.47f, 0f)`
    - 右：`(1.40f, 0.66f, 0f)`
  - `4` 槽：
    - 左 1：`(-1.40f, 0.66f, 0f)`
    - 左 2：`(-0.55f, 1.37f, 0f)`
    - 右 2：`(0.55f, 1.37f, 0f)`
    - 右 1：`(1.40f, 0.66f, 0f)`
- 结果约束：
  - 球到圆心的半径必须保持当前 `1.62f` 不变。
  - “更靠近小骑士”必须通过下移圆心实现，而不是收半径实现。
  - `3` 槽总展开角度必须精确采用 `120f`。
  - `4` 槽总展开角度本轮也采用 `120f`，保持同一视觉弧面。
  - 虚线槽位与实体球都必须自动落到新的槽位坐标，无需单独视觉补丁。

### 3.3 Implementation Checklist
- [x] 1. 更新 `src/Orbs/Runtime/OrbRuntime.cs` 中 `RootOffset.y` 为确认后的下移值。
- [x] 2. 保持 `src/Orbs/Runtime/OrbRuntime.cs` 中 `SlotFanRadius = 1.62f` 不变。
- [x] 3. 更新 `src/Orbs/Runtime/OrbRuntime.cs` 中 `ThreeSlotSpreadDeg` 为 `120f`。
- [x] 4. 更新 `src/Orbs/Runtime/OrbRuntime.cs` 中 `FourSlotSpreadDeg` 为 `120f`。
- [x] 5. 构建项目，确认改动不会破坏编译。
- [x] 6. 回查实现，确认没有偏离 Spec，且没有额外改动其它视觉或战斗逻辑。

## 4. Execution Notes
- 已执行文件修改：`src/Orbs/Runtime/OrbRuntime.cs`
- 实际落地值：
  - `RootOffset = new(0f, -0.15f, 0f)`
  - `SlotFanRadius = 1.62f`
  - `ThreeSlotSpreadDeg = 120f`
  - `FourSlotSpreadDeg = 120f`
- 验证结果：`dotnet build DeVect.csproj` 成功，`0` warnings，`0` errors。

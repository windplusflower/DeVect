# SDD Spec: IceShield HUD 对齐与象限渲染修复

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 详细研究当前冰盾 HUD 显示错误的根因，并准备后续修复方案，使冰盾满足以下三个约束：1）象限不裂开；2）整体大小与上方白色血面具一致；3）每层冰盾与上方对应血面具在水平与垂直位置上严格对齐。
- **In-Scope**:
  - 研究 `IceShieldDisplay` 当前的构建、定位、缩放、象限裁切与动画逻辑。
  - 研究护盾 petal 数据到 HUD 槽位的映射逻辑。
  - 研究 HUD 血条锚点/血面具采样逻辑是否可靠。
  - 输出可执行修复方向，但当前阶段不写实现代码。
- **Out-of-Scope**:
  - 冰盾战斗数值、吸伤逻辑、orb 生成逻辑。
  - 其他 HUD 元素或非冰盾视觉效果。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `src/Visual/IceShieldDisplay.cs`: 冰盾 HUD 的全部视觉逻辑，包含槽位创建、象限 sprite 创建、位置计算、局部 spread、呼吸动画、HUD renderer 采样。
  - `src/Combat/IceShieldState.cs`: 冰盾 petal 状态源；4 个 petal = 1 层盾，最多 4 层。
- **Entry Points**:
  - `src/Orbs/OrbSystem.cs`: 在 `OnHeroUpdate(...)` 中每帧调用 `_iceShieldDisplay.Tick(_shieldState.GetPetalCount())`，是 HUD 更新入口。
  - `DeVect.cs`: 通过 `ModHooks.HeroUpdateHook` 驱动 `OrbSystem.OnHeroUpdate(...)`。
- **Data Models**:
  - `src/Combat/IceShieldState.cs`: `PetalsPerShield = 4`，`MaxShieldLayers = 4`，`MaxPetals = 16`。
- **Resources**:
  - `assets/ice_shield_hud.png`: 冰盾贴图资源。
  - `DeVect.csproj`: 将该资源嵌入程序集，并在 `InstallMod` target 中自动复制构建产物到 `Managed/Mods/DeVect`。
- **Dependencies**:
  - `UnityEngine.SpriteRenderer`: 当前 HUD 由多个 `SpriteRenderer` 组合实现。
  - `GameCameras.instance.hudCamera`: HUD 世界坐标与 viewport 坐标转换依赖。

## 2. Architecture (Optional - Populated in INNOVATE)
- **当前实现事实确认**:
  - 当前实现为“每层盾一个 slot + 每个 slot 内 4 个象限 renderer + 1 个 glow renderer”。
  - 当前实现不是直接挂在血面具对象下，而是每帧自行扫描 HUD `Renderer`，推导血面具位置后，把 slot 放到计算结果上。
- **已确认根因**:
  - **根因 1：象限裂开来自局部 spread + 象限 pivot 双重叠加**
    - `GetQuadrantLocalPosition(...)` 通过 `QuadrantLocalSpreadX/Y` 主动把 4 个象限向四周分开。
    - 同时 4 个象限 sprite 自身使用不同 pivot（右下、左下、右上、左上），pivot 已经在做“围绕中心拼合”的工作。
    - 两套机制叠加后，单个盾内部出现明显横向和纵向裂缝，用户截图中的“上下裂开”就是该问题的直接表现。
  - **根因 2：大小失真来自 slot 级缩放与象限级缩放叠加**
    - 构建时每个 petal 已设置 `localScale = IconBaseScale`。
    - 运行时再次对每个 petal 执行 `shieldScale = (IconBaseScale + ...) * pulse * petalPulse`。
    - 这会形成“初始放大一次 + Tick 再放大一次”的双重缩放，实际显示尺寸明显大于预期血面具尺寸。
  - **根因 3：位置/间距未生效的表象，实际来自‘整组 slot 对齐正确但单盾内部裂开’**
    - 从当前截图看，整体盾组仍然落在前两格血附近，没有出现整组大幅漂移；真正错误的是单层盾内部四个象限被横纵拉开，视觉上像“位置和间距完全不对”。
    - 因此，优先级最高的问题不是继续调 `HealthMaskViewportOffsetX/Y`，而是先消除单盾内部 spread。
  - **根因 4：血面具采样逻辑仍存在不稳定风险，但不是当前截图的首要问题**
    - `TryCollectHealthMaskRenderers(...)` 是按名称关键词 + viewport 范围 + 去重阈值选 renderer；这类启发式逻辑可能误选血条杆或子 renderer。
    - 但当前截图中最突出的错位来自单个 slot 内部裂开，而不是 slot 之间完全错序。
- **修复策略方向**:
  - 第一阶段：把单个盾恢复为“围绕同一中心点拼合”的完整图形，禁止象限局部 spread。
  - 第二阶段：去掉双重缩放，只保留一种缩放来源，使单层盾尺寸以血面具为基准校准。
  - 第三阶段：在单盾稳定后，再校准 `HealthMaskViewportOffsetX/Y` 与血面具真实中心的关系，完成最终对齐。
- **最新截图复核（2026-04-05 11:08 后）**:
  - `不裂开`: 已基本满足。
  - `大小`: 已基本满足，当前单层盾视觉尺寸接近白色血面具。
  - `位置`: 仍不满足，第一层盾整体落在偏右、偏下的位置，没有贴到对应白血正下方。
  - `关键新增判断`: 当前问题大概率不是“盾太大”，而是**血面具位置采样基准本身不对**，导致 `HealthMaskViewportOffsetX/Y` 的微调只能产生很小变化，无法真正把盾推到目标血格中心。
  - `进一步推论`: 当前 `TryCollectHealthMaskRenderers(...)` 可能抓到的是血条相关的子 renderer / 组合 renderer，而不是单个白血面具本体，因此 `viewport.x` 对应的是“错误中心”。
  - `用户提供经验值`: 游戏中单个白血宽度大约为 `0.5`（世界坐标尺度）。后续应优先使用“单个血格宽度/节距”作为稳定几何基准，而不是继续依赖小幅 viewport 偏移猜位置。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Visual/IceShieldDisplay.cs`
  - `const: HealthMaskViewportOffsetX = [保留可调，但本轮仅在必要时微调]`
  - `const: HealthMaskViewportOffsetY = [保留可调，但本轮仅在必要时微调]`
  - `const: IconBaseScale = [重新标定到接近单个白色血面具宽度]`
  - `const: QuadrantLocalSpreadX = 0f`
  - `const: QuadrantLocalSpreadY = 0f`
  - `const: PulseAmplitude = 0f`
  - `const: PetalPulseAmplitude = 0f`
  - `const: BobAmplitude = 0f`
  - `const: SwayAmplitude = 0f`
  - `method: Tick(int petalCount) -> void`
  - `method: UpdateIcons(int petalCount) -> void`
  - `method: GetSlotWorldPositions() -> Vector3[]`
  - `method: TryGetHealthMaskWorldPositions(Camera hudCamera, out Vector3[] worldPositions) -> bool`
  - `method: GetQuadrantLocalPosition(int quadrantIndex) -> Vector3`
  - `rule: CreateRendererObject(...)` 创建 petal 时，初始 `localScale` 必须为 `Vector3.one`，避免构建期放大。
  - `rule: UpdateIcons(...)` 运行时对 petal 只允许一次缩放计算，不允许再乘第二层基础缩放。
  - `rule: 每个 slot 的 4 个 petal 必须共用同一个 slot 中心点，只依赖 quadrant sprite pivot 进行拼接。`
  - `rule: HUD 冰盾位置必须稳定，不允许额外 bob/sway/rotation 导致与血面具视觉失配。`
- `File: src/Combat/IceShieldState.cs`
  - `const: PetalsPerShield = 4`
  - `const: MaxShieldLayers = 4`
  - `rule: 1~4 petal 仅填充同一层盾的 1~4 个象限；5~8 petal 才进入第二层盾。`
- `File: src/Orbs/OrbSystem.cs`
  - `method: OnHeroUpdate(HeroController hero, float deltaTime) -> void`
  - `rule: 保持 `_iceShieldDisplay.Tick(_shieldState.GetPetalCount())` 为唯一 HUD 驱动入口，不改调用链。`
- `File: DeVect.csproj`
  - `target: InstallMod`
  - `rule: 构建后自动复制 `DeVect.dll` / `DeVect.pdb` 到 `Managed/Mods/DeVect`，无需手动部署。`

### 3.1.1 Root-Cause-to-Fix Mapping
- `问题: 象限裂开`
  - `原因`: `GetQuadrantLocalPosition(...)` 额外 spread 与 quadrant pivot 重叠。
  - `修复约束`: `GetQuadrantLocalPosition(...)` 返回 `(0, 0, 0)`，或逻辑等价地取消象限局部偏移。
- `问题: 尺寸过大`
  - `原因`: 创建期 `IconBaseScale` + Tick 期 `shieldScale` 双重缩放。
  - `修复约束`: 创建期 scale 固定为 `Vector3.one`；Tick 期 scale 作为唯一尺寸来源。
- `问题: 与血条未严格对齐`
  - `原因`: 现阶段视觉错位主要被“裂开”掩盖；在消除裂开后，再基于血面具中心做微调。
  - `修复约束`: 只有在单盾恢复完整后，才允许调整 `HealthMaskViewportOffsetX/Y`；不再通过增大象限 spread 伪造间距。
- `问题: viewport 偏移微调没有带来有效位置修复`
  - `原因`: 采样得到的 renderer 中心不是单个白血中心，导致 `HealthMaskViewportOffsetX/Y` 调整量作用在错误基准上。
  - `修复约束`: 下一轮不得只靠继续试错 `HealthMaskViewportOffsetX/Y`；必须先验证采样对象，必要时改为“首个血格锚点 + 固定世界节距（约 0.5）”的确定性布局。
- `问题: 即使采样正确，动画也会制造“未对齐”的视觉噪声`
  - `原因`: `sharedOffset`、`bob`、`rotation`、petal pulse 会让盾持续漂移或膨胀。
  - `修复约束`: HUD 对齐阶段必须禁用这些动画，优先满足稳定对齐。

### 3.2 Implementation Checklist
- [x] 1. 更新 `src/Visual/IceShieldDisplay.cs`：将 `QuadrantLocalSpreadX` 和 `QuadrantLocalSpreadY` 设计为 `0f`，并确保 `GetQuadrantLocalPosition(int quadrantIndex) -> Vector3` 不再引入额外局部偏移。
- [x] 2. 更新 `src/Visual/IceShieldDisplay.cs`：创建 petal renderer 时使用 `Vector3.one` 作为初始 `localScale`，删除创建阶段的基础放大。
- [x] 3. 更新 `src/Visual/IceShieldDisplay.cs`：在 `UpdateIcons(int petalCount)` 中保留单一 `shieldScale` 计算，并把 `IconBaseScale` 重新标定到接近单个白色血面具尺寸。
- [x] 4. 更新 `src/Visual/IceShieldDisplay.cs`：在“无裂开、无双重缩放”前提下复核 `TryGetHealthMaskWorldPositions(...)` 的输出，仅对 `HealthMaskViewportOffsetX/Y` 做最小必要微调，让第一层盾对齐第一格血。
- [x] 5. 执行 `dotnet build "DeVect.csproj"`，确认 0 error，并确认 `Managed/Mods/DeVect` 下 `DeVect.dll`、`DeVect.pdb`、`DeVect.zip` 时间戳更新。
- [ ] 6. 基于构建后游戏截图复核三项验收标准：`不裂开`、`单层盾大小与白血一致`、`第一层盾与第一格血中心对齐`。（待用户回传新截图）

### 3.3 Next Repair Plan
- `目标`: 修复“大小已正常，但位置仍错误”的剩余问题。
- `方案 A（优先）`: 保留现有 HUD 扫描，但把采样从“任意 health renderer 中心”升级为“单个白血面具本体中心”；仅在确认能稳定拿到单个 mask 后再使用其中心。
- `方案 B（兜底，预期更稳）`: 不再依赖每个 mask 单独采样，而是找到左侧第一个白血锚点后，按固定世界节距 `0.5` 水平排布后续盾位；垂直方向只保留一个固定下移量。
- `选择建议`: 优先执行 `方案 B`，因为用户已经提供了单个白血宽度近似值 `0.5`，这比当前不稳定的 renderer 启发式更可控。

## 3.4 Approved Positioning Plan
### 3.4.1 Approved Strategy
- 用户已确认采用 `方案 B`。
- 新定位逻辑不再依赖“每个血面具都单独采样”。
- 新定位逻辑改为：
  - 从 HUD 中找到**最左侧白血面具锚点**。
  - 以该锚点为 `slot 0` 的中心参考。
  - 其余盾位按固定世界节距 `0.5` 递增排列。
  - 垂直方向只使用一个固定世界偏移，使盾挂在血面具正下方。

### 3.4.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - 新增/调整常量：
    - `HealthMaskWorldOffsetX = -0.5f`
    - `HealthMaskWorldSpacing = 0.5f`
    - `HealthMaskWorldOffsetY = [固定世界坐标下移量]`
  - 保留：
    - `TryGetLeftHudAnchorRenderer(...)`
    - `IsEligibleLeftHudAnchorRenderer(...)`
  - 替换定位职责：
    - `TryGetHealthMaskWorldPositions(...) -> bool`
      - 旧行为：收集多个 health renderer，逐个推导 world position
      - 新行为：仅获取一个“最左侧白血锚点”，然后按固定间距生成 4 个 slot world position
  - 弱化/废弃：
    - `TryCollectHealthMaskRenderers(...)`
    - `HealthMaskViewportOffsetX`
    - `HealthMaskMinViewportGapX`

### 3.4.3 Execution Checklist
- [x] A1. 将 `TryGetHealthMaskWorldPositions(...)` 改为“首个血格锚点 + 固定世界节距 0.5”的确定性布局。
- [x] A2. 移除对“多 renderer 收集 + viewport 去重”的依赖，避免继续建立在错误中心点上。
- [x] A3. 用固定世界下移量重新标定盾位垂直位置，使盾稳定位于白血正下方。
- [x] A4. 构建并确认自动部署时间戳更新。
- [ ] A5. 等待用户回传截图，验证第一层盾与第一格血对齐、第二层盾与第二格血对齐。

## 3.5 Corrective Research Conclusion
- `问题`: 为什么引入 `HealthMaskWorldOffsetX = -0.5f` 后，截图位置仍几乎没有变化？
- `结论`: 本次很可能改错了“生效层级”，或者更准确地说，**改到了次级分支，而运行时仍然主要受默认 HUD 锚点分支控制**。
- `最强判断`:
  - `GetSlotWorldPositions()` 里虽然优先调用 `TryGetHealthMaskWorldPositions(...)`，但从截图效果看，运行时结果仍然接近原先默认锚点布局。
  - 这说明当前“首个血格锚点 + 0.5 节距”的逻辑，要么没有真正命中主要路径，要么命中的锚点本身就不是白血真实中心。
- `为什么说这不是单纯偏移量太小`:
  - 如果 `firstWorldPosition.x + HealthMaskWorldOffsetX` 真以 `-0.5f` 生效，第一层盾应出现明显左移，不可能几乎保持原位。
  - 用户回图显示位置几乎不变，因此更像是“改动没有落在真实控制位置的那条路径上”。
- `新的修复方向`:
  - 不再继续围绕 `TryGetHealthMaskWorldPositions(...)` 修补。
  - 下一步应直接把 `GetSlotWorldPositions()` 改成**完全确定性的单一路径**：
    - 直接使用 `GetHudWorldPosition(hudCamera)` 作为首格基准；
    - 再在该基础上引入明确的 `slot0 world x/y` 校准值；
    - 后续层固定 `+0.5f` 节距排列。
  - 这样可以彻底绕开“是否命中了某个血格 renderer”的不确定性，保证改动一定能反映到画面上。

## 3.6 Latest Diagnosis
- `用户质疑`: 为什么连坐标都改不对？
- `当前最直接答案`: 因为前几轮我一直在改**没有实际控制最终位置的分支**。
- `更具体地说`:
  - 运行时大概率根本没有进入“血格定位分支”；
  - 实际在生效的，是 fallback 路径：`GetHudWorldPosition(hudCamera)`；
  - 这条路径真正受控的是左侧 HUD 锚点偏移，例如 `LeftHudAnchorViewportOffsetX`、`LeftHudAnchorViewportOffsetY`，而不是后面几轮改的 `HealthMaskWorldOffsetX` / `HealthMaskWorldSpacing`。
- `证据`:
  - 尺寸修改能稳定生效，说明 DLL 已部署、渲染对象已更新；
  - 但位置相关的 `HealthMask...` 常量多轮修改几乎完全无效；
  - 这最符合“运行时一直走 fallback 锚点分支”的现象。
- `执行修正原则`:
  - 不能再继续修改 `TryGetHealthMaskWorldPositions(...)` 内部常量；
  - 必须直接接管 `GetSlotWorldPositions()` 的返回值来源，或者直接修改 fallback 主路径使用的首格偏移常量；
  - 只有这样，画面位置才会立即变化。

## 3.7 Approved Direct Fix
### 3.7.1 Strategy
- 用户已明确要求直接修改。
- 本轮不再尝试血格 renderer 采样。
- `GetSlotWorldPositions()` 将改为只使用 `GetHudWorldPosition(hudCamera)` 作为基准。
- 在此基准上使用固定世界坐标偏移：
  - `SlotStartWorldOffsetX`: 控制第一层盾相对默认 HUD 锚点的左移/右移
  - `SlotStartWorldOffsetY`: 控制盾相对血条的上下位置
  - `HealthMaskWorldSpacing = 0.5f`: 控制后续层的水平节距

### 3.7.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - 新增常量：
    - `SlotStartWorldOffsetX`
    - `SlotStartWorldOffsetY`
  - `method: GetSlotWorldPositions() -> Vector3[]`
    - 删除对 `TryGetHealthMaskWorldPositions(...)` 返回值的依赖
    - 直接返回 `GetHudWorldPosition(hudCamera) + start offset + i * spacing`
  - `method: TryGetHealthMaskWorldPositions(...)`
    - 保留但不再作为主路径使用，避免继续干扰调试

### 3.7.3 Execution Checklist
- [x] B1. 修改 `GetSlotWorldPositions()`，强制使用单一路径定位。
- [x] B2. 加入明确的首格世界偏移量，使第一层盾明显左移到第一格血下方。
- [x] B3. 构建并确认自动部署时间戳更新。
- [ ] B4. 等待用户回传截图验证位置是否终于发生变化。

## 3.8 Current Tuning State
- `最新截图判断`:
  - `间距`: 已基本正确，不再挤在一起。
  - `剩余问题`: 整组盾仍然相对上方白血**偏左且偏下**。
  - `结论`: 当前主路径已经接管成功，后续只需要微调 `SlotStartWorldOffsetX` 与 `SlotStartWorldOffsetY`。
- `下一步微调方向`:
  - `SlotStartWorldOffsetX`: 向右回调。
  - `SlotStartWorldOffsetY`: 向上回调。

## 3.9 Latest User Feedback
- `最新截图判断`:
  - 第一层盾相比上一版更接近目标，但仍然没有压到第一格白血正下方。
  - 用户明确要求：
    - `最左边的应该再靠左一点`
    - `间距应该再大一点`
- `修正含义`:
  - `SlotStartWorldOffsetX` 需要再次向负方向调整。
  - `HealthMaskWorldSpacing` 需要增大，使后续盾位更贴近上方白血节距。

### 3.8.1 Tuning Checklist
- [x] C1. 将 `SlotStartWorldOffsetX` 调大（更靠右），让第一层盾靠近第一格白血中心。
- [x] C2. 将 `SlotStartWorldOffsetY` 调大（更靠上），让盾更贴近白血下方。
- [x] C3. 构建并确认自动部署时间戳更新。
- [ ] C4. 等待用户回传截图复核最终对齐效果。

### 3.9.1 Tuning Checklist
- [x] D1. 将 `SlotStartWorldOffsetX` 再向左调，使第一层盾落到更靠近第一格白血的位置。
- [x] D2. 将 `HealthMaskWorldSpacing` 调大，使层与层之间的节距更接近白血间距。
- [x] D3. 构建并确认自动部署时间戳更新。
- [ ] D4. 等待用户回传截图复核最终对齐效果。

## 3.10 Latest Tuning Correction
- `用户最新反馈`: 当前位置仍明显不对，且本轮调节幅度过小。
- `执行结论`: 不再做微调，直接进行大步长修正。
- `修正策略`:
  - `SlotStartWorldOffsetX` 直接继续向左大幅调整；
  - `HealthMaskWorldSpacing` 直接继续增大；
  - 本轮目标是先让位置“明显过来”，再做细调。

### 3.10.1 Tuning Checklist
- [x] E1. 将 `SlotStartWorldOffsetX` 大步长左移。
- [x] E2. 将 `HealthMaskWorldSpacing` 大步长增大。
- [x] E3. 构建并确认自动部署时间戳更新。
- [ ] E4. 等待用户回传截图复核是否已进入正确区间。

## 3.11 Latest Tuning Correction
- `用户最新反馈`: 改动仍然不够，说明当前参数仍未进入目标区间。
- `执行结论`: 继续采用大步长调整，而不是小步试探。
- `修正策略`:
  - `SlotStartWorldOffsetX` 再明显向左调整；
  - `HealthMaskWorldSpacing` 再明显增大；
  - 目标是让第一层盾直接压近第一格白血下方，同时第二层盾明显向第二格白血靠拢。

### 3.11.1 Tuning Checklist
- [x] F1. 将 `SlotStartWorldOffsetX` 再次大步长左移。
- [x] F2. 将 `HealthMaskWorldSpacing` 再次大步长增大。
- [x] F3. 构建并确认自动部署时间戳更新。
- [ ] F4. 等待用户回传截图复核是否终于进入正确区间。

## 3.12 Fine Tuning
- `用户最新反馈`: 现在已经接近正确，仅需：
  - 第一层再向左一点点
  - 间距再大一点点
- `执行策略`: 从“大步长修正”切回“小步微调”，避免过冲。

### 3.12.1 Tuning Checklist
- [x] G1. 将 `SlotStartWorldOffsetX` 再向左微调一点。
- [x] G2. 将 `HealthMaskWorldSpacing` 再增大一点。
- [x] G3. 构建并确认自动部署时间戳更新。
- [x] G4. 用户已回传截图，完成最终验收。

## 4. Review
- `最终截图结论`:
  - `不裂开`: 满足。
  - `大小`: 满足，已与上方白血接近。
  - `位置`: 基本满足，第一层与后续层已与白血形成稳定对应关系。
  - `间距`: 满足，已不再重叠，且与白血节距接近。
- `当前实现状态`:
  - HUD 位置逻辑已切换为主路径确定性布局；
  - 后续如需再调，只需微调 `SlotStartWorldOffsetX`、`SlotStartWorldOffsetY`、`HealthMaskWorldSpacing` 三个常量。

## 4.1 Final Micro Tuning
- `用户最新要求`:
  - 再向左一点点
  - 间距再大一点点
- `执行策略`:
  - 仅做小步微调，不改其他逻辑。

### 4.1.1 Tuning Checklist
- [x] H1. 将 `SlotStartWorldOffsetX` 再向左微调一点。
- [x] H2. 将 `HealthMaskWorldSpacing` 再增大一点。
- [x] H3. 构建并确认自动部署时间戳更新。
- [ ] H4. 等待用户最终确认。

## 4.2 Explicit Delta Tuning
- `用户最新要求`:
  - `SlotStartWorldOffsetX` 再左移 `0.1`
  - `HealthMaskWorldSpacing` 再增大 `0.2`
- `执行策略`:
  - 直接按显式增量修改，不做额外推断。

### 4.2.1 Tuning Checklist
- [x] I1. 将 `SlotStartWorldOffsetX` 在当前值基础上减少 `0.1`。
- [x] I2. 将 `HealthMaskWorldSpacing` 在当前值基础上增加 `0.2`。
- [x] I3. 构建并确认自动部署时间戳更新。
- [ ] I4. 等待用户确认结果。

## 4.3 Reverse Delta Tuning
- `用户最新要求`:
  - 右移 `0.05`
  - 间隔减小 `0.1`
- `执行策略`:
  - 直接按显式增量回调当前参数。

### 4.3.1 Tuning Checklist
- [x] J1. 将 `SlotStartWorldOffsetX` 在当前值基础上增加 `0.05`。
- [x] J2. 将 `HealthMaskWorldSpacing` 在当前值基础上减少 `0.1`。
- [x] J3. 构建并确认自动部署时间戳更新。
- [ ] J4. 等待用户确认结果。

## 4.4 Spacing Micro Reduction
- `用户最新要求`: 间隔再小 `0.04`
- `执行策略`: 仅回调 `HealthMaskWorldSpacing`，其他参数保持不变。

### 4.4.1 Tuning Checklist
- [x] K1. 将 `HealthMaskWorldSpacing` 在当前值基础上减少 `0.04`。
- [x] K2. 构建并确认自动部署时间戳更新。
- [ ] K3. 等待用户确认结果。

## 4.5 Spacing Micro Reduction
- `用户最新要求`: 间隔再减 `0.02`
- `执行策略`: 继续仅回调 `HealthMaskWorldSpacing`，其他参数保持不变。

### 4.5.1 Tuning Checklist
- [x] L1. 将 `HealthMaskWorldSpacing` 在当前值基础上减少 `0.02`。
- [x] L2. 构建并确认自动部署时间戳更新。
- [ ] L3. 等待用户确认结果。

## 4.6 Visual Enhancement Plan
- `用户最新要求`:
  - `HealthMaskWorldSpacing` 增大 `0.01`
  - 增加“寒气缭绕”的 HUD 特效
  - 将冰盾贴图中间白色区域改为透明
- `上下文解释`:
  - 结合前序连续调参语境，此处“增大 0.01”默认解释为 `HealthMaskWorldSpacing` 在当前值基础上 `+0.01`。
  - 本轮不改变左右位置主逻辑，只做节距微调与视觉增强。

### 4.6.1 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - `const: HealthMaskWorldSpacing`
    - 当前值基础上 `+0.01`
  - `field: SpriteRenderer[] _mistRenderers`
    - 为每层盾新增一层寒气雾效 renderer，独立于现有 glow renderer
  - `method: EnsureBuilt() -> void`
    - 在每个 slot 下新增 `Mist` renderer
  - `method: UpdateIcons(int petalCount) -> void`
    - 同步控制 mist 的显示、透明度、轻微呼吸缩放
    - 不允许影响现有 slot 定位结果
  - `method: ApplyHudRenderConfig(HudRenderConfig config) -> void`
    - 将 mist renderer 纳入 HUD layer 与 sorting 处理
  - `method: CreateMistSprite() -> Sprite`
    - 新建柔和寒气贴图，表现为围绕冰盾的半透明蓝白雾层
  - `method: CreateShieldTexture(Texture2D source, bool[] backgroundMask) -> Texture2D`
    - 在生成冰盾主贴图时，将“中间白色核心区域”按亮度与中心权重转为透明或接近透明
  - `method: RemapToIcePalette(Color sourceColor, float edgeFactor) -> Color`
    - 只负责冰蓝边缘与主体映射，不再保留白色实心核心

### 4.6.2 Visual Rules
- `寒气缭绕特效`:
  - 必须是围绕单层盾的轻薄雾气，不得盖住白血 HUD。
  - 必须比当前 glow 更松、更虚、更淡。
  - 允许轻微呼吸感，但不得重新引入位置漂移。
- `中间白色透明`:
  - 冰盾中心白色区域应明显挖空，视觉上更像“冰晶外壳 + 中空核心”。
  - 不得通过简单降低整体 alpha 实现；必须主要作用于中心高亮区域。

### 4.6.3 Tuning Checklist
- [x] M1. 将 `HealthMaskWorldSpacing` 在当前值基础上增加 `0.01`。
- [x] M2. 为每层盾新增 mist renderer，并接入 `EnsureBuilt()` / `UpdateIcons()` / `ApplyHudRenderConfig()`。
- [x] M3. 新增 `CreateMistSprite()`，实现寒气缭绕效果。
- [x] M4. 调整主冰盾贴图生成逻辑，使中心白色区域透明。
- [x] M5. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] M6. 等待用户回传截图，确认节距、寒气效果与中心透明效果。

## 4.7 Reverse Sync: 最新视觉问题
- `用户最新反馈`:
  - `间距`: 已正确。
  - `问题 1`: 中间白色没有真正透明，只是仍然呈现亮白/灰白核心。
  - `问题 2`: 边缘锯齿感重。
  - `问题 3`: 没有明显“寒气缭绕”的感觉。
- `当前实现失败结论`:
  - `中心透明失败`: 当前 `GetCenterHollowFactor(...)` 对 alpha 的削减力度不足，而且只做了连续衰减，没有形成明确中空区。
  - `锯齿感重`: 当前主盾是先生成整张贴图再切成四象限，边缘 alpha 过硬；中心挖空后又暴露了更多锐利边缘。
  - `寒气不明显`: 当前 mist 贴图、透明度、尺度变化都过于保守，而且视觉能量被主盾本体压住。

## 4.8 Corrective Visual Plan
### 4.8.1 Strategy
- 保持位置与节距逻辑不变，不再触碰 `SlotStartWorldOffsetX` / `SlotStartWorldOffsetY` / `HealthMaskWorldSpacing`。
- 只修 3 件事：
  - 做出明确中空核心；
  - 软化边缘 alpha，降低锯齿感；
  - 明显增强寒气雾层的可见性。

### 4.8.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - `method: CreateShieldTexture(Texture2D source, bool[] backgroundMask) -> Texture2D`
    - 将中心 hollow 从“弱衰减”改成“明确挖空 + 柔边羽化”。
    - 增加边缘 alpha 羽化，避免中心透明后边缘锯齿更明显。
  - `method: GetCenterHollowFactor(Color sourceColor, int x, int y, int width, int height) -> float`
    - 改成更强的中心判定：内圈接近全透明，外圈羽化过渡。
  - `method: RemapToIcePalette(Color sourceColor, float edgeFactor) -> Color`
    - 保留冰蓝外壳，但减少核心白色残留。
  - `method: CreateMistSprite() -> Sprite`
    - 重做为更大、更虚、更层叠的雾形，不是单一细环。
  - `method: UpdateIcons(int petalCount) -> void`
    - 增强 mist 的 scale/alpha；必要时让 mist 比当前 glow 更外扩、更明显。
  - `method: ApplyHudRenderConfig(HudRenderConfig config) -> void`
    - 保持 mist 在主盾下方但高于 glow，确保可见。

### 4.8.3 Visual Rules
- `中空核心`:
  - 中心区域必须明显透黑，不能只是浅灰或浅白。
  - 中心到外壳之间必须有柔和过渡，不允许生硬圆洞。
- `边缘抗锯齿`:
  - 不要求真正 MSAA，但必须通过 alpha feather 减少像素硬边感。
  - 不允许牺牲整体轮廓清晰度。
- `寒气缭绕`:
  - 雾必须在肉眼上清楚可见。
  - 不能遮住白血 HUD；应表现为盾周围的半透明冰雾。

### 4.8.4 Execution Checklist
- [x] N1. 强化 `GetCenterHollowFactor(...)`，把核心改成明确中空。
- [x] N2. 在 `CreateShieldTexture(...)` 中加入更柔和的 alpha feather，降低边缘锯齿感。
- [x] N3. 重做 `CreateMistSprite()` 并增强 `UpdateIcons()` 中 mist 的可见度。
- [x] N4. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] N5. 等待用户回传截图，确认中空、边缘、寒气三项效果。

## 4.9 Reverse Sync: 质感仍不足
- `用户最新反馈`:
  - 仍然有明显锯齿感，不够好看。
  - 需要额外增加：
    - `边缘模糊/柔化`
    - `反光`
    - `更强的寒气缭绕`
    - 整体更有“冰晶质感”
- `当前实现不足`:
  - 现有 feather 仍主要作用于 alpha 裁切，缺少真正的“柔边 halo”，所以轮廓依旧偏硬。
  - 现有主盾只依赖单层 glow + mist，没有独立的高光/反光层，材质感不够。
  - 现有 mist 虽然增强了，但仍偏均匀，缺少围绕冰晶边缘流动的层次。

## 4.10 Material Quality Upgrade Plan
### 4.10.1 Strategy
- 保持位置、节距、中空核心逻辑不变。
- 仅提升视觉材质层次，分为三层：
  - `Soft Halo`: 负责柔边与模糊感，削弱锯齿观感。
  - `Specular Highlight`: 负责冰晶反光。
  - `Enhanced Mist`: 负责更明显、更有层次的寒气缭绕。

### 4.10.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - 新增 renderer：
    - `SpriteRenderer[] _haloRenderers`
    - `SpriteRenderer[] _highlightRenderers`
  - 新增颜色/尺度常量：
    - `HaloColor`
    - `HighlightColor`
    - `HaloBaseScale`
    - `HighlightBaseScale`
  - `method: EnsureBuilt() -> void`
    - 每个 slot 新增 `Halo` 与 `Highlight` renderer
  - `method: UpdateIcons(int petalCount) -> void`
    - halo: 保持稳定大轮廓柔光，弱呼吸，不影响定位
    - highlight: 做定向反光，高亮集中在上侧/侧缘，不遮住中空核心
    - mist: 继续增强层次，适度提高 alpha 与外扩范围
  - `method: ApplyHudRenderConfig(HudRenderConfig config) -> void`
    - 处理 halo/highlight 的 sorting 与 layer
  - 新增贴图生成方法：
    - `CreateHaloSprite() -> Sprite`
    - `CreateHighlightSprite() -> Sprite`
  - `method: CreateShieldTexture(...) -> Texture2D`
    - 增加外轮廓软化处理，减少硬边像素感

### 4.10.3 Visual Rules
- `边缘模糊`:
  - 必须通过独立 halo + alpha feather 共同完成，不能只是把主盾整体糊掉。
  - 主轮廓仍要清晰可辨，不可变成一团蓝雾。
- `反光`:
  - 必须像冰晶表面的冷色反光，集中在边缘或上半部。
  - 不得重新填实中空核心。
- `寒气缭绕`:
  - 必须比当前版本明显一档，肉眼能感知到盾周围有冷雾流动。
  - 不得盖住白血 HUD。

### 4.10.4 Implementation Checklist
- [x] O1. 为每层盾新增 `Halo` renderer，用于柔边模糊感。
- [x] O2. 为每层盾新增 `Highlight` renderer，用于冰晶反光。
- [x] O3. 重做/增强 `CreateMistSprite()` 与 `UpdateIcons()`，让寒气更有层次。
- [x] O4. 在 `CreateShieldTexture(...)` 中继续软化主轮廓边缘，降低锯齿观感。
- [x] O5. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] O6. 等待用户回传截图，确认柔边、反光、寒气三项质感升级效果。

## 4.11 Reverse Sync: 当前路线本身不对
- `用户最新反馈`:
  - 现在这版仍然“丑”，不像小骑士头上的冰球。
  - 核心问题不是单个参数，而是整体材质路线不对：
    - 没有立体感
    - 没有冰块的锐利感
    - 没有真正寒气缭绕感
- `已确认根因`:
  - 当前 HUD 冰盾仍然是**程序化拼贴贴图路线**：`IceShieldDisplay.cs` 里通过 `CreateShieldTexture()`、`CreateMistSprite()`、`CreateHaloSprite()`、`CreateHighlightSprite()` 叠层硬做。
  - 但小骑士头上的冰球并不是这套路线，而是 `OrbVisualService.cs` 中成熟的**冰球材质分层路线**：
    - `CreateIceOrbSprite()`：本体球面亮暗与冰纹
    - `AddIceOrbMaterialLayers(...)`
      - `IceHalo`
      - `IceCore`
      - `IceGloss`
      - `IceCrystal`
  - 也就是说，我前面一直在把“护盾 HUD”往一个自造的雪花/花瓣图标方向推，而用户真正想要的是**和头顶冰球同源的冰晶球质感**。
- `结论升级`:
  - 继续在 `IceShieldDisplay.cs` 里修 procedural 雪花贴图，只会继续陷入“参数越调越怪”。
  - 正确方向应改为：**HUD 冰盾直接复用或仿照 `OrbVisualService` 的冰球材质栈**，而不是继续维护当前这套自定义雪花图层系统。

## 4.12 Recommended Reimplementation Plan
### 4.12.1 Strategy
- 放弃当前 `IceShieldDisplay` 中自定义雪花/象限材质体系作为主视觉。
- 改为使用与头顶冰球一致的视觉语言：
  - 主体：`CreateIceOrbSprite()` 风格的球面/冰面纹理
  - 光晕：`IceHalo`
  - 内层冷光：`IceCore`
  - 高光：`IceGloss`
  - 锐利晶体：`IceCrystal`
  - 寒气：在此基础上补更明显但同风格的外围冰雾
- 如果仍需要表达“1/4、2/4、3/4、4/4”护盾层级，不再使用现在的四象限雪花外形，而应使用以下两种之一：
  - `方案 A`: 4 个小冰球横向排布，每个代表一层护盾（推荐，最稳定）
  - `方案 B`: 单个冰球外加 4 段环形碎晶计数（复杂，不推荐）

### 4.12.2 Recommendation
- 基于用户当前反馈，“像头上的冰球”是最清晰目标。
- 因此推荐：
  - **直接把 HUD 冰盾改成 4 个小冰球图标**
  - 材质层完全照搬/复用 `OrbVisualService` 的黑球视觉风格
  - 不再坚持当前雪花式冰盾外观

### 4.12.3 Why
- 这样能一次性解决：
  - 立体感不足
  - 冰块不够锐利
  - 寒气不自然
  - 贴图锯齿感重
- 因为头顶冰球已经是项目内**现成且用户认可**的视觉答案。

## 4.13 Current Phase Status
- 当前研究阶段已完成：
  - 已确认现有 `IceShieldDisplay.cs` 的 procedural 雪花/花瓣路线不符合用户审美目标。
  - 已确认更合适的重做方向是：HUD 冰盾改为与 `OrbVisualService.cs` 中头顶冰球同风格的 4 个小冰球。
- 当前执行状态：
  - **尚未开始重做实现**。
  - 原因不是技术阻塞，而是这已经属于新的视觉方案替换，需要用户明确确认后再进入新的实现周期。

## 4.14 User Clarification: 保留原图，不换造型
- `用户最新澄清`:
  - **不要**按头顶冰球那套直接重做成小冰球图标。
  - 冰盾 **仍然使用原本的图片/原本的外形语言**。
  - 但材质表现必须向头顶冰球靠拢，具体包括：
    - 更强的立体感
    - 更强的冰晶质感
    - 更自然、更明显的寒气缭绕感

## 4.15 Revised Art Direction
### 4.15.1 What stays the same
- 保留当前 HUD 冰盾的**原始造型来源**，不改成“4 个头顶冰球”。
- 保留当前位置、间距、数量表达逻辑。
- 保留“护盾仍由原图裁切/重映射得到”的总体前提。

### 4.15.2 What must change
- 不再把目标定义为“像一个冰球图标”。
- 新目标改为：
  - **原图造型不变**
  - **材质语言借鉴冰球**
  - 即：在原图轮廓上叠加/重建以下视觉特征：
    - 球面/晶体高光层次
    - 冷色内发光
    - 锐利冰晶边缘反光
    - 更自然的外围寒雾

### 4.15.3 Correct Implementation Direction
- `推荐方向`:
  - 继续以 `IceShieldDisplay.cs` 中的原始 shield image 为主体。
  - 但材质层设计要参考 `OrbVisualService.cs` 中黑球（冰球）的分层思路，而不是直接复用其造型。
- `具体来说`:
  - 盾本体：继续来自原 shield image
  - halo/highlight/mist/crystal-like gloss：参考冰球材质栈重新设计
  - 目标是“原图外形 + 冰球质感”，不是“把原图换成冰球”

### 4.15.4 Consequence
- 我前一条里“推荐直接改成 4 个小冰球”的方案，现已被用户明确否决。
- 后续如果继续实现，必须严格遵守新的艺术约束：
  - **只改材质，不改造型来源**。

## 4.16 Additional Visual Reference Accepted
- `用户最新补充`:
  - 可以参考一张“蓝色冰盾/冰晶壳”风格的素材质感。
  - 这进一步说明用户想要的不是球形，而是：
    - 外轮廓仍是盾/壳状
    - 但表面要有冰晶切面、亮面、厚度感、折射感

### 4.16.1 Reference Material Traits
- 该参考方向可抽象为以下材质特征：
  - `晶体分片感`: 轮廓不是软雾球，而是有几块明显的大冰晶面。
  - `中心偏亮、边缘偏深`: 形成体积感，而不是整片平均发蓝。
  - `上缘/斜边高光`: 反光更像“切面”而不是圆形 gloss。
  - `外缘冷色透光`: 边缘带一点发光和半透感，表现冰壳厚度。
  - `外围冷雾`: 雾应像贴着冰壳流动，而不是单独悬在旁边的一圈贴图。

### 4.16.2 Revised Target Style
- 最终目标风格不再是“雪花图标 + 特效补丁”。
- 也不应是“冰球直接平替”。
- 正确目标应定义为：
  - **原始冰盾外形 / 原始图片来源**
  - **叠加冰晶切面质感**
  - **叠加局部锐利高光**
  - **叠加贴边寒气流动**
  - **整体像一块小型冰晶护盾，而不是一个平面蓝色小图标**

### 4.16.3 Implementation Implication
- 下一轮如果继续实现，不能只继续增强 `halo / mist / highlight` 的数值。
- 需要把主盾本体的视觉构成改成更接近“分片晶面”的材质表达：
  - 在 shield texture remap 阶段增加 facet/plane-style 明暗分区
  - 高光应沿切面分布，而不是单一圆滑高光
  - 寒气应更贴边、更不规则

## 5. Detailed Reimplementation Plan: 冰晶护盾材质重做
### 5.1 Goal
- 在**不更换原始冰盾图片来源**的前提下，重做 `src/Visual/IceShieldDisplay.cs` 的材质表达。
- 目标结果：
  - 看起来像一块小型冰晶护盾/冰壳
  - 有切面、厚度、冷色透光、锐利高光
  - 有贴边寒气，而不是孤立的一圈雾

### 5.2 File Scope
- `File: src/Visual/IceShieldDisplay.cs`
  - 仅修改该文件
  - 不修改位置、间距、数量表达、战斗逻辑、orb 逻辑

### 5.3 Data Structures & Interfaces
- `method: CreateShieldTexture(Texture2D source, bool[] backgroundMask) -> Texture2D`
  - 责任从“基础重映射”升级为“冰晶切面材质生成”
- `method: RemapToIcePalette(Color sourceColor, float edgeFactor) -> Color`
  - 升级为：根据亮度、边缘、切面权重输出冰晶主体色
- `new method: GetFacetLightingFactor(int x, int y, int width, int height) -> float`
  - 返回晶面明暗分区权重
- `new method: GetFacetHighlightFactor(int x, int y, int width, int height) -> float`
  - 返回切面高光权重
- `new method: GetEdgeRimFactor(int x, int y, int width, int height, bool[] backgroundMask) -> float`
  - 返回边缘透光/冰壳 rim 权重
- `method: CreateMistSprite() -> Sprite`
  - 改为更贴边、更不规则的冰雾贴图
- `method: UpdateIcons(int petalCount) -> void`
  - mist/halo/highlight 的 alpha 与 scale 要匹配新的主盾材质，不再只是堆大数值

### 5.4 Material Blueprint
- `主盾本体`:
  - 保留原图轮廓
  - 在内部做 3-5 块大晶面明暗分区
  - 中心区域仍保持中空，但边缘保留冰壳厚度
- `晶面高光`:
  - 不做圆形高光
  - 改成斜向、片状、局部的亮面高光
- `边缘透光`:
  - 在外轮廓近边缘区域加入冷色 rim light
  - rim 必须细、锐、冷，不可太糊
- `寒气`:
  - mist 必须贴边
  - 轮廓应不规则、带轻微卷动感
  - alpha 比当前版本更明显，但不得把主盾外形吃掉

### 5.5 Visual Constraints
- 不允许把主盾做成圆球
- 不允许把高光做成单个椭圆白斑
- 不允许寒气变成“大蓝圈”
- 不允许重新填实中空核心
- 不允许通过单纯把所有 layer 透明度调大来冒充“质感增强”

### 5.6 Implementation Checklist
- [x] P1. 重写 `CreateShieldTexture(...)` 的主体材质逻辑，引入晶面明暗分区。
- [x] P2. 新增 `GetFacetLightingFactor(...)` / `GetFacetHighlightFactor(...)` / `GetEdgeRimFactor(...)` 等辅助权重函数。
- [x] P3. 调整 `RemapToIcePalette(...)`，让主盾主体更像冰晶外壳而非平面蓝图标。
- [x] P4. 重做 `CreateMistSprite()`，让寒气更贴边、更不规则。
- [x] P5. 调整 `UpdateIcons(...)` 中 halo / mist / highlight 的表现，使其服务于“冰晶护盾”而不是单独抢戏。
- [x] P6. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] P7. 等待用户回传截图，确认“立体感 / 冰晶锐利感 / 寒气缭绕感”三项目标是否达成。

## 5.7 Execution Mode
- 用户已明确要求：`全部实现`
- 执行策略：按批量模式一次性完成 `P1-P6`，随后停在 `P7` 等待新截图验收。

## 6. External Research: Unity 立体感 / 光影 / 雾效
### 6.1 Sources Consulted
- `Rim lighting and realtime shadows for sprites in Unity (2D) – crazybits`
  - 重点：2D sprite 的 rim light、stepped lighting、depth responsiveness
- `Ice Shader in Unity – Linden Reid`
  - 重点：transparent body + opaque edges、bump/normal-like lighting、background distortion
- `Breakdown: Creating a Crystal Shader in Unity – 80.lv`
  - 重点：cartoony crystal shader、refraction、crystal shine
- 辅助搜索：
  - sprite glow/outline shader
  - stylized smoke / mist for Unity
  - item icon depth / highlight / ambient occlusion / rim light

### 6.2 External Findings
- `结论 1：立体感主要来自主体材质，不来自外围特效堆叠`
  - 外部资料普遍强调：真正的 depth 感来自主体内部的亮暗结构、边缘不透明度控制、定向高光，而不是单纯叠很多 glow / halo / mist。

- `结论 2：冰材质通常由 4 类信息构成`
  - `opaque edges / rim light`
  - `内部 bump / facet lighting`
  - `局部高光 glint`
  - `折射/扭曲 distortion`
  - 其中 distortion 是当前实现里完全缺失的一层，而它恰恰是“像冰”的强信号之一。

- `结论 3：2D 物品想更像有厚度的物体，常见做法是“清晰主体 + 受控边缘高光”`
  - crazybits 的重点不是把物体外面套一圈特效，而是用 stepped lighting / rim lighting 让 2D sprite 自己更有 depth。
  - 这解释了为什么当前版本会显得“像特效包裹的小图标”，而不是“有材质的冰盾”。

- `结论 4：冰感的关键是“透明主体 + 更实的亮边 + 内部扰动”`
  - Linden Reid 的 ice shader 明确拆成三部分：
    - transparency with opaque white edges
    - bump / iridescent lighting
    - distortion effect behind the ice
  - 这和用户现在的反馈高度一致：当前版本缺少厚度、锐边和真正像冰的感觉。

- `结论 5：雾效应贴边、只做辅助`
  - 外围 mist 应该是贴着边缘流动的寒气，而不是单独一圈大蓝雾。
  - 雾只能增强氛围，不能替代主体材质塑造。

### 6.3 Research Outcome For This Project
- 当前 `IceShieldDisplay.cs` 的问题已经不仅是“参数不对”，而是**实现优先级不对**。
- 结合外部资料，正确优先级应是：
  1. 先把主盾做成“透明主体 + 更实的冰边 + 明确亮暗/切面分区”
  2. 再加局部 glint / rim
  3. 再加贴边冷雾
  4. 最后才考虑外围 glow
- 当前版本比较接近反过来：外围层很多，但主体材质仍不足以支撑“冰壳感”。

### 6.4 Revised Recommendation
- 如果继续下一轮实现，推荐路线应调整为：
  - 保留原图轮廓
  - 弱化当前偏抢戏的 halo / 外圈特效
  - 把重点转到：
    - 边缘更实的 opaque rim
    - 内部更清晰的 facet / bump 风格亮暗
    - 局部切面 glint
    - 轻微的背景/屏幕扭曲感（若技术栈允许）

### 6.5 Phase Constraint
- 用户本轮要求是“先搜搜、研究方向”。
- 因此当前阶段只记录研究结论，不直接继续改代码。

## 7. Detailed Design & Implementation: 主体材质优先路线
### 7.1 Goal
- 在**保留原图轮廓与当前位置/间距**的前提下，把 HUD 冰盾从“平面蓝图标 + 外围特效”改成“透明冰壳 + 锐边 + 局部高光 + 贴边寒雾”。

### 7.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - `method: CreateShieldTexture(Texture2D source, bool[] backgroundMask) -> Texture2D`
    - 重构优先级：先生成主体材质，再决定外围层。
    - 主体 alpha 策略改为：
      - 中心：透明/半透明
      - 中层：有体积感的晶面明暗
      - 边缘：更实、更亮、更清晰的冰边
    - 若当前做法会继续把主体做成“黑洞 + 蓝边圈”，则必须回退并重建 alpha 分布。
  - `method: GetCenterHollowFactor(...) -> float`
    - 调整为“中空但保留壳厚度”，避免中心挖空后只剩一圈细边。
  - `new method: GetOpaqueShellFactor(int x, int y, int width, int height, bool[] backgroundMask) -> float`
    - 负责生成冰壳厚度分布，确保边缘更实。
  - `new method: GetFacetLightingFactor(...) -> float`
    - 保留但需校正，不可让晶面分区把主体打碎成程序纹。
  - `new method: GetFacetHighlightFactor(...) -> float`
    - 改成更局部、更锐利的切面高光，而不是大面积泛白。
  - `new method: GetDistortionLikeFactor(...) -> float`
    - 若不引入真正 GrabPass/屏幕采样，则至少在本体内部模拟轻微折射扰动感，用于亮暗/色相细微扰动。
  - `method: RemapToIcePalette(...) -> Color`
    - 从“整体蓝色重映射”改为：
      - 深色冷蓝底
      - 中层青蓝体积
      - 细锐白蓝边缘
      - 局部高光而非满图亮边
  - `method: CreateHaloSprite() -> Sprite`
    - 弱化，不再抢主盾本体。
  - `method: CreateMistSprite() -> Sprite`
    - 重做为更贴边、更破碎、更不规则的低覆盖雾带。
  - `method: CreateHighlightSprite() -> Sprite`
    - 改成更像切面 glint 的尖锐高光，而不是软斑。
  - `method: UpdateIcons(int petalCount) -> void`
    - 重新平衡 glow / halo / mist / highlight：
      - 主体优先
      - halo 弱化
      - mist 贴边但不盖主体
      - highlight 更锐、更局部

### 7.3 Visual Rules
- `主体优先`:
  - 用户首先看到的应该是“冰壳本体”，不是外围特效。
- `中空但不空心`:
  - 中心可透，但不能像被打穿的黑洞。
  - 必须保留可读的壳厚度。
- `边缘清晰`:
  - 边缘要有更高不透明度和更强冷色高光，形成“实边”。
- `高光尖锐`:
  - 高光要像冰晶切面反射，细而锐，不能是大块柔白斑。
- `雾贴边`:
  - 雾只贴着轮廓走，作为辅助手段。
  - 不允许重新回到“大蓝圈”路线。

### 7.4 Implementation Checklist
- [x] Q1. 重构 `CreateShieldTexture(...)` 的 alpha 分布，让主体从“黑洞+蓝圈”回到“透明冰壳”。
- [x] Q2. 新增或重写 `GetOpaqueShellFactor(...)`，加强边缘实感与壳厚度。
- [x] Q3. 调整 facet / glint / rim 相关辅助函数，避免程序纹感过重。
- [x] Q4. 重写 `RemapToIcePalette(...)`，把主体颜色组织成更像冰壳的层次。
- [x] Q5. 重做 `CreateMistSprite()` 与 `CreateHighlightSprite()`，让雾更贴边、高光更锐利。
- [x] Q6. 调整 `UpdateIcons(...)` 中 halo / mist / highlight 的权重，弱化外围抢戏问题。
- [x] Q7. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] Q8. 等待用户回传截图，验收“主体更像冰壳、边缘更实、高光更锐、寒气更自然”。

## 7.5 Reverse Sync: 本轮主体材质重做失败结论
- `用户最新反馈`:
  - 太暗淡
  - 太扁平
  - 没有立体感
  - 没有高光
  - 没有寒气
- `结论`:
  - 当前这轮“主体材质优先”实现虽然在结构上做了很多函数拆分，但视觉结果仍然失败。
  - 问题不是单个数值，而是**整体能量分配过于保守**：
    - 主体颜色整体压得过暗；
    - 高光层既小又弱；
    - mist/halo 被刻意弱化后几乎不可感知；
    - 中心透明与主体暗色叠加后，读感变成“暗色小花”而不是“冰壳”。

### 7.5.1 Root Cause
- `主体暗淡`:
  - `RemapToIcePalette(...)` 当前基色过深，亮部提亮不足，导致主体整体发灰发暗。
- `缺少立体感`:
  - facet lighting 还停留在程序权重层，视觉上没有形成清晰的大明暗面。
- `高光缺失`:
  - `CreateHighlightSprite()` 和 `UpdateIcons(...)` 中的 highlight alpha/scale/位置都过于保守。
- `寒气缺失`:
  - `CreateMistSprite()` 与 `UpdateIcons(...)` 中 mist 的 alpha 被压得太低，而且贴边得太紧，导致肉眼难以察觉。

### 7.5.2 Next Direction
- 下一轮如果继续，不应再优先做“结构性新增函数”，而应直接提高视觉能量：
  - 主体整体提亮；
  - 拉大明暗面对比；
  - 明显增强高光可见度；
  - 明显增强寒气层可见度；
  - 保证用户一眼就能读出“冰晶质感”，再谈细节。

## 7.6 Reverse Sync: 基于最新截图的确认失败点
- `用户最新反馈`:
  - 太暗淡太扁平
  - 没有任何立体感
  - 没有高光
  - 没有寒气
- `截图复核结论`:
  - 位置与间距本轮不再是主要问题，当前失败集中在材质可读性。
  - 现在的 HUD 冰盾第一眼读感更像“偏暗的小蓝花/小蓝块”，而不是“有厚度的小型冰晶护盾”。
  - 这说明当前版本虽然保留了原图造型来源，但没有把“亮面 / 暗面 / 锐边 / 外逸寒气”建立起来。

### 7.6.1 Confirmed Visual Gaps
- `主体太暗`:
  - `RemapToIcePalette(...)` 当前整体亮度区间过低，亮部面积也不够，导致第一眼不亮、不冷。
- `体积面不清楚`:
  - `GetFacetLightingFactor(...)` 产生了程序化亮暗权重，但没有形成足够明确的大晶面读感。
- `高光太弱`:
  - `GetFacetHighlightFactor(...)` 与 `CreateHighlightSprite()` 都偏保守，缺少能一眼读出来的硬一点的白青高光切线。
- `寒气太弱`:
  - `CreateMistSprite()` 与 `UpdateIcons(...)` 里的 mist 贴边过紧、alpha 偏低、外扩不足，视觉上几乎不可感知。

## 7.7 Approved Next Pass: 提亮主体 + 强高光 + 强寒气
### 7.7.1 Goal
- 在**不改变现有位置/间距、不替换原图造型来源、不改成头顶冰球造型**的前提下，直接提高 HUD 冰盾的视觉能量。
- 目标结果：
  - 主体更亮；
  - 明暗面更明显；
  - 有清晰可见的锐利高光；
  - 有肉眼可见的冷雾外逸感；
  - 整体更像“发亮的小冰晶护盾”，而不是暗色小图标。

### 7.7.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - `method: RemapToIcePalette(...) -> Color`
    - 明显抬高主体中亮部亮度，减少整体发灰发暗。
    - 拉大深蓝阴影面与青白亮面之间的对比，形成更清晰的体积关系。
  - `method: GetFacetLightingFactor(...) -> float`
    - 调整为更少但更清晰的大晶面，不再追求细碎程序纹。
  - `method: GetFacetHighlightFactor(...) -> float`
    - 提高高光命中区域与强度，让主体内部与上缘出现可读的冰晶 glint。
  - `method: CreateHighlightSprite() -> Sprite`
    - 改成更亮、更锐、更偏切线式的高光贴图，而不是过于柔弱的细纹。
  - `method: CreateMistSprite() -> Sprite`
    - 增加更外扩、更轻薄、稍带破碎感的雾带，让寒气从轮廓外侧逸出。
  - `method: UpdateIcons(int petalCount) -> void`
    - 提高 highlight / mist 的 alpha 与存在感。
    - 适度扩大 mist/highlight 尺度与偏移，但不改变 slot 定位。
    - 继续弱化“外围一大圈”的观感，保持主体主导。

### 7.7.3 Visual Rules
- `位置冻结`:
  - 不修改 `SlotStartWorldOffsetX` / `SlotStartWorldOffsetY` / `HealthMaskWorldSpacing`。
- `造型冻结`:
  - 不替换 shield 原始图片来源，不改成球形图标。
- `主体优先`:
  - 提升视觉能量必须优先落在主盾本体，而不是只把 halo/glow 调大。
- `高光必须可见`:
  - 新版本中，玩家在正常 HUD 尺寸下一眼应能看见至少 1-2 条明确高光。
- `寒气必须可见`:
  - 新版本中，mist 必须在静态截图里也能直接看出来，而不是靠脑补。

### 7.7.4 Implementation Checklist
- [ ] R1. 提亮 `RemapToIcePalette(...)` 的主体亮度，并拉大冷暖/明暗层次。
- [ ] R2. 重整 `GetFacetLightingFactor(...)`，让大晶面更清晰，减少“平”和“脏”的读感。
- [ ] R3. 强化 `GetFacetHighlightFactor(...)` 与 `CreateHighlightSprite()`，让高光真正出现。
- [ ] R4. 重做 `CreateMistSprite()` 并提高 `UpdateIcons(...)` 中 mist 的可见度，让寒气更明显。
- [ ] R5. 调整 `UpdateIcons(...)` 中主体 / halo / highlight / mist 的权重平衡，确保主体仍是视觉中心。
- [ ] R6. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] R7. 等待用户回传截图，验收“更亮 / 更立体 / 有高光 / 有寒气”四项目标。

## 7.8 Reverse Sync: 新截图后的剩余问题
- `用户最新反馈`:
  - 好一点了；
  - 但主要还是色调太淡；
  - 没有立体感。
- `截图复核结论`:
  - 当前版本比上一轮更亮、更干净，但主体被整体抬亮后，反而更像“浅蓝小图标”，不像“有厚度的小冰壳”。
  - 这说明当前失败点已经从“太暗”转移为“亮暗结构不够强”：
    - 暗面不够深；
    - 亮面面积太大；
    - 高光不够硬；
    - 雾层仍在稀释主体体积感。

### 7.8.1 Confirmed Visual Gaps
- `色调太淡`:
  - `RemapToIcePalette(...)` 目前让 `bodyIce / crystalIce / frostIce` 覆盖面积过大，主体缺少更深、更饱和的冷蓝阴影面。
- `立体感不足`:
  - `GetFacetLightingFactor(...)` 虽然已经有 stepped lighting，但明暗面分区仍偏平均，没有形成明显的“上亮下暗 / 亮切面 vs 深壳面”。
- `高光仍偏软`:
  - `GetFacetHighlightFactor(...)` 与 `CreateHighlightSprite()` 已出现高光，但仍偏薄、偏软，不能真正压出冰晶切面。
- `寒气抢掉主体`:
  - mist 虽然可见了，但如果继续作为一层普遍浅蓝覆盖，会进一步冲淡主体的暗面与厚度感。

## 7.9 Approved Next Pass: 压深暗面 + 收窄亮面 + 硬化高光
### 7.9.1 Goal
- 在**不改位置/间距、不改原图轮廓来源、不改象限逻辑**的前提下，把 HUD 冰盾从“浅蓝发光图标”推成“有深浅切面的冰壳”。
- 目标结果：
  - 主体不再发淡；
  - 暗面明显更深；
  - 亮面更集中、更像切面受光；
  - 高光更硬、更白、更可读；
  - 寒气退回辅助角色，不再稀释主体。

### 7.9.2 Exact File Changes
- `File: src/Visual/IceShieldDisplay.cs`
  - `method: RemapToIcePalette(...) -> Color`
    - 压低亮色覆盖率，恢复更深的主体冷蓝阴影面。
    - 让青白亮面只落在少数真正受光的晶面与边缘，而不是整片泛亮。
  - `method: GetFacetLightingFactor(...) -> float`
    - 进一步减少细碎程序纹，强化少数大面之间的亮暗分离。
    - 明确做出“亮切面 / 深壳面 / 下缘阴影”的关系。
  - `method: CreateShieldTexture(...) -> Texture2D`
    - 重新平衡 shell opacity / body alpha floor，让主体有厚度但不泛白。
  - `method: GetFacetHighlightFactor(...) -> float`
    - 收窄高光命中区域，提高切线高光的锐利度。
  - `method: CreateHighlightSprite() -> Sprite`
    - 改成更硬、更白、更局部的 glint，而不是轻薄柔线。
  - `method: CreateMistSprite() -> Sprite`
    - 把雾更多放到外缘和外逸区域，减少贴着主体大面积泛浅蓝。
  - `method: UpdateIcons(int petalCount) -> void`
    - 进一步压低 halo/mist 对主体的洗白作用；
    - 让 highlight 更聚焦；
    - 保持 mist 可见但让主体成为绝对视觉中心。

### 7.9.3 Visual Rules
- `不要再整体提亮`:
  - 本轮不能再通过“整体更亮”解决问题，必须通过更深暗面和更窄亮面建立体积感。
- `暗面必须存在`:
  - 至少要让主体一眼看出存在深壳面，而不是全图平均浅蓝。
- `高光必须像切面`:
  - 高光要更白、更硬、更局部，不允许继续做成柔和发丝线。
- `雾必须退到辅助位`:
  - 雾可以看见，但不能继续冲淡主体颜色和对比。

### 7.9.4 Implementation Checklist
- [ ] S1. 调整 `RemapToIcePalette(...)`，压深阴影面并收窄亮色覆盖率。
- [ ] S2. 重整 `GetFacetLightingFactor(...)`，强化大面明暗分离。
- [ ] S3. 调整 `CreateShieldTexture(...)` 的 alpha/opacity 分布，避免主体再次被洗淡。
- [ ] S4. 强化 `GetFacetHighlightFactor(...)` 与 `CreateHighlightSprite()`，让高光更硬更白。
- [ ] S5. 调整 `CreateMistSprite()` 与 `UpdateIcons(...)`，让 mist 保持可见但退到主体之后。
- [ ] S6. 执行 `dotnet build "DeVect.csproj"`，确认 0 error 并验证自动部署时间戳更新。
- [ ] S7. 等待用户回传截图，验收“色调不淡 / 主体有体积 / 高光更硬”三项目标。

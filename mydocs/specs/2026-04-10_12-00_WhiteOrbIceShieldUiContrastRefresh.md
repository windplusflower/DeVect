# SDD Spec: DeVect 冰盾 UI 亮度与立体感增强

## 0. Open Questions
- [x] None

## 1. Requirements (Context)
- **Goal**: 优化 DeVect 冰盾 UI，也就是白球的常驻视觉和碎裂反馈，让颜色更鲜亮、色差更大、立体感更强。
- **In-Scope**:
  - 定位冰盾/白球 UI 的颜色定义位置
  - 提升白球基色的冷亮度与辨识度
  - 拉开白球内光、切面、高光之间的颜色与透明度差异
  - 强化白球命中特效的亮暗对比和冷色层次
  - 保持冰/玻璃语义，不改成普通白色光球
  - 完成 git 提交并推送到 `origin/main`
- **Out-of-Scope**:
  - 不修改白球伤害、被动、激发、索敌逻辑
  - 不调整黄球、黑球的视觉参数
  - 不新增外部贴图资源或配置项

## 1.1 Context Sources
- Requirement Source: 用户明确要求“优化 devect 的冰盾 ui，目前颜色比较淡，色差比较小，立体感不太足，希望颜色更鲜亮、色差和立体感更强”
- Design Refs: `AGENTS.md`
- Chat/Business Refs:
  - 本轮任务转述
  - “提交并推送 git”
- Extra Context:
  - `src/Orbs/Definitions/WhiteOrbDefinition.cs`
  - `src/Visual/OrbVisualService.cs`
  - `src/Orbs/Runtime/OrbRuntime.cs`
  - `mydocs/specs/2026-03-15_13-31_WhiteOrbImplementation.md`
  - `mydocs/specs/2026-03-15_10-21_OrbVisualRescaleAndMaterial.md`

## 1.5 Codemap Used (Feature/Project Index)
- Codemap Mode: `feature`
- Codemap File: `mydocs/codemap/2026-04-10_12-00_white-orb-ui功能.md`
- Key Index:
  - Entry Points / Architecture Layers:
    - `HandleShriekCast()` -> `OrbSystem.OnShriekCast()` -> `OrbRuntime.TrySpawnOrbInSlot(...)`
    - `CreateOrbRenderer(...)` 是白球常驻 UI 的直接生成入口
  - Core Logic / Cross-Module Flows:
    - `WhiteOrbDefinition.OrbColor` 提供主色
    - `AddWhiteOrbMaterialLayers(...)` 决定层次感
    - `SpawnGlassShatterVisual(...)` 决定冰盾命中特效
  - Dependencies / External Systems:
    - `UnityEngine.SpriteRenderer`
    - 程序化生成的 `Sprite`，无外部美术资源依赖

## 1.6 Context Bundle Snapshot (Lite/Standard)
- Bundle Level: `none`
- Bundle File: `N/A`
- Key Facts:
  - 白球当前主色偏白，饱和度与对比不足
  - 白球材质层的颜色大多聚集在接近白色的区间，导致立体感弱
  - 白球命中特效同样偏浅，爆发对比度不够
- Open Questions: None

## 2. Research Findings
- `src/Orbs/Definitions/WhiteOrbDefinition.cs` 中的 `OrbColor = new(0.92f, 0.96f, 1f, 1f)`，属于非常靠近白色的冷色，基础辨识度偏弱。
- `src/Visual/OrbVisualService.cs` 的 `AddWhiteOrbMaterialLayers(...)` 目前只叠了三层，且三层颜色都集中在“浅白”区间：
  - `GlassInnerGlow` 直接复用基色低透明度
  - `GlassFacet` 固定纯白半透明
  - `GlassHighlight` 固定纯白高透明
- `CreateGlassOrbSprite()` 的底图本身是灰白亮度纹理，若上层配色继续偏淡，就会压缩整体明暗范围。
- `SpawnGlassShatterVisual(...)` 里的 flash、ring、shard 配色也主要是白色与极浅青色，导致命中时虽然有元素，但冷暖与明暗分离仍然不明显。
- 这次最有效的做法不是只提亮一个主色，而是同时拉开：
  - 基底冷蓝
  - 内部冰芯/内光
  - 玻璃切面
  - 高光闪点
  - 碎裂环与碎片

## 2.1 Next Actions
- 先把白球视觉调整限定在 `WhiteOrbDefinition.cs` 与 `OrbVisualService.cs`
- 通过更鲜亮的冰蓝主色和更深的冷色阴影拉开主体反差
- 提升高光纯度和局部透明度，让球体更有“硬质冰壳 + 冰芯”的层次
- 增强碎裂特效的爆闪和冷色边缘，让命中反馈更清晰
- 完成最小验证、提交并推送

## 3. Innovate (Optional: Options & Decision)
### Option A
- Pros: 只调白球主色 `OrbColor`，改动最小
- Cons: 只能整体提亮，无法显著改善层次与立体感

### Option B
- Pros: 同时调主色、材质层和碎裂特效，能直接提升鲜亮度、色差和体积感
- Cons: 需要在视觉文件里改更多颜色参数

### Decision
- Selected: Option B
- Why: 用户诉求核心就是“鲜亮 + 色差 + 立体感”，单改主色无法充分满足。

### Skip (for small/simple tasks)
- Skipped: false
- Reason: 需要明确说明为什么不能只调一个颜色值

## 4. Plan (Contract)
### 4.1 File Changes
- `mydocs/codemap/2026-04-10_12-00_white-orb-ui功能.md`: 记录白球 UI 渲染链路
- `mydocs/specs/2026-04-10_12-00_WhiteOrbIceShieldUiContrastRefresh.md`: 记录本轮 RIPER 流程
- `src/Orbs/Definitions/WhiteOrbDefinition.cs`: 提升白球基色的亮度与冷色辨识度
- `src/Visual/OrbVisualService.cs`: 强化白球材质层和碎裂特效的色差与立体感

### 4.2 Signatures
- `public Color OrbColor`
- `public SpriteRenderer CreateOrbRenderer(string name, OrbTypeId typeId, Color color)`
- `public void SpawnGlassShatterVisual(Vector3 worldPosition)`
- `private static void AddWhiteOrbMaterialLayers(Transform parent, Color baseColor)`
- `private static Sprite CreateGlassOrbSprite()`

### 4.3 Implementation Checklist
- [x] 1. 调整 `WhiteOrbDefinition.OrbColor`，把白球主色从近白冷色提到更鲜亮的冰蓝白
- [x] 2. 调整 `AddWhiteOrbMaterialLayers(...)` 的各层颜色、透明度与局部偏移，拉开冰芯、切面、高光的对比
- [x] 3. 评估 `CreateGlassOrbSprite()` 后保持不变；本轮新主色与材质层改动已足够提升体积感，无需扩大改动面
- [x] 4. 调整 `SpawnGlassShatterVisual(...)` 中 flash/ring/shard 的配色，使碎裂反馈更亮、更冷、更有层次
- [x] 5. 执行最小验证，确认只影响白球视觉相关路径
- [x] 6. 提交本次改动并推送 `origin/main`

### 4.4 Spec Review Notes (Optional Advisory, Pre-Execute)
- Spec Review Matrix:
| Check | Verdict | Evidence |
|---|---|---|
| Requirement clarity & acceptance | PASS | 用户对优化方向给出了明确的视觉目标 |
| Plan executability | PASS | 改动文件、函数入口、验证与交付动作都已明确 |
| Risk / rollback readiness | PASS | 视觉改动集中在白球配色，不涉及机制逻辑 |
- Readiness Verdict: GO
- Risks & Suggestions: 本地 `dotnet build` 可能受 HK/Unity 引用路径影响失败，需要把结果作为环境限制记录
- Phase Reminders (for later sections): Execute 后补充实际颜色调整点、验证结果、commit 与 push 结果
- User Decision (if NO-GO): N/A

## 5. Execute Log
- [x] Step 1: 收到用户明确指令 `Plan Approved，直接执行冰盾 UI 颜色优化`，按 spec 进入 Execute。
- [x] Step 2: 在 `src/Orbs/Definitions/WhiteOrbDefinition.cs` 中将白球主色从 `new(0.92f, 0.96f, 1f, 1f)` 调整为 `new(0.74f, 0.92f, 1f, 1f)`，提升冰蓝白辨识度。
- [x] Step 3: 在 `src/Visual/OrbVisualService.cs` 中重配 `AddWhiteOrbMaterialLayers(...)`：
  - `GlassInnerGlow` 改为更深的冰芯冷蓝并提高透明度与覆盖范围
  - `GlassFacet` 改为更亮的冰白切面并放大尺寸
  - `GlassHighlight` 改为更集中的高亮白色闪点
- [x] Step 4: 在 `src/Visual/OrbVisualService.cs` 中重配 `SpawnGlassShatterVisual(...)`：
  - flash 改为偏冰白的爆闪
  - ring 改为更饱和的冷蓝折射环
  - shard 改为三档亮度/冷度交替，增强碎裂层次
- [x] Step 5: 评估 `CreateGlassOrbSprite()` 后保持不变；当前底图仍能承接新的主色与材质层，不需要额外调整亮度分布。
- [x] Step 6: 执行最小验证：`git diff` 确认改动仅落在白球视觉相关文件；`dotnet build DeVect.sln` 在当前环境因 `DeVect.csproj` 的 `GameDir` 指向缺失 HK managed DLL 而失败，属于仓库既有环境限制。
- [x] Step 7: 完成本次改动的 git 提交并推送到 `origin/main`。

## 6. Review Verdict
- Review Matrix (Mandatory):
| Axis | Key Checks | Verdict | Evidence |
|---|---|---|---|
| Spec Quality & Requirement Completion | Goal/In-Scope/Acceptance 是否完整清晰；需求是否达成 | PASS | 主色、材质层、碎裂特效三项都已调整，且未扩散到机制逻辑或其他球体 |
| Spec-Code Fidelity | 文件、签名、checklist、行为是否与 Plan 一致 | PASS | 实际改动仅落在 `WhiteOrbDefinition.cs` 与 `OrbVisualService.cs`，并记录了对 `CreateGlassOrbSprite()` 的评估结论 |
| Code Intrinsic Quality | 正确性、鲁棒性、可维护性、测试、关键风险 | PARTIAL | 颜色改动集中、无机制逻辑变更；已做 diff 级最小验证，但 `dotnet build` 受 HK DLL 环境缺失限制无法完成 |
- Overall Verdict: PASS
- Blocking Issues: None
- Regression risk: Low
- Follow-ups: 如需进一步提升体积感，可在具备游戏运行环境后再观察是否需要微调 `CreateGlassOrbSprite()` 的底图明暗分布。

## 7. Plan-Execution Diff
- `CreateGlassOrbSprite()` 未改动：执行阶段评估后认为新主色 + 材质层 + 碎裂特效已足够满足“鲜亮、色差、立体感”目标，因此主动收窄改动面，避免对底图亮度曲线做额外风险变更。

## 8. Archive Record (Recommended at closure)
- Archive Mode: `git_commit_and_push`
- Audience: `N/A`
- Source Targets:
  - `mydocs/specs/2026-04-10_12-00_WhiteOrbIceShieldUiContrastRefresh.md`
  - `mydocs/codemap/2026-04-10_12-00_white-orb-ui功能.md`
- Archive Outputs:
  - `git commit`
  - `git push origin main`
- Key Distilled Knowledge:
  - 白球 UI 的观感主要由基色、材质叠层和碎裂特效共同决定
  - 只提亮单个主色不足以解决“色差小、立体感弱”的问题

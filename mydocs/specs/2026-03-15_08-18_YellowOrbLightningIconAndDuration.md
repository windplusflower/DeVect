# SDD Spec: 黄球闪电图标缩放、显示时长与索敌范围调试

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 确认黄球触发时显示的闪电图标是否来自项目内 `assets/闪电.png`；如果当前不是，则切换为该图片，并延长图标可见时长，确保玩家能清楚看见；同时为黄球索敌范围增加一个可随时关闭的红色调试框，便于调试范围参数；并根据调试反馈继续缩小闪电图标尺寸与索敌范围，提升观察便利性。
- **In-Scope**:
  - 核查当前黄球闪电特效的图标来源。
  - 若当前为运行时代码绘制图标，则改为使用 `assets/闪电.png`。
  - 调整闪电图标短暂特效的显示时长，提升可辨识度。
  - 如采用外部图片资源，补齐工程构建/安装链路，确保 mod 安装后可读取该资源。
  - 为黄球索敌范围添加红色可视化调试框。
  - 调试框必须能通过常量或单一开关快速关闭，满足“临时调试”诉求。
  - 将黄球闪电贴图视觉尺寸缩小到当前的一半。
  - 临时缩小黄球索敌范围，优先保证调试红框能在常见战斗视野中观察到。
- **Out-of-Scope**:
  - 不调整黄球伤害、触发条件、索敌逻辑。
  - 不修改非闪电图标相关的 UI 或其他球种视觉。
  - 不重做整套资源管理系统。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `src/Visual/OrbVisualService.cs`: 黄球闪电特效的唯一实现位置；当前 `CreateLightningSprite()` 通过 `Texture2D` + 像素绘制在运行时生成图标，不是读取 `assets/闪电.png`。
  - `src/Visual/OrbVisualService.cs`: `SpawnLightningVisual(Vector3 worldPosition)` 负责实例化闪电特效对象，并通过 `LightningLifetime = 0.22f` 控制显示时长。
  - `src/Visual/OrbVisualService.cs`: `LightningScale = 0.65f` 控制闪电图标世界空间尺寸，若要把贴图视觉缩小一半，应优先调整该常量而非改图片资源本体。
  - `src/Combat/OrbCombatService.cs`: `TryPickRandomEnemyInRange(HeroController hero)` 使用 `Physics2D.OverlapBoxNonAlloc(...)` 以 Hero 当前位置为中心做矩形索敌；当前没有任何调试可视化。
  - `src/Combat/OrbCombatService.cs`: `EnemySearchHalfWidth = 20f`、`EnemySearchHalfHeight = 10f` 是索敌范围的真实参数来源，调试框必须和这两个常量严格一致。
- **Entry Points**:
  - `src/Combat/OrbCombatService.cs`: 负责黄球命中敌人的战斗流程，命中后会联动视觉层生成闪电特效。
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 黄球被动/激发能力定义，间接触发战斗与视觉表现。
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: `OnPassive(...)` 与 `OnEvocation(...)` 都会进入索敌逻辑，是观察调试框的主要触发场景。
- **Data Models**:
  - `src/Visual/TransientVisual.cs`: 短生命周期视觉对象模型，持有生命周期、位移和透明度衰减参数。
- **Dependencies**:
  - `assets/闪电.png`: 项目内现成闪电图片资源，目标是作为黄球闪电图标来源。
  - `UnityEngine.Texture2D` / `Sprite` / `SpriteRenderer`: 图片解码与特效渲染依赖。
  - `UnityEngine.Debug.DrawLine(...)`: 可用于绘制仅调试可见、不参与游戏逻辑的红色范围框，适合“临时开关”诉求。
  - `DeVect.csproj`: 若资源文件需要随构建产物进入安装目录，需在此补充复制规则。
- **Observed Reality**:
  - 当前黄球闪电图标不是来自 `assets/闪电.png`，而是 `src/Visual/OrbVisualService.cs` 内部手绘折线像素图。
  - 当前显示时长为 `0.22f` 秒，偏短，符合用户“看不清”的反馈。
  - 执行期构建验证发现：当前工程未直接暴露 `Texture2D.LoadImage(...)`，PNG 运行时解码需改用 `UnityEngine.ImageConversion.LoadImage(...)`，并补充 `UnityEngine.ImageConversionModule` 引用。
  - 当前项目没有 `Debug.DrawLine`、`OnDrawGizmos` 或其他范围调试绘制实现，因此需要新增最小调试可视化方案。
- 用户最新反馈表明：当前闪电贴图仍偏大，且索敌框可能因为范围过大超出常见视野，需要继续缩小 `LightningScale` 与索敌矩形尺寸。
- 最新实测反馈表明：`Debug.DrawLine(...)` 方案在当前 Hollow Knight mod 运行环境下对玩家不可见，说明它不适合作为游戏内调试可视化手段。
- 最新实测反馈表明：即使 `EnemySearchHalfWidth` 已缩小到 `8f`，当“敌人与小骑士水平距离为 21”时仍可能吃到激发伤害，说明当前索敌判定并不等价于“目标 Transform 与 Hero Transform 的水平距离阈值”。
- 从实现看，当前索敌依据是 `Physics2D.OverlapBoxNonAlloc(hero.transform.position, EnemySearchBoxSize, 0f, ...)` 与敌方 `Collider2D` 的重叠关系，而不是 `HealthManager.transform.position` 到 Hero 的点距离；因此只要敌方任一碰撞体的一部分进入盒体，就会被纳入候选。
- 最新调试反馈表明：当前可视化与严格过滤机制已生效，用户现在希望把范围改成非对称矩形：左 `15`、右 `15`、上 `15`、下 `5`。

## 2. Architecture (Optional - Populated in INNOVATE)
- 本任务预计不需要进入 INNOVATE；保持现有视觉服务结构，仅替换图标来源并微调生命周期参数。
- 对索敌范围调试，优先采用 `OrbCombatService` 内部的轻量调试绘制：由单个布尔常量控制，在每次索敌前以 `Debug.DrawLine(...)` 绘制红色矩形边界，不引入额外 GameObject，不污染正式逻辑。
- 基于最新反馈，上述 `Debug.DrawLine(...)` 路线判定为无效，需要在后续 PLAN 中改为“游戏内可见”的运行时可视化方案，例如临时 `GameObject + LineRenderer/SpriteRenderer` 方案，并同样保持单点开关。
- 基于最新反馈，后续若用户要把“索敌范围”理解为敌人与 Hero 的点距离限制，则需要把判定从“Collider 进入 OverlapBox”升级为“先候选，再按 `HealthManager.transform.position` / `Collider.bounds.center` 做二次距离过滤”；该调整将改变当前战斗语义，必须先写入新 Plan 再改代码。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Visual/OrbVisualService.cs`
  - 保留方法签名：
    - `public void SpawnLightningVisual(Vector3 worldPosition)`
    - `private static Sprite CreateLightningSprite()`
  - 调整常量：
    - `private const float LightningLifetime = 0.45f;`
      - 设计意图：从 `0.22f` 提升到 `0.45f`，在不显著拖影的前提下将可视时间提升到约 2 倍。
    - `private const float LightningScale = 0.325f;`
      - 设计意图：在当前 `0.65f` 的基础上缩小到一半，直接降低闪电贴图显示尺寸。
  - 新增/调整静态字段：
    - `private static Texture2D? _lightningTexture;`
    - `private static Sprite? _lightningSprite;`
  - 新增私有方法签名：
    - `private static string GetLightningAssetPath()`
    - `private static Texture2D LoadLightningTexture()`
  - 实现约束：
    - `CreateLightningSprite()` 不再手绘像素闪电折线。
    - `CreateLightningSprite()` 必须从 `assets/闪电.png` 读取图片并生成 `Sprite`。
    - 锚点维持底部中心（`new Vector2(0.5f, 0f)`），避免特效飘移位置变化。
    - 若图片读取失败，应保留明确异常信息，不能静默吞错。
    - `SpawnLightningVisual(...)` 的调用方和外部接口不变，避免影响黄球战斗逻辑。

- `File: DeVect.csproj`
  - 新增资源声明：
    - 将 `assets\闪电.png` 作为内容文件纳入构建输出。
  - 调整安装目标：
    - 在现有 `InstallMod` 目标中，复制 `assets\闪电.png` 到 `$(InstallDir)\assets\闪电.png`。
  - 新增程序集引用：
    - `UnityEngine.ImageConversionModule`
  - 实现约束：
    - 构建后 mod 安装目录必须包含 `assets/闪电.png`，以支持运行时通过磁盘路径加载。
    - 不引入新的 NuGet 包或额外构建脚本。

- `File: src/Combat/OrbCombatService.cs`
  - 保留方法签名：
    - `public HealthManager? TryPickRandomEnemyInRange(HeroController hero)`
    - `public void TickDebugVisuals()`
    - `public void DisposeDebugVisuals()`
  - 新增常量/字段：
    - `private const bool DrawEnemySearchDebugBox = true;`
    - `private static readonly Color EnemySearchDebugColor = new(1f, 0.1f, 0.1f, 1f);`
    - `private const float EnemySearchDebugDuration = 0.12f;`
    - `private const float EnemySearchLeftRange = 15f;`
    - `private const float EnemySearchRightRange = 15f;`
    - `private const float EnemySearchUpRange = 15f;`
    - `private const float EnemySearchDownRange = 5f;`
    - `private const bool UseStrictTargetDistanceFilter = true;`
    - `private static GameObject? _enemySearchDebugRoot;`
    - `private static LineRenderer? _enemySearchDebugRenderer;`
    - `private static float _enemySearchDebugVisibleUntil;`
  - 新增私有方法签名：
    - `private static void DrawEnemySearchBounds(Vector3 center)`
    - `private static void EnsureEnemySearchDebugRenderer()`
    - `private static void UpdateEnemySearchDebugRenderer(Vector3 center)`
    - `private static void TickEnemySearchDebugRenderer()`
    - `private static bool IsTargetWithinStrictRange(HeroController hero, HealthManager target)`
  - 实现约束：
    - 调试框必须严格使用左右/上下四个方向范围常量生成，确保与实际索敌范围一致。
    - 调试框绘制逻辑只服务调试，不影响索敌命中、伤害、随机选敌结果。
    - 调试开关必须集中在单一常量 `DrawEnemySearchDebugBox`，后续关闭时只改一处即可。
    - 旧的 `Debug.DrawLine(...)` 方案作废，改为 `GameObject + LineRenderer` 的游戏内可见红色矩形线框方案。
    - 调试框对象必须复用，不在每次索敌时重复创建。
    - 本轮将索敌范围改为非对称矩形：左 `15`、右 `15`、上 `15`、下 `5`。
    - `OverlapBoxNonAlloc(...)` 的中心与尺寸必须与上述非对称矩形一致，不能再使用对称半径盒。
    - 在 `OverlapBoxNonAlloc(...)` 找到候选敌人后，若 `UseStrictTargetDistanceFilter = true`，则必须再按 `target.transform.position` 相对 Hero 的水平/垂直偏移做二次过滤；超出左/右/上/下任一方向阈值的目标不得进入候选列表。

- `File: src/Orbs/OrbSystem.cs`
  - 保留方法签名：
    - `public void OnHeroUpdate(HeroController hero, float deltaTime)`
    - `public void OnSceneChanged()`
    - `public void OnShutdown()`
    - `public void ResetAll()`
  - 实现约束：
    - `OnHeroUpdate(...)` 必须在现有 Runtime tick 之外调用 `_combatService.TickDebugVisuals()`，用于隐藏过期调试框。
    - `OnSceneChanged()`、`OnShutdown()`、`ResetAll()` 必须调用 `_combatService.DisposeDebugVisuals()`，避免场景切换或退出后残留临时调试对象。

### 3.2 Implementation Checklist
- [x] 1. 更新 `mydocs/specs/2026-03-15_08-18_YellowOrbLightningIconAndDuration.md`，固定本次文件变更、资源加载方式和生命周期目标值。
- [x] 2. 修改 `DeVect.csproj`，把 `assets/闪电.png` 纳入输出并在 `InstallMod` 期间复制到 mod 安装目录。
- [x] 3. 修改 `DeVect.csproj`，补充 `UnityEngine.ImageConversionModule` 引用，支持 PNG 运行时解码。
- [x] 4. 修改 `src/Visual/OrbVisualService.cs`，移除运行时手绘闪电逻辑，改为从 `assets/闪电.png` 加载 `Texture2D` 并创建缓存 `Sprite`。
- [x] 5. 修改 `src/Visual/OrbVisualService.cs` 中的 `LightningLifetime` 为 `0.45f`，保持其余透明度衰减与位移动画逻辑不变。
- [x] 6. 构建工程，验证编译通过且资源复制链路生效。
- [x] 7. 复查 `src/Orbs/Definitions/YellowOrbDefinition.cs` 与 `src/Combat/OrbCombatService.cs` 的调用面，确认不需要接口级改动。
- [x] 8. 修改 `src/Combat/OrbCombatService.cs`，加入红色索敌范围调试框与单点开关。
- [x] 9. 构建工程，验证调试框改动编译通过。
- [x] 10. 修改 `src/Visual/OrbVisualService.cs`，把闪电贴图视觉缩放调整为当前的一半。
- [x] 11. 修改 `src/Combat/OrbCombatService.cs`，将索敌范围临时缩小到更容易在屏幕内观察的尺寸，并保持调试框同步。
- [x] 12. 构建工程，验证缩放与范围调整编译通过。
- [x] 13. 修改 `src/Combat/OrbCombatService.cs`，将不可见的 `Debug.DrawLine(...)` 调试框替换为游戏内可见的 `LineRenderer` 红色矩形框，并保持单点开关。
- [x] 14. 修改 `src/Combat/OrbCombatService.cs`，在 `OverlapBox` 候选之后增加严格矩形二次过滤，修复“水平距离为 21 仍可能命中”的问题。
- [x] 15. 构建工程，验证新的调试框与严格过滤编译通过。
- [x] 16. 修改 `src/Combat/OrbCombatService.cs`，把索敌范围与红框改为左 `15`、右 `15`、上 `15`、下 `5` 的非对称矩形。
- [x] 17. 构建工程，验证非对称矩形范围调整编译通过。

### 3.3 Verification Criteria
- 黄球闪电特效使用的图片来源应为 `assets/闪电.png`，而非代码绘制折线。
- `SpawnLightningVisual(...)` 外部行为保持兼容：调用方无需修改。
- 闪电图标在命中后停留时间明显长于当前实现，目标值为 `0.45f` 秒。
- 构建完成后，mod 安装目录存在 `assets/闪电.png`，避免运行时找不到资源。
- 黄球每次执行索敌时，Hero 周围会短暂显示一个红色矩形框，尺寸与实际索敌范围一致。
- 将 `DrawEnemySearchDebugBox` 改为 `false` 后，不需要删除代码即可关闭调试框。
- 黄球闪电图标世界尺寸为之前的一半，不改贴图资源本体。
- 索敌范围临时缩小后，红框应更容易完整落在常见可视区域内。
- 即使敌方碰撞体边缘进入 `OverlapBox`，只要敌人 `transform.position` 的水平偏移大于 `8f` 或垂直偏移大于 `4.5f`，也不能吃到黄球激发伤害。
- 调试框必须在实际游戏画面中可见，而不是仅编辑器调试可见。
- 红框与实际索敌范围必须一致，新的目标矩形为：左 `15`、右 `15`、上 `15`、下 `5`。

### 3.4 Execution Notes
- 构建期曾遇到 `Texture2D.LoadImage(...)` 不可用的问题，已按实际环境改为 `ImageConversion.LoadImage(...)` 并补齐 `UnityEngine.ImageConversionModule` 引用。
- 已验证 `bin/Debug/net472/assets/闪电.png` 与 `$(InstallDir)/assets/闪电.png` 均存在。
- 已在 `src/Combat/OrbCombatService.cs` 中加入 `DrawEnemySearchDebugBox` 常量；当前设为 `true`，改为 `false` 即可关闭红色索敌范围框。
- 已将 `src/Combat/OrbCombatService.cs` 的索敌范围从半宽 `20f` / 半高 `10f` 临时缩小为半宽 `8f` / 半高 `4.5f`；调试框会自动同步到新尺寸。
- 已完成缩放与范围调整后的构建验证：`dotnet build` 成功，`0` 警告、`0` 错误。
- 本轮执行前，已确认旧版 `Debug.DrawLine(...)` 调试框方案在当前运行环境不可见，因此接下来要升级为真正的游戏内可视方案。
- 已将 `Debug.DrawLine(...)` 替换为 `LineRenderer` 运行时红框，并通过 `OrbSystem.OnHeroUpdate(...)` 驱动可见时长；场景切换、重置、退出时会主动清理调试对象。
- 已在 `src/Combat/OrbCombatService.cs` 中加入严格二次过滤：即使敌方碰撞体进入 `OverlapBox`，只要 `target.transform.position` 相对 Hero 的水平偏移大于 `8f` 或垂直偏移大于 `4.5f`，仍会被排除。
- 已完成本轮修复后的构建验证：`dotnet build` 成功，`0` 警告、`0` 错误。
- 本轮目标是继续把范围改成非对称矩形：左 `15`、右 `15`、上 `15`、下 `5`，并保持调试框与严格过滤同步。
- 已将 `src/Combat/OrbCombatService.cs` 的范围常量改为左 `15`、右 `15`、上 `15`、下 `5`；`OverlapBox` 中心、红框顶点与严格过滤条件均已同步到该非对称矩形。
- 已完成非对称范围调整后的构建验证：`dotnet build` 成功，`0` 警告、`0` 错误。
- 已按最新需求关闭调试框：`src/Combat/OrbCombatService.cs` 中的 `DrawEnemySearchDebugBox` 已改为 `false`；当前仅保留代码以便后续需要时快速重新开启。
- 构建验证时发现：若将调试开关声明为 `const false`，会触发不可达代码告警；因此实际落地应改为非 `const` 静态布尔开关，既保持默认关闭，也避免编译告警。

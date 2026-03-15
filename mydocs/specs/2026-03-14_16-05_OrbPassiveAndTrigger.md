# SDD Spec: 黄球被动与激发系统

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 在现有三黄球系统上增加两套战斗能力：`被动` 与 `激发`。被动在骨钉累计命中 3 次后由当前所有已存在黄球分别触发；激发则特指“三球已满时再次 fireball 后，被右侧挤出并销毁的那个黄球”在消失前触发一次强力效果。两者都要在以小骑士为中心的矩形范围内随机选择一个敌对单位造成伤害，并在敌人头顶显示闪电图片。
- **In-Scope**:
  - 延续现有三虚线球、fireball 填充黄球、固定世界右优先、切场景保留状态等既有行为。
  - 增加黄球战斗语义：被动、激发、索敌、伤害应用、闪电视觉提示。
  - 修复“游戏进行中退出时可能卡在退出/标题过渡页面”的稳定性问题，确保模组在退出流程中不继续驱动运行时对象或战斗逻辑。
  - 矩形索敌范围固定为：相对小骑士中心左右 20 单位、上下 10 单位。
  - 被动伤害系数为骨钉伤害的三分之一；激发伤害系数为骨钉伤害的一倍。
  - 当三球已满且再次满足激发条件时，执行“左插入 -> 左/中右移 -> 右球挤出消失”的动画表现。
  - 保持单文件主实现风格，优先在 `DeVect.cs` 内扩展运行时与战斗逻辑。
- **Out-of-Scope**:
  - 新增配置菜单、存档版本迁移、资源包导入流程。
  - 复杂连锁伤害、暴击、元素异常、音效系统。
  - 多场景持久化保存闪电残留视觉。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `DeVect.cs`: 当前唯一核心实现；包含 Mod 生命周期、fireball 识别、骨钉命中统计、黄球运行时视觉。
  - `DeVect.cs`: `DeVectMod.HandleFireballCast()` 是黄球生成主入口，后续激发逻辑大概率挂接在这里。
  - `DeVect.cs`: `DeVectMod.OnSlashHit(...)` 是骨钉累计计数入口，后续被动触发逻辑将从这里升级。
  - `DeVect.cs`: `OrbRuntime` 当前只负责 3 槽虚线球与黄球显示，尚未具备动画队列、强制挤压、特效挂件管理能力。
- **Entry Points**:
  - `DeVect.cs`: `Initialize(...)` 注册 `ModHooks.HeroUpdateHook`、`ModHooks.SlashHitHook`、`On.PlayMakerFSM.OnEnable`、`SceneManager.activeSceneChanged`。
  - `DeVect.cs`: `FireballDetectAction.OnEnter()` 识别 fireball 施放并回调 `HandleFireballCast()`。
- **Data Models**:
  - `DeVect.cs`: `DeVectSettings` 仅含通用开关和基础血量示例设置，与黄球功能无直接数据结构隔离。
  - `DeVect.cs`: `OrbRuntime` 通过 `_yellowOrbs[3]` 表示三槽占用状态，当前不记录单球元数据、动画状态、特效生命周期。
- **Dependencies**:
  - `Modding.ModHooks.SlashHitHook`: 骨钉命中敌人检测。
  - `On.PlayMakerFSM.OnEnable`: Spell Control FSM 注入 fireball 识别。
  - `Assembly-CSharp.HealthManager` / `HitInstance`: 对敌伤害应用核心类型。
  - `UnityEngine.Physics2DModule`: 计划用于 `Physics2D.OverlapBoxAll` 或等效查询实现矩形索敌。
  - `UnityEngine.SpriteRenderer`: 当前黄球与后续闪电图片都可基于运行时生成 Sprite 实现。
  - `UnityEngine.SceneManagement`: 切场景时保留黄球占用态；后续需确保不会残留闪电特效对象。

## 2. Architecture (Optional - Populated in INNOVATE)
- Strategy/Pattern: 研究阶段暂定采用“Mod 层负责触发条件与伤害选择，OrbRuntime 负责球位状态与挤压动画，轻量运行时特效对象负责闪电图片显示”。
- Trade-offs:
  - 优点：延续现有单文件架构，改动集中，便于保持既有行为稳定。
  - 代价：`DeVect.cs` 会继续膨胀，需要在 PLAN 阶段严格切分职责边界，避免 Mod 逻辑与视觉状态耦合失控。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: DeVect.cs`
  - `class DeVectMod`
    - 新增字段：
      - `private const float EnemySearchHalfWidth = 20f;`
      - `private const float EnemySearchHalfHeight = 10f;`
      - `private static readonly Vector2 EnemySearchBoxSize = new(40f, 20f);`
      - `private readonly Collider2D[] _enemySearchResults = new Collider2D[128];`
      - `private int _lastKnownNailDamage;`
    - 调整方法签名：
      - `private void OnSlashHit(Collider2D otherCollider, GameObject slash)`
      - `private void HandleFireballCast()`
    - 新增方法签名：
      - `private void TriggerPassiveOrbs()`
      - `private void TriggerEvocation()`
      - `private HealthManager? TryPickRandomEnemyInRange()`
      - `private bool TryDealOrbDamage(HealthManager target, int damage, AttackTypes attackType, float direction)`
      - `private int GetCurrentNailDamage()`
      - `private static int GetCeilThirdDamage(int baseDamage)`
      - `private static float GetHitDirection(Transform heroTransform, Transform enemyTransform)`
      - `private void ShowLightningEffect(HealthManager target)`
  - `sealed class OrbRuntime`
    - 职责扩展：
      - 维护三槽黄球占用状态。
      - 提供“当前黄球数量”与“枚举所有已存在黄球”的能力。
      - 当三球已满时执行强制左插入、位移动画、右侧挤出销毁。
      - 管理临时闪电图片对象和球挤压动画对象，不影响切场景状态保留。
    - 新增字段：
      - `private readonly OrbSlotRuntime[] _orbSlots = new OrbSlotRuntime[3];`
      - `private readonly List<TransientVisual> _transientVisuals = new();`
    - 新增方法签名：
      - `public int GetActiveOrbCount()`
      - `public bool HasAnyActiveOrb()`
      - `public IEnumerable<int> EnumerateActiveOrbSlots()`
      - `public bool TryForceInsertOrbFromLeft(out bool evictedOrbTriggered)`
      - `public void TickAnimations(float deltaTime)`
      - `public void SpawnLightningVisual(Vector3 worldPosition)`
  - `sealed class OrbSlotRuntime`
    - 职责：记录单槽黄球引用、目标局部坐标、动画状态。
    - 字段：
      - `public Transform Anchor { get; }`
      - `public SpriteRenderer? OrbRenderer { get; set; }`
      - `public Vector3 CurrentLocalPosition { get; set; }`
      - `public Vector3 TargetLocalPosition { get; set; }`
      - `public float MoveLerpT { get; set; }`
      - `public bool IsOccupied => OrbRenderer != null`
  - `sealed class TransientVisual`
    - 职责：管理临时图片特效生命周期。
    - 字段：
      - `public GameObject Root { get; }`
      - `public float LifetimeRemaining { get; set; }`
      - `public Vector3 Velocity { get; set; }`

### 3.1.1 关键行为约束（无代码）
- 被动触发规则：使用全局骨钉命中计数器；当 `_nailHitCounter` 达到 3 的倍数时，当前所有已存在黄球各自独立触发 1 次被动索敌伤害。
- 被动伤害规则：以当前骨钉伤害为基础，按 `Ceiling(nailDamage / 3)` 计算；每个黄球各自随机挑选一次范围内敌人，可重复命中同一敌人，不要求去重。
- 激发触发规则：仅在 `HandleFireballCast()` 被调用且当前三球已满时触发，不再走“无新增”逻辑，而是执行一次强制左插入；激发主体不是所有球，而是“被右侧挤出并销毁的那个旧黄球”。
- 激发位移规则：新球从最左槽位出现，原左球移动到中槽，原中球移动到右槽，原右球播放被挤出动画并销毁；动画结束后槽位占用恢复为 3 个球。
- 激发伤害规则：由“被挤出销毁的右侧旧黄球”触发，在其挤出过程中或销毁前，对范围内随机 1 个敌人造成 `1x 骨钉伤害`；若范围内无敌人，则仅播放球挤压动画，不造成伤害。
- 索敌范围：以小骑士当前位置为中心的轴对齐矩形，半宽 20、半高 10。
- 闪电视觉：仅在实际成功造成伤害时生成；位置在目标敌人头顶上方；属于短生命周期临时对象，切场景时直接清理，不持久化。
- 切场景规则：只持久化三槽占用数量，不持久化动画中间态、临时闪电对象、被挤出中的球对象。
- 兼容性规则：不得破坏现有 `right -> top -> left` 常规填球顺序；激发只覆盖“三球已满时再次 fireball”的分支。
- 退出稳定性规则：当游戏进入退出/应用关闭流程时，模组必须立即进入“停机态”，禁止再响应 `HeroUpdate`、`SlashHit`、`PlayMakerFSM.OnEnable`、`activeSceneChanged` 等驱动逻辑；同时销毁当前运行时对象，避免退出阶段仍存在悬挂动画对象、未清理临时特效或对 Hero / GameManager 生命周期的晚期访问。
- 运行时销毁顺序规则：`OrbRuntime.Dispose()` 不得在销毁根节点后再访问任何槽位锚点 `Transform`；`OrbSlotRuntime.Clear()` 必须能在锚点已失效、场景正卸载、对象已被 Unity 标记销毁时安全执行。
- 日志规则：Hollow Knight 模组的业务/调试日志必须走 Modding API 日志通道，确保进入 `ModLog.txt`；不得把模组自定义诊断信息建立在 Unity 默认日志作为主通道上。

### 3.2 Implementation Checklist
- [x] 1. 更新 `mydocs/specs/2026-03-14_16-05_OrbPassiveAndTrigger.md`，固化用户确认后的触发语义、取整规则与激发入口约束。
- [x] 2. 在 `DeVect.cs` 扩展 `DeVectMod` 的战斗逻辑：记录当前骨钉伤害、在 `OnSlashHit(...)` 中于 3 的倍数触发全部黄球被动、在 `HandleFireballCast()` 中分流“普通填球”与“满槽后由被挤出球触发激发”。
- [x] 3. 在 `DeVect.cs` 增加范围索敌与伤害应用方法，基于 `HealthManager` / `HitInstance` 对随机敌人结算被动或激发伤害。
- [x] 4. 在 `DeVect.cs` 扩展 `OrbRuntime` 结构，使其支持读取活跃黄球数量、枚举球槽、强制左插入、识别被挤出旧球、右侧挤出和逐帧位移动画。
- [x] 5. 在 `DeVect.cs` 增加短生命周期闪电图片运行时对象，并在伤害成功时将其生成到目标头顶。
- [x] 6. 在 `DeVect.cs` 将动画与临时特效接入 `OnHeroUpdate()` 驱动，确保跟随、插球动画、挤出销毁和闪电淡出都能逐帧更新。
- [x] 7. 在 `DeVect.cs` 补强场景切换与卸载清理，保证 `_orbRuntime.Dispose()` 会清掉挤压动画残留对象和闪电对象，同时仍保留填充槽位数量。
- [x] 8. 运行 `dotnet build -c Debug` 验证签名、引用与编译状态；若实现现实与 Spec 不一致，先回写 Spec 再继续。
- [x] 9. 在 `DeVect.cs` 增加退出保护状态与退出事件清理逻辑，确保应用关闭或退出流程开始后停止所有运行时驱动与对象操作。
- [x] 10. 修复 `OrbRuntime.Dispose()` / `OrbSlotRuntime.Clear()` 的销毁顺序空引用，避免返回主菜单时在场景切换回调里因访问已销毁锚点导致连锁异常和主菜单 UI 异常。
- [x] 11. 在 `DeVect.cs` 中显式改用 Modding API 日志入口，避免日志通道语义不清，并使后续排障统一落到 `ModLog.txt`。
- [x] 12. 完善 `hk-api` skill，补充 HK Mod 日志通道约定与 `ModLog.txt` 常见目录说明。

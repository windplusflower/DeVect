# SDD Spec: 小骑士三虚线球与波动黄球效果

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 在小骑士头顶生成 3 个未填充的虚线球（扇形排布）；当小骑士发射 fireball（波）时，从右到左寻找首个未被黄球占用的槽位，并在该虚线球位置生成一个黄球；并在骨钉命中敌人每累计 3 次时，由黄球逻辑打印一条日志。
- **In-Scope**:
  - 在 Mod 初始化后创建并维护三球视觉对象。
  - 监听 fireball 施放事件并在触发时生成黄球。
  - 采用固定扇形槽位 `left/top/right`，fireball 时按 `right -> top -> left` 顺序选择首个未占用槽位生成黄球。
  - 统计骨钉命中敌人次数；每 3 次输出一次日志。
  - 黄色填充物必须表现为“圆球”而非方块。
  - 三个槽位的左右语义必须基于屏幕/世界固定方向，不随小骑士左右朝向翻转。
  - 切换场景后必须保留当前球槽占用状态，并在新场景中恢复显示。
- **Out-of-Scope**:
  - 伤害数值、碰撞伤害、敌人交互改动。
  - 存档结构升级与菜单配置扩展（除非后续明确要求）。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `DeVect.cs`: 当前唯一 Mod 主体文件；包含 `DeVectMod` 生命周期、Hook 注册与设置项。
- **Entry Points**:
  - `DeVect.cs`: `Initialize(...)` 为运行时入口，适合注册 HeroController / ModHooks 监听。
  - `DeVect.cs`: `Unload()` 为卸载入口，适合解除 Hook 与清理对象。
- **Data Models**:
  - `DeVect.cs`: `DeVectSettings` 当前包含 `Enabled`、`MinBaseHealth`。
- **Dependencies**:
  - `Modding` / `ModHooks`: Mod 生命周期与游戏数据 Hook。
  - `UnityEngine`: `GameObject`、`Transform`、渲染组件、对象实例化。
  - `UnityEngine.Physics2DModule`: `Collider2D` 命中统计所需程序集引用。
  - `UnityEngine.InputLegacyModule`: `Input.GetAxisRaw()` 判定 fireball 所需程序集引用。
  - `UnityEngine.SceneManagement`: 显式监听场景切换，确保切场景前冻结黄球状态。
  - `MMHOOK_Assembly-CSharp`: 可用于 `On.HeroController.*` 等游戏逻辑拦截。
  - `HollowKnight FSM (Spell Control)`: 用于识别 fireball 施放时机（通过 `On.PlayMakerFSM.OnEnable` 注入 Action）。
  - `ModHooks.SlashHitHook`: 用于统计骨钉命中事件。

## 2. Architecture (Optional - Populated in INNOVATE)
- Strategy/Pattern: 不进入 INNOVATE，直接采用“FSM 注入识别 fireball + 常驻视觉跟随器 + SlashHit 计数器”。
- Trade-offs:
  - 优点：fireball 识别精确、与现有单文件项目兼容。
  - 代价：需维护 FSM 注入幂等逻辑，避免重复注入。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: DeVect.cs`
  - `class DeVectMod`
    - 新增字段：
      - `private OrbRuntime? _orbRuntime;`
      - `private int _nailHitCounter;`
      - `private bool _spellFsmInjected;`
      - `private int _persistedFilledSlots;`
    - 新增方法签名：
      - `private void OnHeroUpdate()`
      - `private void OnSlashHit(Collider2D otherCollider, GameObject slash)`
      - `private void OnPlayMakerFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)`
      - `private void OnActiveSceneChanged(Scene from, Scene to)`
      - `private void HandleFireballCast()`
      - `private static bool IsEnemyCollider(Collider2D collider)`
      - `private void ResetRuntimeState()`
      - `private void SuspendVisualRuntimeForSceneChange()`
  - `sealed class OrbRuntime`
    - 职责：创建/维护 3 个虚线球槽位 + 最多 3 个黄球实例，跟随小骑士头顶。
    - 新增方法签名：
      - `public OrbRuntime(Transform heroTransform, int initialFilledSlots)`
      - `public void EnsureBuilt()`
      - `public void TickFollow()`
      - `public bool TryGetNextAvailableSlotWorldPosition(out Vector3 position)`
      - `public bool TrySpawnYellowOrbInNextAvailableSlot()`
      - `public int GetFilledSlotCount()`
      - `public void Dispose()`
    - 修正规则：
      - 运行时根节点不得直接继承小骑士朝向镜像；应仅同步位置，不同步左右翻转。
      - 虚线球与黄球均需使用圆形视觉资源；至少黄球必须是圆形填充。
  - `sealed class FireballDetectAction : HutongGames.PlayMaker.FsmStateAction`
    - 职责：注入到 Spell Control 相关状态，进入状态时判定是否 fireball，若是则回调 `HandleFireballCast`。
    - 新增成员签名：
      - `public Action? OnFireballCast { get; set; }`
      - `public override void OnEnter()`

### 3.1.1 关键行为约束（无代码）
- 三虚线球扇形：相对头顶锚点使用固定三槽位 `left/top/right`（right 为“最右边”定义，不依赖朝向翻转）。
- fireball 判定：在 Spell Control 的 `Spell Choice` 与 `QC` 状态注入动作；当竖直输入不触发上/下法术时视为 fireball。
- 黄球生成策略：fireball 触发时，按 `right -> top -> left` 顺序选择首个未占用槽位生成黄球；若三个槽位均已占用，则本次不新增黄球。
- 3 次骨钉命中：仅当 `IsEnemyCollider(...) == true` 时累计；`_nailHitCounter % 3 == 0` 触发日志。
- 视觉修正：当前实现存在“黄球显示为方块”与“朝向翻转导致左右槽位镜像”的现实偏差，执行时必须先修正该偏差再保持既有功能。
- 场景切换修正：切场景期间允许销毁旧视觉对象，但不得丢失已填充槽位数量；新场景 Hero 可用后必须按持久状态重建黄球。
- 可靠性修正：不能仅依赖 `HeroController.instance == null` 推断场景切换；必须额外监听显式场景切换事件以先行冻结状态。

### 3.2 Implementation Checklist
- [x] 1. 在 `DeVect.cs` 注册/卸载新增 Hook：`On.PlayMakerFSM.OnEnable`、`ModHooks.SlashHitHook`、`ModHooks.HeroUpdateHook`。
- [x] 2. 在 `DeVect.cs` 修正 `OrbRuntime`：视觉根节点仅跟随位置、不继承朝向翻转；槽位顺序始终保持世界右侧优先。
- [x] 3. 在 `DeVect.cs` 修正黄球渲染资源，确保生成的是黄色圆球而不是黄色方块。
- [x] 3. 在 `DeVect.cs` 增加 `FireballDetectAction`，并实现 Spell Control 指定状态注入（带幂等保护）。
- [x] 4. 在 `DeVect.cs` 实现骨钉命中敌人计数器逻辑，每累计 3 次输出日志。
- [x] 5. 在 `DeVect.cs` 完成卸载清理（解除 Hook、销毁运行时对象、重置计数）。
- [x] 6. 在 `DeVect.csproj` 补充 `UnityEngine.Physics2DModule.dll` 与 `UnityEngine.InputLegacyModule.dll` 引用，确保 `Collider2D` / `Input.GetAxisRaw()` 编译通过。
- [x] 7. 在 `DeVect.cs` 增加场景切换状态保留：场景过渡时仅销毁视觉运行时，不重置已填充槽位数量；新 Hero 出现后自动恢复。
- [x] 8. 在 `DeVect.cs` 增加显式场景切换监听，避免 `HeroUpdate` 路径遗漏导致切场景状态丢失。

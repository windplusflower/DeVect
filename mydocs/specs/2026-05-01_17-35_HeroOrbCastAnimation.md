# SDD Spec: Hero Orb Cast Animation

## 0. Open Questions (MUST BE CLEAR BEFORE CODING)
- [ ] None

## 1. Requirements (Context)
- Goal: 在每次“排队生成一个球”时，让小骑士本体额外播放一次接近原版上吼/白吼的施法动画；每生成 1 个球播 1 次，且球应在展开释放段出现，而不是在低头蓄势段出现。
- In-Scope: 仅改动 DeVect 自己的生球链路与英雄视觉表现；动画期间不能影响角色移动；保留当前 0.2s 的逐个生球节奏。
- In-Scope: 兼容下法术单发生成、上法术批量排队生成，以及球槽满时的挤出/激发流程。
- Out-of-Scope: 不恢复原版上吼伤害、冲击波、镜头震动、重力锁定、输入锁定或原 Spell Control 全套行为。
- Out-of-Scope: 不改当前法术消耗、攻击锁、球伤害、球被动/激发规则。

## 1.5 Code Map (Project Topology)
- Core Logic:
  - `src/Orbs/OrbSystem.cs`: 球生成主系统；`OnDiveCast()` / `OnShriekCast()` 入队，`TickSpawnQueue()` 以 `0.2s` 节奏逐个消费，`HandleSpellCast()` 真正生成球或触发挤出激发；当前需要把“播放动画”和“真正出球”拆成两个时点。
  - `src/Orbs/QueuedOrbSpawn.cs`: 排队生球的数据结构，目前只记录球类型与数量。
  - `DeVect.cs`: 把 `SpellDetectAction` 注入 Hero 的 `Spell Control` FSM，并把 neutral/down/up spell 分别转发到 `OrbSystem`。
  - `src/Fsm/SpellDetectAction.cs`: 读取当前输入方向，拦截法术并回调 `OnNeutralSpellCast` / `OnSmallSkillCast` / `OnBigSkillCast`。
- Visual Layer:
  - `src/Visual/OrbVisualService.cs`: 已承载英雄光环、闪电、碎裂等瞬时视觉，适合作为英雄施法视觉运行时的归属位置。
  - `src/Orbs/Runtime/OrbRuntime.cs`: 负责球槽跟随与球出生动画；当前只处理球自身视觉，不处理 Hero 动画。
- HK Runtime / API Facts:
  - `hkapi/HeroAnimationController.cs`: 常规英雄动画控制器；`cState.casting` 只会播放 `"Fireball"`，不能直接得到上吼动画。
  - `hkapi/HeroAnimationController.cs`: 暴露 `public tk2dSpriteAnimator animator` 与 `GetClipDuration(string clipName)`，可直接访问 Hero 的 tk2d 动画播放器。
  - `hkapi/HeroController.cs`: 暴露 `StopAnimationControl()` / `StartAnimationControl()`，用于临时停止默认动画状态机覆盖。
  - `fsm-export/Common/resources/Knight__Spell_Control__fsm_20204.md`: 原版上吼流程在 `Spell Control` FSM 内播放 `"Scream Start" -> "Scream" -> "Scream End"`，同时伴随 `RelinquishControl`、`AffectedByGravity(false)`、持续清零速度等会影响移动的动作。

## 2. Architecture & Strategy
- Strategy A (Recommended): 只借用 Hero 身上的 tk2d 动画片段，做一个“非阻塞 Scream 视觉覆盖”运行时。
- Strategy A Pros:
  - 能直接复用原版 `"Scream Start" / "Scream" / "Scream End"` 片段，视觉最接近原版白吼。
  - 不需要进入原版 `Spell Control` 的整套控制流，因此不会自动带入锁输入、失重、清零速度、镜头波等副作用。
  - 可以精确绑定到“每消费一个排队生球”这一时刻，而不是只绑定到一次施法输入。
- Strategy A Cons:
  - 需要自己处理动画覆盖时长与恢复，避免被 `HeroAnimationController` 下一帧顶掉。
  - 需要在移动过程中允许短时视觉覆盖，可能与跑步/跳跃姿态有轻微视觉竞争。
- Strategy B: 直接借用或触发 `Spell Control` 里的上吼状态。
- Strategy B Pros:
  - 最“原味”，几乎完全复刻原版流程。
- Strategy B Cons:
  - 原版状态自带 `RelinquishControl`、`StopAnimationControl`、`AffectedByGravity(false)`、`SetVelocity2d(0,0)` / `everyFrame` 等控制逻辑，会直接破坏“不能影响移动”的需求。
  - 很难只抽出动画、不抽出控制副作用；维护成本和行为风险都更高。
- Decision: 采用 Strategy A，在 DeVect 内新增一层 Hero 施法视觉覆盖，而不是复用原版施法控制状态。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Visual/HeroOrbCastAnimationRuntime.cs`
  - `internal sealed class HeroOrbCastAnimationRuntime`
  - Responsibility: 在不改 Hero 移动控制的前提下，临时接管 Hero 的 tk2d 动画播放器，顺序播放 `"Scream Start" -> "Scream" -> "Scream End"`，结束后恢复默认动画控制。
  - `public bool TryPlay(HeroController hero)`: 若当前 Hero、`HeroAnimationController`、`tk2dSpriteAnimator` 或必须动画 clip 缺失则返回 `false`；否则开始一次新的非阻塞施法视觉覆盖并返回 `true`。
  - `public float GetReleaseDelay(HeroController hero)`: 返回球应延迟到何时真正出现；当前定义为 `Scream Start` 的 clip 时长，表示球在进入 `Scream` 展臂释放段时生成。
  - `public void Tick(HeroController hero, float deltaTime)`: 维护当前播放阶段与剩余时长；若 Hero 失效或播放完成则恢复默认动画控制并清空内部状态。
  - `public void Cancel()`: 在场景切换、Reset、Shutdown 时强制恢复并清空状态。
- `File: src/Visual/OrbVisualService.cs`
  - Add field: `private readonly HeroOrbCastAnimationRuntime _heroOrbCastAnimationRuntime = new();`
  - `public void TryPlayHeroOrbCastAnimation(HeroController hero)`: 转发到运行时，作为 `OrbSystem` 的唯一调用入口。
  - `public void TickHeroOrbCastAnimation(HeroController hero, float deltaTime)`: 在现有 `OnHeroUpdate` 视觉 tick 中推进动画运行时。
  - `public void DisposeTransientVisuals()`: 现有清理链路中一并调用 `_heroOrbCastAnimationRuntime.Cancel()`，避免切场景后残留控制状态。
- `File: src/Orbs/OrbSystem.cs`
  - `public void OnHeroUpdate(HeroController hero, float deltaTime)`: 先推进原始起手队列，再推进所有待释放球，最后继续 Hero 动画与球视觉 tick。
  - `private bool TryProcessNextQueuedSpawn(HeroController hero)`: 只负责按原始 `0.2s` 节奏消费“起手事件”；每次消费时立刻开播 Hero 起手动画，并把这颗球登记为一个独立的待释放任务。
  - `private void TickDelayedSpawns(HeroController hero, float deltaTime)`: 推进所有待释放任务的剩余时间，时间到的任务在同帧真正出球。
  - `private void HandleSpellCast(HeroController hero, OrbTypeId spawnType)`: 只负责真正出球/挤出激发，不再在函数内部立刻开播动画。
  - `private struct PendingOrbRelease`: 轻量数据结构，记录 `OrbTypeId` 与 `RemainingDelaySeconds`。
- `File: src/Visual/HeroOrbCastAnimationRuntime.cs`
  - Stage clips:
    - `private const string ScreamStartClip = "Scream Start";`
    - `private const string ScreamLoopClip = "Scream";`
    - `private const string ScreamEndClip = "Scream End";`
  - Stage timing source: 使用 Hero 动画 clip 的实际时长；其中“出球延时”取 `Scream Start` 的 clip 时长，不依赖 FSM wait 常量。
  - Recovery rule: 结束或取消时，调用 `hero.StartAnimationControl()` 恢复默认动画控制；不调用任何会影响输入、重力、速度的 Hero 方法。

### 3.2 Implementation Notes
- 触发点拆成两个时点：队列消费到该颗球时先播 `Scream Start`，等进入 `Scream` 展臂释放段再真正出球；但“起手节奏”和“出球节奏”必须解耦，后续球的起手不能等待前一颗球真正落地。
- 视觉覆盖允许被新的生球重入刷新：如果上一次 `Scream` 还没播完又来了下一个球，`TryPlay(...)` 应重置到 `Scream Start` 重新开始一次完整短动画。
- 为了防止 `HeroAnimationController` 在下一帧用跑步/待机把 `Scream` 顶掉，运行时开始播放前需要 `hero.StopAnimationControl()`；播放结束后再 `hero.StartAnimationControl()`。
- 不进入原版 `Spell Control` 上吼状态，不触发 roar wave、镜头震动、Scr Heads、重力锁或速度清零。
- 若任意一个 `Scream` clip 在当前 Hero 动画库中缺失，则静默跳过，不阻断生球逻辑。
- 允许同时存在多颗“待释放”的球；它们共享同一个固定释放延时，但各自独立倒计时，因此整体出球序列等价于“原生球节奏整体后移一个起手动画时长”，而不是“原生间隔 + 动画时长”。

### 3.3 Implementation Checklist
- [x] 1. 新增 `src/Visual/HeroOrbCastAnimationRuntime.cs`，实现非阻塞 `Scream Start -> Scream -> Scream End` 动画覆盖与恢复逻辑。
- [x] 2. 更新 `src/Visual/OrbVisualService.cs`，接入该运行时的播放、tick 与清理入口。
- [x] 3. 更新 `src/Orbs/OrbSystem.cs`，把 Hero 传入队列消费链路，并把真正出球时刻延后到 `Scream Start` 结束后的释放段。
- [x] 4. 自检场景切换 / reset / shutdown 的清理路径，确保不会遗留动画控制关闭状态。

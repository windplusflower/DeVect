# SDD Spec: 回合推进系统第三阶段（极限闪避触发）

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- 本 Spec 中的 `Ghost Dash` 统一指代 vanilla `shadow dash` 语义，也就是 `HeroController.cState.shadowDashing == true` 的 dash；普通 dash 不计入本阶段。
- Non-blocking: 是否需要把 `Carefree Melody`、`Baldur Shell` 这类会在 `TakeDamage` 下游把伤害抵成 `0` 的情况也排除出“极限闪避”？本 Spec 默认 **不模拟** 这些下游免伤，只要第 `3` 个 dash 物理步上与 `hazardType == 1 && damageAmount > 0` 的普通敌伤碰撞重合，就视为有效极限闪避。若实测出现误报，再单开 spec 收紧。
- Non-blocking: `Dashmaster` 下冲若同时满足 `cState.shadowDashing == true`，本 Spec 默认 **计入**；原因是 vanilla 仍走同一条 shadow dash 无敌语义，而不是独立的新免伤系统。

## 1. Requirements (Context)
- **Goal**: 在现有 `HeroTookDamage`、`HeroNailParry` 之外，新增第三类回合推进来源：`Ghost Dash Dodge`。当小骑士进入一次 shadow dash 后，在**第 `3` 个 dash 物理步**上，若与一个“如果没有冲刺无敌帧就理应受伤”的普通敌伤碰撞重合，则推进 `1` 回合，并让当前所有 active orb 各自触发 `1` 次被动。
- **In-Scope**:
  - 继续复用 `OrbSystem.AdvanceRound(RoundAdvanceSource source)` 统一入口，不新增平行的球被动结算路径。
  - 判定严格绑定“dash 物理步”，不是 `HeroUpdate` 渲染帧，也不是按秒数近似换算。
  - 只认 shadow dash 期间的普通敌伤 / 接触伤害，即与第一阶段一致的 `hazardType == 1` 范围。
  - 每次 dash 最多触发一次 `Ghost Dash Dodge` 回合推进。
  - 保持 `HeroTookDamage`、`HeroNailParry` 两类已有来源继续有效，且三类来源继续共享同一套 `AdvanceRound(...) -> TriggerPassiveOrbs(...)` 主通路。
- **Out-of-Scope**:
  - 普通 dash 无敌判定；vanilla 普通 dash 本身没有这套免伤语义。
  - 把“第 `3` 个物理步”放宽为“第 `3` 步之后任意时刻都算”。
  - 直接用 `Physics2D.Overlap*` 重写整套受伤碰撞系统，替代 vanilla `HeroBox`。
  - 把尖刺、酸、落坑等 `hazardType != 1` 的环境伤害计入极限闪避。
  - 改动三类球的 `OnPassive(...)` / `OnEvocation(...)` 逻辑。
  - 新增 UI、极限闪避特效、回合数可视化。

## 1.5 Code Map (Project Topology)
- **Current Mod Entry**:
  - `DeVect.cs`: 当前已注册 `ModHooks.AfterTakeDamageHook` 与 `On.HeroController.NailParry`，并把前两类来源转发到 `OrbSystem`。
  - `src/Orbs/OrbSystem.cs`: 已有统一 `AdvanceRound(...)`、`OnHeroTookDamage(...)`、`OnHeroNailParry(...)`。
  - `src/Orbs/RoundAdvanceSource.cs`: 当前已有 `HeroTookDamage = 1`、`HeroNailParry = 2`。
- **Vanilla HK Evidence**:
  - `HeroController.HeroDash()`: 进入 dash 时立刻设置 `cState.dashing = true`；若拥有黑冲且冷却完成，同时设置 `cState.shadowDashing = true`。
  - `HeroController.FixedUpdate()`: 当 `cState.dashing == true` 时，每个物理步都会调用一次 `Dash()`。
  - `HeroBox.OnTriggerEnter2D / OnTriggerStay2D -> CheckForDamage(...)`: 这是英雄受伤碰撞的真实入口，负责解析 `DamageHero` 组件或 `damages_hero` FSM。
  - `HeroController.TakeDamage(...)`: 当 `cState.shadowDashing && hazardType == 1` 时直接 `return`；说明 shadow dash 会拦截普通敌伤。
  - `HeroBox.CheckForDamage(...)`: 若 `heroCtrl.cState.shadowDashing && component.shadowDashHazard`，会在进入 `TakeDamage(...)` 前就提前 `return`；说明有一部分 hurtbox 连 `TakeDamage` 都不会走到，但它们仍然是“shadow dash 免伤命中”的真实证据。
- **Why This Matters**:
  - 如果只在 `HeroUpdate` 里轮询位置或布尔值，拿到的是渲染帧语义，不是用户要求的“第 `3` 个物理帧”。
  - 如果只 hook `TakeDamage(...)`，会漏掉 `HeroBox` 提前拦截的 `shadowDashHazard` 场景。

## 2. Architecture (INNOVATE)
### 2.1 用户设想可行性分析
- **结论**: 你的设想**可行**，但要精确定义为“第 `3` 个 dash 物理步上的真实伤碰重合”，而不是“第 `3` 个渲染帧做一次裸 overlap 查询”。
- **为什么可行**:
  - vanilla 已经把 dash 的物理推进集中在 `FixedUpdate -> Dash()`，因此“进入 dash 后第几个物理步”是可计数的。
  - vanilla 已经把英雄受伤碰撞集中在 `HeroBox.CheckForDamage(...)`，因此“此刻如果没有冲刺无敌帧，理应受伤”的语义也有现成入口可复用。
- **为什么不建议做成裸 `Physics2D.Overlap*` 快照**:
  - 你需要手工复刻 `HeroBox` 对 `DamageHero` 与 `damages_hero` FSM 的解析逻辑，维护成本高。
  - `HeroBox` 里有 `shadowDashHazard` 的提前返回；只做裸 overlap 很容易与 vanilla 的“哪些 collider 真正算 hero hurtbox”逐渐漂移。
  - 若 overlap 查询发生在错误的 Unity 生命周期点，可能和当步的物理回调顺序错位，造成“第 `3` 步”前后各偏一拍。

### 2.2 方案对比
- **方案 A：在 `ModHooks.HeroUpdateHook` 中检测 `cState.dashing`，累计 `3` 帧后做一次重叠查询**
  - 优点：实现表面简单。
  - 问题：`HeroUpdateHook` 来自 `HeroController.Update()`，是渲染帧语义，不是物理步；在掉帧 / 高帧率下都会偏离用户定义。
  - 结论：拒绝。
- **方案 B：在第 `3` 个物理步用 `Physics2D.Overlap*` 直接扫描英雄碰撞体**
  - 优点：与用户“3 帧后判断是否重合”的表述最接近。
  - 问题：要自己重建 `HeroBox` 的判断边界，还要处理 `DamageHero` / `damages_hero` 双路径和 `shadowDashHazard` 特例。
  - 结论：**技术上可行，但不作为首选主方案**；仅在 `HeroBox` 中央入口无法稳定 hook 时作为 fallback。
- **方案 C：`HeroDash()` 开启 dash 会话，`Dash()` 计物理步，在 `HeroBox.CheckForDamage(...)` 的真实伤碰回调里，只在第 `3` 个物理步判定**
  - 优点：
    - 精确对齐用户定义的“第 `3` 个物理步”。
    - 直接复用 vanilla 真实受伤碰撞入口，不需要并行实现另一套 hurtbox 规则。
    - 能覆盖 `DamageHero` 与 `damages_hero` FSM 两种 hurtbox 来源。
    - 能看到 `shadowDashHazard` 这类在 `TakeDamage(...)` 前就被 shadow dash 拦截的案例。
  - 代价：
    - 需要维护一个“当前 dash 会话”的轻量状态。
    - 需要多加两个低层 hook：`HeroDash` / `Dash` / `HeroBox.CheckForDamage`。
  - 结论：**采用**。
- **方案 D：继续沿用 `AfterTakeDamageHook`，把“未受伤”反推成极限闪避**
  - 优点：不新增碰撞 hook。
  - 问题：真正 ghost dash 成功时，很多 case 根本不会走到 `AfterTakeDamageHook`；而且“没受伤”并不等于“发生了极限闪避”。
  - 结论：拒绝。

### 2.3 选定架构
- **术语对齐**:
  - 玩家表述使用 `Ghost Dash Dodge`。
  - 代码内部以 vanilla 字段命名为准，统一采用 `shadow dash / shadowDashing` 语义，避免和普通 dash 混淆。
- **判定主线**:
  1. `On.HeroController.HeroDash`：执行 `orig(self)` 后，若当前主角 `self == HeroController.instance` 且 `self.cState.shadowDashing == true`，开启一段“shadow dash 会话”，并把 `dashPhysicsSteps` 置为 `0`。
  2. `On.HeroController.Dash`：每次执行 `orig(self)` 后，若 shadow dash 会话仍激活，则 `dashPhysicsSteps++`。这一步的计数语义就是“已进入第几个 dash 物理步”。
  3. `On.HeroBox.CheckForDamage`：在调用 `orig(self, otherCollider)` 前，解析当前 `otherCollider` 是否代表一个有效的普通敌伤碰撞；若当前正处于 shadow dash 会话，且 `dashPhysicsSteps == 3`，则认定为一次有效 `Ghost Dash Dodge`。
  4. 一旦本次 dash 已触发极限闪避，当前 dash 会话立刻标记 `Consumed`，后续同一次 dash 上的其他 hurtbox 不再重复推进。
- **为什么把判定挂在 `CheckForDamage(...)` 上**:
  - 这是 vanilla 已经确定的“英雄此刻碰到了会伤害他的东西”的集中入口。
  - 在这里可以同时看到：
    - `DamageHero` 组件路径；
    - `damages_hero` FSM 路径；
    - `shadowDashHazard` 这种在 `TakeDamage(...)` 之前就会被拦截的特殊路径。
- **严格性约束**:
  - 只认 `dashPhysicsSteps == 3`，不是 `>= 3`。
  - 只认当前 dash 实例中的第一次有效命中。
  - 只认 `hazardType == 1 && damageAmount > 0` 的普通敌伤。
  - 只认当前主角实例，不处理其他 `HeroController` 引用。
- **去重层次**:
  - 第一层：dash 会话自身的 `Consumed` 标志，保证“每次 dash 最多一次”。
  - 第二层：`OrbSystem` 内部可额外保留一个“同帧只推进一次”的防御性去重字段，防止未来 hook 叠加。
- **Fallback**:
  - 如果 `MMHOOK_Assembly-CSharp` 对 `HeroBox.CheckForDamage` 的 hook 表面不可用，则退化为同时 hook `HeroBox.OnTriggerEnter2D` 与 `HeroBox.OnTriggerStay2D`，并共用同一个“解析 hurtbox + 第 `3` 步判定”的辅助方法。

### 2.4 Trade-offs
- **优点**:
  - 与用户定义高度一致，真正按 dash 物理步计数。
  - 不需要重写伤碰系统，行为更贴近 vanilla。
  - 与现有 `AdvanceRound(...)` 架构天然兼容，第三类来源只是新增入口，不会污染球系统内部。
- **代价**:
  - 比前两类来源多一层“会话状态机”，实现复杂度明显更高。
  - 默认不模拟 `Carefree Melody` / `Baldur Shell` 这种更后面的免伤逻辑，极少数 case 可能被视为“有效极限闪避”。
  - 如果未来要把判定从“第 `3` 步”改成“某个窗口”，需要再开 spec，而不是在本阶段里偷偷放宽条件。

## 3. Detailed Design & Implementation (PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Orbs/RoundAdvanceSource.cs`
  - 新增第三类来源：
    - `HeroShadowDashDodge = 3`
  - 原因：代码命名直接对齐 vanilla `shadowDashing` 字段；文档与对外描述仍可继续使用 `Ghost Dash Dodge`。

- `File: src/Combat/HeroShadowDashDodgeTracker.cs`
  - 新增一个专职 tracker，负责保存“当前 shadow dash 会话”的最小状态。
  - 推荐字段：
    - `private const int RequiredDashPhysicsSteps = 3;`
    - `private bool _sessionActive;`
    - `private bool _sessionConsumed;`
    - `private int _dashPhysicsSteps;`
    - `private int _dashStartFrame;`
  - 推荐方法：
    - `public void OnDashStarted(HeroController hero)`
    - `public void OnDashPhysicsStep(HeroController hero)`
    - `public bool TryDetectGhostDashDodge(HeroController hero, Collider2D otherCollider, out string? debugDetail)`
    - `public void Reset()`
  - 行为约束：
    - `OnDashStarted(...)` 只有在 `hero.cState.shadowDashing == true` 时才开启新会话。
    - `OnDashPhysicsStep(...)` 只有在会话激活且 `hero.cState.dashing == true` 时才累加步数。
    - `TryDetectGhostDashDodge(...)` 只有在 `_sessionActive && !_sessionConsumed && _dashPhysicsSteps == 3` 时才继续解析 collider。
    - 一旦成功返回 `true`，立即把 `_sessionConsumed = true`。
  - 解析逻辑：
    - 支持 `DamageHero` 组件路径：
      - 读取 `damageDealt`
      - 读取 `hazardType`
      - 保留 `shadowDashHazard` 作为 debug 信息
    - 支持 `damages_hero` FSM 路径：
      - 读取 FSM `damageDealt`
      - 读取 FSM `hazardType`
    - 至少要求 `damageAmount > 0 && hazardType == 1`
  - 额外守卫：
    - 要求 `hero == HeroController.instance`
    - 要求 `hero.cState.shadowDashing == true`
    - 要求没有明显的非 dash 伤害拦截状态，例如：
      - `!hero.cState.invulnerable`
      - `!hero.cState.recoiling`
      - `!hero.cState.dead`
      - `!hero.cState.hazardDeath`
      - `hero.transitionState == HeroTransitionState.WAITING_TO_TRANSITION`
      - `!hero.playerData.GetBool("isInvincible")`
    - `parryInvulnTimer > 0f` 的 case 默认 **不计入**，因为这代表“不是只有 dash 无敌帧在保护英雄”。

- `File: DeVect.cs`
  - 新增字段：
    - `private readonly HeroShadowDashDodgeTracker _shadowDashDodgeTracker = new();`
  - `Initialize(...)`
    - 新增：`On.HeroController.HeroDash += OnHeroDashStarted;`
    - 新增：`On.HeroController.Dash += OnHeroDashStepped;`
    - 新增：`On.HeroBox.CheckForDamage += OnHeroBoxCheckForDamage;`
  - `Unload()`
    - 对应注销以上三条 hook。
  - 新增方法签名：
    - `private void OnHeroDashStarted(On.HeroController.orig_HeroDash orig, HeroController self)`
    - `private void OnHeroDashStepped(On.HeroController.orig_Dash orig, HeroController self)`
    - `private void OnHeroBoxCheckForDamage(On.HeroBox.orig_CheckForDamage orig, HeroBox self, Collider2D otherCollider)`
  - 调用顺序建议：
    - `OnHeroDashStarted(...)`：先 `orig(self)`，再根据 `self.cState.shadowDashing` 决定是否开启会话。
    - `OnHeroDashStepped(...)`：先 `orig(self)`，再累加 `dashPhysicsSteps`；这样在第 `3` 个物理步对应的 trigger callback 发生时，计数已经是 `3`。
    - `OnHeroBoxCheckForDamage(...)`：
      - 先尝试 `_shadowDashDodgeTracker.TryDetectGhostDashDodge(...)`
      - 若成功：`EnsureOrbSystem(); _orbSystem?.OnHeroShadowDashDodge(HeroController.instance, debugDetail);`
      - 再执行 `orig(self, otherCollider)`，保持 vanilla 受伤 / 免伤流程不变
  - 生命周期：
    - 在 `OnActiveSceneChanged(...)`、`OnApplicationQuitting()`、`ResetRuntimeState()` 中补 ` _shadowDashDodgeTracker.Reset();`
    - 任何 hero 不存在或 mod disable 的路径都不能残留上一段 dash 会话状态。

- `File: src/Orbs/OrbSystem.cs`
  - 新增字段：
    - `private int _lastShadowDashDodgeAdvanceFrame = -1;`
  - 新增方法签名：
    - `public void OnHeroShadowDashDodge(HeroController hero, string? debugDetail = null)`
    - `private bool ShouldAdvanceRoundFromHeroShadowDashDodge(HeroController hero)`
  - 行为：
    - `OnHeroShadowDashDodge(...)` 先走 `CanProcess()` / `hero != null` 守卫。
    - 需要时先 `RestoreRuntimeIfNeeded(hero)`，与前两类来源保持一致。
    - `ShouldAdvanceRoundFromHeroShadowDashDodge(...)` 至少满足：
      - `hero.cState.shadowDashing`
      - `Time.frameCount != _lastShadowDashDodgeAdvanceFrame`
    - 通过后记录 `_lastShadowDashDodgeAdvanceFrame = Time.frameCount;`
    - 调用 `AdvanceRound(hero, RoundAdvanceSource.HeroShadowDashDodge, debugDetail);`
  - 生命周期调整：
    - `OnSceneChanged()` / `OnShutdown()` / `ResetAll()` 一并把 `_lastShadowDashDodgeAdvanceFrame` 复位。
  - 不改内容：
    - `AdvanceRound(...)`、`TriggerPassiveOrbs(...)`、各类球定义与持久化结构不需要为第三类来源开新分支。

### 3.1.1 关键行为约束（无代码）
- `Ghost Dash Dodge` 的正式定义：
  - 当前 dash 必须是 shadow dash。
  - 只在该次 dash 的第 `3` 个物理步上判定。
  - 该物理步上必须与一个有效普通敌伤 hurtbox 重合。
  - 一次 dash 最多推进 `1` 回合。
- 以下情况 **不得** 推进 `Ghost Dash Dodge` 回合：
  - 普通 dash。
  - 第 `1` / `2` 个物理步上的 overlap。
  - 第 `4` 步及以后才首次出现的 overlap。
  - `hazardType != 1` 的环境伤害。
  - 仅因 `HeroUpdate` 位置轮询推断出来的“疑似擦弹”。
  - 仅因 `TakeDamage(...)` 没有实际掉血而反推出来的“疑似 ghost dodge”。
  - 同一次 dash 上多个 hurtbox 同时命中。
- 并存规则：
  - `HeroTookDamage`、`HeroNailParry`、`HeroShadowDashDodge` 三类来源彼此并列，共享 `AdvanceRound(...)`。
  - 一个 ghost dash dodge 不应额外走 `HeroTookDamage`，因为 vanilla 本来就会被 shadow dash 免伤分支拦住。
- 文档规则：
  - 若本阶段未来进入实现，README 中“回合推进来源”的描述需要补成三类，而不再只写“受伤推进”。

### 3.2 Implementation Checklist
- [ ] 1. 修改 `src/Orbs/RoundAdvanceSource.cs`，新增 `HeroShadowDashDodge = 3`。
- [ ] 2. 新建 `src/Combat/HeroShadowDashDodgeTracker.cs`，封装 shadow dash 会话状态与第 `3` 个物理步判定。
- [ ] 3. 修改 `DeVect.cs`，注册 / 注销 `On.HeroController.HeroDash`、`On.HeroController.Dash`、`On.HeroBox.CheckForDamage` 三个 hook。
- [ ] 4. 在 `DeVect.cs` 中新增 `OnHeroDashStarted(...)`、`OnHeroDashStepped(...)`、`OnHeroBoxCheckForDamage(...)` 三个包装方法，并遵守既定调用顺序。
- [ ] 5. 修改 `src/Orbs/OrbSystem.cs`，新增 `OnHeroShadowDashDodge(...)` 与防御性去重字段。
- [ ] 6. 在 `OnActiveSceneChanged(...)`、`OnApplicationQuitting()`、`ResetRuntimeState()`、`OrbSystem.OnSceneChanged()`、`OrbSystem.OnShutdown()`、`OrbSystem.ResetAll()` 中补足 tracker / 去重状态清理。
- [ ] 7. 若 `On.HeroBox.CheckForDamage` 无法稳定 hook，则退化为 `OnTriggerEnter2D` + `OnTriggerStay2D` 双 hook 方案，并保持同样的第 `3` 步限制。
- [ ] 8. 运行 `dotnet build -c Debug` 验证编译通过。
- [ ] 9. 手测以下场景：
  - 拥有黑冲时，穿过普通敌人 / 攻击 hurtbox，并在第 `3` 个 dash 物理步上重合，推进 `1` 回合。
  - 同一次 dash 上同时擦到多个 hurtbox，只推进一次。
  - 仅在第 `1` 或 `2` 个物理步重合，第 `3` 步已经脱离，不推进。
  - 第 `3` 步没有重合，但第 `4` 步才重合，不推进。
  - 没有黑冲、只是普通 dash 时，不推进。
  - 受伤推进与 parry 推进仍保持原有行为，不受 ghost dodge 检测干扰。
  - 尖刺 / 酸 / 落坑等环境伤害不被误判为 ghost dodge。
  - `Dashmaster` 下冲 + 黑冲的 case 与本 Spec 默认行为一致。

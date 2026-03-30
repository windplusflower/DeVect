# SDD Spec: 回合推进系统第二阶段（拼刀触发）

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- 本 Spec 将“拼刀成功”收敛为 vanilla 实际调用 `HeroController.NailParry()` 的事件，而不是对所有骨钉攻击 / nail art 动画窗口做二次推导。若后续发现某类窗口不会进入 `NailParry()`，需要另开 spec 处理。
- Non-blocking: 首版是否需要按敌人 / FSM 状态做白名单过滤？本 Spec 默认 **不做**，仅在出现误报时再补诊断层。

## 1. Requirements (Context)
- **Goal**: 在第一阶段 `HeroTookDamage` 之外，新增第二类回合推进来源：小骑士成功触发一次 `Parry` 时，推进 `1` 回合，并让当前所有已存在球各自触发 `1` 次被动。
- **In-Scope**:
  - 复用 `OrbSystem` 已存在的统一 `AdvanceRound(...)` 入口，不新增平行的被动结算通路。
  - 新增 `Parry` 对应的检测与转发路径，使其成为第二类 `RoundAdvanceSource`。
  - 检测逻辑以 HK API 的真实成功信号为准：`HeroController.NailParry()` 是主触发点；`parryInvulnTimer > 0` 只作为成功后的佐证，不作为独立事件源。
  - 保持 `TriggerPassiveOrbs(...)`、三类球 `OnPassive(...)`、满槽激发、持久化同步等既有行为不变。
  - 保持第一阶段的受伤推进逻辑继续有效；拼刀推进只是新增来源，不替换 `HeroTookDamage`。
- **Out-of-Scope**:
  - 改写 `TriggerPassiveOrbs(...)` 或 `IOrbDefinition` 接口。
  - 新增按敌人类型、Boss 名称、FSM 状态名区分的球效果分支。
  - 实现 ParryKnight 那种“拼刀伤害敌人 / 限制非拼刀伤害”的整套系统。
  - 基于 `HeroUpdate` 轮询攻击窗口去反推“可能拼刀”。
  - UI、回合可视化、拼刀特效重做。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `DeVect.cs`: Mod 生命周期与 Hook 汇总入口；当前已接入 `AfterTakeDamageHook` 并把受伤推进转发给 `OrbSystem`。
  - `src/Orbs/OrbSystem.cs`: 当前回合推进与球被动结算核心；`OnHeroTookDamage(...)` 最终调用 `AdvanceRound(...)` 与 `TriggerPassiveOrbs(...)`。
  - `src/Orbs/RoundAdvanceSource.cs`: 当前只有 `HeroTookDamage = 1`，是第二类来源新增值的落点。
- **HK API Evidence**:
  - `HeroController.NailParry()`: 直接把 `parryInvulnTimer` 设为 `INVUL_TIME_PARRY`。
  - `HeroController.TakeDamage(...)`: 当 `parryInvulnTimer > 0f && hazardType == 1` 时直接 `return`，说明真正成功拼刀会阻止普通敌伤结算。
  - `HeroController.QuakeInvuln()` / `CycloneInvuln()`: 也会写入 `parryInvulnTimer`，因此 `parryInvulnTimer > 0` 不能单独当作拼刀触发条件。
- **Reference Mod**:
  - `ParryKnight` 当前版本的核心入口是 `On.HeroController.NailParry`，随后读取敌人当前 FSM `ActiveStateName` 并与白名单匹配。
  - 我没有在该版本源码里看到直接向 `Attack` FSM 的 `Clash` 状态插桩的实现；可迁移的核心思想是“以 `NailParry` 为准，再用 FSM 状态做补充过滤 / 诊断”，而不是把 FSM 当作唯一真相源。

## 2. Architecture (INNOVATE)
### 2.1 方案对比
- **方案 A：在 `HeroUpdate` 轮询 `parryInvulnTimer > 0`，视为发生拼刀**
  - 优点：不用新增 hook，实现表面最简单。
  - 问题：`parryInvulnTimer` 同时被 `NailParry()`、`QuakeInvuln()`、`CycloneInvuln()` 复用；轮询无法知道“何时刚刚发生了真正的 parry”，会把下砸无敌、旋风斩无敌误判成拼刀。
  - 结论：拒绝。
- **方案 B：只依赖 FSM 状态（例如 `Attack` / `Clash` 或敌人 `Enemy Damager` / `Attack` 当前状态）来推断拼刀**
  - 优点：语义上最贴近“攻击窗口碰撞”。
  - 问题：FSM 逻辑不在源码中，状态名维护成本高；不同敌人状态差异极大；当前需求只需要“成功推进一回合”，并不需要知道具体是哪种攻击。
  - 结论：拒绝作为首版正式实现。
- **方案 C：以 `On.HeroController.NailParry` 为主信号，`parryInvulnTimer > 0` 为 post-condition，FSM 仅作可选诊断**
  - 优点：直接复用 vanilla 成功拼刀信号；误报面最小；实现改动集中在入口层，不会污染球系统内部。
  - 代价：首版不会记录“是哪只敌人、哪个状态”导致了拼刀；若未来出现个别异常 case，需要再追加 FSM 诊断层。
  - 结论：**采用**。
- **方案 D：沿用 `AfterTakeDamageHook`，把“未受伤但 damageAmount == 0”视为拼刀**
  - 优点：继续沿用现有伤害入口。
  - 问题：真正成功拼刀时，`HeroController.TakeDamage(...)` 会在 `parryInvulnTimer > 0` 分支提前返回；很多情况下根本不会进入“可观察的 damage=0 事件”。而且 `0` 伤害还可能来自其他免伤机制。
  - 结论：拒绝。

### 2.2 选定架构
- **核心判定**: 新增 `On.HeroController.NailParry` hook，把 vanilla 已确认成功的 parry 事件转发到 `OrbSystem`。
- **成功条件**:
  - 先执行 `orig(self)`，确保游戏原始 `NailParry()` 已经把 `parryInvulnTimer` 设好。
  - 只处理当前主角实例 `self == HeroController.instance`。
  - `self.parryInvulnTimer > 0f` 仅作为“orig 已成功执行”的确认，不单独在别的地方轮询。
- **回合推进路径**:
  - `DeVect.cs` 新增 `OnHeroNailParry(...)`。
  - `OrbSystem` 新增 `OnHeroNailParry(HeroController hero)`。
  - `OnHeroNailParry(...)` 内部通过 `AdvanceRound(...)` 推进，不允许直接调用 `TriggerPassiveOrbs(...)`。
- **去重策略**:
  - 首版在 `OrbSystem` 内增加“同帧只推进一次”的轻量去重，例如 `_lastParryAdvanceFrame`。
  - 目的不是修正 vanilla，而是防御未来 hook 叠加或极端多次回调，避免一帧多次推进。
- **日志策略**:
  - 现有 `AdvanceRound(...)` 不应继续强绑定 `hazardType` / `damageAmount` 这类受伤专属参数。
  - 推荐把日志改成“来源必填、细节可选”的形式，使 `HeroTookDamage` 与 `HeroNailParry` 共用同一个入口。
- **FSM 参考的落点**:
  - 首版不把 `Attack FSM / Clash` 状态作为推进的必要条件。
  - 如果后续出现“`NailParry` 触发但不应推进”的异常样本，再在 `OnPlayMakerFSM.OnEnable` 侧加一个只读诊断层，记录 parry 发生时附近敌人的 `Attack` FSM `ActiveStateName`，再决定是否升级为白名单过滤。

### 2.3 Trade-offs
- **优点**:
  - 以游戏原生成功信号为准，语义最干净。
  - 不需要把敌人特化规则、FSM 白名单或状态缓存塞进 `OrbSystem`。
  - 对现有代码的改动集中在 hook 注册与新入口方法，最符合“只复用 `AdvanceRound()`、不改 `TriggerPassiveOrbs()`”的要求。
  - 与第一阶段的 `HeroTookDamage` 形成并列来源，后续第三类来源也可继续复用同一模式。
- **代价**:
  - 首版不知道具体是哪只敌人、哪个状态触发了 parry。
  - 如果某些特殊互动也调用了 `NailParry()`，首版会先把它们视为有效 parry；需要靠日志和后续白名单收敛。
  - 对“所有 nail art 有效窗口都可拼刀”的假设不做额外补丁；只认实际进入 `NailParry()` 的成功事件。

## 3. Detailed Design & Implementation (PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Orbs/RoundAdvanceSource.cs`
  - 修改：在现有枚举中新增第二类来源。
  - 推荐值：
    - `HeroTookDamage = 1`
    - `HeroNailParry = 2`
  - 原因：名称直接绑定 HK API 的真实信号源，避免把“Parry”泛化到未来别的自定义无敌动作。

- `File: DeVect.cs`
  - `Initialize(...)`
    - 新增：`On.HeroController.NailParry += OnHeroNailParry;`
  - `Unload()`
    - 新增：`On.HeroController.NailParry -= OnHeroNailParry;`
  - 新增方法签名：
    - `private void OnHeroNailParry(On.HeroController.orig_NailParry orig, HeroController self)`
  - 行为：
    - 始终先执行 `orig(self)`，保持原版拼刀逻辑与无敌帧设置不变。
    - 若 `!_settings.Enabled || _isShuttingDown || self == null`，直接返回。
    - 若 `self != HeroController.instance`，直接返回。
    - 若 `self.parryInvulnTimer <= 0f`，直接返回；这一步只是确认 `orig` 已成功设置 parry 状态。
    - `EnsureOrbSystem();`
    - `_orbSystem?.OnHeroNailParry(self);`
  - 约束：
    - 不在这里直接调用 `TriggerPassiveOrbs(...)`。
    - 不在这里做敌人 FSM 白名单判断；若未来需要诊断，再单独扩展。

- `File: src/Orbs/OrbSystem.cs`
  - 新增字段：
    - `private int _lastParryAdvanceFrame = -1;`
  - 新增方法签名：
    - `public void OnHeroNailParry(HeroController hero)`
    - `private bool ShouldAdvanceRoundFromHeroParry(HeroController hero)`
  - 推荐调整：
    - 将现有 `AdvanceRound(HeroController hero, RoundAdvanceSource source, int hazardType, int damageAmount)` 重构为更通用的签名，例如：
      - `private void AdvanceRound(HeroController hero, RoundAdvanceSource source, string debugDetail = null)`
    - `OnHeroTookDamage(...)` 继续把 `hazardType` / `damageAmount` 作为 `debugDetail` 传入。
    - `OnHeroNailParry(...)` 直接调用不带额外受伤参数的同一入口。
  - 行为：
    - `OnHeroNailParry(...)` 先走 `CanProcess()` / `hero != null` 守卫。
    - 需要时先 `RestoreRuntimeIfNeeded(hero)`，与受伤推进路径保持一致。
    - `ShouldAdvanceRoundFromHeroParry(...)` 至少满足：
      - `hero.parryInvulnTimer > 0f`
      - `Time.frameCount != _lastParryAdvanceFrame`
    - 通过后记录 `_lastParryAdvanceFrame = Time.frameCount;`
    - 调用 `AdvanceRound(hero, RoundAdvanceSource.HeroNailParry, null);`
  - 生命周期调整：
    - `OnShutdown()` / `ResetAll()` 需要把 `_lastParryAdvanceFrame` 复位。
    - `OnSceneChanged()` 可不强制复位该字段，但复位也安全；推荐一并清掉，降低状态残留心智负担。
  - 不改内容：
    - `TriggerPassiveOrbs(HeroController hero)` 逻辑完全不动。
    - 三类球定义、持久化结构、运行时生成 / 激发流程都不需要为 parry 新开分支。

- `File: mydocs/specs/2026-03-30_13-32_TurnAdvanceOnHeroTakeDamage.md`
  - 若本轮后续进入实现阶段，建议补一条“第二阶段由 `HeroNailParry` 扩展”的交叉引用，避免第一阶段 spec 被误读为最终完整设计。

### 3.1.1 关键行为约束（无代码）
- 拼刀推进的唯一正式触发源是 `On.HeroController.NailParry`；`parryInvulnTimer > 0` 只是验证，不是独立事件。
- 以下情况 **不得** 推进 `Parry` 回合：
  - `QuakeInvuln()` 带来的无敌时间。
  - `CycloneInvuln()` 带来的无敌时间。
  - 任何仅因 `HeroUpdate` 轮询发现 `parryInvulnTimer > 0` 的情况。
  - 任何仅因 `damageAmount == 0` 推断出的“疑似拼刀”。
- 回合推进规则：
  - 每次有效 `NailParry` 最多推进 `1` 回合。
  - 推进后仍由 `AdvanceRound(...)` 统一执行 `_roundCounter++`、日志、`TriggerPassiveOrbs(...)`、同步持久化快照。
  - 即使当前没有 active orb，也允许回合数推进，但不会产生额外球效果。
- 受伤推进并存规则：
  - `HeroTookDamage` 与 `HeroNailParry` 是并列来源，互不覆盖。
  - 成功 parry 时，vanilla `TakeDamage(...)` 已因 `parryInvulnTimer > 0f && hazardType == 1` 提前返回，正常情况下不会再触发 `HeroTookDamage` 推进。
- FSM 参考规则：
  - 首版不维护敌人状态白名单。
  - 若后续需要定位异常 case，优先追加日志 / 诊断，不直接把状态名硬编码进首版主流程。

### 3.2 Implementation Checklist
- [ ] 1. 修改 `src/Orbs/RoundAdvanceSource.cs`，新增 `HeroNailParry = 2`。
- [ ] 2. 修改 `DeVect.cs`，在 `Initialize(...)` / `Unload()` 注册与注销 `On.HeroController.NailParry`。
- [ ] 3. 在 `DeVect.cs` 新增 `OnHeroNailParry(...)`，并遵守“先 `orig`、后转发”的调用顺序。
- [ ] 4. 修改 `src/Orbs/OrbSystem.cs`，新增 `_lastParryAdvanceFrame`、`OnHeroNailParry(...)` 与 `ShouldAdvanceRoundFromHeroParry(...)`。
- [ ] 5. 轻量重构 `AdvanceRound(...)`，去掉对受伤专属参数的强耦合，使 `HeroTookDamage` 与 `HeroNailParry` 复用同一个入口。
- [ ] 6. 复核 `OnShutdown()` / `ResetAll()` / `OnSceneChanged()` 的状态清理，确认 parry 去重字段不会跨生命周期残留。
- [ ] 7. 运行 `dotnet build -c Debug` 验证编译通过。
- [ ] 8. 手测以下场景：
  - 普通敌人攻击与主角骨钉攻击成功拼刀一次，会推进 `1` 回合并触发当前所有 active orb 被动。
  - 连续两次独立拼刀会推进两次，不依赖受伤。
  - 成功拼刀不会再额外触发 `HeroTookDamage` 路径。
  - 仅使用下砸无敌、旋风斩无敌，不会错误推进 `Parry` 回合。
  - 当前没有球时，拼刀仍会推进回合计数，但没有球效果。
- [ ] 9. 若手测中出现疑似误报，再追加一轮诊断：记录 parry 发生时附近敌人的 `Attack` FSM `ActiveStateName`，评估是否需要参考 ParryKnight 增加白名单过滤。

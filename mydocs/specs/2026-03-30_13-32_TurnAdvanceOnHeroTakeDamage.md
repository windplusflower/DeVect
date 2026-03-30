# SDD Spec: 回合推进系统第一阶段（受伤触发）

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- 另外两类回合推进来源尚未确定；本 Spec 只定义第一类来源：`小骑士实际受伤`。
- Non-blocking: 未来是否需要把“当前回合数”暴露为 UI 或存档字段，当前阶段不做。

## 1. Requirements (Context)
- **Goal**: 把当前“每累计 `3` 次有效骨钉命中，所有已存在球各自触发一次被动”的旧机制，替换为新的“回合推进系统”。第一阶段仅实现第一类回合推进来源：小骑士在 `TakeDamage` 流程里实际受到一次有效伤害时，推进 `1` 回合，并让当前所有已存在球各自触发 `1` 次被动。
- **In-Scope**:
  - 移除旧的 `_nailHitCounter % 3 == 0` 被动触发语义。
  - 在 `OrbSystem` 中建立统一的 `AdvanceRound(...)` 入口，后续其他来源只需复用该入口。
  - 第一阶段只接入“小骑士实际受伤”这一种来源，不预埋其他会改变行为的半成品 hook。
  - 回合推进后，三类球继续沿用各自现有的 `OnPassive(...)` 行为与数值，不修改黄/白/黑球平衡。
  - 受伤判定采用“实际伤害已成立”的语义，避免把无敌、暗冲、拼刀无伤、护符免伤、`damageAmount == 0` 等情况误判成回合推进。
  - 第一阶段把“受伤”范围收窄为 `hazardType == 1` 的普通敌伤/接触伤害；不把尖刺、酸、落坑等环境伤害计入回合推进，避免非战斗场景刷回合。
  - 保持现有施法生成球、满槽挤出激发、切场景保留球序/储伤等既有行为不变。
- **Out-of-Scope**:
  - 另外两类未来回合推进来源的实现与占位逻辑。
  - 新的 UI、回合图标、回合计数可视化。
  - 黄/白/黑球伤害、视觉、搜索范围、萨满加成等平衡重做。
  - 让环境自伤（尖刺/酸/落坑）推进回合。
  - 改动 `IOrbDefinition` 接口或为三种球单独新增“按不同推进来源分支处理”的行为。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `DeVect.cs`: Mod 生命周期入口；当前注册了 `ModHooks.SlashHitHook`，并把旧的被动推进交给 `OrbSystem.OnSlashHit(...)`。
  - `src/Orbs/OrbSystem.cs`: 当前回合相关逻辑的真正核心；维护 `_nailHitCounter`，在 `OnSlashHit(...)` 中达到 `3` 的倍数时调用 `TriggerPassiveOrbs(hero)`。
  - `src/Orbs/OrbTriggerContext.cs`: 球体结算上下文；当前只承载英雄、骨钉伤害、战斗/视觉服务等公共信息。
  - `src/Orbs/Definitions/*.cs`: 三类球的 `OnPassive(...)` / `OnEvocation(...)` 具体行为定义；本阶段应尽量保持不动。
- **Entry Points**:
  - `DeVect.cs`: `Initialize(...)` / `Unload()` 负责注册和注销 `ModHooks`、FSM 注入、场景切换与退出流程。
  - `DeVect.cs`: `OnSlashHit(...)` 当前是旧回合推进入口，阶段一完成后不再承载推进语义。
  - HK API: `ModHooks.TakeDamageHook` 在 `HeroController.TakeDamage()` 开始处触发，时机过早。
  - HK API: `ModHooks.AfterTakeDamageHook` 在实际扣血前触发，已越过 `CanTakeDamage()`、暗冲/拼刀无伤、部分护符免伤等判定，更接近“真正受伤”语义。
- **Data Models**:
  - `src/Orbs/Runtime/OrbInstance.cs`: 单球运行时对象，承载当前储伤与待移除标记。
  - `src/Orbs/OrbPersistentState.cs`: 场景切换时保留球序与储伤；与回合推进来源无直接耦合。
- **Docs Impact**:
  - `README.md`
  - `README-en.md`
  - 以上两份当前都还写着“每 `3` 次有效骨钉命中触发被动”，实现后必须同步改文案。

## 2. Architecture (INNOVATE)
### 2.1 方案对比
- **方案 A：保留 `_nailHitCounter`，把受伤事件折算成“+3 次命中”**
  - 优点：改动最小。
  - 问题：只是把旧模型换皮，无法形成真正的“统一回合推进入口”；未来第二、第三类来源还会继续污染 hit-counter 语义。
  - 结论：拒绝。
- **方案 B：新增 `AdvanceRound(...)`，但接 `ModHooks.TakeDamageHook`**
  - 优点：名字上最贴近用户所说的 `TakeDamage`。
  - 问题：`TakeDamageHook` 发生在 `HeroController.TakeDamage()` 最开始，早于 `CanTakeDamage()`、暗冲、拼刀无伤、部分免伤护符、`damageAmount == 0` 等过滤；会把“伤害尝试”错当成“实际受伤”。
  - 结论：拒绝作为第一阶段正式实现。
- **方案 C：新增 `AdvanceRound(...)`，接 `ModHooks.AfterTakeDamageHook`，并在 `damageAmount > 0` 且 `hazardType == 1` 时推进**
  - 优点：语义最接近“实际受伤”；不再绑定 SlashHit；未来其他来源只需继续调用 `AdvanceRound(...)`；同时能避开环境伤害刷回合。
  - 代价：需要把入口层从 `SlashHitHook` 改成 `AfterTakeDamageHook`，并移除旧计数逻辑。
  - 结论：**采用**。

### 2.2 选定架构
- **核心抽象**: 在 `OrbSystem` 中引入统一的 `AdvanceRound(...)`，它负责：
  - 累加运行时回合计数（仅运行时，不进存档）。
  - 记录调试日志，标明推进来源。
  - 结算当前所有 active orbs 的 `OnPassive(...)`。
- **第一阶段唯一来源**: `HeroTookDamage`。
- **入口选择**: 使用 `ModHooks.AfterTakeDamageHook`，而不是 `ModHooks.TakeDamageHook`。
- **行为边界**:
  - 只要是一次符合条件的有效受伤事件，就推进 `1` 回合。
  - 一次回合推进，只让当前所有已存在球各自结算 `1` 次被动。
  - 若当前没有球，也允许回合数推进，但不会产生额外球效果。
- **扩展方式**:
  - 第二、第三类来源未来只需要新增各自检测，并在通过判定后调用同一个 `AdvanceRound(...)`。
  - 本阶段不为未知来源添加空实现、占位 hook 或临时状态字段。

### 2.3 Trade-offs
- **优点**:
  - 正式摆脱“3 hit = 1 passive wave”的旧设计，把系统语义提升到“round advancement”。
  - 最大化复用现有球定义，不改黄/白/黑球各自的被动算法。
  - 明确区分“伤害尝试”和“实际受伤”，减少误触发。
  - 避免环境陷阱在非战斗中刷白球衰减/黑球储伤。
- **代价**:
  - 第一阶段只覆盖 `hazardType == 1`，部分 Boss/场景 hazard 伤害不会推进回合。
  - `SlashHitHook` 将暂时退出回合系统；若未来某类来源再次需要 slash 语义，需要在新 spec 中重新引入。
  - 当前没有回合 UI，玩家只能从球被动表现感知回合推进。

## 3. Detailed Design & Implementation (PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Orbs/RoundAdvanceSource.cs`
  - 新增：`internal enum RoundAdvanceSource`
    - `HeroTookDamage = 1`
  - 作用：统一标识回合推进来源，避免后续继续用布尔字段/注释表达语义。

- `File: DeVect.cs`
  - `Initialize(...)`
    - 删除：`ModHooks.SlashHitHook += OnSlashHit;`
    - 新增：`ModHooks.AfterTakeDamageHook += OnHeroAfterTakeDamage;`
  - `Unload()`
    - 删除：`ModHooks.SlashHitHook -= OnSlashHit;`
    - 新增：`ModHooks.AfterTakeDamageHook -= OnHeroAfterTakeDamage;`
  - 删除旧入口：
    - `private void OnSlashHit(Collider2D otherCollider, GameObject slash)` 整体移除；第一阶段不保留 no-op 入口。
  - 新增方法签名：
    - `private int OnHeroAfterTakeDamage(int hazardType, int damageAmount)`
  - 行为：
    - 若 `!_settings.Enabled || _isShuttingDown`，直接返回原 `damageAmount`。
    - 若 `damageAmount <= 0`，直接返回原 `damageAmount`。
    - `EnsureOrbSystem();`
    - `_orbSystem?.OnHeroTookDamage(hazardType, damageAmount);`
    - 返回原 `damageAmount`，不在本阶段修改游戏原始受伤数值。

- `File: src/Orbs/OrbSystem.cs`
  - 删除字段：
    - `private const float SlashHitDedupWindowSeconds = 0.08f;`
    - `private int _nailHitCounter;`
    - `private int _lastProcessedSlashInstanceId;`
    - `private float _lastProcessedSlashTime;`
  - 新增字段：
    - `private int _roundCounter;`
  - 删除方法：
    - `public void OnSlashHit(Collider2D otherCollider, GameObject? slash)`
  - 新增方法签名：
    - `public void OnHeroTookDamage(int hazardType, int damageAmount)`
    - `private void AdvanceRound(HeroController hero, RoundAdvanceSource source, int hazardType, int damageAmount)`
    - `private static bool ShouldAdvanceRoundFromHeroDamage(int hazardType, int damageAmount)`
  - 调整方法：
    - `OnSceneChanged()`：不再维护 slash 去重状态；只保留视觉清理、运行时挂起、FSM 注入重置。
    - `OnShutdown()`：无需再清 slash 状态。
    - `ResetAll()`：改为重置 `_roundCounter`，并清理运行时对象。
    - `TriggerPassiveOrbs(HeroController hero)`：保留为被动结算核心，不改变三类球的被动顺序与移除时机。
  - 行为：
    - `OnHeroTookDamage(...)` 只负责入口守卫与来源过滤，不直接写球逻辑。
    - `ShouldAdvanceRoundFromHeroDamage(...)` 返回 `damageAmount > 0 && hazardType == 1`。
    - `AdvanceRound(...)` 执行 `_roundCounter++`、日志、`TriggerPassiveOrbs(hero)`、再同步 `_persistentState`。

- `File: src/Orbs/OrbTriggerContext.cs`
  - 本阶段不改构造签名。
  - 原因：推进来源只影响“是否触发一轮被动”，不影响三类球的被动计算参数；无需把 `RoundAdvanceSource` 下沉到每个 `IOrbDefinition`。

- `File: README.md`
  - 需要把“每累计 `3` 次有效骨钉命中”改为“每当小骑士实际受伤一次，推进 `1` 回合并触发当前球被动”。

- `File: README-en.md`
  - 需要把 `Every 3 valid nail hits...` 改成新的受伤推进文案。

### 3.1.1 关键行为约束（无代码）
- 回合推进的定义：一次符合条件的事件，只推进 `1` 回合，不合并，不补算。
- 第一阶段唯一合格来源：
  - 来源钩子必须来自 `AfterTakeDamage`，不是 `TakeDamage` 起始点。
  - `damageAmount` 必须 `> 0`。
  - `hazardType` 必须为 `1`。
- 以下情况不得推进回合：
  - 旧的骨钉命中累计。
  - 暗冲穿敌、拼刀无伤、无敌帧内敌伤、护符把伤害抵成 `0`、`damageAmount <= 0`。
  - 尖刺、酸、落坑等环境伤害。
- 被动结算规则：
  - 每次回合推进时，抓取当下所有 active orb 的快照，逐个调用其 `OnPassive(...)`。
  - 结算中被标记 `IsPendingRemoval` 的球，仍沿用现有“结算后统一移除”的处理方式。
  - 黄/白/黑球的 `OnPassive(...)` 数值、范围、目标选择逻辑本阶段完全不改。
- 回合计数规则：
  - `_roundCounter` 只作为运行时调试/语义承载，不写入 `OrbPersistentState`，不跨重启保存。
  - 场景切换不重置 `_roundCounter`；`ResetAll()` / 关模组 / 退出流程重置。
- 生命周期规则：
  - 回调入口 `OnHeroAfterTakeDamage(...)` 必须遵守现有 `_settings.Enabled` 与 `_isShuttingDown` 守卫。
  - 不允许在退出流程或禁用状态下推进回合。
- 文档一致性规则：
  - 代码切换到新回合系统后，README 中不得继续出现“3 hit passive”旧描述。

### 3.2 Implementation Checklist
- [ ] 1. 新建 `src/Orbs/RoundAdvanceSource.cs`，定义统一的回合推进来源枚举，当前仅含 `HeroTookDamage`。
- [ ] 2. 修改 `DeVect.cs`，用 `ModHooks.AfterTakeDamageHook` 替换旧的 `ModHooks.SlashHitHook` 注册/注销。
- [ ] 3. 在 `DeVect.cs` 新增 `OnHeroAfterTakeDamage(int hazardType, int damageAmount)` 包装方法，并保持原伤害值透传返回。
- [ ] 4. 修改 `src/Orbs/OrbSystem.cs`，删除 `_nailHitCounter`、slash 去重字段和 `OnSlashHit(...)` 旧路径。
- [ ] 5. 在 `src/Orbs/OrbSystem.cs` 中新增 `OnHeroTookDamage(...)`、`AdvanceRound(...)`、`ShouldAdvanceRoundFromHeroDamage(...)`，建立统一回合推进入口。
- [ ] 6. 保持 `TriggerPassiveOrbs(...)` 作为唯一被动结算器；确认白球移除、黑球储伤、黄球单体索敌等旧逻辑不被回合入口改坏。
- [ ] 7. 修改 `README.md` 与 `README-en.md`，同步把旧的 “3 次命中触发被动” 文案改成“受伤推进回合”。
- [ ] 8. 运行 `dotnet build -c Debug` 验证编译通过；若实现与本 Spec 有偏差，先回写 Spec 再继续。
- [ ] 9. 手测以下场景：
  - 持有黄/白/黑球时，被普通敌伤命中一次，会立即推进 `1` 回合并触发所有 active orb 被动。
  - 连续普通敌伤命中会按次数逐次推进，不再依赖 SlashHit 次数。
  - 旧的骨钉命中 `3` 次不再触发任何额外被动波次。
  - 暗冲穿敌、拼刀无伤、护符免伤为 `0` 伤害时，不推进回合。
  - 尖刺/酸/落坑伤害不推进回合。
  - 满槽挤出激发、切场景保留球序/储伤仍然正常。

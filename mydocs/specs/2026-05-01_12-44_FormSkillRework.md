# SDD Spec: Form Skill Rework

## 0. Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- Goal: 把当前 `三法术 -> 三类球/冰盾 + 多事件推进被动` 的系统，重构成 `冰形态 / 电形态` 双形态体系，并把技能输入、球生成、被动触发、环身特效与骨钉伤害 hook 统一到新的玩法规则上。
- In-Scope:
  - 重做施法输入拦截，支持 `法术键切换形态`、`下+法术` 小技能、`上+法术` 大技能。
  - `不按上/下，只按法术键` 时：只执行 `切换形态`，消耗 `1x` 法术 Soul，不释放原版法术。
  - 只保留 `电球` 和 `冰球` 两种球定义，删除白球与旧冰盾玩法的主流程地位。
  - 切换到电形态时，角色身上持续显示闪电缭绕特效；切换到冰形态时，角色身上持续显示冰雾缭绕特效。
  - 电形态：
    - 小技能：生成 `1` 个电球。
    - 大技能：生成 `当前房间累计已生成电球数` 个电球。
  - 冰形态：
    - 小技能：生成 `1` 个冰球。
    - 大技能：生成 `距离小骑士 20 范围内敌人数 + 2` 个冰球，并对范围内所有敌人造成 `1x` 骨钉伤害。
  - 被动触发规则改为：`每次切换形态时，触发所有当前球的被动`。
  - `电球`、`冰球` 的 `被动` 与 `激发` 效果默认保留当前既有机制；若本任务未明确要求修改，则保持原有机制不变。
  - 保留当前 `球槽上限 + 满槽挤出 + 激发` 机制。
  - 新增 `排队生球`：每个待生成的球间隔 `0.2s` 依次生成；生成期间 Hero 只能移动，不能攻击，但允许继续施法把新的生成请求排队。
  - 切换场景时，立即清空当前场上已生成但未消失的球，并清空当前房间累计已生成电球计数。
  - 玩家骨钉实际伤害改为运行时 hook 到 `1` 点，不改 PlayerData 存档值。
  - 玩家骨钉技也纳入 `1` 点伤害限制；其它可能共用 `AttackTypes.Nail` / `AttackTypes.NailBeam` 的来源，如果实现上安全且不易误伤，则一并限制，否则不额外扩张范围。
  - 电球与冰球一切基于骨钉伤害的数值，统一读取 `基础骨钉伤害 + 护符加成` 的运行时结果，而不是被强制改写后的实际对敌伤害值。
- Out-of-Scope:
  - 修改 PlayerData 或存档中的 `nailDamage` 持久值。
  - 新增全新美术资源包或外部依赖。
  - 保留旧版三法术各自独立球种的兼容逻辑。

## 1.5 Code Map (Project Topology)
- Core Logic:
  - `DeVect.cs`: Mod 生命周期、Hook 注册/卸载、Spell Control FSM 注入、场景切换、保存前清理、当前骨钉伤害读取入口。
  - `src/Orbs/OrbSystem.cs`: 当前核心玩法编排器；负责法术转球、被动/激发触发、场景恢复、球槽容量、Spell FSM 消耗判定。
  - `src/Fsm/SpellDetectAction.cs`: 插入 `Spell Control` FSM 的自定义 Action；当前用 `Vertical` 输入决定三种原版法术映射，并手动扣 Soul。
  - `src/Combat/OrbCombatService.cs`: 敌人搜索、球伤害投递、命中特效定位；后续也适合承接冰大招范围搜索与 AoE 伤害。
- Orb Definitions:
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 现有黄球（电系近义）被动/激发伤害规则。
  - `src/Orbs/Definitions/IceOrbDefinition.cs`: 现有冰球定义，但当前职责是叠加冰盾花瓣，不是直接攻击球。
  - `src/Orbs/Definitions/WhiteOrbDefinition.cs`: 现有白球定义；本任务按需求应移除其玩法地位。
  - `src/Orbs/Definitions/OrbTypeId.cs`: 球类型枚举，当前是 `Yellow / Black / White`。
  - `src/Orbs/Definitions/OrbDefinitionRegistry.cs`: 球定义注册表。
- Runtime & Persistence:
  - `src/Orbs/Runtime/OrbRuntime.cs`: 球槽运行时、左插右挤、移除塌缩、场景重建、环绕位置动画。
  - `src/Orbs/Runtime/OrbInstance.cs`: 单个球运行时实例，持有类型、定义、渲染器、当前伤害值。
  - `src/Orbs/Runtime/OrbInstanceSnapshot.cs`: 球持久化快照结构。
  - `src/Orbs/OrbPersistentState.cs`: 当前仅保存球快照；未来若需要形态或房间计数，也可能在此扩展或另建状态对象。
- Visuals:
  - `src/Visual/OrbVisualService.cs`: 球渲染、闪电命中特效、冰雾/冰晶瞬时特效；也是实现环身电特效/冰雾特效的首选入口。
  - `src/Visual/IceShieldDisplay.cs`: 旧冰盾 HUD 显示；若完全移除冰盾体系，该文件应退场或仅保留无用代码待清理。
  - `src/Visual/TransientVisual.cs`: 短时特效运行时数据。
- Combat State:
  - `src/Combat/IceShieldState.cs`: 旧冰盾花瓣计数与吸伤逻辑；若需求确认完全移除冰盾，此文件和相关 hook 将失去主要用途。
  - `src/Combat/HeroShadowDashDodgeTracker.cs`: 旧版“黑冲极限闪避推进回合”检测。
- Docs / Product Rules:
  - `README.md`: 目前文档仍描述旧版 `黄球/白球/黑球槽` 体系，后续执行阶段需要同步更新。
- External Dependencies:
  - `Spell Control` FSM on `HeroController`: 当前法术拦截注入目标。
  - `InputHandler.Instance?.inputActions / HeroActions`: HK 侧推荐的施法方向输入读取方式。
  - `On.HeroController.*`, `ModHooks.*`: 当前 Mod 主 Hook 面。

## 2. Architecture (Optional - Populated in INNOVATE)
- 本任务不进入 INNOVATE；优先在现有 `DeVect.cs -> SpellDetectAction -> OrbSystem -> OrbDefinition / Combat / Visual` 结构内做最小正确改造。
- 采用以下最小结构扩展：
  - `FormState`: 记录当前形态（电 / 冰）、当前房间累计已生成电球数、是否处于排队生球锁定中、当前待生成队列。
  - `QueuedOrbSpawn`（或更小改动的逐球队列）: 排队生成必须保持严格 FIFO；实现上应按“每个待生成球作为独立队列项”处理，避免把多球任务拆成“取 1 个后把剩余数量回插到队尾”，从而打乱不同施法之间的先后顺序。
  - `HeroAuraVisual`: 作为 `OrbVisualService` 内的新增持续特效分支，而不是新开独立视觉服务文件。
  - `NailDamageHook`: 直接挂在命中前的全局伤害入口上，尽量不修改既有球系统伤害逻辑。
- 关键设计取舍：
  - `被动效果不变`，因此电球继续复用现有 `YellowOrbDefinition`，冰球继续复用现有 `IceOrbDefinition` 的“当前定义语义”。
  - `切换形态` 只改被动触发时机，不改球定义内部数值公式。
  - `排队生球` 不重写 `OrbRuntime`；只在 `OrbSystem` 上层按 `0.2s` 节流调用现有 `HandleSpellCast(...)` / 满槽激发链路。
  - `骨钉伤害改 1` 不碰存档，不改 `GetCurrentNailDamage()`，而是在最终传给敌人的 `HitInstance` 上改写 `DamageDealt`，从而让球伤害仍能读取未被钩子污染的“基础骨钉伤害 + 护符加成”。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: DeVect.cs`
  - 新增 Hook 注册/卸载：
    - `On.HitTaker.Hit += OnHitTakerHit;`
    - `On.HitTaker.Hit -= OnHitTakerHit;`
  - 新增私有方法签名：
    - `private void OnHitTakerHit(On.HitTaker.orig_Hit orig, GameObject targetGameObject, HitInstance damageInstance, int recursionDepth)`
  - 保持现有 Spell FSM 注入入口，但其回调语义将从“按法术种类直生球”改为“切形态 / 入队形态技能”。
  - 行为约束：
    - `OnHitTakerHit(...)` 只在 `damageInstance.Source == HeroController.instance.gameObject` 且攻击类型属于 `AttackTypes.Nail` 或 `AttackTypes.NailBeam` 时考虑改写伤害。
    - 伤害改写发生在 `orig(...)` 之前，只改 `damageInstance.DamageDealt = 1`，不修改 `PlayerData.nailDamage`。
    - 若后续核对发现某些非 Hero 普攻来源也共用了 `AttackTypes.Nail/NailBeam` 且被误伤，则缩回到更保守的 Hero 来源判定，不再扩张。

- `File: src/Fsm/SpellDetectAction.cs`
  - 保留类名：`public sealed class SpellDetectAction : FsmStateAction`
  - 新增属性签名：
    - `public Action? OnNeutralSpellCast { get; set; }`
    - `public Func<bool>? ShouldConsumeNeutralSpell { get; set; }`
    - `public Func<bool>? ShouldConsumeBigSpell { get; set; }`
  - 现有属性 `OnFireballCast / OnDiveCast / OnShriekCast` 可保留或重命名复用，但最终语义应明确映射为：
    - `up` -> 大技能
    - `down` -> 小技能
    - neutral -> 切换形态
  - 行为约束：
    - 输入读取改为 `InputHandler.Instance?.inputActions`，不再使用 `Input.GetAxisRaw("Vertical")`。
    - `up.IsPressed` 时，若 `ShouldConsumeBigSpell == true`，执行“大技能回调”，手动扣除 `3x` 法术消耗并 `FSM CANCEL`。
    - `down.IsPressed` 时，若 `ShouldConsumeDiveSpell == true`，执行“小技能回调”，手动扣除 `1x` 法术消耗并 `FSM CANCEL`。
    - 无上下输入时，若 `ShouldConsumeNeutralSpell == true`，执行“切换形态回调”，手动扣除 `1x` 法术消耗并 `FSM CANCEL`。
    - 若对应条件不满足，则 `Finish()` 并让原 FSM 继续原始法术逻辑。

- `File: src/Orbs/OrbSystem.cs`
  - 新增类型/字段：
    - `private FormMode _currentForm = FormMode.Lightning;`
    - `private readonly Queue<QueuedOrbSpawn> _pendingSpawns = new();`
    - `private float _spawnQueueTimer;`
    - `private bool _isSpawnQueueLockActive;`
    - `private int _roomGeneratedLightningOrbCount;`
    - `private const float SpawnIntervalSeconds = 0.2f;`
  - 新增公开方法签名：
    - `public void OnNeutralSpellCast()`
    - `public bool ShouldConsumeNeutralSpell()`
    - `public bool ShouldConsumeBigSpell()`
    - `public void OnSmallSkillCast()`
    - `public void OnBigSkillCast()`
  - 新增私有方法签名：
    - `private void ToggleForm(HeroController hero)`
    - `private void TriggerAllOrbPassivesFromFormSwitch(HeroController hero)`
    - `private void EnqueueOrbSpawn(OrbTypeId orbType, int count)`
    - `private void TickSpawnQueue(HeroController hero, float deltaTime)`
    - `private bool TryProcessNextQueuedSpawn(HeroController hero)`
    - `private OrbTypeId GetOrbTypeForCurrentForm()`
    - `private int GetSmallSkillSoulCost()`
    - `private int GetBigSkillSoulCost()`
    - `private int GetCurrentSpellCastCost()`
    - `private int CountEnemiesWithinTwentyRange(HeroController hero)`
    - `private void DealIceBigSkillRoomAoe(HeroController hero, int baseDamage)`
    - `private void ClearRoomScopedState()`
  - 现有公开方法语义调整：
    - `OnFireballCast() / OnDiveCast() / OnShriekCast()` 最终都不再区分三法术球种，而是统一路由到 `OnNeutralSpellCast / OnSmallSkillCast / OnBigSkillCast` 的输入语义。
  - 行为约束：
    - 当前形态默认值建议为 `Lightning`，除非现有初始化更适合复用旧默认火球语义。
    - `neutral spell`：切换 `Lightning <-> Ice`，然后立刻触发当前场上所有球的被动；消耗 `1x` 法术魂；不推进旧回合系统。
    - `small skill`：按当前形态入队 `1` 个对应球。
    - `big skill`：
      - 电形态：入队 `当前房间累计已生成电球数` 个电球；若当前计数为 `0`，则该次大技能只消耗魂而不生成电球；并且这些通过大招生成的电球本身也会继续计入房间累计电球数。
      - 冰形态：先对 `20` 范围内所有敌人造成 `1x` 当前基础骨钉伤害，再入队 `敌人数 + 2` 个冰球。
    - 所有入队球按 `0.2s` 间隔依次调用现有球生成链路；若槽位已满，则沿用现有 `左插入 -> 右挤出 -> 激发` 机制。
    - 生成队列运行时 Hero 只能移动，不能攻击；但允许继续施法把更多生成请求压入队列。
    - 场景切换时必须清空：运行时球、排队生成、房间累计电球数、旧版回合推进临时状态。
    - `OnHeroUpdate(...)` 中继续维护 `_runtime.TickFollow()` / 视觉刷新，同时新增 `TickSpawnQueue(...)`。
    - 旧版 `受伤/拼刀/黑冲` 推进被动入口保留 Hook 但不再驱动球被动；其旧逻辑应从 `AdvanceRound(...)` 主链上退出，避免与“切形态触发被动”双重生效。

- `File: src/Orbs/Definitions/OrbTypeId.cs`
  - 保持当前枚举值不变，采用语义复用：
    - `Yellow` 作为电球。
    - `Black` 作为冰球。
    - `White` 不再产出，但为减小改动量可暂时保留枚举值以兼容旧快照和少量分支。

- `File: src/Orbs/Definitions/YellowOrbDefinition.cs`
  - 保留现有电球伤害语义与视觉语义。
  - 行为约束：
    - 本任务只修改其被动触发时机与生成入口，不改内部伤害公式。

- `File: src/Orbs/Definitions/IceOrbDefinition.cs`
  - 保留现有冰球被动/激发语义。
  - 行为约束：
    - 本任务只修改其生成入口与触发时机，不改内部效果公式，除非为脱离旧冰盾总线所必需的最小接线改动。

- `File: src/Combat/OrbCombatService.cs`
  - 新增方法签名：
    - `public int CountEnemiesInRange(HeroController hero, float horizontalRadius, float verticalRadius)` 或更小改动地新增 `public List<HealthManager> FindAllEnemiesInRadius(HeroController hero, float radius)`
  - 行为约束：
    - 冰大招的敌人计数与 AoE 伤害必须基于 `距 Hero 20 范围内` 的敌人，而不是沿用旧矩形 15/15/15/5 搜索框。
    - 冰大招 AoE 伤害值使用 `GetCurrentNailDamage()` 的结果，而不是被 `OnHitTaker.Hit` 改成 `1` 之后的最终落地伤害。

- `File: src/Visual/OrbVisualService.cs`
  - 新增持续视觉接口：
    - `public void TickHeroFormAura(HeroController hero, FormMode formMode, bool visible)`
    - `public void ClearHeroFormAura()`
  - 新增私有实现：
    - 1 组电形态闪电缭绕持续特效。
    - 1 组冰形态冰雾缭绕持续特效。
  - 行为约束：
    - 允许完全复用现有程序化闪电/冰雾/冰晶素材语言，不新增外部资源。
    - 形态环身特效需要跟随 Hero，且与球槽视觉互不覆盖。
    - 场景切换、停机、禁用 Mod 时必须完整清理。

- `File: src/Visual/IceShieldDisplay.cs`
  - 本轮不主动扩展。
  - 行为约束：
    - 若移除旧冰盾主流程后该 HUD 不再有入口，则应从 `OrbSystem` 的更新路径中退出显示，但文件可暂时保留。

- `File: README.md`
  - 执行阶段需要同步重写文档，确保玩法说明与新双形态体系一致。

### 3.2 Implementation Checklist
- [ ] 1. 更新 `mydocs/specs/2026-05-01_12-44_FormSkillRework.md`，固化“未说明则保留原机制”、双形态输入语义、排队生球、切场景清空与骨钉伤害 hook 边界。
- [ ] 2. 修改 `src/Fsm/SpellDetectAction.cs`，把输入读取改为 `InputHandler.Instance?.inputActions`，并把 `上/下/中立` 分别路由为 `大技能 / 小技能 / 切形态`。
- [ ] 3. 修改 `DeVect.cs`，调整 Spell FSM 注入接线，新增 `OnHitTaker.Hit` hook，并把骨钉 / 骨钉技最终伤害在命中前改为 `1`。
- [ ] 4. 修改 `src/Orbs/OrbSystem.cs`，新增当前形态、房间累计电球计数、排队生成、0.2s 节流、切形态触发所有球被动的主流程。
- [ ] 5. 修改 `src/Orbs/OrbSystem.cs`，让旧版 `受伤 / 拼刀 / 黑冲` 入口不再推进球被动，但保留未明确要求删除的其它既有机制。
- [ ] 6. 修改 `src/Combat/OrbCombatService.cs`，补充 `20` 范围内敌人统计与枚举能力，供冰大招 AoE 与生球数量使用。
- [ ] 7. 修改 `src/Visual/OrbVisualService.cs`，新增电形态闪电环身特效与冰形态冰雾环身特效，并接入场景切换/禁用清理。
- [ ] 8. 修改 `src/Orbs/Definitions` 与相关调度接线，仅保留电球和冰球的产出入口；白球不再生成，但其未被点名删除的底层代码可暂保留以减少改动。
- [ ] 9. 修改 `src/Orbs/OrbSystem.cs` 或相关清理入口，确保切场景时清空场上球、生成队列与房间累计电球数。
- [ ] 10. 修改 `README.md`，把玩法文档从旧版三法术三球体系更新为双形态 + 小大技能 + 切形态触发被动的新规则。
- [ ] 11. 运行 `dotnet build -c Debug`，验证编译通过；若实现与 Spec 不一致，先回写 Spec 再继续。

### 3.3 Behavioral Rules
- `未明确要求修改的地方，保留原有机制` 是本任务最高层局部兼容原则。
- `电球` 等价沿用当前 `YellowOrbDefinition`；`冰球` 等价沿用当前 `IceOrbDefinition`。
- 只修改：产出入口、被动触发时机、形态切换、排队生成、切场景清空、骨钉伤害 hook。
- 保留：槽位容量、满槽挤出、激发、球跟随与动画、现有伤害公式、现有程序化球体视觉语言、冰盾状态与其 HUD 展示链路。
- 切形态消耗 `1x` 法术 Soul，不释放原版法术，但必须 `FSM CANCEL`，避免原施法落地。
- 小技能消耗 `1x` 法术 Soul；大技能消耗 `3x` 法术 Soul；`法术扭曲者` 仍按现有折扣逻辑影响一次法术基础消耗，因此大技能总成本为 `3 * 当前单次法术成本`。
- 按当前 HK 原版施法链路，本轮实现沿用 `HeroController.TakeMP(int)`，因此启动施法时只检查并消耗 `MPCharge` 主魂，不直接把 `MPReserve` 当作可立即支付的法术成本来源；Soul Vessel 的后续表现仍交给原版 UI / FSM 链路处理。
- 生成队列期间 Hero 不能攻击；若实现上能安全限制更多主动攻击入口，可一并限制，但不得影响移动。
- 大技能在 Soul 不足 `3x` 时不得启动，也不得部分生效。
- 电形态大技能的生成数量取“本房间累计已生成电球数”，该计数在进入新房间时清零，并且由该大技能额外生成出来的每个电球也会继续增加该计数。
- 冰形态大技能先算敌人数，再造成 AoE，再按 `敌人数 + 2` 入队冰球；若范围内 `0` 敌人，也至少生成 `2` 个冰球。
- 场景切换时，无论是已生成球还是尚未出队的待生成球，全部清空。
- 骨钉实际对敌伤害改为 `1` 点，仅限可安全识别为 Hero 近战 / 骨钉技的 `AttackTypes.Nail` / `AttackTypes.NailBeam` 命中；若实现中无法无风险扩大到其它类型，则不强行扩大。
- 球伤害和冰大招 `1x` 骨钉伤害依然读取 `GetCurrentNailDamage()`，即“基础骨钉伤害 + 护符加成”结果，而不是命中前被 hook 成 `1` 的最终敌伤。

### 3.4 Verification Criteria
- 只按法术键时：切换电/冰形态，消耗 `1x` 法术 Soul，不放原版法术，并立即触发当前所有球被动。
- 下+法术时：按当前形态每次只排入 `1` 个对应球，按 `0.2s` 间隔生成。
- 上+法术时：电形态按“本房间累计电球数”排队生成；冰形态按“20 范围敌人数 + 2”排队生成，并对范围敌人立刻造成一次 `1x` 骨钉伤害。
- 排队期间 Hero 仍可移动，但不能攻击；再次施法可继续往队列中追加任务。
- 满槽时仍然走现有左插右挤与激发逻辑。
- 切场景后，场上球、排队队列、房间累计电球数全部清空。
- 电形态持续显示闪电缭绕；冰形态持续显示冰雾缭绕；切形态后视觉能即时切换。
- Hero 普通骨钉与骨钉技对敌最终伤害落地为 `1`，但电球/冰球伤害仍继续随着骨钉升级、力量、亡者之怒等提高。

### 3.5 Execution Notes
- 已将 `src/Fsm/SpellDetectAction.cs` 改为基于 `InputHandler.Instance?.inputActions` 读取 `up/down`，不再使用 `Input.GetAxisRaw("Vertical")`。
- 已把施法输入重映射为：`neutral -> 切形态`、`down -> 小技能`、`up -> 大技能`。
- 已将扣魂实现改为沿用 `HeroController.TakeMP(int)`：直接按 `1x / 3x` 成本扣主魂，并继续复用原版 `MP LOSE -> MP DRAIN` UI / Vessel 链路，而不是手工改写 `MPReserve`。
- 已在 `src/Orbs/OrbSystem.cs` 中落地：双形态状态、排队生球、`0.2s` 生成节流、房间累计电球数、切形态触发所有球被动、场景切换清空球与队列。
- 实现中严格遵守了“未明确要求修改的地方保留原机制”：黄球与黑球槽的被动/激发公式未改，槽位容量、左插右挤、激发仍沿用现有运行时。
- 已在 `src/Visual/OrbVisualService.cs` 中新增跟随 Hero 的持续形态 aura：电形态复用现有程序化电弧语言，冰形态复用现有冰雾/冰晶语言。
- 已在 `DeVect.cs` 中新增 `On.HitTaker.Hit` hook，并仅在 `damageInstance.Source == HeroController.instance.gameObject` 且攻击类型为 `AttackTypes.Nail / NailBeam` 时，把最终敌伤改为 `1`。
- 已在 `DeVect.cs` 中采用 `DoAttack + Attack + CanNailArt + StartCyclone` 组合拦截，确保排队期间普通骨钉与骨钉技都会被阻止。
- 已运行 `dotnet build -c Debug`，当前编译通过，`0` 警告、`0` 错误。
- 本轮只同步更新了 `README.md` 的核心玩法说明；`README-en.md` 仍保留旧文案，后续如需对外发布建议再补齐英文说明。

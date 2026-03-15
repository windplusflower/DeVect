# SDD Spec: 黑白法区分与萨满百分比加伤

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 在当前三球替代三法术的实现上，拉开黑法与白法的伤害定位：黑法保持当前强度，白法下调；同时按法术获取状态分别限制黄/白/黑球的生成资格；并把 `萨满之石` 从当前固定加值改为百分比加伤且支持向上取整。
- **In-Scope**:
  - 复核当前火球/上吼/下砸到黄/白/黑球的接管路径与生成判定。
  - 识别黑球、白球当前基础伤害与被动/激发倍率落点。
  - 识别 `萨满之石` 当前固定 `Focus +2` 的接入点与后续替换为百分比加伤的最佳位置。
  - 识别“是否已获得对应法术”的最小改动实现路径。
  - 为后续 PLAN 输出精确改动文件、函数签名与执行清单。
- **Out-of-Scope**:
  - 本阶段不直接改代码。
  - 本阶段不重做球槽系统、不重做其他护符（如法术扭曲者、灵魂捕手、噬魂者）。
  - 本阶段不扩展 UI 文案、本地化、存档迁移或外部配置系统。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `src/Orbs/OrbSystem.cs`: 三球系统总线；负责三法术到三球的入口映射、容量恢复、触发上下文构造，以及当前 `萨满之石 -> Focus +2` 与法术护符读取。
  - `src/Orbs/OrbTriggerContext.cs`: 三球定义共享上下文；当前只透传 `NailDamage` 与 `FocusBonus`，若改为百分比加伤，这里很可能需要新增统一伤害修正字段或方法。
  - `src/Combat/OrbCombatService.cs`: 提供 `ceil(1/3)` 等公共伤害工具；若百分比加伤要统一向上取整，这里适合承载通用取整工具。
- **Spell Replacement Entry Points**:
  - `src/Fsm/SpellDetectAction.cs`: Spell Control FSM 注入动作；按上下方向判断白法/黑法并手动扣蓝。若要做到“未获得对应法术则不能生成对应球”，这里和上层 `ShouldConsume*Spell` / `On*Cast` 配合关系需要一起调整。
  - `DeVect.cs`: Mod 入口；将 `SpellDetectAction` 注入 FSM，并把 `ShouldConsumeFireballSpell` / `ShouldConsumeDiveSpell` / `ShouldConsumeShriekSpell` 绑定到 `OrbSystem`。
- **Orb Definitions**:
  - `src/Orbs/Definitions/BlackOrbDefinition.cs`: 黑球定义；当前初值 `ceil(nail * 0.75) + FocusBonus`，被动每轮 `+ nail + FocusBonus`，激发打出累计值。用户要求黑法保持当前伤害，因此它将成为白球调整的基准参照。
  - `src/Orbs/Definitions/WhiteOrbDefinition.cs`: 白球定义；当前初值 `ceil(nail / 3) + FocusBonus`，被动对范围敌人造成当前值后 `-1`，激发对范围敌人造成 `2x 当前值`。这是本轮主要需要下调的目标。
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 黄球定义；当前同样读取 `FocusBonus`。若 `萨满之石` 改成“统一百分比加伤”，黄球也可能受影响，需在 PLAN 阶段明确是否纳入。
  - `src/Orbs/Definitions/IOrbDefinition.cs`: 定义统一初值接口 `GetInitialDamage(OrbTriggerContext context)`；适合作为三球初值百分比修正的统一注入点。
- **Charm / PlayerData Dependencies**:
  - `src/Orbs/OrbSystem.cs`: 当前通过 `PlayerData.GetBool("equippedCharm_19")` 判断 `萨满之石`，并固定返回 `2` 点 `FocusBonus`。
  - `src/Fsm/SpellDetectAction.cs`: 当前只读取 `equippedCharm_33` 处理 `法术扭曲者` 蓝耗折扣，没有读取法术获取状态。
  - `C:\Users\33361\.config\opencode\skills\hk-api\hkapi\CheatManager.cs:199`: 本地 HK 反编译源码表明三个法术升级入口分别写入 `fireballLevel`、`quakeLevel`、`screamLevel`，并在无任何法术时补 `hasSpell`；说明“对应法术是否已获得”可按三个 level 独立判断，而不是只看总开关 `hasSpell`。
  - `C:\Users\33361\.config\opencode\skills\hk-api\rules\systems\spell-system.md:157`: 技能规则文档确认法术升级状态就是通过 `shadeFireballLevel / shadeScreamLevel / shadeQuakeLevel` 一类等级字段区分升级态，说明按 spell-specific level 做判定是符合 HK 现有数据模型的。
- **Observed Reality**:
  - 当前工程已经做了四槽与 `Focus +2` 的一轮护符重平衡，但现在三球都共享同一套 `FocusBonus` 固定加值语义，不能体现用户要的“同一种球区分白法/黑法档位”。
  - 用户已明确纠正：这里的“黑法/白法”指的是 HK 三法术各自的二段升级关系，而不是黑球/白球本身；因此本轮核心不是调整“黑球 vs 白球”的相对定位，而是让同一种球根据对应法术等级区分白法档与黑法档。
  - 当前 `OrbSystem.ShouldConsumeFireballSpell()` 与 `ShouldConsumeDiveSpell()` 只判断 `CanProcess() && hero != null`，并未区分玩家是否拥有对应法术；`DeVect.cs:256` 的 `ShouldConsumeShriekSpell()` 甚至错误复用了 `ShouldConsumeFireballSpell()`，说明法术资格判定目前仍是空白区。
  - 当前 `萨满之石` 并不是真正的“百分比法伤加成”，而是被抽象成统一固定加值 `FocusBonus = 2`，其优点是实现简单，但无法表达“百分比、向上取整、不同球基线不同”的需求。
  - 用户已修正数值：`萨满之石` 百分比固定为 `+20%`，且作用于三种球；该百分比不是替换原伤害公式，而是“单独根据基础骨钉伤害计算额外加伤，再加到最终伤害上”，最终结果向上取整。
  - 用户进一步明确：`萨满之石` 的额外伤害不能只按“球最终伤害统一 +20% 骨钉”处理，而要跟随各结算点自己的基础倍率；也就是先对 `baseNailDamage * 0.2 * 该结算点倍率` 取上整，再加到该结算点最终伤害中。例如黄球黑法被动对应倍率是 `1/3`，因此萨满额外值应为 `ceil(baseNailDamage * 0.2 / 3)`。
  - 用户已明确指出当前黑球基线应视为“初值 `1x nail`、每轮储伤 `+1x nail`”；因此上一版 PLAN 中把黑球当前初值写成 `ceil(nail * 0.75)` 属于错误，需要回滚为 `1x nail` 口径后再设计白法档。
  - 额外复核发现两处现实问题需要纳入本轮计划：`DeVect.cs:256` 的 `ShouldConsumeShriekSpell()` 当前错误复用火球判定；`src/Fsm/SpellDetectAction.cs:68` 在火球 `ShouldConsumeFireballSpell()` 为 `false` 时仍会执行 `OnFireballCast()`，这会导致“未解锁法术不生成球”的目标无法成立。

## 2. Architecture (Optional - Populated in INNOVATE)
- 暂未进入 INNOVATE；当前先锁定事实与未知项。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Selected Scope
- 本轮仅实现三项：
  - 按三法术各自等级区分同种球的白法/黑法伤害。
  - 未获得对应法术时，不生成对应球，也不拦截原 FSM 的默认行为。
  - `萨满之石` 改为对三球统一提供 `ceil(基础骨钉伤害 * 25%)` 的额外加伤，并加到最终伤害上。
- 本轮不改：球槽容量、法术扭曲者、灵魂捕手、噬魂者、吸虫之巢。

### 3.2 Data Structures & Interfaces
- `File: src/Orbs/OrbSystem.cs`
  - 删除或停用现有固定 `FocusBonus` 语义，改为新增公开方法：
    - `public int GetShamanStoneBonusFromNailDamage()`
    - `public bool CanGenerateOrbForSpell(OrbTypeId orbType)`
  - 新增私有方法：
    - `private static int GetSpellLevelForOrb(PlayerData playerData, OrbTypeId orbType)`
    - `private static bool HasUnlockedSpellForOrb(PlayerData playerData, OrbTypeId orbType)`
  - 行为约束：
    - `GetShamanStoneBonusFromNailDamage()` 在装备 `equippedCharm_19` 时返回 `ceil(GetCurrentNailDamage() * 0.2)`，否则返回 `0`。
    - `CanGenerateOrbForSpell(...)` 需按映射关系分别检查：`Yellow -> fireballLevel > 0`、`White -> screamLevel > 0`、`Black -> quakeLevel > 0`。
    - `HandleSpellCast(...)` 在无法生成对应球时必须直接返回，不得生成球、不得触发挤出逻辑。

- `File: src/Orbs/OrbTriggerContext.cs`
  - 将现有字段从 `FocusBonus` 调整为：
    - `int BaseShamanBonus`
    - `Func<OrbTypeId, int> GetSpellLevel`
  - 行为约束：
    - `GetSpellLevel(orbType)` 返回该球对应法术的等级，取值仅允许 `0/1/2`；其中 `0` 表示未解锁，但正常生成链路不应在未解锁时进入。
    - `BaseShamanBonus` 仅表示 `ceil(baseNailDamage * 0.2)` 的原始基值；具体到每个被动/激发结算点时，仍需按该结算点的基础倍率折算后再向上取整。
    - 由于被动触发时运行时内可能同时存在黄/白/黑混合球，`OrbTriggerContext` 不能只携带单一 `SpellLevel`；必须支持按球种动态查询等级。

- `File: src/Orbs/Definitions/IOrbDefinition.cs`
  - 保留 `GetInitialDamage(OrbTriggerContext context)`。
  - 行为约束：
    - 三球定义内部必须显式区分 `SpellLevel == 1` 与 `SpellLevel == 2` 两档基础伤害。
    - `萨满之石` 额外值必须按“该结算点所对应的基础骨钉倍率”单独折算，不能直接把 `ceil(baseNailDamage * 0.2)` 原样加到所有结算点。

- `File: src/Orbs/Definitions/YellowOrbDefinition.cs`
  - 保留现有方法签名。
  - 行为约束：
    - 黄球需根据 `fireballLevel` 区分白法/黑法两档基础值。
    - `OnPassive(...)` 与 `OnEvocation(...)` 的最终伤害都需按“基础公式结果 + 该结算点倍率折算后的萨满额外值”结算。

- `File: src/Orbs/Definitions/WhiteOrbDefinition.cs`
  - 保留现有方法签名。
  - 行为约束：
    - 白球需根据 `screamLevel` 区分白法/黑法两档基础值；其中 `SpellLevel == 1` 的白法档必须低于当前实现，`SpellLevel == 2` 的黑法档保持当前实现体感不变。
    - 被动衰减 `-1` 规则保持不变。
    - 激发倍率如无必要不改，优先通过初值区分白/黑法。

- `File: src/Orbs/Definitions/BlackOrbDefinition.cs`
  - 保留现有方法签名。
  - 行为约束：
    - 黑球需根据 `quakeLevel` 区分白法/黑法两档基础值；`SpellLevel == 2` 的黑法档保持当前实现，`SpellLevel == 1` 的白法档在初值与/或每轮储伤上适当降低。
    - 激发伤害仍等于当前累计值。

- `File: src/Fsm/SpellDetectAction.cs`
  - 保留现有方法签名。
  - 行为约束：
    - 仅当对应的 `ShouldConsume*Spell()` 返回 `true` 时，才执行 `On*Cast()`、`ConsumeSpellCost()` 与 `Fsm.Event("FSM CANCEL")`。
    - 当 `ShouldConsume*Spell()` 为 `false` 时，必须完全放行原 FSM，不得调用对应 `On*Cast()`。
    - 修正当前普通火球分支在 `ShouldConsumeFireballSpell()` 为 `false` 时仍调用 `OnFireballCast()` 的问题。

- `File: DeVect.cs`
  - 保留现有 Hook 结构。
  - 行为约束：
    - `ShouldConsumeFireballSpell()`、`ShouldConsumeDiveSpell()`、`ShouldConsumeShriekSpell()` 都必须分别调用 `OrbSystem` 中各自正确的资格判断。
    - 修复 `ShouldConsumeShriekSpell()` 错绑到 `ShouldConsumeFireballSpell()` 的问题。

### 3.3 Damage Rules
- 统一萨满规则：
  - `baseNailDamage` 指 `GetCurrentNailDamage()` 的返回值，即已经包含力量、亡者之怒等现有骨钉修正后的基础骨钉伤害。
  - `BaseShamanBonus = hasShamanStone ? ceil(baseNailDamage * 0.2) : 0` 仅作为上下文原始基值；真正结算时仍需按该伤害点的倍率重新折算。
  - 通用规则为：`ShamanExtra = ceil(baseNailDamage * 0.2 * damageScale)`。
  - `damageScale` 指该结算点相对基础骨钉的倍率；例如：
    - 黄球黑法被动：`1/3`
    - 黄球白法被动：`1/4`
    - 黄球黑法激发：`1`
    - 黄球白法激发：`0.75`
    - 白球初值黑法：`1/3`
    - 白球初值白法：`1/4`
    - 黑球初值黑法：`1`
    - 黑球初值白法：`0.75`
    - 黑球被动储伤黑法：`1`
    - 黑球被动储伤白法：`0.75`
  - 对于白球这类“先生成初值、后用当前值结算 2x 激发”的球种，萨满额外值也应沿着其存储伤害语义传播：即萨满只在生成初值时按初值倍率加入一次，后续 `2x` 激发自然放大该累计值，不再在激发瞬间额外重复补一次独立萨满值。
- 法术等级映射：
  - 黄球使用 `fireballLevel`
  - 白球使用 `screamLevel`
  - 黑球使用 `quakeLevel`
- 白法/黑法规则：
  - `SpellLevel == 2`：保持当前黑法档表现。
  - `SpellLevel == 1`：在同球种内适当降低基础伤害。
  - `SpellLevel <= 0`：不生成对应球。

### 3.4 Formula Locking
- 为避免执行时二义性，本轮将“保持当前黑法伤害”解释为：
  - 某球种当前代码中的基础伤害公式，整体下沉为该球的 `SpellLevel == 2` 档。
- 将“白法适当降低”解释为：
  - 同球种新增 `SpellLevel == 1` 档，基础值低于当前代码；优先降低基础值，不改现有行为结构。
- 具体落地建议（推荐采用）:
  - `Yellow`：
    - 黑法档：维持当前 `被动 ceil(nail/3)`、`激发 nail`
    - 白法档：`被动 max(1, ceil(nail/4))`、`激发 max(1, ceil(nail*0.75))`
  - `White`：
    - 黑法档：维持当前 `初值 ceil(nail/3)`
    - 白法档：`初值 max(1, ceil(nail/4))`
    - 被动群伤与 `2x` 激发结构不变，只通过初值体现白/黑法差异
    - 萨满对白球的作用点仅在“初值生成”阶段；白球被动和激发直接使用包含萨满加值后的 `CurrentDamage`
  - `Black`：
    - 黑法档：维持当前 `初值 nail`、`每轮储伤 +nail`
    - 白法档：`初值 max(1, ceil(nail*0.75))`、`每轮储伤 +max(1, ceil(nail*0.75))`

### 3.5 Implementation Checklist
- [x] 1. 更新 `mydocs/specs/2026-03-15_16-54_SpellOrbDifferentiationAndShamanScaling.md`，修正“黑法/白法”语义，锁定 `萨满之石 +20%` 与“三球都吃百分比”的规则。
- [x] 2. 修改 `src/Fsm/SpellDetectAction.cs`，确保未解锁对应法术时完全放行原 FSM，不触发球生成也不扣蓝。
- [x] 3. 修改 `DeVect.cs` 与 `src/Orbs/OrbSystem.cs`，补齐三种法术各自的资格判断，并修复 `ShouldConsumeShriekSpell()` 错绑问题。
- [x] 4. 修改 `src/Orbs/OrbTriggerContext.cs`，将旧 `FocusBonus` 替换为按球种查询法术等级与按倍率计算萨满加伤的上下文能力。
- [x] 5. 修改 `src/Orbs/Definitions/YellowOrbDefinition.cs`，为火球对应球加入白法/黑法双档基础伤害，并按各自倍率折算 `萨满 +20%` 额外值。
- [x] 6. 修改 `src/Orbs/Definitions/WhiteOrbDefinition.cs`，为上吼对应球加入白法/黑法双档初值，并仅在初值生成阶段按对应倍率注入萨满额外值。
- [x] 7. 修改 `src/Orbs/Definitions/BlackOrbDefinition.cs`，为下砸对应球加入白法/黑法双档初值/储伤，并按各自倍率折算 `萨满 +20%` 额外值。
- [x] 8. 清理 `src/Orbs/OrbSystem.cs` 中旧的 `Focus +2` 逻辑，统一改为 `ShamanBonus` 注入。
- [x] 9. 运行 `dotnet build -c Debug` 验证编译通过。
- [x] 10. 复核最终实现与 Spec 一致；若公式或行为需偏离本计划，必须先回写 Spec。

### 3.6 Verification Criteria
- 未获得某法术时，按该方向输入不会生成对应球，也不会额外扣蓝或错误取消原 FSM。
- 已获得白法未获得黑法时，同球种伤害低于当前实现。
- 已获得黑法时，同球种伤害与当前实现一致，仅额外叠加 `萨满之石 +20%` 加成。
- 装备 `萨满之石` 后，三球所有伤害结算点都额外增加 `ceil(baseNailDamage * 0.2)`。
- `dotnet build -c Debug` 通过，且未误改球槽/其他护符行为。

### 3.7 Execution Notes
- 已在 `src/Fsm/SpellDetectAction.cs` 中移除“火球不可消费时仍调用 `OnFireballCast()`”的错误路径；现在只有对应 `ShouldConsume*Spell()` 为真时才会生成球、扣蓝并取消原 FSM。
- 已在 `DeVect.cs` 中修复 `ShouldConsumeShriekSpell()` 错绑到火球判定的问题。
- 已在 `src/Orbs/OrbSystem.cs` 中加入按 `fireballLevel` / `screamLevel` / `quakeLevel` 的法术资格与等级判断；未解锁对应法术时不会生成对应球。
- 已在 `src/Orbs/OrbTriggerContext.cs` 中移除旧 `FocusBonus` 固定加值语义，改为提供按球种读取法术等级、按倍率计算萨满额外值的上下文能力。
- 已在 `src/Orbs/Definitions/YellowOrbDefinition.cs` 中实现火球球的白法/黑法双档：白法被动 `1/4`、激发 `0.75x`；黑法保留被动 `1/3`、激发 `1x`；两者都按各自倍率叠加萨满额外值。
- 已在 `src/Orbs/Definitions/WhiteOrbDefinition.cs` 中实现上吼球的白法/黑法双档初值：白法 `1/4`、黑法 `1/3`；萨满只在初值生成阶段注入，后续被动与 `2x` 激发直接继承当前存储值。
- 已在 `src/Orbs/Definitions/BlackOrbDefinition.cs` 中实现下砸球的白法/黑法双档：黑法保留初值 `1x`、被动储伤 `+1x`；白法改为初值 `0.75x`、被动储伤 `+0.75x`；两者都按对应倍率叠加萨满额外值。
- 已运行 `dotnet build -c Debug`，构建通过，结果为 `0` 警告、`0` 错误。

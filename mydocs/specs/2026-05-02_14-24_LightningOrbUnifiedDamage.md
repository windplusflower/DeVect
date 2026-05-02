# SDD Spec: 电球统一伤害、README 纯机制化与冰大招萨满修正

## 0. Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- Goal: 修改当前电球规则，使其不再区分白波/黑波伤害档位，统一为 `被动 = ceil(当前骨钉伤害 x 1/3) + 对应护符额外值`、`激发 = ceil(当前骨钉伤害 x 2/3) + 对应护符额外值`；同时把 README 收敛为纯机制说明，并把冰形态大技能改为装备 `萨满之石` 时造成 `ceil(当前骨钉伤害 x 4/3)`。
- In-Scope:
  - 识别当前电球数值定义与护符加伤接入点。
  - 更新电球被动/激发公式，移除按 `fireballLevel` 区分白波/黑波的逻辑。
  - 保留当前“伤害吃护符加成”的行为，包括通过 `GetCurrentNailDamage()` 间接继承 `力量` / `亡者之怒`，以及通过 `萨满之石` 百分比补伤。
  - 修改 `OrbSystem` 中冰形态大技能的 AoE 伤害：默认仍为 `1x` 当前骨钉伤害，装备 `萨满之石` 时改为 `ceil(当前骨钉伤害 x 4/3)`。
  - 同步更新 README 中关于电球、冰形态大技能、解锁规则、护符联动和示例公式的描述。
  - 移除 README 中主观玩法建议、游玩提示、定位/Role 等非纯机制说明段落。
- Out-of-Scope:
  - 不修改冰球、冰盾、形态输入、球槽容量或其它球种数值。
  - 不调整 `GetCurrentNailDamage()` 的骨钉伤害来源。
  - 不引入新的配置项、开关或存档迁移逻辑。

## 1.5 Code Map (Project Topology)
- Core Logic:
  - `src/Orbs/OrbSystem.cs`: 球系统主编排器；创建 `OrbTriggerContext`，统一提供 `GetCurrentNailDamage()`、`GetScaledShamanBonus(...)` 所需上下文，并负责冰形态大技能 AoE 伤害的调用入口 `OnShriekCast()` / `DealIceBigSkillRoomAoe(...)`。
  - `src/Orbs/OrbTriggerContext.cs`: 电球定义读取的共享上下文；当前通过 `GetScaledShamanBonus(float damageScale)` 按具体倍率计算 `萨满之石` 额外伤害。
  - `DeVect.cs`: `GetCurrentNailDamage()` 入口；先读取 `PlayerData.nailDamage`，再叠加 `力量` 与 `亡者之怒`，因此电球基础伤害已经天然继承这两类护符收益。
- Orb Definitions:
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 当前电球定义；已改为统一倍率版本，`被动 = 1/3`、`激发 = 2/3`，并在每个结算点附加 `GetScaledShamanBonus(scale)`。
  - `src/Orbs/Definitions/IOrbDefinition.cs`: 电球定义遵循的统一接口；本轮不需要改签名。
- Documentation:
  - `README.md`: 中文主说明；当前已改为纯机制说明，电球使用统一伤害档位，并明确冰形态大技能的 `萨满之石 -> 4/3` 规则。
  - `README-en.md`: 英文说明；当前已与中文版本对齐，使用统一电球伤害描述，并明确 Ice-form big skill 的 `Shaman Stone -> 4/3` 规则。
- Dependencies:
  - `PlayerData.fireballLevel`: 当前只应继续承担“是否解锁波系法术”的资格语义，不再参与电球伤害档位切换。
  - `equippedCharm_19` (`萨满之石`): 通过 `OrbSystem.GetShamanStoneBonusFromNailDamage()` 与 `OrbTriggerContext.GetScaledShamanBonus(...)` 参与电球补伤。
- Observed Reality:
  - 工作区中的 `README.md` 与 `README-en.md` 当前已存在未提交改动；后续执行时必须基于现有内容增量修改，不能回滚。
  - 当前实现里的电球白波/黑波分档已经移除，并固定为 `被动 1/3`、`激发 2/3`。
  - 当前冰形态大技能已经改为：默认 `1x`，装备 `萨满之石` 时为 `ceil(current nail damage x 4/3)`。
  - 当前中英文 README 已移除 `定位 / Role`、`推荐思路 / Suggested Play Patterns`、`游玩提示 / Tips` 等主观段落，仅保留客观机制说明。

## 2. Architecture (Optional - Populated in INNOVATE)
- 本任务暂无显著架构权衡，预计可在现有结构上做最小改动；如后续发现 README 双语同步范围需要额外取舍，再在 PLAN 阶段锁定。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 File Changes
- `src/Orbs/Definitions/YellowOrbDefinition.cs`
  - 仅保留统一倍率常量，不再保留按白波/黑波分档的四个倍率常量。
  - 保留现有公开方法签名：
    - `public int GetInitialDamage(OrbTriggerContext context)`
    - `public void OnPassive(OrbTriggerContext context, OrbInstance instance)`
    - `public void OnEvocation(OrbTriggerContext context, OrbInstance instance)`
  - 允许调整或删除仅为分档服务的私有方法：
    - `private static float GetPassiveScale(OrbTriggerContext context)`
    - `private static float GetEvocationScale(OrbTriggerContext context)`
  - 保留统一私有伤害结算入口：
    - `private static int GetScaledDamage(OrbTriggerContext context, float scale)`
- `README.md`
  - 修改“解锁规则”中电球档位描述，去掉“白/黑阶段数值不同”，只保留“需要解锁波系法术才能生成电球”的资格语义。
  - 修改“电球”章节的被动/激发数值为统一档位。
  - 修改“冰形态大技能”与“萨满之石”描述，明确冰形态大技能默认 `1x`，装备 `萨满之石` 时为 `ceil(当前骨钉伤害 x 4/3)`。
  - 修改“护符联动 / 萨满之石”示例，使示例倍率与新的固定倍率一致。
  - 删除或改写 `定位`、`推荐思路`、`游玩提示` 等主观段落，只保留客观机制说明。
- `README-en.md`
  - 与中文 README 同步同一组规则变化，去掉 `Vengeful Spirit tier / Shade Soul tier` 对电球伤害的分档描述。
  - 同步冰形态大技能 `Shaman Stone -> 4/3` 规则。
  - 删除或改写 `Role`、`Suggested Play Patterns`、`Tips` 等主观段落，只保留客观机制说明。
- `src/Orbs/OrbSystem.cs`
  - 保留现有公开方法签名。
  - 新增或调整私有伤害计算入口：
    - `private int GetIceBigSkillDamage()` 或等价最小实现。
  - 调整 `OnShriekCast()` 中冰形态分支传入 `DealIceBigSkillRoomAoe(...)` 的伤害值来源。

### 3.2 Behavioral Contract
- 电球统一规则：
  - `Passive`: 对附近随机一名敌人造成 `ceil(current nail damage x 1/3)`，再叠加 `Shaman Stone` 对应倍率额外值。
  - `Evocation`: 对附近随机一名敌人造成 `ceil(current nail damage x 2/3)`，再叠加 `Shaman Stone` 对应倍率额外值。
- 护符规则保持：
  - `力量` / `亡者之怒` 继续通过 `GetCurrentNailDamage()` 抬高基础伤害。
  - `萨满之石` 继续通过 `GetScaledShamanBonus(scale)` 结算额外伤害，因此：
    - 被动额外值改为 `ceil(current nail damage x 0.2 x 1/3)`。
    - 激发额外值改为 `ceil(current nail damage x 0.2 x 2/3)`。
- 冰形态大技能规则更新：
  - 未装备 `萨满之石`：AoE 伤害保持 `1x current nail damage`。
  - 装备 `萨满之石`：AoE 伤害改为 `ceil(current nail damage x 4/3)`。
  - 该加成只作用于冰形态大技能的直接 AoE 命中，不改变后续冰球生成数量或冰球本身的攒盾效果。
- 保留不变：
  - 电球仍然只随机命中附近一名敌人。
  - 电球仍然使用现有闪电命中特效与 `AttackTypes.Generic` 投递。
  - `fireballLevel` 仍用于判断是否已解锁波系法术，但不再影响电球被动/激发倍率。
  - README 仍保留输入、消耗、解锁、球槽、数值和护符联动等机制说明，但去掉主观玩法建议和引导性提示。

### 3.3 Implementation Checklist
- [x] 1. 更新本 Spec，锁定“电球不再区分黑白法，统一 `1/3` 被动、`2/3` 激发，并继续吃护符加成”的契约。
- [x] 2. 修改 `src/Orbs/Definitions/YellowOrbDefinition.cs`，删除按 `fireballLevel` 切换白波/黑波倍率的逻辑，改为固定使用 `1/3` 与 `2/3`。
- [x] 3. 保留 `GetScaledShamanBonus(scale)` 接线，使 `萨满之石` 继续按新的固定倍率补伤。
- [x] 4. 重新修改 `README.md`，去掉主观玩法建议与游玩提示，只保留客观机制说明，并补充冰形态大技能的 `萨满之石 -> 4/3` 规则。
- [x] 5. 重新修改 `README-en.md`，去掉主观玩法建议与游玩提示，只保留客观机制说明，并补充冰形态大技能的 `Shaman Stone -> 4/3` 规则。
- [x] 6. 修改 `src/Orbs/OrbSystem.cs`，让冰形态大技能默认为 `1x`，装备 `萨满之石` 时改为 `ceil(current nail damage x 4/3)`。
- [x] 7. 运行 `dotnet build -c Debug`，确认编译通过且未引入接口错误。
- [x] 8. 复核 README 与代码实现一致；如实现中需要偏离本计划，先回写 Spec 再继续。

### 3.4 Verification Criteria
- `src/Orbs/Definitions/YellowOrbDefinition.cs` 中不再存在基于 `context.GetSpellLevel(OrbTypeId.Yellow)` 的电球伤害分档逻辑。
- 电球被动固定按 `1/3` 结算，激发固定按 `2/3` 结算。
- `萨满之石` 在电球被动与激发上仍分别按各自倍率给出额外伤害，而不是丢失或重复叠加。
- `src/Orbs/OrbSystem.cs` 中冰形态大技能对敌 AoE 在未装备 `萨满之石` 时保持 `1x`，装备后变为 `ceil(current nail damage x 4/3)`。
- `README.md` 与 `README-en.md` 不再把电球写成两档伤害，明确冰形态大技能的 `萨满之石 -> 4/3` 规则，并移除主观玩法建议/提示段落。
- `dotnet build -c Debug` 通过。

### 3.5 Execution Notes
- 推荐实现保持最小化：直接在 `YellowOrbDefinition` 中把常量替换为 `1f / 3f` 与 `2f / 3f`，删除不再需要的 `GetPassiveScale(...)` / `GetEvocationScale(...)`，避免改动 `OrbTriggerContext`、`OrbSystem` 或其它球定义。
- README 编辑必须基于当前工作区已有改动继续前进，不能覆盖或回滚现有文案重写。
- 已在 `src/Orbs/Definitions/YellowOrbDefinition.cs` 中移除基于 `fireballLevel` 的电球伤害分档；当前被动固定为 `1/3`，激发固定为 `2/3`，并继续沿用 `GetScaledShamanBonus(scale)`。
- 已在 `src/Orbs/OrbSystem.cs` 中新增最小伤害入口 `GetIceBigSkillDamage()`；当前冰形态大技能 AoE 默认 `1x` 当前骨钉伤害，装备 `萨满之石` 时改为 `ceil(current nail damage x 4/3)`。
- 已重新收敛 `README.md` 与 `README-en.md`：删除 `定位 / Role`、`推荐思路 / Suggested Play Patterns`、`游玩提示 / Tips` 等主观段落，只保留机制说明，并同步冰形态大技能的 `萨满之石 -> 4/3` 规则。
- 已运行 `dotnet build -c Debug`，构建通过，结果为 `0` 警告、`0` 错误。
- 已检查工作区内无意外生成的 `nul` / `NUL` 文件。

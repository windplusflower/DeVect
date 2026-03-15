# SDD Spec: 法术护符重做与三球数值校准

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 在现有“三球替代三法术”的基础上，重新设计原本围绕法术生效的护符，使其改为增强三球体系；同时复核黄/白/黑三球当前数值与原版火球/上吼/下砸的大致强度映射，提出需要调整的平衡方案。
- **In-Scope**:
  - 盘点当前模组中三球替代三法术的接管链路与现有数值实现。
  - 识别当前哪些原版护符在“三法术被替换后”会失效或价值显著下降。
  - 为每个纳入范围的护符寻找一个合适的 Defect 能力牌设计映射，并形成可执行的改造方向。
  - 对比原版法术强度语义与当前三球伤害/收益曲线，给出数值是否偏高/偏低的结论。
  - 明确后续实施预计会改动的代码落点。
- **Out-of-Scope**:
  - 本阶段不直接写代码，不修改任何运行时实现。
  - 本阶段不扩展到非护符的法术相关系统，如法术学习流程、法术升级获取方式、UI 文案本地化。
  - 本阶段不臆造无法验证的《杀戮尖塔 2》卡牌细节；无法访问外部资料处会显式标注风险。
  - 默认不改造与法术无直接关系的护符，如指南针、采集虫群、心脏/生命血/移动/骨钉技/梦钉/召唤物等，除非后续设计需要把某个“原本与法术弱相关”的护符纳入三球体系二期联动。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `src/Orbs/OrbSystem.cs`: 三球总调度中心；负责火球/上吼/下砸到黄/白/黑球的入口映射、满槽激发、骨钉累计触发被动，是护符效果最终最可能挂接的总线层。
  - `src/Combat/OrbCombatService.cs`: 统一负责索敌、伤害结算、命中特效定位；若护符影响目标选择、命中范围或附加伤害，这里大概率需要扩展。
  - `src/Visual/OrbVisualService.cs`: 三球与命中特效的唯一视觉服务；若护符引入新状态提示或额外视觉反馈，会落在这里。
- **Spell Replacement Entry Points**:
  - `src/Fsm/SpellDetectAction.cs`: Spell Control FSM 注入动作；当前已经把 `verticalInput` 映射为 `火球 -> 黄球`、`下砸 -> 黑球`、`上吼 -> 白球`，并手动处理法术耗蓝与 `Spell Twister` 折扣。
  - `DeVect.cs`: Mod 入口；负责把 `SpellDetectAction` 注入 Spell Control FSM，并把 SlashHit、HeroUpdate 等 Hook 转发到 `OrbSystem`。
- **Orb Definitions**:
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 黄球行为定义；当前被动为随机敌人 `ceil(nail/3)` 伤害，激发为随机敌人 `1x nail`。
  - `src/Orbs/Definitions/WhiteOrbDefinition.cs`: 白球行为定义；当前生成初值 `ceil(nail/2)`，被动对范围内全部敌人造成当前存储伤害后 `-1`，激发对全部敌人造成 `2x 当前存储伤害`。
  - `src/Orbs/Definitions/BlackOrbDefinition.cs`: 黑球行为定义；当前生成初值 `1x nail`，被动每轮 `+1x nail` 储伤，激发对范围内最低血目标造成全部储伤。
  - `src/Orbs/Definitions/OrbDefinitionRegistry.cs`: 三球注册点；后续若要引入“护符修饰后的定义分支/运行时策略”，这里可能要参与依赖装配。
- **Orb Runtime & State**:
  - `src/Orbs/Runtime/OrbRuntime.cs`: 三槽位生成、左插入、右挤出、初始伤害赋值；当前白球/黑球的初始值都在这里固化。
  - `src/Orbs/Runtime/OrbInstance.cs`: 单球运行时状态；`CurrentDamage` 已承载白球/黑球的动态数值，后续护符若改变储伤层数、倍率、触发次数，可直接作用于这里。
  - `src/Orbs/OrbTriggerContext.cs`: 球行为调用上下文，聚合 Hero、当前骨钉伤害、战斗服务和视觉服务，是护符加成读写的天然透传对象。
- **Charm / PlayerData Related Dependencies**:
  - `src/Fsm/SpellDetectAction.cs`: 当前已读取 `equippedCharm_33`（法术扭曲者）决定耗蓝 `24/33`，说明护符读取目前是散落在入口动作中的。
  - `DeVect.cs`: 当前 `GetCurrentNailDamage()` 已读取 `equippedCharm_25`（力量）与 `equippedCharm_6`（亡者之怒），说明数值修饰暂未抽象为统一护符服务。
  - `C:\Users\33361\.config\opencode\skills\hk-api\rules\core\item-ids.md`: 本地 HK API 规则文档已确认 `equippedCharm_19/20/21/33` 分别对应萨满之石、灵魂捕手、噬魂者、法术扭曲者。
  - `C:\Users\33361\.config\opencode\skills\hk-api\hkapi\SpellFluke.cs`: 本地反编译源码表明 Flukenest/虫群化火球存在单独替换实现，并且会受萨满之石影响，说明“火球相关护符”语义不能只看纯伤害倍率。
- **Observed Reality**:
  - 当前工程已经完成三球基础移植，法术接管链路完整，但护符效果仍基本停留在“原版法术语义”，因此替换后会出现部分护符失效、部分护符只影响耗蓝而不影响球表现。
  - 现有三球数值全部以“当前骨钉伤害”为基础，而不是以原版法术固定伤害/升级级差为基础；这意味着随骨钉升级成长的曲线会与原版法术成长曲线天然不同。
  - 黄球更像持续 chip + 单体激发；白球更像群体清场/持续衰减；黑球更像单体蓄力收割。三者内部定位已经形成，但未必和火球/上吼/下砸的原版定位完全一致。
  - 当前代码中还没有统一的“CharmEffectService / OrbModifierPipeline”；如果要让多个护符稳定影响三球，后续大概率需要先做护符读取抽象，而不是把判断继续散落到各球定义里。
  - 外部 wiki 当前无法直接抓取，研究阶段只能以本地 hk-api 文档、反编译源码和仓库实现为主；涉及《杀戮尖塔 2》能力牌一一映射时，存在资料缺口风险。
  - 已继续实测用户建议的 `action=query&prop=revisions&rvslots=main&rvprop=content&format=json` 方案，包含：`hkss.huijiwiki.com/api.php?action=query&prop=revisions&titles=护符&rvslots=main&rvprop=content&format=json`、`hkss.huijiwiki.com/api.php?action=query&prop=revisions&titles=技能_(空洞骑士)&rvslots=main&rvprop=content&format=json`、`sts2.huijiwiki.com/api.php?action=query&prop=revisions&titles=模板:查找/卡牌&rvslots=main&rvprop=content&format=json`；在当前 CLI 环境里也全部返回 Cloudflare `Just a moment...` challenge 页面，而非 JSON / wikitext。
  - 因此在当前 CLI 网络环境里，不能指望通过 MediaWiki API 直接拿到这三站内容；后续研究若不借助用户补充材料，只能依赖本地 hk-api 资料和仓库代码进行保守推导。
  - 用户已补充 `护符` 维基正文文本，已确认原版“直接与法术强相关”的护符至少包括：`灵魂捕手`（骨钉命中获得更多灵魂）、`萨满之石`（提高法术威力）、`噬魂者`（大幅提高骨钉命中获得的灵魂）、`法术扭曲者`（减少法术消耗）、`吸虫之巢`（将复仇之魂变成吸虫群）。
  - 用户补充文本还确认：HK 原版护符槽上限为 `11`，初始 `3`，额外可获得 `8`；这给后续若要讨论 `扩容/扩容+` 的映射提供了上限参考，但当前模组实现仍固定为三球槽，而非护符槽或可扩充球槽。
  - 从“原版法术护符语义”看，可先分三类：`资源类`（灵魂捕手、噬魂者、法术扭曲者）、`威力类`（萨满之石）、`行为替换类`（吸虫之巢）。这种分类与 Defect 候选牌的 `集中 / 循环 / 专属替换` 语义较容易对接。

## 2. Architecture (Optional - Populated in INNOVATE)
- **用户已补充的 Defect 候选能力牌素材**:
  - `暴涨+`: 失去 `1` 个充能球栏位，获得 `3` 点力量、`3` 点敏捷。
  - `扩容` / `扩容+`: 获得 `2 / 3` 个充能球栏位。
  - `碎片整理+`: 获得 `2` 点集中。
  - `回响形态` / `回响形态+`: 每回合打出的第一张牌会被打出两次。
  - `循环` / `循环+`: 在回合开始时，触发最右侧充能球的被动能力 `1 / 2` 次。
  - `旋转工艺` / `旋转工艺+`: 在回合开始时生成 `1` 个玻璃充能球；升级版额外立刻生成 `1` 个玻璃充能球。
  - `雷霆` / `雷霆+`: 每当激发闪电充能球时，对被命中的敌人造成 `6 / 8` 点伤害。
- **当前可用于护符改造的 Defect 设计语义抽象**:
  - `集中(Focus)`：最适合作为三球统一数值增益层，等价于“所有球的基础伤害/效果值上浮”。
  - `循环(Loop)`：最适合作为“自动额外触发球被动”的护符模板。
  - `雷霆(Electrodynamics/Thunder-like trigger bonus)`：最适合作为“黄球激发时追加定向伤害”的专属强化。
  - `旋转工艺(Auto-channel Frost/Glass)`：最适合作为“白球自动生成/续航”的专属强化。
  - `扩容(Capacitor)`：语义上对应球槽扩展，但当前模组固定三槽；若采用该牌思路，需要决定是直接扩槽，还是转译为“额外触发次数/储能上限/免费生成”。
  - `回响形态(Echo Form)`：最适合作为“首次触发重复结算”类高强度护符模板，但强度风险最高。
  - `暴涨+ (Hyperbeam-like drawback/payoff slot trade)`：更像高风险高收益的构筑型效果，适合映射到“减少球槽，换取面板收益”类护符，但与 HK 原版法术护符天然对应关系较弱。
- **已确认的 HK 法术护符候选池（优先级排序）**:
  - `高优先级-直接法术改造`：`萨满之石`、`法术扭曲者`、`吸虫之巢`。
  - `中优先级-法术资源循环`：`灵魂捕手`、`噬魂者`。
  - `低优先级-本轮先不碰`：其余与法术无直接关系的护符。
- **初步映射方向（待进入 INNOVATE 细化）**:
  - `萨满之石 -> 碎片整理+ / Focus`：给三球引入统一“集中”层，提升基础球伤/储伤/被动值。
  - `法术扭曲者 -> 循环 / 轻量循环`：在保留减耗蓝的同时，附加较轻的自动额外被动触发，强调“施法效率提升”。
  - `吸虫之巢 -> 黄球专属行为替换`：不再是单纯增伤，而是把黄球激发改写成分裂、多段、追踪或链式小体，语义上对齐“火球变成一群吸虫”。
  - `灵魂捕手 -> 轻量资源回流 / 轻量循环`：施放球后返还部分 soul，或在骨钉累计触发时给一个轻量额外收益。
  - `噬魂者 -> 更强资源回流 / 更强循环`：比灵魂捕手更强，但要避免把三球系统变成无限施法机。
- **推荐方案 A（平衡优先，建议采用）**:
  - 设计原则：严格遵守用户新约束；仅允许“直接采用已给出的 Defect 能力牌效果，可调数值”，禁止我自创混合效果。
  - `萨满之石`：保留推荐，直接映射 `碎片整理+`。
    - 方案：装备后获得 `+2 Focus`。
    - 落地含义：黄球被动/激发、白球生成初值、黑球生成与储伤都直接 `+2`。
    - 评价：这是最干净、最不需要二次解释的一项。
  - `法术扭曲者`：撤回上一版“减耗蓝 + 弱循环”的混合设计，因为这属于新创效果。
    - 可选方案 1：完全不改，继续只保留 HK 原版减耗蓝。
    - 可选方案 2：直接映射 `循环`，即“回合开始触发最右球被动 1 次”；但这会替换而不是叠加原版减耗蓝语义。
    - 当前推荐：若坚持“不创造新效果”，第一期先 **不改**，避免把它做成逻辑杂糅体。
  - `灵魂捕手`：撤回上一版“生成/激发返魂”，因为这不是用户提供的能力牌效果。
    - 当前可选 Defect 候选中，没有与“获得更多资源用于施法”完全等价且不失真的能力牌。
    - 当前推荐：第一期 **不改**。
  - `噬魂者`：同上。
    - 当前推荐：第一期 **不改**。
  - `吸虫之巢`：上一版“3 个追踪吸虫”的细化描述仍属我创造的实现语义，需要收紧。
    - 用户已明确指定改为 `扩容 / 扩容+` 路线，不再沿用“吸虫群”行为替换。
    - 由于当前模组固定 `3` 球槽，`扩容` 不能直接翻译成“新增真实槽位”，需要在 PLAN 阶段从以下两种严格等价转译里选一：
      - `方案 A`：把三槽上限临时扩为 `4/5` 槽，真正实现额外球槽；Defect 味最纯，但改动面较大。
      - `方案 B`：保持 UI 仍为三球，但允许额外“溢出缓存槽/隐藏槽”承载第 `4/5` 个球；逻辑上等价于扩容，视觉上更保守。
    - 当前推荐：如果要尽量忠于牌面，优先 `真实扩槽`，即 `吸虫之巢 -> 扩容`（+2 球槽）或弱化版 `+1 球槽`。
  - 三球基础值调整建议：
    - 黄球：维持现状，作为最依赖护符和 Focus 的稳定球。
    - 白球：基础值从 `ceil(nail/2)` 下调到 `max(1, ceil(nail/3))`。
    - 黑球：基础生成从 `1x nail` 下调到 `max(1, ceil(nail*0.75))`；被动累计保持 `1x nail`，让它更偏“养成终结”而不是“落地就高”。
    - 理由：在引入 Focus 后，白球和黑球天然收益更大，必须先压裸值。
- **备选方案 B（Defect 味更重，不推荐首期）**:
  - `萨满之石 -> Focus +2` 不变。
  - `法术扭曲者 -> 循环`：改为“每次进入战斗后，自动获得 1 层 Loop；每隔固定时间自动触发最右球被动”。
  - `灵魂捕手 -> 扩容`：不返魂，改为第一个球临时享受双槽位收益或额外被动次数。
  - `噬魂者 -> 扩容+`：更强版本。
  - `吸虫之巢 -> 回响形态`：让第一发黄球激发结算两次。
  - 评价：Defect 味道更浓，但会更脱离 HK 原护符语义，学习成本也更高。
- **风险评估与取舍**:
  - `Focus` 是必要抽象；没有这层，`萨满之石` 很难做得既统一又直观。
  - 在“禁止新创效果”的前提下，很多原本可做的混合翻译方案都必须砍掉；首期真正安全且纯净的只剩 `萨满之石 -> 碎片整理+`。
  - `Loop` 若要采用，必须完整作为某个护符的直接替换效果，而不能再和“减耗蓝”“返魂”之类原语义拼接。
  - `灵魂捕手/噬魂者` 若没有更贴近资源语义的候选牌，宁可不改，也不要硬套输出牌。
  - `吸虫之巢` 若没有更贴近“分裂/衍生体/追踪群体”的候选牌，宁可不改，也不要让我脑补实现。
- **推荐执行顺序**:
  - Phase 1：先引入统一 `Focus` 读取层，并落地 `萨满之石 -> 碎片整理+`。
  - Phase 2：若用户接受“直接替换原护符语义”，继续落地 `吸虫之巢 -> 扩容`。
  - Phase 3：最后再决定 `灵魂捕手` / `噬魂者` 是否保留不改，还是等待更多候选牌资料。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Selected Scope
- 本轮只落地两项护符改造与对应平衡收口：
  - `萨满之石 -> 碎片整理+`：引入统一 `Focus +2`。
  - `吸虫之巢 -> 扩容`：采用用户确认的 `真实扩槽 +1`。
- 本轮明确 **不改**：`法术扭曲者`、`灵魂捕手`、`噬魂者`。

### 3.2 Data Structures & Interfaces
- `File: src/Orbs/OrbSystem.cs`
  - 新增公开方法签名：
    - `public int GetCharmFocusBonus()`
    - `public int GetOrbSlotCapacity()`
  - 新增私有方法签名：
    - `private static bool HasShamanStone(PlayerData playerData)`
    - `private static bool HasFlukenest(PlayerData playerData)`
  - 行为约束：
    - `GetCharmFocusBonus()` 仅在装备 `equippedCharm_19` 时返回 `2`，否则返回 `0`。
    - `GetOrbSlotCapacity()` 默认返回 `3`；装备 `equippedCharm_11`（吸虫之巢）时返回 `4`。
    - 不得在 `OrbSystem` 中引入其他护符的自定义新效果。

- `File: src/Orbs/OrbTriggerContext.cs`
  - 新增只读属性/构造参数：
    - `int FocusBonus`
  - 行为约束：
    - `FocusBonus` 由 `OrbSystem.CreateTriggerContext(...)` 注入，供三球定义读取。

- `File: src/Orbs/Runtime/OrbRuntime.cs`
  - 保留现有签名，同时新增/调整：
    - `public int Capacity { get; }`
    - `public void EnsureCapacity(int capacity, Transform heroTransform, IReadOnlyList<OrbInstanceSnapshot> snapshots, OrbDefinitionRegistry definitions)`
    - `public bool TrySpawnOrbInNextAvailableSlot(OrbTypeId typeId, int initialDamage, OrbDefinitionRegistry definitions)`
    - `public bool TryForceInsertOrbFromLeft(OrbTypeId newTypeId, int initialDamage, OrbDefinitionRegistry definitions, out OrbInstance? evictedOrb)`
  - 行为约束：
    - 槽位容量必须支持 `3` 与 `4` 两种状态；本轮不支持更高值。
    - 当容量为 `4` 时，插槽布局需要新增第 4 个真实槽位，并保持“左插入、右挤出”的既有语义不变。
    - 容量变化后，所有占位虚线、球体跟随、补位、移除逻辑都必须按新槽位数完整工作。
    - `TrySpawnOrbInNextAvailableSlot(...)` 与 `TryForceInsertOrbFromLeft(...)` 不再自己计算基础伤害，而是接收外部已算好的 `initialDamage`，避免把 Focus 规则散落到运行时层。

- `File: src/Orbs/Runtime/OrbSlotRuntime.cs`
  - 若当前实现固定为三槽常量/索引，需改为支持动态索引集合。
  - 行为约束：
    - 不得再假设只有 `Left/Center/Right` 三个位；需要允许第 4 槽参与动画、补位和虚线显示。

- `File: src/Orbs/Definitions/YellowOrbDefinition.cs`
  - 保留现有签名：
    - `public void OnPassive(OrbTriggerContext context, OrbInstance instance)`
    - `public void OnEvocation(OrbTriggerContext context, OrbInstance instance)`
  - 行为约束：
    - 黄球被动伤害从 `ceil(nail/3)` 改为 `ceil(nail/3) + FocusBonus`。
    - 黄球激发伤害从 `max(1, nail)` 改为 `max(1, nail) + FocusBonus`。

- `File: src/Orbs/Definitions/WhiteOrbDefinition.cs`
  - 保留现有签名。
  - 行为约束：
    - 白球生成初值不再由 `OrbRuntime.GetInitialDamageForOrb(...)` 直接只看骨钉，而是由上层传入“已含 Focus 的初值”。
    - 白球本轮基础值从 `ceil(nail/2)` 下调为 `max(1, ceil(nail/3))`，再叠加 `FocusBonus`。
    - 白球每次被动衰减仍固定 `-1`，不受 Focus 影响。

- `File: src/Orbs/Definitions/BlackOrbDefinition.cs`
  - 保留现有签名。
  - 行为约束：
    - 黑球生成初值改为 `max(1, ceil(nail * 0.75)) + FocusBonus`。
    - 黑球每次被动储伤改为 `max(1, nail) + FocusBonus`。
    - 黑球激发伤害继续等于当前累计值，不再额外乘系数。

- `File: src/Orbs/Definitions/IOrbDefinition.cs`
  - 如当前接口不足以让“生成初值”和“被动/激发”都共享 Focus 规则，可新增：
    - `public int GetInitialDamage(OrbTriggerContext context)`
  - 若采用该方案，则三球初值统一下沉到定义层；否则由 `OrbSystem` 在生成前集中计算。
  - 推荐：优先新增 `GetInitialDamage(...)`，让三球自己的基础值逻辑留在各自定义里。

- `File: DeVect.cs`
  - 保留现有 Hook 结构。
  - 行为约束：
    - 不新增新的护符 Hook；只复用 `PlayerData` 读取 `equippedCharm_19` 和 `equippedCharm_11`。
    - 若 `GetCurrentNailDamage()` 已含力量/亡者之怒，这些结果应继续作为三球基础输入；`Focus` 只在法术护符层额外叠加，不能覆盖骨钉相关加成。

### 3.3 Behavioral Rules
- `萨满之石` 只实现 `碎片整理+` 的等价语义：`+2 Focus`；不附带任何其他额外触发、返魂或特殊动画规则。
- `吸虫之巢` 只实现 `扩容` 的等价语义：真实球槽 `+1`；不附带分裂弹、追踪体或额外伤害规则。
- 未装备 `吸虫之巢` 时，系统表现必须与当前三槽版本一致。
- 装备 `吸虫之巢` 后，三球系统容量变为 `4`；第 4 槽必须真实存在、可见、可补位、可被右侧挤出。
- 满 `4` 槽时再次施法，仍然遵循“左插入 -> 右挤出 -> 挤出球激发”的总规则。
- Focus 加值必须直接进入球基础值，不得通过独立乘区或隐藏倍数实现。
- 白球/黑球基础值下调属于为 `Focus` 接入做的平衡配套，必须与护符改造同轮落地。
- `法术扭曲者`、`灵魂捕手`、`噬魂者` 本轮保持原样，不在代码中留下半成品开关或占位逻辑。

### 3.4 Implementation Checklist
- [x] 1. 更新 `mydocs/specs/2026-03-15_15-35_CharmRebalanceAndOrbTuning.md`，锁定“禁止新创效果”、`吸虫之巢 -> 真实扩槽 +1` 与本轮范围。
- [x] 2. 复核 `src/Orbs/Runtime/OrbRuntime.cs`、`src/Orbs/Runtime/OrbSlotRuntime.cs` 与相关可视布局代码，明确三槽常量、索引、布局半径、动画逻辑的所有固定点，并回写 Spec 如有遗漏。
- [x] 3. 设计并实现四槽真实布局方案，修改 `src/Orbs/Runtime/OrbRuntime.cs` 及相关运行时数据结构，使容量可在 `3/4` 间切换。
- [x] 4. 修改 `src/Orbs/OrbSystem.cs` 与 `src/Orbs/OrbTriggerContext.cs`，接入 `FocusBonus` 与 `OrbSlotCapacity` 读取，并把容量/Focus 注入运行时与触发上下文。
- [x] 5. 修改三球定义文件，使黄/白/黑按选定公式读取 `FocusBonus`；同时下调白球与黑球基础裸值。
- [x] 6. 修改 `src/Orbs/Definitions/IOrbDefinition.cs` 及生成链路，把初始伤害计算从 `OrbRuntime` 下沉到定义层。
- [x] 7. 运行 `dotnet build -c Debug`，确保四槽运行时与 Focus 改造编译通过。
- [x] 8. 进行代码复核，确认未误改 `法术扭曲者`、`灵魂捕手`、`噬魂者` 的任何行为；若现实与 Spec 不一致，先回写 Spec 再继续。

### 3.5 Verification Criteria
- 未装备 `萨满之石`、`吸虫之巢` 时，系统仍为原三槽版本，三球基础行为与当前一致（除白/黑为 Focus 平衡做的基础值调整外）。
- 装备 `萨满之石` 后，黄球被动/激发、白球初值、黑球初值与储伤都稳定获得 `+2`。
- 装备 `吸虫之巢` 后，界面上能真实看到第 `4` 个球槽，第 `4` 个槽位能生成球、补位、显示虚线，并参与右侧挤出。
- 装备 `吸虫之巢` 且满 `4` 槽时，再次施法会挤出最右球并正确激发。
- 白球新裸值应低于旧版，黑球新初值应低于旧版，但带 `萨满之石` 时手感不应明显弱于当前版本。
- 构建通过，且没有引入与本轮无关的新护符逻辑。

### 3.6 Execution Notes
- 已将 `src/Orbs/Runtime/OrbRuntime.cs` 从固定三槽常量实现改为支持 `3/4` 动态容量的真实槽位运行时；新增第 4 槽后仍保持“右侧 oldest、左插入、右挤出”的总语义。
- 扩槽时，旧三槽快照会整体右移到四槽布局中的 `1/2/3` 位，保留新增的最左槽作为额外容量；缩回三槽时会移除最左额外槽并把其余球左移回 `0/1/2`。
- 已在 `src/Orbs/OrbSystem.cs` 中接入 `equippedCharm_19`（萨满之石）与 `equippedCharm_11`（吸虫之巢）读取，分别输出 `Focus +2` 与 `容量 4`。
- 已在 `src/Orbs/Definitions/IOrbDefinition.cs` 新增 `GetInitialDamage(OrbTriggerContext context)`，把白/黑初值计算下沉到各球定义中，避免 `Focus` 规则散落到运行时层。
- 已在 `src/Orbs/Definitions/YellowOrbDefinition.cs`、`src/Orbs/Definitions/WhiteOrbDefinition.cs`、`src/Orbs/Definitions/BlackOrbDefinition.cs` 中按方案接入 `FocusBonus`，并同步下调白球/黑球基础裸值。
- 已运行 `dotnet build -c Debug`，编译通过；本轮未改动 `src/Fsm/SpellDetectAction.cs`，因此 `法术扭曲者` 仍只保留原有减耗蓝实现，没有被混入新效果。

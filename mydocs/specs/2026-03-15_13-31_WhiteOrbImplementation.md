# SDD Spec: 白球（玻璃球）实现

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 在现有黄球系统基础上实现第二种球 `白球（玻璃球）`，并持续统一优化球系视觉表现。白球由小骑士原本的上吼法术生成，具备 `被动` 与 `激发` 两种触发方式，并拥有随被动逐次衰减的独立伤害值。
- **In-Scope**:
  - 黄球与白球的所有伤害基数统一改为“实际骨钉伤害”，必须吃护符伤害加成，不再直接读取未修正的 `PlayerData.nailDamage` 基础值。
  - 白球基础伤害 = `ceil(当前骨钉伤害 / 2)`。
  - 白球被动：每次触发时，对黄球同款矩形判定框内 `所有敌人` 造成当前白球伤害；结算后该白球当前伤害 `-1`。
  - 白球伤害衰减：某个白球当前伤害减到 `0` 后自动消失；若其左边仍有球，则左侧球向右滚动补位。
  - 白球激发：沿用当前球系统的激发入口，伤害为该白球“当前被动伤害”的 `2 倍`；激发伤害读取的是衰减后的实时值。
  - 白球索敌范围与黄球完全一致，复用当前 `OrbCombatService` 的矩形范围参数。
  - 白球生成取代小骑士原本的 `吼 / Shriek` 施法效果；火球继续走黄球生成逻辑。
  - 白球贴图需要有玻璃碎片与高光层次，整体保持立体感。
  - 白球命中特效默认采用“白色碎玻璃迸裂 + 短暂折射环”方案，风格偏脆裂、锐利、轻量，不走电击语义。
  - 黄球命中特效从“外部闪电图标贴片”升级为运行时绘制的真实电弧风格：整体应为 `细长`、`交错`、`偏蓝白冷色` 的闪电束，不允许继续使用“黄、粗、卡通感强”的旧风格。
  - 黄球新特效需要保留“电击”语义，但视觉方向改为接近真实雷暴分叉：主弧线清晰、辅弧线错位、亮芯 + 冷色外晕，避免图标感和平面贴纸感。
  - 黄球命中特效在战斗实测中的可见性必须高于当前版本；允许进一步提高亮度、尺寸、分叉密度、端点爆闪和外晕范围，但仍需保持“细长锐利”而非“粗胖卡通”。
- **Out-of-Scope**:
  - 黑球设计与第三种球生成逻辑。
  - 新增配置菜单、数值可配置化、音效系统扩展。
  - 重做黄球机制或改动当前黄球索敌范围参数。
  - 为黄球命中特效新增外部 PNG 美术资源；默认优先采用运行时绘制或程序化生成。

## 1.5 Code Map (Project Topology)
- **Core Logic**:
  - `DeVect.cs`: 模组生命周期入口；注册 HeroUpdate、SlashHit、Spell Control FSM 注入与场景切换钩子。
  - `src/Orbs/OrbSystem.cs`: 当前球系统主编排器；负责普通生成、满槽激发、被动触发、持久化同步。
  - `src/Combat/OrbCombatService.cs`: 当前矩形索敌、随机单体选择、伤害结算、闪电特效定位的统一战斗服务。
  - `src/Visual/OrbVisualService.cs`: 当前球体贴图生成与瞬时特效管理中心；已具备黄球立体材质层、白球玻璃特效，以及黄球旧版闪电图片加载能力，是本次黄球视觉重绘的核心改动点。
  - `src/Orbs/Runtime/OrbRuntime.cs`: 当前三槽球运行时；负责槽位跟随、插入动画、右侧挤出、补位与运行时快照。
- **Entry Points**:
  - `DeVect.cs`: `HandleFireballCast()` -> 当前黄球生成入口。
  - `src/Fsm/FireballDetectAction.cs`: 当前 Spell Control FSM 自定义 Action，只区分横向 fireball 输入。
  - `src/Orbs/OrbSystem.cs:80`: `OnFireballCast()` -> 当前“施法生成球”总入口，后续需要扩展为区分火球/吼。
  - `src/Orbs/OrbSystem.cs:153`: `TriggerPassiveOrbs(...)` -> 当前全部已存在球的被动批量触发入口。
- **Data Models**:
  - `src/Orbs/Definitions/OrbTypeId.cs`: 已预留 `White = 3`，说明白球类型枚举已存在但未接入实现。
  - `src/Orbs/Definitions/IOrbDefinition.cs`: 球定义接口，仅暴露颜色、被动、激发；暂未承载“实例可变伤害”。
  - `src/Orbs/Runtime/OrbInstance.cs`: 当前仅保存 `TypeId / Definition / Renderer`，尚不支持白球独立当前伤害值。
  - `src/Orbs/OrbPersistentState.cs`: 当前只记录球类型顺序，未记录白球衰减后的当前伤害。
  - `src/Orbs/Runtime/OrbInstanceSnapshot.cs`: 当前快照仅保存 `TypeId / SlotIndex`，后续若要切场景保留白球伤害，需要扩展实例快照。
- **Dependencies**:
  - `Modding.ModHooks.HeroUpdateHook` / `SlashHitHook`: 驱动球运行时与被动触发。
  - `PlayMakerFSM` + `Spell Control`: 施法拦截核心；根据 `Input.GetAxisRaw("Vertical") > 0.1f` 可识别上吼。
  - `Assembly-CSharp.HealthManager` / `HitInstance`: 对敌结算伤害。
  - `UnityEngine.Physics2D.OverlapBoxNonAlloc`: 当前矩形范围检索实现，可扩展出“范围内所有敌人”枚举。
  - `assets/闪电.png`: 当前黄球命中特效旧资源；本轮需求目标是弱化乃至移除其视觉主导地位，改为更真实的蓝白细电弧程序化表现。

## 2. Architecture (Optional - Populated in INNOVATE)
- Strategy/Pattern: 直接沿用现有模块化结构，不新开架构层；在 `OrbSystem` 中把“施法生成球”从单一 fireball 扩展为“按法术类型路由到不同球类型”，并把白球的衰减伤害作为 `OrbInstance` 级别可变状态管理。
- Trade-offs:
  - 优点：复用现有黄球索敌框、动画、补位与特效生命周期管理，改动集中在定义层、运行时实例层、Spell Control 拦截层。
  - 代价：`OrbPersistentState` / `OrbInstanceSnapshot` 需要从“仅类型”升级到“类型 + 可变实例数据”，否则白球跨场景会丢失当前伤害现实。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: DeVect.cs`
  - `class DeVectMod`
    - 调整注入方法签名：
      - `private bool InjectSpellDetector(PlayMakerFSM fsm, string stateName)`
    - 新增回调方法签名：
      - `private void HandleShriekCast()`
      - `private bool ShouldConsumeShriekSpell()`
    - 保持现有：
      - `private void HandleFireballCast()`
      - `private bool ShouldConsumeFireballSpell()`
      - `private bool ShouldConsumeShriekSpell()`（实现上复用球系统的统一可消费判定）
    - 责任变更：
      - 在 Spell Control FSM 注入的 Action 中，同时支持横向火球与上吼分流。
      - 上吼被拦截后不执行原版施法，改为生成白球或触发满槽激发。
      - `GetCurrentNailDamage()` 必须返回“实际骨钉伤害”，至少纳入力量护符（`equippedCharm_25`）和亡者之怒（`equippedCharm_6 && health == 1`）乘区，再做整数取整。

- `File: src/Fsm/FireballDetectAction.cs`
  - 重命名为：`src/Fsm/SpellDetectAction.cs`
  - `public sealed class SpellDetectAction : FsmStateAction`
    - 新增属性：
      - `public Action? OnFireballCast { get; set; }`
      - `public Action? OnShriekCast { get; set; }`
      - `public Func<bool>? ShouldConsumeFireballSpell { get; set; }`
      - `public Func<bool>? ShouldConsumeShriekSpell { get; set; }`
    - 行为约束：
      - `verticalInput > 0.1f` 视为上吼，调用 `OnShriekCast`。
      - `-0.1f <= verticalInput <= 0.1f` 视为火球，调用 `OnFireballCast`。
      - 若对应 `ShouldConsume...` 返回 `true`，则手动扣蓝、发送 `FSM CANCEL`、阻断原法术。
      - 不处理下砸，保留原逻辑穿透。

- `File: src/Orbs/OrbSystem.cs`
  - 新增公开方法签名：
    - `public void OnShriekCast()`
  - 新增私有方法签名：
    - `private void HandleSpellCast(OrbTypeId spawnType)`
  - 责任变更：
    - `OnFireballCast()` 固定传入 `OrbTypeId.Yellow`。
    - `OnShriekCast()` 固定传入 `OrbTypeId.White`。
    - 当槽位未满时，生成对应类型球。
    - 当槽位已满时，沿用当前 `TryForceInsertOrbFromLeft(...)`，被挤出的旧球执行其自身定义的 `OnEvocation(...)`。
    - 每次被动触发后，若某白球伤害降到 0，则要求运行时立即移除并执行左侧补位，同时刷新持久化状态。

- `File: src/Orbs/Definitions/WhiteOrbDefinition.cs`
  - 新增：`internal sealed class WhiteOrbDefinition : IOrbDefinition`
    - 属性：
      - `public OrbTypeId TypeId => OrbTypeId.White`
      - `public string DisplayName => "White"`
      - `public Color OrbColor => new(0.92f, 0.96f, 1f, 1f)`
    - 方法：
      - `public void OnPassive(OrbTriggerContext context, OrbInstance instance)`
      - `public void OnEvocation(OrbTriggerContext context, OrbInstance instance)`
    - 行为约束：
      - 若 `instance.CurrentDamage <= 0`，直接返回。
      - 被动：对范围内所有敌人逐个调用伤害结算，伤害值取 `instance.CurrentDamage`。
      - 被动成功执行后，无论命中敌人数是否为 0，都将 `instance.CurrentDamage -= 1`。
      - 若衰减后 `instance.CurrentDamage <= 0`，标记该白球待移除。
      - 激发：伤害值为 `instance.CurrentDamage * 2`，不再额外衰减；若当前伤害已为 0，则不造成伤害。
      - 命中成功时，调用白球碎裂特效。

- `File: src/Orbs/Definitions/OrbDefinitionRegistry.cs`
  - 调整构造注册：
    - `new YellowOrbDefinition(), new WhiteOrbDefinition()`
  - 保持：
    - `GetDefaultTypeForFireball() => OrbTypeId.Yellow`

- `File: src/Orbs/Runtime/OrbInstance.cs`
  - 调整构造签名：
    - `public OrbInstance(OrbTypeId typeId, IOrbDefinition definition, SpriteRenderer renderer, int currentDamage = 0)`
  - 新增可变属性：
    - `public int CurrentDamage { get; set; }`
    - `public bool IsPendingRemoval { get; set; }`
  - 行为约束：
    - 黄球默认 `CurrentDamage = 0`，不参与白球衰减逻辑。
    - 白球生成时必须写入其初始伤害。

- `File: src/Orbs/Runtime/OrbInstanceSnapshot.cs`
  - 调整结构字段：
    - `public OrbInstanceSnapshot(OrbTypeId typeId, int slotIndex, int currentDamage)`
    - `public int CurrentDamage { get; }`

- `File: src/Orbs/OrbPersistentState.cs`
  - 责任变更：
    - `FilledOrbSequence` 从 `List<OrbTypeId>` 升级为 `List<OrbInstanceSnapshot>`。
  - 方法签名：
    - `public List<OrbInstanceSnapshot> FilledOrbs { get; } = new();`
    - `public void ReplaceFromRuntime(IReadOnlyList<OrbInstanceSnapshot> snapshots)`
    - `public int GetFilledCount()`
    - `public void Clear()`

- `File: src/Orbs/Runtime/OrbRuntime.cs`
  - 新增公开方法签名：
    - `public bool RemoveOrb(OrbInstance instance)`
  - 新增私有方法签名：
    - `private void CollapseSlotsAfterRemoval(int removedSlotIndex)`
    - `private void AssignInstanceToSlot(OrbSlotRuntime slot, OrbInstance instance, int targetSlotIndex, float duration)`
    - `private int GetInitialDamageForOrb(OrbTypeId typeId, int nailDamage)`
  - 责任变更：
    - `EnsureBuilt(...)` 从 `OrbPersistentState.FilledOrbs` 恢复类型与白球当前伤害。
    - `TrySpawnOrbInNextAvailableSlot(...)` / `TrySpawnOrbInSlot(...)` 接收 `nailDamage` 入参，并在生成白球时初始化 `CurrentDamage = ceil(nailDamage / 2)`。
    - `SnapshotActiveOrbs()` 需要记录 `CurrentDamage`。
    - `RemoveOrb(...)` 要支持删除任意已存在球，并把其左侧球逐个向右补位，维持整体靠右填充视觉。
    - 删除后空出的最左槽位显示虚线环。

- `File: src/Orbs/Runtime/OrbSlotRuntime.cs`
  - 保持现有字段；不新增对外接口。
  - 行为约束：
    - 允许在普通挤出动画之外，支持“中槽删球 -> 左槽右滚补位”或“右槽删球 -> 中/左右滚补位”。

- `File: src/Orbs/OrbTriggerContext.cs`
  - 保持构造签名。
  - 依赖新增约束：
    - `Combat` 需提供“范围内全部敌人枚举”接口。
    - `Runtime` 需允许定义层标记并移除衰减为 0 的白球。

- `File: src/Combat/OrbCombatService.cs`
  - 新增方法签名：
    - `public List<HealthManager> FindAllEnemiesInRange(HeroController hero)`
    - `public Vector3 GetGlassHitVisualPosition(HealthManager target)`
    - `public static int GetCeilHalfDamage(int baseDamage)`
  - 行为约束：
    - `FindAllEnemiesInRange(...)` 复用黄球同一判定框、同一过滤规则，但返回全部有效敌人。
    - `GetCeilHalfDamage(...)` 返回 `Mathf.CeilToInt(Math.Max(1, baseDamage) / 2f)`。
    - 白球特效定位默认使用目标中心略上方，可直接复用敌人碰撞盒高度估算。

- `File: src/Visual/OrbVisualService.cs`
  - 新增资源缓存字段：
    - `private static Sprite? _glassOrbSprite;`
  - 新增方法签名：
    - `public SpriteRenderer CreateOrbRenderer(string name, OrbTypeId typeId, Color color)`
    - `public void SpawnGlassShatterVisual(Vector3 worldPosition)`
    - `private static Sprite CreateGlassOrbSprite()`
    - `private static void AddWhiteOrbMaterialLayers(Transform parent, Color baseColor)`
    - `private static Sprite CreateGlassShardSprite()`
    - `private static Sprite CreateRefractionRingSprite()`
  - 行为约束：
    - 黄球球体继续走现有球面光照 + 电弧外环，但命中特效需从“黄系粗闪电贴图”升级为更真实的蓝白细电弧方案。
    - 白球改用独立球面底图：半透明白青色基底、碎裂纹路、高光切面、轻微冷色边缘。
    - `SpawnGlassShatterVisual(...)` 生成 1 个中心折射环 + 若干碎片瞬时飞散对象，生命周期短于闪电特效。
    - 白球命中特效需要明显强于当前版本：允许增大环尺寸、增加核心闪光、提升碎片数量/速度/亮度，并让特效在战斗中一眼可见，但仍保持“玻璃脆裂”语义而非电击语义。
    - 黄球命中特效新方案应满足：主闪电形体细长、至少包含 1 条主弧 + 多条短分叉、主色偏蓝白、中心高亮、边缘冷色晕光，避免继续出现卡通图标观感。
    - 若实现路径选择程序化绘制，则应优先在 `CreateLightningSprite()` 及其相关缓存逻辑内完成，不扩散到战斗层和球逻辑层。
  - 本轮黄球重绘的精确设计合同：
    - 保留现有对外接口：
      - `public void SpawnLightningVisual(Vector3 worldPosition)`
      - `private static Sprite CreateLightningSprite()`
    - 允许新增但仅限视觉内部使用的方法签名：
      - `private static Texture2D CreateLightningTexture()`
      - `private static void DrawLightningBolt(Texture2D texture, Vector2[] points, Color coreColor, Color glowColor, int coreRadius, int glowRadius)`
      - `private static Vector2[] BuildLightningPath(int seed, Vector2 start, Vector2 end, float horizontalJitter, int segmentCount)`
      - `private static void StampSoftPixel(Texture2D texture, int x, int y, Color color, int radius)`
      - `private static void DrawLightningBranch(Texture2D texture, Vector2 origin, float angleDeg, float length, int seed)`
    - 替换策略：
      - 删除或废弃 `LoadLightningTexture()` 与 `GetLightningAssetPath()` 的命中特效主路径职责，不再依赖 `assets/闪电.png` 生成黄球攻击特效精灵。
      - `_lightningTexture` 保留与否由实现决定；若保留，则其来源必须改为程序化生成的纹理缓存，而非磁盘图片解码。
    - 视觉结构约束：
      - 纹理画布建议提升到 `96x96` 或 `128x128`，保证细长主电弧和支叉不会糊成粗块。
      - 主电弧整体方向为“自下向上略偏斜”，锚点仍保持 `new Vector2(0.5f, 0f)`，以兼容现有命中位置语义。
      - 主电弧至少 `6` 段折线点，路径抖动应形成自然折线，而不是规则 Z 字。
      - 分叉电弧至少 `2` 条，长度明显短于主电弧，并从主电弧中上段分离，形成交错感。
      - 颜色分层至少两层：核心层接近白色或极浅蓝，外晕层为青蓝色，透明度低于核心层。
      - 线宽目标：核心感知宽度约为旧图标的 `35%~50%`，整体观感必须更细、更锐利。
      - 允许额外加入极弱的端点爆闪或雾化晕圈，但不得压过主电弧主体。
    - 动效兼容约束：
      - `SpawnLightningVisual(...)` 生命周期仍保持短促爆发，不修改调用方逻辑。
      - 若为提升真实感而调整 `LightningScale`、`LightningLifetime`、颜色或缩放曲线，改动必须限制在视觉文件内，不影响战斗命中与伤害节奏。
      - 新特效在深色场景和亮色场景中都必须可见，因此蓝白配色不可过淡。
      - 本轮可见性增强优先级高于“完全克制”，可以适度增加闪电占屏面积和瞬时爆闪强度，但不能退回黄色粗图标风格。
    - 验收标准：
      - 肉眼观感不再像“黄色图标贴纸”。
      - 截图中能清晰分辨主弧、分叉、冷色外晕三层语义。
      - 与白球玻璃特效并列时，黄球视觉语言明显是“雷击”，且质感等级不低于白球。
      - 实战中一眼能看到黄球命中点，不会因为线太细或面积太小而被敌人、美术背景或命中特效淹没。

- `File: DeVect.csproj`
  - 仅当白球特效改为外部图片资源时才更新复制规则；本次默认采用运行时绘制，不新增外部 PNG。

### 3.2 Implementation Checklist
- [x] 1. 将 `src/Fsm/FireballDetectAction.cs` 重构为 `src/Fsm/SpellDetectAction.cs`，支持在 Spell Control 中区分横向火球与上吼，并分别提供消费原法术的回调与判定函数。
- [x] 2. 更新 `DeVect.cs` 的 FSM 注入与回调接线，把火球路由到黄球、把上吼路由到白球，并确保上吼被白球逻辑完全取代，不落回原版吼施法。
- [x] 3. 新建 `src/Orbs/Definitions/WhiteOrbDefinition.cs`，实现白球被动与激发：被动 AOE 全体伤害、每次后伤害减 1、激发伤害取当前伤害的 2 倍、命中触发碎玻璃特效。
- [x] 4. 扩展 `src/Orbs/Definitions/OrbDefinitionRegistry.cs` 注册白球定义，并保持火球默认生成黄球。
- [x] 5. 扩展 `src/Orbs/Runtime/OrbInstance.cs`、`src/Orbs/Runtime/OrbInstanceSnapshot.cs`、`src/Orbs/OrbPersistentState.cs`，让白球当前伤害可持久化并在切场景后恢复。
- [x] 6. 扩展 `src/Combat/OrbCombatService.cs`，新增“范围内全部敌人”查询、白球半骨钉伤害取整方法、白球特效定位方法。
- [x] 7. 扩展 `src/Visual/OrbVisualService.cs`，为白球增加玻璃质感球体绘制与碎裂命中特效，同时兼容现有黄球视觉管线。
- [x] 8. 扩展 `src/Orbs/Runtime/OrbRuntime.cs`，让白球生成时写入初始伤害，支持任意槽位白球因衰减归零而删除，并按“左侧向右滚动补位”更新动画与快照。
- [x] 9. 扩展 `src/Orbs/OrbSystem.cs`，统一处理按法术类型生成球、满槽激发、白球被动后衰减删除、运行时与持久化同步。
- [x] 10. 运行 `dotnet build -c Debug` 验证编译通过；若实现与 Spec 有偏差，先更新 `mydocs/specs/2026-03-15_13-31_WhiteOrbImplementation.md` 再继续。
- [x] 11. 增强 `src/Visual/OrbVisualService.cs` 的白球命中特效可见性，提升亮度、尺寸、碎片密度与爆发感，同时保持碎玻璃风格。
- [x] 12. 修正 `DeVect.cs` 的骨钉伤害获取逻辑，统一返回吃护符加成后的实际伤害，并让黄球/白球都基于该值结算。
- [x] 13. 重绘 `src/Visual/OrbVisualService.cs` 的黄球命中特效，移除旧版黄粗闪电图标观感，改为更真实的蓝白细长交错电弧方案，并保持现有生命周期与战斗触发接口不变。
- [x] 14. 在 `src/Visual/OrbVisualService.cs` 内将黄球闪电资源路径切换为程序化生成纹理缓存，清理对 `assets/闪电.png` 的主依赖，并确保 `SpawnLightningVisual(...)` 的调用方无须改动。
- [x] 15. 运行 `dotnet build -c Debug` 验证黄球新特效改造后的编译通过；若程序化纹理实现与既有 Spec 不一致，先回写 Spec 再继续。
- [x] 16. 进一步增强 `src/Visual/OrbVisualService.cs` 的黄球命中特效可见性，提高蓝白闪电的亮度、外晕、端点爆闪和分叉存在感，同时保持细长冷色电弧风格。

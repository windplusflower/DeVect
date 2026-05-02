# SDD Spec: Electric Lower Body Effect

## 0. Open Questions (MUST BE CLEAR BEFORE CODING)
- [ ] None

## 1. Requirements (Context)
- Goal: 彻底重做当前项目中小骑士 `电形态` 的本体特效，不参考现有电形态视觉语言，改为“黄色电流缠绕小骑士下半身”的新表现。
- In-Scope: 仅重做 `电形态` 持续显示在 Hero 身上的视觉效果；重点区域是脚部、腿部与下半身躯干，电流应围绕身体缠绕并有持续跳动感。
- In-Scope: 在不遮挡头部的前提下，电流整体密度和炫光感要明显高于第一版重做结果；允许更多下半身分支、热点和底部积电感。
- In-Scope: 底部横向积电带宽度应接近小骑士本体宽度，避免向两侧铺得过宽；整组特效最低边应与小骑士脚底基本对齐。
- In-Scope: 新效果必须避免遮挡头部、角和上半身主要轮廓；允许少量辉光上扬，但主要电流体块不得覆盖头部区域。
- In-Scope: 保持现有玩法逻辑、形态切换逻辑、黄球生成逻辑和其他瞬时闪电特效不变。
- Out-of-Scope: 不修改 `冰形态` 光环，不修改球槽、伤害、法术输入、排队生球、施法动画或现有落雷命中特效。
- Out-of-Scope: 不引入外部贴图资源文件；默认继续使用程序化生成 sprite 的方式完成新视觉。

## 1.5 Code Map (Project Topology)
- Core Logic:
  - `src/Orbs/OrbSystem.cs`: 英雄每帧更新入口；在 `OnHeroUpdate(HeroController hero, float deltaTime)` 中调用 `_visualService.TickHeroFormAura(hero, _currentForm, true)`，决定何时显示当前形态本体特效。
  - `src/Orbs/FormMode.cs`: 形态枚举；`FormMode.Lightning` 是本次重做目标分支。
- Visual Layer:
  - `src/Visual/OrbVisualService.cs`: 英雄形态本体特效的唯一实现位置；负责构建 aura root、分配 6 层 `SpriteRenderer`、生成程序化 sprite，并在 `TickLightningHeroAura(...)` / `TickIceHeroAura(...)` 中逐帧更新姿态。
  - `src/Visual/OrbVisualService.cs`: `ApplyHeroAuraStyle(FormMode formMode)` 为电形态选择当前 6 层 sprite；`GetHeroAuraRootWorldPosition(...)` 决定整个特效相对脚底的锚点高度。
  - `src/Visual/OrbVisualService.cs`: `CreateHeroLightningGlowSprite()`、`CreateHeroLightningRibbonSprite()`、`CreateHeroLightningSparkSprite()` 是当前电形态使用的 3 类程序化贴图生成函数，属于本次重做的直接替换目标。
- Runtime Constraints:
  - `src/Visual/OrbVisualService.cs`: 现有电形态 root 锚点位于 `feetY + heroHeight * 0.14f`，说明当前效果本就挂在脚底附近，但 layer 的纵向尺寸和摆放仍会上探到中上半身，需要通过新布局重新压低视觉重心。
  - `src/Visual/OrbVisualService.cs`: 所有 aura layer 当前都使用 `sortingLayerName = "HUD"` 和 16~21 的 sorting order；新方案需兼容同一渲染层级体系。
- Product / Design Context:
  - `README.md`: 明确 `电形态` 是当前核心玩法展示对象之一，因此 Hero 本体视觉需要明显、稳定、可辨识，但不能破坏小骑士本体 silhouette。

## 2. Architecture (Optional - Populated in INNOVATE)
- None in RESEARCH. 当前任务可先直接进入 PLAN，无需额外方案分叉阶段。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 Data Structures & Interfaces
- `File: src/Visual/OrbVisualService.cs`
  - `private void ApplyHeroAuraStyle(FormMode formMode)`
    - Lightning 分支继续使用 `HeroAuraLayerCount = 6`，但改为新的下半身电流图层组合，不再使用当前“中腰部环绕 ribbon + spark”视觉语义。
  - `private void TickLightningHeroAura(float time, float heroHeight)`
    - Responsibility: 每帧只更新“下半身电流缠绕”效果的 6 层位姿、缩放、透明度和抖动。
    - Design Rule: 所有 layer 的 `localPosition.y`、纵向缩放和摆动上界都必须控制在 Hero 下半身范围内；不允许形成上探到头部的主视觉体块。
  - `private static Vector3 GetHeroAuraRootWorldPosition(HeroController hero, FormMode formMode, float heroHeight)`
    - Lightning 分支的 root 锚点继续基于脚底，但要进一步贴近下半身视觉中心，使所有 layer 自下向上包裹膝部到腹部区域，而不是从腰部向上发散。
  - New sprite factories to add in `src/Visual/OrbVisualService.cs`:
    - `private static Sprite CreateHeroLightningCoilSprite()`
      - 生成主要“缠绕电弧”贴图；轮廓应是细长、断续、纵向攀附的闪电丝带，适合贴在左右腿和腹部前侧。
    - `private static Sprite CreateHeroLightningBandSprite()`
      - 生成较宽但高度受限的底部环流贴图；用于脚边和胯部下沿的横向电流积聚。
    - `private static Sprite CreateHeroLightningArcSprite()`
      - 生成短促跳弧贴图；用于腿侧和脚边的小范围跳电，不形成大面积遮挡。
    - `private static Sprite CreateHeroLightningKnotSprite()`
      - 生成局部高亮电结贴图；用于脚踝、膝侧或胯下的亮点闪烁，增强“缠绕点”而不是“包裹整个人”的观感。
  - Existing sprite factories expected to retire from lightning-hero-aura use:
    - `private static Sprite CreateHeroLightningGlowSprite()`
    - `private static Sprite CreateHeroLightningRibbonSprite()`
    - `private static Sprite CreateHeroLightningSparkSprite()`
    - 这些函数可以删除，或保留但不再被 Hero Lightning Aura 使用；本次以最小改动为优先，若无其他引用可直接替换/删除。

### 3.2 Visual Layout Contract
- Layer 0: `Band Base`
  - 位置职责: 脚底到小腿下段的横向电流积聚层。
  - 视觉职责: 提供最低部的黄色辉光和电流底座，强调“电是从下半身缠出来的”。
- Layer 1: `Left Leg Coil`
  - 位置职责: Hero 左腿外侧，沿脚踝到膝部上卷。
  - 视觉职责: 主电流之一，纵向细长，轻微左右摆动。
- Layer 2: `Right Leg Coil`
  - 位置职责: Hero 右腿外侧，对称于左腿。
  - 视觉职责: 第二条主电流，与左腿错相抖动，避免完全镜像死板。
- Layer 3: `Center Waist Coil`
  - 位置职责: 双腿之间到下腹区域，但上界必须停在头部以下明显距离。
  - 视觉职责: 连接左右腿电流，形成“下半身被电流束缚/缠绕”的中心结构。
- Layer 4: `Left Arc / Knot`
  - 位置职责: 左脚边或左膝侧局部跳电。
  - 视觉职责: 高频短跳弧和亮点闪烁。
- Layer 5: `Right Arc / Knot`
  - 位置职责: 右脚边或右膝侧局部跳电。
  - 视觉职责: 与 Layer 4 错相闪烁，增加活性。

### 3.3 Motion & Bounds Rules
- 所有 lightning layer 的主要可见区域应落在 `feetY` 到 `feetY + heroHeight * 0.58f` 之间。
- 允许极少量辉光羽化超出该范围，但任何实心电流线条、亮结或高 alpha 区域不得接近头部区域。
- 颜色主调以黄色为主，允许少量偏白高亮作为放电热点，但不能回到旧版偏蓝白电弧观感。
- 在保持下半身边界的前提下，优先增加“密度”和“炫感”，具体通过更多交错分支、更亮热点、更明显脚边积电和更活跃的局部闪弧实现，而不是把特效整体抬高到上半身。
- 抖动方式继续复用当前 `SampleSteppedJitter(...)` / `SampleAuraJitter(...)` 体系，避免引入新的运行时状态对象。
- 新 sprite 的 pivot 需偏向底部，使贴图自然从脚边向上缠绕，而不是围绕中心点膨胀。
- 底部 `Band` 层既要提供脚边积电感，又不能形成过宽“地面横线”；其主要亮区宽度应收敛到 Hero 站姿本体附近。
- 为避免视觉噪音，单层 alpha 与宽度要受控，优先通过多层错相叠加形成复杂感，而不是单层大面积铺开。

### 3.4 Implementation Constraints
- 只改 `src/Visual/OrbVisualService.cs`。
- 不改 `src/Orbs/OrbSystem.cs`、`README.md`、`IceShieldDisplay`、球逻辑或任何 HK hook。
- 不新增外部资源文件，不新增新类，不新增运行时持久状态。
- 如果旧 lightning hero aura sprite factory 已无引用，应一并清理，避免残留死代码。

### 3.5 Implementation Checklist
- [x] 1. 更新 `src/Visual/OrbVisualService.cs` 的 lightning aura 样式绑定，使 6 个 layer 改用新的 `Band / Coil / Arc / Knot` sprite 组合。
- [x] 2. 重写 `src/Visual/OrbVisualService.cs` 的 `TickLightningHeroAura(float time, float heroHeight)`，把电形态动画布局限制到 Hero 下半身并形成黄色缠绕电流效果。
- [x] 3. 调整 `src/Visual/OrbVisualService.cs` 的 `GetHeroAuraRootWorldPosition(...)` lightning 分支，使 root 更贴近脚部/下半身中心。
- [x] 4. 在 `src/Visual/OrbVisualService.cs` 中新增或替换对应的程序化 lightning sprite factory，生成新的下半身电流图形，并清理不再使用的旧 lightning hero aura sprite factory。
- [x] 5. 自检所有 lightning hero aura 调用点，确认只改视觉、不影响冰形态、施法动画和其他瞬时闪电特效。

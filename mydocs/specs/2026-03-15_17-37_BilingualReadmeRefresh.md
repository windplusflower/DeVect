# SDD Spec: 双语 README 完善与提交

## 0. 🚨 Open Questions (MUST BE CLEAR BEFORE CODING)
- None

## 1. Requirements (Context)
- **Goal**: 完善中英文 README，使其与当前实现保持一致，尤其覆盖三法术白法/黑法分档、未解锁法术不生成对应球、萨满之石按基础骨钉伤害倍率提供额外加伤等最新规则；随后提交本次文档改动。
- **In-Scope**:
  - 更新 `README.md` 中文说明。
  - 更新 `README-en.md` 英文说明。
  - 统一两份 README 的结构、术语与关键数值描述。
  - 使用 git 提交本次文档更新。
- **Out-of-Scope**:
  - 不修改运行时代码逻辑。
  - 不新增发布流程、截图资源、安装脚本或外链文档站。

## 1.5 Code Map (Project Topology)
- **Docs**:
  - `README.md`: 中文主说明，面向中文玩家与开发者。
  - `README-en.md`: 英文说明，需与中文版本保持规则一致。
- **Source of Truth for Current Behavior**:
  - `src/Orbs/OrbSystem.cs`: 三球生成资格、法术等级读取、萨满入口。
  - `src/Orbs/OrbTriggerContext.cs`: 萨满倍率额外伤害计算入口。
  - `src/Orbs/Definitions/YellowOrbDefinition.cs`: 黄球白法/黑法分档与倍率。
  - `src/Orbs/Definitions/WhiteOrbDefinition.cs`: 白球白法/黑法初值规则。
  - `src/Orbs/Definitions/BlackOrbDefinition.cs`: 黑球白法/黑法初值与储伤规则。
- **Observed Reality**:
  - 当前两份 README 仍停留在旧规则：把萨满描述为固定 `+2`，并把黑球初值写成 `0.75x nail` 常态，没有体现白法/黑法分档与“未解锁法术不生成对应球”的规则。

## 2. Architecture (Optional - Populated in INNOVATE)
- FAST 模式，跳过。

## 3. Detailed Design & Implementation (Populated in PLAN)
### 3.1 File Changes
- `README.md`
  - 重写/扩写核心说明，覆盖：玩法概览、球槽规则、三球白法/黑法分档、护符联动、实战提示。
- `README-en.md`
  - 与中文版保持内容对齐，使用自然英文表达同一规则。

### 3.2 Content Contract
- README 必须明确说明：
  - 三法术各自分白法/黑法，两档会影响对应球的数值。
  - 未获得对应法术时，不能生成对应球。
  - 萨满之石额外伤害为 `ceil(base nail * 0.2 * scale)`，其中 `scale` 为该伤害结算点对应的基础倍率。
  - 白球的萨满加成只在初值生成时注入一次，后续被动与激发继承当前存储值。
  - 黑球黑法当前基线为初值 `1x nail`、被动储伤 `+1x nail`。

### 3.3 Implementation Checklist
- [x] 1. 新建并落地本次 README 任务 Spec。
- [ ] 2. 更新 `README.md`，使其反映当前真实规则并提升完整度。
- [ ] 3. 更新 `README-en.md`，与中文版保持一致。
- [ ] 4. 检查 README 内容与当前代码实现一致。
- [ ] 5. 查看 git 状态、差异与近期提交风格，准备提交。
- [ ] 6. 提交本次 README 改动。

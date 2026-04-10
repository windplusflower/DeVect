# SDD Spec: DeVect 法术方向输入修复与双仓推送

## 0. Open Questions
- [x] None

## 0.1 Project Registry
| project_id | project_path | project_type | marker_file |
|---|---|---|---|
| DeVect | `/home/windflower/workspace/DeVect` | csharp | `.git` |
| StubbornKnight | `/home/windflower/workspace/StubbornKnight` | csharp | `.git` |

## 0.2 Multi-Project Config
- **workdir**: `/home/windflower/workspace`
- **active_project**: `DeVect`
- **active_workdir**: `/home/windflower/workspace/DeVect`
- **change_scope**: `cross`
- **related_projects**: `StubbornKnight`

## 1. Requirements (Context)
- **Goal**: 修复 `DeVect` 中法术方向判定读取错误输入源的问题，并将 `DeVect` 与 `StubbornKnight` 的本地提交都推送到各自远端。
- **In-Scope**:
  - 将 `src/Fsm/SpellDetectAction.cs` 中的纵向方向判定从 `Input.GetAxisRaw("Vertical")` 改为 `InputHandler.Instance.inputActions`
  - 保持方向映射为 `up -> shriek`、`down -> dive`、否则 `fireball`
  - 记录并自查本次修改
  - 提交并推送 `DeVect`
  - 推送 `StubbornKnight` 当前本地领先远端的提交
- **Out-of-Scope**:
  - 调整 `DeVect` 的 FSM 注入点或球体行为
  - 修改 `StubbornKnight` 现有代码
  - 新增 UI、设置项或资源文件

## 1.1 Context Sources
- Requirement Source: 用户本轮明确要求“要一起修，并且把两个都推一下”，并指定按 SDD-RIPER 执行 `PLAN -> EXECUTE -> REVIEW -> ARCHIVE`
- Design Refs: `AGENTS.md`
- Chat/Business Refs:
  - 用户给出的 bug 描述与替换映射
  - `StubbornKnight` 已完成的同类修复说明
- Extra Context:
  - `src/Fsm/SpellDetectAction.cs`
  - `DeVect.cs`
  - `README.md`
  - `/home/windflower/workspace/StubbornKnight/StubbornKnight.cs`
  - `/home/windflower/workspace/StubbornKnight/mydocs/specs/2026-04-10_08-32_input-handler-direction-fix.md`

## 1.5 Codemap Used (Per-Project Index)
### DeVect
- Codemap File: `N/A`
- Key Index:
  - Entry Points: `DeVectMod.Initialize()` 注册 `PlayMakerFSM.OnEnable`，通过 `InjectSpellDetector()` 将 `SpellDetectAction` 插入 Hero 的 `Spell Control` FSM
  - Core Logic: `SpellDetectAction.OnEnter()` 负责把原版施法方向映射到 `OnFireballCast` / `OnDiveCast` / `OnShriekCast`
  - Dependencies: `InputHandler.Instance.inputActions`、`PlayerData.instance`、`GameCameras.instance`

### StubbornKnight
- Codemap File: `N/A`
- Key Index:
  - Reference Implementation: `StubbornKnight.TryGetCurrentIntentDirection(...)` 已切换到 `inputActions.up/down.IsPressed`
  - Related Evidence: `main` 分支当前领先 `origin/main` 2 个提交，待推送

## 1.6 Context Bundle Snapshot (Lite/Standard)
- Bundle Level: `none`
- Bundle File: `N/A`
- Key Facts:
  - Hollow Knight 方向输入应优先跟随 `InputHandler.Instance.inputActions`
  - 直接读取 `UnityEngine.Input.GetAxisRaw("Vertical")` 会绕过原版输入系统与按键映射
  - 当前 `SpellDetectAction` 只需读取上下方向，不需要解析横向方向
- Open Questions: None

## 2. Research Findings
- `DeVect` 当前在 `SpellDetectAction.OnEnter()` 里直接读取 `Input.GetAxisRaw("Vertical")`，与 Hollow Knight 原版动作输入来源不一致。
- 用户已给出明确替换规则，且 `StubbornKnight` 的同类修复已经证明 `inputActions.up/down.IsPressed` 是可用实现路径。
- `SpellDetectAction` 的行为是三分支判定：上吼、下砸、否则火球，因此本次无需抽通用方向解析层，直接替换输入源即可。
- `StubbornKnight` 当前仓库无需改代码，只需把本地已有的 2 个提交推送到远端。

## 2.1 Next Actions
- 生成本次任务 spec，并将 `DeVect` 修改限定在 `src/Fsm/SpellDetectAction.cs`
- 按用户提供的方向映射改写输入读取逻辑
- 执行最小验证并回写 review 结论
- 提交 `DeVect`，然后推送 `DeVect` 与 `StubbornKnight`

## 3. Innovate (Optional: Options & Decision)
### Option A
- Pros: 直接在 `SpellDetectAction` 内用 `inputActions.up/down.IsPressed` 替换，改动最小
- Cons: 未抽出共享辅助逻辑

### Option B
- Pros: 在 `DeVect` 中额外抽取统一方向辅助函数，后续若新增其它拦截点可复用
- Cons: 对当前单文件 bug 修复来说改动面扩大，没有直接收益

### Decision
- Selected: Option A
- Why: 这次需求只覆盖 `SpellDetectAction`，且用户已指定精确映射规则，最小改动更稳妥。

### Skip (for small/simple tasks)
- Skipped: false
- Reason: 需要明确说明为何不额外抽象

## 4. Plan (Contract)
### 4.1 File Changes (grouped by project)
#### [DeVect]
- `mydocs/specs/2026-04-10_08-47_DeVectSpellInputHandlerFixAndPush.md`: 记录本轮 RIPER 全流程
- `src/Fsm/SpellDetectAction.cs`: 将纵向输入改为读取 `InputHandler.Instance.inputActions`

#### [StubbornKnight]
- 无代码文件变更；仅推送当前本地领先远端的提交

### 4.2 Signatures (grouped by project)
#### [DeVect]
- `public override void OnEnter()`
- `private static void ConsumeSpellCost()`

#### [StubbornKnight]
- `N/A`

### 4.3 Implementation Checklist (grouped by project, dependency order)
#### [DeVect]
- [x] 1. 在 spec 中记录需求、边界、参考实现与执行计划
- [x] 2. 修改 `SpellDetectAction.OnEnter()`，用 `InputHandler.Instance.inputActions.up/down.IsPressed` 替代 `Input.GetAxisRaw("Vertical")`
- [x] 3. 保持“上 -> shriek，下 -> dive，否则 fireball”的既有行为，并在输入对象缺失时回落到 fireball 分支
- [x] 4. 执行最小验证并回填 execute log / review verdict
- [ ] 5. 提交 `DeVect` 当前修改

#### [StubbornKnight]
- [x] 6. 确认 `StubbornKnight` 仅存在待推送提交，无需额外代码改动
- [ ] 7. 将 `StubbornKnight` 当前 `main` 推送到 `origin/main`

### 4.4 Contract Interfaces (cross-project only)
| Provider | Interface / API | Consumer(s) | Breaking Change? | Migration Plan |
|---|---|---|---|---|
| `N/A` | 无跨项目接口变更 | `N/A` | No | `N/A` |

### 4.5 Spec Review Notes (Optional Advisory, Pre-Execute)
- Spec Review Matrix:
| Check | Verdict | Evidence |
|---|---|---|
| Requirement clarity & acceptance | PASS | 用户明确给出 bug 文件、替换规则与交付动作 |
| Plan executability | PASS | 文件、方法、push 范围与校验动作已明确 |
| Risk / rollback readiness | PASS | `DeVect` 只改单文件输入读取，`StubbornKnight` 只做 push |
| Cross-project contract completeness | PASS | 无跨项目接口修改，仅存在双仓协同推送 |
- Readiness Verdict: GO
- Risks & Suggestions: `dotnet build` 很可能因本机缺少 HK/Unity DLL 引用失败，需要把结果作为环境限制记录
- Phase Reminders (for later sections): Execute 后补充 git 提交号、push 结果与 review 证据
- User Decision (if NO-GO): User explicitly requested execute-all and push in the same turn

## 5. Execute Log (grouped by project)
#### [DeVect]
- [x] Step 1: 新建本轮多项目 spec，记录 `DeVect` 修复范围与 `StubbornKnight` 推送边界
- [x] Step 2: 在 `src/Fsm/SpellDetectAction.cs` 中移除 `Input.GetAxisRaw("Vertical")`，改为读取 `InputHandler.Instance.inputActions.up/down.IsPressed`
- [x] Step 3: 执行 `rg` 与 `dotnet build` 做最小验证；确认文件中已无 `Input.GetAxisRaw`，构建因 `DeVect.csproj` 配置的 Hollow Knight DLL 路径不存在而失败

#### [StubbornKnight]
- [x] Step 4: 确认 `StubbornKnight` 当前 `main` 本地领先 `origin/main` 2 个提交，仅待执行 push

## 6. Review Verdict
- Review Matrix (Mandatory):
| Axis | Key Checks | Verdict | Evidence |
|---|---|---|---|
| Spec Quality & Requirement Completion | Goal/In-Scope/Acceptance 是否完整清晰；需求是否达成 | PASS | 目标聚焦单文件输入源替换与双仓推送；`SpellDetectAction` 已按用户给定映射切换输入来源 |
| Spec-Code Fidelity | 文件、签名、checklist、行为是否与 Plan 一致 | PASS | 实际改动仅涉及计划内的 spec 与 `src/Fsm/SpellDetectAction.cs`；`OnEnter()` 仍保持上/下/默认三分支行为 |
| Code Intrinsic Quality | 正确性、鲁棒性、可维护性、测试、关键风险 | PARTIAL | 逻辑简单且 fallback 明确；但本地无法完成编译验证，因为缺少 HK/Unity DLL 依赖 |
- Overall Verdict: PASS
- Blocking Issues: None
- Regression risk (per project):
  - DeVect: Low
  - StubbornKnight: Low
- Cross-project consistency: PASS
- Follow-ups:
  - 在配置了有效 `GameDir` 的环境中重新执行 `dotnet build`
  - 进游戏验证三种施法输入：上吼、下砸、普通火球

## 6.1 Touched Projects
| project_id | Files Changed | Reason |
|---|---|---|
| DeVect | `mydocs/specs/2026-04-10_08-47_DeVectSpellInputHandlerFixAndPush.md`, `src/Fsm/SpellDetectAction.cs` | 记录任务 spec 并修复法术输入判定 |
| StubbornKnight | `None` | 仅推送已有本地提交 |

## 7. Plan-Execution Diff
- 无偏差。实现按计划只修改 `SpellDetectAction` 的输入来源，未扩展到其它 FSM 或系统。

## 8. Archive Record (Recommended at closure)
- Archive Mode: `git_commit_and_push`
- Audience: `N/A`
- Source Targets:
  - `mydocs/specs/2026-04-10_08-47_DeVectSpellInputHandlerFixAndPush.md`
- Archive Outputs:
  - `git commit`
  - `git push`
- Key Distilled Knowledge:
  - Hollow Knight mod 的方向判定应优先跟随 `InputHandler.Instance.inputActions`
  - 对仅区分上/下/默认的法术分支，直接替换输入源即可，不需要额外抽象方向层

# DeVect

`DeVect` 是一个从空目录初始化的 Hollow Knight Mod 模板，用于修改小骑士本体逻辑。

## 目录结构

```text
DeVect/
├── DeVect.csproj
├── DeVect.cs
├── README.md
└── assets/
```

## 已内置

- `Mod` 主类（`DeVectMod`）
- `IGlobalSettings<DeVectSettings>` 持久化配置
- `IMenuMod` 菜单（含开关 + 基础血量选项）
- 构建后自动复制到 `Managed/Mods/DeVect`

## 路径配置

模板已写入本机已验证路径：

`D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed`

如果你的 Modding API 不在默认位置，构建时传入：

```powershell
dotnet build -c Release -p:ModdingDir="<你的Modding.dll所在目录>"
```

## 构建

```powershell
dotnet build -c Release
```

构建成功后会自动安装到：

`D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods\DeVect`

## 开发建议

后续你可以在 `DeVect.cs` 里继续加：

- `On.HeroController.*`（移动、冲刺、跳跃）
- `ModHooks.*`（全局数值与行为）
- `On.PlayMakerFSM.OnEnable`（FSM 注入）

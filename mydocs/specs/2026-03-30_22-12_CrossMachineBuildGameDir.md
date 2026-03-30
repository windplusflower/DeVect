# SDD Spec: 跨机器 Hollow Knight 构建路径方案

## 1. Discovery
- 当前机器通过 `find /home/windflower -name Assembly-CSharp.dll` 找到的实际 Hollow Knight game managed 目录是：
  - `/home/windflower/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed`
- 当前机器还找到了带 MMHOOK 的 API 提取目录：
  - `/home/windflower/.openclaw/workspace/HollowKnight/api-1.5.78.11833-v74/extracted`
  - `/home/windflower/.openclaw/workspace/HollowKnight/moddingapi-1578/extracted`
- 现状核对：
  - 游戏 `Managed` 目录内存在 `Assembly-CSharp.dll`、`PlayMaker.dll`、`UnityEngine*.dll`
  - 游戏 `Managed` 目录内当前不存在 `MMHOOK_Assembly-CSharp.dll`
  - 因此“只修正 `GameDir`”并不能保证当前机器可编译，还需要允许 MMHOOK 引用目录单独配置

## 2. Current Problem
- `DeVect.csproj` 原先把 `GameDir` 直接硬编码成 Windows 路径 `D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed`
- 同一个 `GameDir` 同时被用于：
  - 解析游戏原始 DLL 引用
  - 解析 MMHOOK / `MonoMod.RuntimeDetour` 引用
  - 计算安装目录 `$(GameDir)/Mods/DeVect`
- 这个模型有两个问题：
  - 不同机器的游戏安装路径不同，硬编码一定失效
  - 有些机器的 MMHOOK DLL 不在游戏目录，而是在单独解压的 Modding API 目录

## 3. Implemented Scheme
### 3.1 Property precedence
- `GameDir` 优先级：
  - `msbuild /p:GameDir=...`
  - 根目录 `local.props`
  - 环境变量 `HOLLOW_KNIGHT_MANAGED_DIR`
- `ApiDir` 优先级：
  - `msbuild /p:ApiDir=...`
  - 根目录 `local.props`
  - 环境变量 `HOLLOW_KNIGHT_API_DIR`
  - 若以上都未设置，则回退为 `$(GameDir)`
- `InstallDir`：
  - 默认 `$(GameDir)/Mods/DeVect`
  - 也允许通过 `local.props` 或 `/p:InstallDir=...` 覆盖

### 3.2 File responsibilities
- `GameDir` 仅负责游戏本体 DLL：
  - `Assembly-CSharp.dll`
  - `PlayMaker.dll`
  - `UnityEngine*.dll`
- `ApiDir` 仅负责 Modding API DLL：
  - `MMHOOK_Assembly-CSharp.dll`
  - `MMHOOK_PlayMaker.dll`
  - `MonoMod.RuntimeDetour.dll`

### 3.3 Repository changes
- `DeVect.csproj`
  - 条件导入 `local.props`
  - 去掉硬编码 Windows `GameDir`
  - 将路径拼接统一改为 `/`，避免 Linux 下 `\` 路径失效
  - 校验错误信息明确指向 `local.props`、命令行参数或环境变量
  - 将 MMHOOK 引用切到 `ApiDir`
- `.gitignore`
  - 忽略 `local.props`
- `local.props.example`
  - 提供最小可复制模板，避免每台机器直接改项目文件

## 4. Recommended Local Setup
### 4.1 当前机器建议配置
```xml
<Project>
  <PropertyGroup>
    <GameDir>/home/windflower/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight_Data/Managed</GameDir>
    <ApiDir>/home/windflower/.openclaw/workspace/HollowKnight/api-1.5.78.11833-v74/extracted</ApiDir>
  </PropertyGroup>
</Project>
```

### 4.2 其他机器配置方式
1. 复制 `local.props.example` 为仓库根目录 `local.props`
2. 将 `GameDir` 改成该机器实际的 Hollow Knight `Managed` 目录
3. 若 MMHOOK DLL 已安装在游戏目录，可不写 `ApiDir`
4. 若 MMHOOK DLL 在独立解压目录，单独填写 `ApiDir`

## 5. Validation Rules
- 构建前必须满足：
  - `$(GameDir)/Assembly-CSharp.dll` 存在
  - `$(ApiDir)/MMHOOK_Assembly-CSharp.dll` 存在
  - `$(ApiDir)/MMHOOK_PlayMaker.dll` 存在
  - `$(ApiDir)/MonoMod.RuntimeDetour.dll` 存在
- 这能把错误从“编译期找不到引用”前移为“路径配置错误”，更容易定位

## 6. Limits
- 当前环境没有 `dotnet`、`msbuild`、`mono`，因此本次只能完成静态配置设计与文件修改，不能在本机实际跑一遍构建
- 当前游戏目录未安装 HK Modding API，所以若不额外设置 `ApiDir`，构建仍会失败；这属于环境问题，不是 `GameDir` 机制问题

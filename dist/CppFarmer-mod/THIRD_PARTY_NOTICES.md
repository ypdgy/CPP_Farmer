# 第三方组件声明 / Third-Party Notices

本安装包除了 C++ Farmer 自己的两个 DLL，还捆绑了**未经修改的 BepInEx 官方发行版**，
这样玩家不必再单独下载 BepInEx。BepInEx 是第三方组件，不属于本项目，
其权利归原作者所有。

## BepInEx 5.4.23.6 (Windows x64 / Unity Mono)

| 项目 | 内容 |
|---|---|
| 官方仓库 | **https://github.com/BepInEx/BepInEx** |
| 本包所用版本 | 5.4.23.6（`v5.4.23.6` tag，2026-10-23 发布） |
| 官方发行页 | https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6 |
| 原始压缩包 | `BepInEx_win_x64_5.4.23.6.zip`（官方 Release 附件） |
| 许可证 | **GNU LGPL-2.1**（或任何更新版本），Copyright (C) 2020 BepInEx Team |
| 许可证全文 | [LICENSES/BepInEx-LGPL-2.1.txt](LICENSES/BepInEx-LGPL-2.1.txt) |
| 源码获取 | 官方仓库的 `v5.4.23.6` tag；二进制未作任何修改 |

> 许可说明：BepInEx 项目官方仓库的 README 与 About 侧栏均标注 **LGPL-2.1**，
> LICENSE 的最后一次提交为「Update license to LGPL 2.0 (2020-11-09)」。
> 注意：`v5.4.23.6` 这个 tag 内自带的 `LICENSE` 文件仍是早期 MIT 文本（发布 tag
> 未同步许可证文件），但以官方仓库当前的 LGPL-2.1 声明为准。

本包中包含的 BepInEx 文件（全部取自官方 zip，逐字节一致，未修改）：

- `winhttp.dll`（UnityDoorstop 加载器）
- `doorstop_config.ini`、`.doorstop_version`
- `BepInEx/core/` 下的 18 个 `.dll` / `.xml`
- `LICENSES/BepInEx-changelog.txt`（官方 changelog）

SHA-256 校验（与官方 zip 一致，可用于自行核对）：

```
0f39399eba05dea9419781187ae2a6b0555afcdf1859f1319b6213be7729013b  winhttp.dll
780f175821781c14fdc349bf214603b7bab594ac556aecb1f6679b80248133f0  BepInEx/core/BepInEx.dll
80abc22c8d77622f1af422686749f572a8ee6606b922b8346fe110c0f628817c  BepInEx/core/BepInEx.Preloader.dll
4d5c6dfa0f771c6a5b1b0c559aca0bd0ece7d08b08fff894708dc3b73ce73cfc  doorstop_config.ini
```

### BepInEx 自带的上游库

BepInEx 的 `core/` 目录还包含它自己依赖的第三方库，各自的仓库与许可：

| 库 | 上游仓库 | 说明 |
|---|---|---|
| UnityDoorstop 4.6.0 | https://github.com/NeighTools/UnityDoorstop | 提供 `winhttp.dll` |
| HarmonyX 2.9.0 | https://github.com/BepInEx/HarmonyX | 运行时方法补丁 |
| Harmony 2 (0Harmony) | https://github.com/pardeike/Harmony | 同上 |
| MonoMod | https://github.com/MonoMod/MonoMod | RuntimeDetour / Utils |
| Mono.Cecil 0.10.4 | https://github.com/jbevain/cecil | 程序集读写 |

版本清单来自官方 `v5-lts` README 的 "Used libraries"。各库的完整许可证请见
其官方仓库；本包只原样再分发官方 BepInEx 发行版中的文件。

## 与「不提供反编译产物」声明的关系

BepInEx **不是**游戏文件，也**不是**反编译产物；它是独立开源的第三方模组框架。
本包与源码仓库仍然**不包含**任何游戏程序集、反编译源码、IL 反汇编、元数据 dump、
原始资源或逆向分析中间文件。C++ Farmer 自身源码按本仓库根目录的
[MIT 许可证](../../LICENSE) 授权（Copyright (c) 2026 C++ Farmer contributors），
与上面的 BepInEx LGPL-2.1 许可证是两份独立文件。

## 安装行为

`install.bat` 只会在**游戏目录里还没有 BepInEx 时**安装这份捆绑版本：

- 若 `BepInEx\core\BepInEx.dll` 已存在 → 完全跳过，不覆盖你现有的 BepInEx、配置或其它模组
- 若 `winhttp.dll` / `doorstop_config.ini` 已存在 → 保留原文件并给出提示
- 卸载（`uninstall.bat`）只删除 `BepInEx\plugins\CppFarmer\`，**不会移除 BepInEx**

`winhttp.dll` 是 DLL 代理加载器，个别安全软件可能误报；如不放心，可从上面的
官方仓库/发行页自行下载同版本 BepInEx 并安装，再单独放置本项目的插件 DLL。

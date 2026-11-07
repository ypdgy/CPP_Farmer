# C++ Farmer · 安装包

在游戏《编程农场》(**The Farmer Was Replaced**) 内置编辑器里直接写 **C++**。
本目录是**自包含**的：重置游戏文件后，把它整个覆盖回游戏目录即可恢复。

> [!CAUTION]
> ### 装之前请先备份存档 · 本模组可能导致存档损坏
>
> **This mod can corrupt or lose save data. Back up your save folder before installing.**
>
> 模组会改变游戏的解析行为与代码文件参与存档的方式，可能造成代码文件消失、
> 保存失败、存档不一致、进度或成就异常。游戏自动存档（默认每 30 秒）会删除
> 存档目录里「不属于当前打开窗口」的 `.py` 文件；**退出游戏后**再改存档目录。
>
> ```
> %USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\Saves\
> ```
>
> 详情见下方 [存档安全](#存档安全)。作者不对任何存档、进度或成就的损失负责。

```
CppFarmer-mod/
├─ install.bat        安装（询问游戏目录，按需装 BepInEx，再装插件）
├─ uninstall.bat      卸载（只删 BepInEx\plugins\CppFarmer\）
├─ README.md          本文件
├─ THIRD_PARTY_NOTICES.md   捆绑 BepInEx 的来源、许可证与哈希
├─ LICENSES/          BepInEx LGPL-2.1 许可证全文 + 官方 changelog
├─ winhttp.dll / doorstop_config.ini / .doorstop_version   BepInEx 加载器
├─ BepInEx/core/      BepInEx 5.4.23.6 官方未修改文件
└─ BepInEx/plugins/CppFarmer/
   ├─ CppFarmer.dll          托管插件（Harmony 补丁）
   └─ cppfarmer_native.dll   原生 C++ 编译器（全静态链接）
```

**关于反编译产物**：本包及源码仓库不包含、不链接、不分发任何**游戏**程序集、
反编译源码、IL/反汇编输出、游戏原始资源或逆向中间文件。构建源码时需要你
**本机已合法安装**的游戏与 BepInEx 作为编译期引用。

**关于第三方组件**：本包捆绑了 **BepInEx 5.4.23.6**（Windows x64 / Unity Mono）
的官方未修改二进制，这样不必再单独下载 BepInEx。它是独立开源的第三方框架，
不是游戏文件、不是反编译产物：

- 官方仓库：**https://github.com/BepInEx/BepInEx**
- 发行页：https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6
- 许可证：**GNU LGPL-2.1**（或任何更新版本），Copyright (C) 2020 BepInEx Team
- 全文见 `LICENSES/BepInEx-LGPL-2.1.txt`，完整来源清单与 SHA-256 见 `THIRD_PARTY_NOTICES.md`

---

## 安装

**不需要单独下载 BepInEx**：安装器已内置 5.4.23.6。

0. **退出游戏，备份 `Saves\` 目录**（见 [存档安全](#存档安全)）
1. 双击 `install.bat`——它会**先询问游戏安装位置**：

```
Where is the game installed?
Examples:
  C:\Program Files (x86)\Steam\steamapps\common\The Farmer Was Replaced
  D:\SteamLibrary\steamapps\common\The Farmer Was Replaced
Tip: you can drag the game folder into this window to paste its path.
Game folder:
```

   - 直接**把游戏文件夹拖进窗口**回车即可（带引号的路径会自动去掉引号）
   - 输入 `q` 退出；路径不对会提示并重新询问
   - 也可以跳过询问：`install.bat "<path to game>"`

安装器会先确认目录里有 `TheFarmerWasReplaced.exe`，然后按下面规则处理 BepInEx：

| 游戏目录状态 | 安装器行为 |
|---|---|
| 没有 `BepInEx\core\BepInEx.dll` | 复制内置的 `BepInEx\core\`、`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version` |
| 已有 BepInEx | **完全跳过**，不覆盖你的版本、配置或其它模组 |
| 已有 `winhttp.dll` / `doorstop_config.ini` | 保留原文件并给出提示，绝不覆盖 |

只想装插件、不碰 BepInEx 时设 `CPPFARMER_SKIP_BEPINEX=1`；也可以完全跳过安装脚本，
手动把 `BepInEx\plugins\CppFarmer\` 复制到游戏目录（此时需自行装好 BepInEx）。

首次安装 BepInEx 后**启动一次游戏**让它生成 `BepInEx\config\`，再看
`BepInEx\LogOutput.log` 里应出现：

```
[Info   : C++ Farmer] native compiler: cppfarmer 0.2
[Info   : C++ Farmer] harmony patches applied
[Info   : C++ Farmer] C++ reference button added to the toolbar
```

> 日志里出现 `not a valid .NET assembly` 针对原生 DLL，是 BepInEx 的正常提示，会被跳过。

---

## 使用

1. 游戏内把代码文件名改成以 `.cpp` 结尾（例如 `main.cpp`）
2. 写 C++，按运行键（F5）
3. 中文输入法请切到英文标点——全角 `；` `（）` 会给出明确报错，不会被静默替换

右上角按钮栏会多一个 **C++** 按钮，点开是模组自己的说明页；说明页里还有
「常见错误与函数调用」一页，列出所有会被明确拒绝的写法与改法。
文档首页也会出现一个指向说明页的链接。**这两页都是运行时内存里的文本，
不写入 `StreamingAssets`，卸载后自动消失。**

编辑器针对 `.cpp` 窗口做了：C++ 关键字补全（含中文词条说明）、C++ 语法着色
（`//` 是注释色、`#include` 不是灰色注释）、`{` 回车自动缩进、字符串/注释内不弹补全、
失焦时不滚回文件开头、执行高亮按行映射回 C++ 源码。`.py` 窗口完全不受影响。

### 多文件

`#include "hello.cpp"` 会变成游戏自己的 `import`（`from hello import *`），
被包含的文件作为独立窗口编译，节点归属那个窗口——执行高亮与断点能跳进去，
和原生 `import` 一致。

- 文件名要能当标识符：`hello.cpp` → 导入为 `hello`；`my-lib.cpp` 会报错
- 被包含文件没打开时会自动打开
- `#include <...>` 忽略（没有 C++ 标准库可链接）

### 快速回退

把 `BepInEx\config\cppfarmer.cfg` 的 `General / Enabled` 改成 `false` 并重启，
即可完全关闭所有补丁，游戏恢复原生行为。或直接运行 `uninstall.bat`。

排查时看 `LogOutput.log` 里 `cpp parse ...` 这几行：
`begin` → `source N chars` → `includes prepared` → `transpiled N chars` →
`tokenizing` → `calling game parser`，能直接看出停在哪一步。

---

## 存档安全

**这个模组不写存档、不删存档文件、不改 `save.json`、不碰物品与科技树。**
安装器只往**游戏目录**新增 BepInEx 与插件文件，不接触存档目录。
但它让 `.cpp` 窗口作为普通代码文件参与游戏的加载与保存，以下游戏自身行为会因此影响到你：

- 磁盘文件名是 **`<窗口名>.py`**：窗口 `main.cpp` 对应 `main.cpp.py`。
  手工新增文件必须叫 `xxx.cpp.py`，只建 `xxx.cpp` 不会被加载。
- 自动存档（`autosave`，默认开启，每 30 秒）写回所有窗口后，会**删除存档目录里
  不属于当前打开窗口的 `.py` 文件**。游戏运行时新增/改名/恢复文件，可能马上被删。
- `autosave` 与 `file watcher` 互斥，游戏会自动关掉其中一个。
- 保存中途失败或崩溃会留下写了一半的状态，弹窗是笼统的「写入存档文件失败」。

历史故障（已修复）：早期 `#include` 会在 `codeWindows` 注册别名，导致保存时重复键异常。
现在只改写 token 里的模块名，不碰窗口表。

出问题时：

1. **先退出游戏**，把 `Saves\Save0` 改名保留（例如 `Save0_broken`）
2. 从自己的备份复制回去；也可从 `Backup\Save0_backupNNN` 挑一个早期快照
   （游戏会轮换清理快照，**不要当作唯一保障**）
3. 必要时先 `uninstall.bat` 关闭模组，确认存档本身是好的，再装回来

更完整的备份命令、恢复步骤与免责声明见源码仓库的 `docs/SAVE_SAFETY.md`。

---

## 排障文件

都在 `BepInEx\plugins\CppFarmer\`，可以安全删除，插件会按需重建：

| 文件 | 内容 |
|---|---|
| `bp_debug.txt` | 断点映射链路：窗口名、断点行号、输出行号、字符偏移 |
| `last_generated.py` | 生成脚本被游戏词法器拒绝时的转储 |
| `cpp_unlocks.txt` | 观测到的科技树词表，用于校准编译期门禁 |
| `cppfarmer_reference.md` | 可选，存在时覆盖内置的 C++ 说明页 |
| `cppfarmer_errors.md` | 可选，存在时覆盖内置的「常见错误」页 |

运行时诊断文件只写在插件目录，不会写进游戏存档目录。

---

## 配置

`BepInEx\config\cppfarmer.cfg`

| 键 | 默认 | 说明 |
|---|---|---|
| `General / Enabled` | true | 总开关，false 时完全关闭所有补丁 |
| `General / EnforceUnlocks` | true | 编译期科技树门禁；读不到词表时自动放行 |
| `General / VerboseLog` | false | 输出转译细节到日志 |
| `UI / HelpButton` | true | 右上角 C++ 按钮 |
| `UI / HelpButtonRightMargin` | 10 | 仅影响 IMGUI 兜底按钮 |
| `UI / HelpButtonTopMargin` | 8 | 同上 |
| `UI / HomeLink` | true | 文档首页的 C++ 说明链接 |
| `UI / UnlockTooltips` | true | 科技树悬停时显示该解锁带来的 C++ 写法 |
| `UI / NoCompleteInStrings` | true | C++ 字符串、字符字面量、注释内不弹补全 |
| `UI / SmartIndent` | true | `{` 结尾的行回车后多缩进一级 |
| `UI / WordTooltips` | true | 补全列表与悬停时的 C++ 词条说明 |

---

## 关于「用 Steam 重置游戏文件」

Steam 的「验证游戏文件完整性」只补回**游戏官方文件**：

1. **不会删除** `winhttp.dll`、`BepInEx\` 及 `BepInEx\plugins\` 下的东西，
   所以验证完插件还在，行为不会变。
2. **不会修复文档页显示问题**。游戏文档是运行时渲染的文本；本模组对文档
   只做内存内改写，从不写盘。文档异常不是文件损坏，校验找不回来。

如果目的是「确认问题是不是插件造成的」，最快的办法是**不重置**，直接
`uninstall.bat` → 启动游戏对比 → 再 `install.bat` 装回来。
真重置了也不用重新下载本包：直接双击 `install.bat`，它会检测 BepInEx 是否还在，
缺了就自动装回来。

---

## 已知限制

- 游戏内数字都是浮点数，整型除法映射为向下取整，**负数相除与 C++ 不同**
- 不支持位运算、指针、类与继承、模板、`switch`、`std::set`、`std::pair`、
  `#include <...>`；遇到时给出带行号的明确错误，游戏内说明页有替代写法
- 生成脚本仍由游戏解释器逐节点执行，**没有性能收益**；操作数、成就统计、
  科技树解锁与手写脚本一致
- 一个 C++ 语句可能展开成多行脚本，断点/执行高亮有时会落到整行而非精确 token
- 输入 `}` 不会自动回退一级缩进，和原版一样用 Shift+Tab
- 需要随游戏更新维护补丁点；补丁失效时插件会尽量降级而不是崩游戏
- 捆绑的 `winhttp.dll` 是 DLL 代理加载器，个别安全软件可能误报；可从
  https://github.com/BepInEx/BepInEx 下载同版本自行核对

---

## 卸载

```bat
uninstall.bat
```

只删除 `BepInEx\plugins\CppFarmer\`。游戏本体、BepInEx、存档都不动。

**BepInEx 不会被卸载**——其它模组可能依赖它。要彻底移除，退出游戏后手动删除
游戏目录里的 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx\`。
注意删除 `BepInEx\` 会一并删掉所有其它模组和它们的配置。

---

## 声明 / Notice

**不提供反编译产物 / No decompiled game material is included.** 本包不含任何
**游戏**程序集、可执行文件、资源、文档原文、反编译源码、IL 反汇编、逆向中间文件。

**第三方组件 / Third-party component.** 本包捆绑 BepInEx 5.4.23.6 官方未修改发行文件，
官方仓库 **https://github.com/BepInEx/BepInEx**，GNU LGPL-2.1
（Copyright (C) 2020 BepInEx Team）。许可证全文在 `LICENSES/BepInEx-LGPL-2.1.txt`，
完整来源、上游库清单与 SHA-256 校验在 `THIRD_PARTY_NOTICES.md`。
BepInEx 及其上游库的权利归各自作者所有。

本插件在**运行时**通过 Harmony 对已加载的方法打补丁，不覆盖、不修补游戏文件；
安装器只会新增 BepInEx 与插件目录。所有游戏数据、成就、统计与科技树状态
仍由游戏自身管理。

**使用风险自负 / Use at your own risk.** 本模组会改变游戏的解析行为与代码文件的
保存方式，**可能导致存档损坏或丢失**（代码文件消失、保存失败、存档不一致、
进度或成就异常）。作者不对任何存档、进度或成就的损失负责；请在完整备份后使用。
**The mod may corrupt or lose save data. The authors are not responsible for any
loss of saves, progress or achievements. Always keep a full backup.**

本项目是**非官方**爱好者作品，与游戏开发者、发行商、Valve/Steam 无关联，
需要你自行合法拥有该游戏。C++ Farmer 自身许可证：MIT。

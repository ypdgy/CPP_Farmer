# 存档安全 / Save Safety

> [!CAUTION]
> **本模组可能导致存档损坏或丢失。使用前请先完整备份存档目录。**
>
> **This mod can corrupt or lose save data. Back up your save folder before installing.**
> 模组会改变游戏的解析行为以及代码文件参与存档的方式，可能造成代码文件消失、
> 保存失败、存档不一致、进度或成就异常。作者不对任何存档、进度或成就的损失负责。
> **Use at your own risk. The authors are not responsible for any loss of saves,
> progress or achievements.**

本文说明风险来自哪里、怎么备份、出事怎么恢复。这里描述的是**游戏自身的存档机制**，
不是本模组私有的文件格式；理解它可以避免绝大多数「代码文件不见了」的情况。

---

## 1. 模组对存档做了什么、没做什么

**做了**：

- 让以 `.cpp` 结尾的窗口作为普通代码文件参与加载与保存（`Saver.IsValidCodeFileName` 放行）
- 在解析时把 `.cpp` 文本转译成游戏语言脚本，交给游戏自己的解析器执行
- 在保存失败时把真实异常记进 `BepInEx\LogOutput.log`（仅诊断，不写存档）

**没做**：

- 不写、不删、不重命名任何存档文件
- 不修改 `save.json`、不碰物品/科技树/成就/统计字段
- 不修改游戏安装目录下的任何文件（只新增 `BepInEx\plugins\CppFarmer\`）

所以风险的来源主要是两个：**游戏自动存档会清理它认为多余的文件**，以及
**转译后的脚本是真实执行的脚本**——C++ 逻辑写错（死循环、越界、无限种植）造成的
世界状态变化，和手写脚本写错完全一样，需要自行回退存档。

插件可以在 `BepInEx\config\cppfarmer.cfg` 里设 `General / Enabled = false` 完全关闭，
但这**不会**恢复已经丢失的代码文件；恢复只能靠备份。

---

## 2. 存档目录与文件命名

存档根目录（Windows 默认）：

```
%USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\
├─ Saves\Save0\            当前存档（也可能叫 Save1…）
│   ├─ save.json           世界状态、科技树、物品、成就等
│   ├─ __builtins__.py     游戏内置定义，勿删勿改
│   ├─ main.py             窗口 `main.py` 的代码（原生脚本）
│   ├─ main.cpp            窗口 `main.cpp` 的代码
│   ├─ dfs.cpp             窗口 `dfs.cpp` 的代码
│   └─ cppfarmer_api.hpp   外部编辑器用的 API 头（非游戏文件，可删）
└─ Backup\                 游戏自己留下的快照目录
    └─ Save0_backupNNN\    每次保存前的快照（会被轮换清理）
```

关键点：**`.cpp` 窗口直接以窗口名存盘；`.py` 窗口仍是窗口名 + `.py`**。

| 编辑器里的窗口名 | 磁盘文件 |
|---|---|
| `main.cpp` | `main.cpp` |
| `dfs.cpp` | `dfs.cpp` |
| `main.py` | `main.py` |

- 手工新增 C++ 文件直接建 `xxx.cpp` 即可。
- 旧版本留下的 `xxx.cpp.py` 会在**加载时自动改名**为 `xxx.cpp`，代码不会丢；
  两者同时存在时以 `.cpp` 为准，多余的 `.py` 被删除。
- 关掉 `Storage / UseCppExtension`（设为 `false`）可退回旧的 `<窗口名>.py` 规则，
  下次保存会把文件改回去。
- 改名直接改文件名；`.py` 窗口仍需连 `.py` 一起改。
- `__builtins__.py` 是特例，游戏保存时不会删除它。
- `cppfarmer_api.hpp` 是模组放的 IDE 辅助文件，不是游戏文件；
  游戏不加载、自动存档也不清理它。不需要时设 `Storage / ExportEditorHeader = false`。

---

## 3. 三个真实的风险点

### 3.1 自动存档会删除「多余」的代码文件

游戏选项 `autosave`（默认 `enabled`，每 30 秒一次）写回所有当前打开的窗口之后，
会**删除存档目录里所有「不属于当前打开窗口」的 `.py` 文件**
（`WriteCodeFiles` 末尾清理，用来同步编辑器里删除窗口的操作）。
本模组把同一条规则扩展到 `.cpp`，否则关闭的 C++ 窗口会一直留在磁盘上。

因此：

- 游戏**运行时**往存档目录里放新文件，可能 30 秒内就被删掉；
- 游戏运行时用外部编辑器**新建/改名**文件，同样可能被删；
- 恢复备份时如果游戏还开着，也可能刚复制过去就被清掉。

> 结论：**任何对存档目录的新增、改名、删除、恢复，都要在退出游戏后进行。**
> 修改**已经打开的窗口**的内容是安全的（file watcher 会把外部改动同步进窗口），
> 但 `file watcher` 与 `autosave` **互斥**：游戏在其中一个被启用时会自动把另一个
> 关掉，两者不能同时工作（这是游戏自身的选项逻辑）。

### 3.2 保存中途失败 / 崩溃

游戏保存是「先做快照 → 写 `save.json` → 写所有代码文件 → 清理多余文件」的异步流程。
中途崩溃、磁盘满、被外部程序锁住文件，都可能留下写了一半的状态。
弹窗提示通常是笼统的「写入存档文件失败」。

本模组在这条链路上的历史故障：`#include` 早期实现会在 `codeWindows` 里注册别名，
而保存时 `CaptureCodeFiles` 枚举窗口并做 `dictionary.Add(fileName, ...)`，
同一窗口出现两次就抛重复键异常。**已修复**：现在只在 token 层改写 import 的模块名，
不碰 `codeWindows`。写在这里是为了说明「模组确实曾导致保存失败」，这类风险不应被低估。

### 3.3 脚本逻辑造成的世界状态变化

转译产物由游戏真实执行。C++ 里的死循环、错误的种植/收割逻辑、无限使用物品等，
会像手写脚本一样改变存档内容。这类问题**备份也防不住**，只能靠备份回退。

---

## 4. 备份（安装前必做）

1. **完全退出游戏**（确认任务管理器里没有 `TheFarmerWasReplaced` 进程）。
2. 复制整个 `TheFarmerWasReplaced` 的用户数据目录，而不只是 `Saves\Save0`：

```bat
set "DATA=%USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced"
robocopy "%DATA%" "%USERPROFILE%\Desktop\TFWR-backup-%DATE:/=-%" /E /COPY:DAT /R:1 /W:1
```

   只想备份最小集合，至少也要复制：

```bat
set "DATA=%USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced"
xcopy "%DATA%\Saves" "%USERPROFILE%\Desktop\TFWR-saves-backup\Saves\" /E /I /Y
xcopy "%DATA%\Backup" "%USERPROFILE%\Desktop\TFWR-saves-backup\Backup\" /E /I /Y
```

3. 检查目标目录里确实有 `save.json` 和你的 `.cpp` 代码文件。
4. 之后每次**升级插件、改模组前端、大改代码文件**之前，再备份一次。

---

## 5. 恢复

1. **退出游戏**（务必，否则 `autosave` 可能再次清理你刚放回去的文件）。
2. 把出问题的目录改名保留（例如 `Save0_broken`），不要直接覆盖删除，方便事后对比。
3. 从备份复制 `Saves\Save0\` 回去；如果需要更早的状态，也可以从
   `Backup\Save0_backupNNN\` 里挑一个快照（按目录编号/时间判断）。
4. 启动游戏，确认代码窗口和世界状态都回来了，再决定是否重新启用模组。

先关闭模组再恢复往往更稳妥：把 `BepInEx\config\cppfarmer.cfg` 的
`General / Enabled` 设为 `false`，或直接运行 `uninstall.bat`，重启确认存档本身是好的，
再装回来。

---

## 6. 症状速查

| 症状 | 原因 | 处理 |
|---|---|---|
| 手工放进去的 `xxx.cpp` 不见了 | 游戏运行时被 `autosave` 清理 | 退出游戏后重新复制 |
| 出现 `xxx.cpp.py` 双份 | 旧格式未被迁移（外部程序锁住文件） | 退出游戏后删掉 `.py` 那份，保留 `.cpp` |
| 「写入存档文件失败」弹窗 | 保存链路异常（重复键、文件被占用、磁盘问题） | 退出游戏 → 备份 → 看 `LogOutput.log` 的 `save write failed` → 删掉多余 `.py` |
| 代码窗口内容与预期不符 | file watcher 与 autosave 抢占，或外部编辑器写入时机不对 | 只启用其中一个；退出游戏后手工改 |
| 世界状态被脚本改坏 | C++ 逻辑错误，脚本真实执行 | 从备份或 `Backup\Save0_backupNNN` 恢复 |
| 顶部注释报 `error_invalid_expression` | 旧版本解析回退 bug，游戏把 C++ 原文当成自己的语言 | 升级到最新版；日志搜 `cpp parse` 确认 |
| 想彻底回退 | — | `uninstall.bat` 只删插件目录，游戏本体与存档不动 |

> `Backup\` 快照由游戏自己轮换清理，可能已经包含出问题之后的状态，
> **不要当作唯一保障**；自己的那份复制才是可靠的。

---

## 7. 免责声明 / Disclaimer

本模组是**非官方**爱好者作品，与游戏开发者、发行商、Valve/Steam 无关联。
它以运行时补丁方式改变游戏的解析行为，**可能导致存档损坏或丢失**，包括但不限于
代码文件消失、保存失败、存档不一致、进度或成就异常。

**使用风险自负。** 请在完整备份后使用，并自行评估风险。作者不对任何存档、进度、
成就或其他数据的损失承担责任。

**USE AT YOUR OWN RISK.** This unofficial mod changes how the game parses and saves
code files and **may corrupt or lose save data**, including lost code files, failed
writes, inconsistent saves, and affected progress or achievements. Always keep a
complete backup. The authors are not liable for any loss of saves, progress,
achievements or other data.

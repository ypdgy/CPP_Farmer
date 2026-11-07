# C++ Farmer

在游戏《编程农场》(**The Farmer Was Replaced**) 内置编辑器里，直接用 **C++** 写控制脚本。

> **NOTICE**
> 本项目为**Vibe Coding**产物，全部代码均为AI生成，有任何BUG也是AI修，若介意请勿使用。
> 本项目尚未支持DLC内容，因为我没买。

> [!WARNING]
> ### 本仓库不提供任何反编译产物
>
> **No decompiled, disassembled, extracted, or otherwise reverse-engineered game
> material is included in this repository or in any of its releases.**
>
> 具体不包含：游戏程序集（`.dll` / `.exe`）、反编译出的 C# 源码、IL 反汇编输出、
> 元数据/字符串堆 dump、逆向分析中间文件、以及开发期使用的一次性分析脚本。
> 也不包含任何游戏原始资源、存档或文档文本。
>
> 本仓库只包含本项目**原创**的编译器源码、插件源码、文档、示例，以及由这些源码
> 自行编译出的二进制。详见 [声明](#声明--notice)。
>
> **唯一例外**：发布包为了让安装器开箱即用，额外捆绑了第三方组件
> **BepInEx 5.4.23.6** 的官方未修改二进制。它不是游戏文件，也不是反编译产物；
> 官方仓库 **https://github.com/BepInEx/BepInEx **，LGPL-2.1 许可，
> 详见 [第三方声明](dist/CppFarmer-mod/THIRD_PARTY_NOTICES.md)。

> [!CAUTION]
> ### 装之前请先备份存档 · 本模组可能导致存档损坏
>
> **This mod can corrupt or lose save data. Back up your save folder before installing.**
>
> 这是一个改写编译器行为的模组，不是纯 UI 插件。它**自己不写存档**，但它会让游戏在
> 加载、保存、自动存档时处理 `.cpp` 文件——而游戏的自动存档（默认开启，每 30 秒一次）
> 会**删除存档目录里不属于当前打开窗口的 `.py` 文件**。放错文件、游戏运行中改文件、
> 保存过程中崩溃、或模组本身的缺陷，都可能造成**代码文件丢失、存档写入失败、存档不一致**。
> 已发生过一次真实故障（`#include` 注册别名导致保存时报「写入存档文件失败」），虽已修复，
> 但这类风险无法完全排除。
>
> ```
> %USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\Saves\Save0
> ```
>
> **退出游戏后**把整个 `Saves\` 目录复制一份再装；出问题时先恢复备份再排查。
> 细节见 [存档安全](#存档安全save-safety) 与 [docs/SAVE_SAFETY.md](docs/SAVE_SAFETY.md)。
> 作者不对任何存档、进度或成就的损失负责。

---

## 这是什么

游戏本身只支持一门自研的类 Python 语言，没有 C++ 执行路径。C++ Farmer 的做法是：

```
.cpp 源码  ──►  C++ 前端编译器（本仓库）  ──►  游戏语言脚本  ──►  游戏自己的解释器执行
```

- 一个用 C++20 写的**单文件前端编译器**：词法 → 递归下降语法 → 语义映射 → 缩进代码生成
- 一个 **BepInEx 5 / Harmony 插件**：在解析入口把生成脚本交给游戏，并接管补全、着色、说明页
- 生成产物仍然由游戏自己的解释器逐节点执行，因此**操作数、成就统计、科技树解锁与手写脚本完全一致**

这不是性能方案：C++ 在这里买到的是表达力（结构体、类型、工程性），不是帧率。

## 功能

| 功能 | 说明 |
|---|---|
| C++ 子集 | 变量/类型、函数、`if/else`、`while`、`for`、范围 for、结构体、枚举、`?:`、`cout <<` |
| 科技树门禁 | 编译期按存档实际解锁项检查，未解锁的构造直接给出带行号的错误 |
| 中文输入友好 | 全角标点明确报错；不可见空白（NBSP、BOM、零宽空格）静默忽略 |
| 多文件 | `#include "x.cpp"` 走游戏原生 import 机制，执行高亮与断点可跨文件 |
| 编辑器集成 | C++ 关键字补全、C++ 语法着色、字符串/注释内不弹补全、`{` 回车自动缩进 |
| 词条说明 | 补全列表与代码悬停时，C++ 关键字和标准库写法都有中文说明（不支持的直接标注） |
| 说明页 | 右上角按钮 + 文档首页链接，指向模组自带的 C++ 参考，内含「常见错误与函数调用」一页 |
| 科技树提示 | 悬停节点显示该科技解锁后可用的 C++ 写法 |
| 断点 | `.cpp` 行号换算成生成脚本的字符偏移；跨文件由游戏原生 `import` 定位到被包含窗口 |
| 执行高亮 | 运行/单步的光块按行映射回 `.cpp` 源码，不会落在注释或空行上 |
| 零侵入 | 不改动游戏**原有**文件；安装器按需新增 `BepInEx\`、`winhttp.dll`、`doorstop_config.ini` 和插件目录 |

## 快速开始（玩家）

0. **先备份 `Saves\Save0\`**（见 [存档安全](#存档安全save-safety)）
1. 从 [Releases](../../releases) 下载 `CppFarmer-mod.zip` 并解压
2. 双击 `install.bat`（或把 `BepInEx/plugins/CppFarmer/` 整个目录复制到游戏目录）
3. **BepInEx 已内置**：安装器检测到游戏里还没有 BepInEx 时，会自动装上捆绑的
   5.4.23.6；**已装过 BepInEx 则完全跳过**，不覆盖你的版本、配置或其它模组
4. 启动游戏，把代码文件改名为以 `.cpp` 结尾，例如 `main.cpp`
5. 写 C++，按运行键

不再需要单独下载安装 BepInEx。若只想装插件、不碰 BepInEx，设环境变量
`CPPFARMER_SKIP_BEPINEX=1`；也可以完全跳过安装脚本，手动放置 DLL（此时需自行装好 BepInEx）。

`install.bat` 双击后会**询问游戏安装位置**（可拖拽游戏文件夹进窗口粘贴）；
也可以把游戏目录作为第一个参数传入跳过询问。
`uninstall.bat` 同样会询问，**只删除插件目录**，不会移除 BepInEx、游戏文件或存档。

```bat
install.bat "<path to game>"
```

日志在 `BepInEx\LogOutput.log`，搜索 `C++ Farmer`。

### 一个例子

```cpp
#include "hello.cpp"

int main() {
    for (int i = 0; i < 4; i++) {
        move(North);
    }
    std::cout << "done" << std::endl;
    return 0;
}
```

`#include` 会把 `hello.cpp` 作为独立模块导入，因此它的函数体节点仍归属那个窗口——
执行高亮和断点能跳进去，和原生 `import` 的表现一致。

## 存档安全(Save Safety)

> 完整版（含备份脚本、恢复步骤、历史故障与免责声明）见
> [docs/SAVE_SAFETY.md](docs/SAVE_SAFETY.md)。

### 先说结论

1. **装之前备份**：把整个 `Saves\Save0\` 复制一份（关掉游戏再复制）。
2. **改文件前关游戏**：外部编辑器、改名、删除，都在游戏退出后进行。
3. **出问题先恢复备份**：`Saves` 换回备份，再来看日志。

### 存档目录里的文件是怎么对应的

游戏把每个打开的代码窗口写成一个文件，文件名是 **`窗口名 + .py`**：

| 编辑器里看到的窗口 | 磁盘上的文件 |
|---|---|
| `main.cpp` | `main.cpp.py` |
| `dfs.cpp` | `dfs.cpp.py` |
| `main.py`（原生脚本） | `main.py` |

所以：

- 想**手工新增**一个 C++ 文件，请写成 `xxx.cpp.py`；只建 `xxx.cpp` 不会被加载（游戏的
  加载只枚举扩展名为 `.py` 的文件）。
- 想**改名**，请连同 `.py` 一起改，否则会出现两个窗口或直接读不到。

### 自动存档会删文件

游戏的自动存档（选项 `autosave`，默认开启，每 30 秒一次）在写回所有窗口之后，会
**删除存档目录里所有“不是当前打开的窗口”的 `.py` 文件**——这是游戏自身的行为，用来
清理已删除的代码窗口。

这意味着：

- 游戏运行时往存档目录里放新文件，可能 30 秒内就被删掉；
- 游戏运行时用外部编辑器**新建/改名**代码文件，同样可能被删；
- 用外部编辑器**修改已有窗口的内容**是安全的（游戏有 file watcher，会把内容同步进窗口），
  但 `autosave` 与 `file watcher` 互斥，只能开一个。

### 这个模组本身会不会写存档

- 插件**不写**存档、不改游戏文件、不碰成就与科技树：它只在内存里把 `.cpp` 文本转译成
  游戏脚本，然后交给游戏自己的解析器。
- 但转译后的脚本是**游戏真实执行的内容**，如果你的 C++ 逻辑写错（比如死循环、越界读写），
  造成的后果和手写脚本写错一样，需要自行回退存档。
- 早期版本有一个已知问题已修复：`#include` 曾在存档里注册别名，导致保存时报
  「写入存档文件失败」。现在 `#include` 只在解析期改写模块名，不碰 `codeWindows`。

### 万一存档出问题

| 症状 | 处理 |
|---|---|
| 代码文件消失 | 从备份里复制回来；**不要在游戏运行时**恢复，先退出游戏 |
| 「写入存档文件失败」弹窗 | 看 `BepInEx\LogOutput.log` 里 `save write failed` 的真实异常；先退出游戏，把 `Saves\Save0` 备份后删掉多余的 `.py` |
| 进游戏报 `error_invalid_expression` 指向注释 | 旧版本的解析回退 bug，升级到最新版；日志里搜索 `cpp parse` 可确认 |
| 想彻底回退 | 删掉 `BepInEx\plugins\CppFarmer\`（或跑 `uninstall.bat`），游戏本身没有被动过 |

游戏每次保存前会在 `Backup\` 下留快照（`Save0_backupNNN`），也可以从那里取回旧版本，
但**不要把它当作唯一保障**——快照也可能包含出问题之后的状态。

## 从源码构建

需要 **g++ (C++20)**、**.NET SDK 6+**、以及**本机已合法安装的游戏 + BepInEx**
作为编译期引用。游戏程序集不在仓库里，也不提供下载，详见 [docs/BUILD.md](docs/BUILD.md)。

```bash
# C++ 侧：编译器、目录监听器、原生 DLL
bash scripts/build.sh

# C# 侧：指定游戏目录
dotnet build plugin/CppFarmer.csproj -c Release \
    -p:GameDir="<path to game>"

# 一键构建并安装
plugin\build_and_install.bat "<path to game>"

# 生成 Release 压缩包
python scripts/pack.py
```

也可以用环境变量 `CPPFARMER_GAME`，或创建 git-忽略的 `plugin/GameDir.local.props`。

## 仓库结构

```
CppFarmer/
├─ cpp_front/src/     原生 C++ 前端编译器
│   ├─ lexer.hpp  ast.hpp  parser.hpp  codegen.hpp
│   ├─ main.cpp          cppfarmer      命令行编译器
│   ├─ watch.cpp         cppfarmer-watch    目录监听 / 批量转译
│   └─ dll.cpp           cppfarmer_transpile_ex  供插件 P/Invoke 的原生接口
├─ plugin/src/         BepInEx / Harmony 插件（C#）
│   ├─ Plugin.cs            补丁注册、转译链路、断点映射、行/字符坐标换算
│   ├─ NativeBridge.cs      P/Invoke 与 UTF-8 内存管理
│   ├─ CppIncludes.cs       #include → import 的模块映射
│   ├─ CppWords.cs          C++ 词表、字符串内补全拦截
│   ├─ CppHighlighter.cs    C++ 语法着色、失焦保持滚动位置
│   ├─ CppIndent.cs         `{` 结尾回车自动缩进一级
│   ├─ CppTooltips.cs       补全/悬停的 C++ 词条说明
│   ├─ CppBlink.cs          执行高亮坐标回映射到源码
│   ├─ CppHelp.cs           独立说明页（含常见错误页）、首页链接
│   └─ CppUnlockInfo.cs     科技树 tooltip 的 C++ 能力提示
├─ scripts/            构建、目录监听与打包脚本（pack.py 生成 Release zip）
├─ examples/           可直接编译的示例
├─ tests/              特性用例、错误用例、多文件用例
├─ src/main.cpp        起步模板（供目录监听使用）
├─ dist/               CppFarmer-mod/（包内文本源 + 捆绑的 BepInEx 载荷）+ 可上传的 zip
└─ docs/               架构、语法、存档安全、构建、排障
```

## 文档

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — 架构、集成点、并发约束、成就与科技树保护
- [docs/LANGUAGE.md](docs/LANGUAGE.md) — C++ 子集支持矩阵与语义映射
- [docs/SAVE_SAFETY.md](docs/SAVE_SAFETY.md) — 存档安全：备份、自动存档行为、恢复与免责声明
- [docs/BUILD.md](docs/BUILD.md) — 构建前置、路径配置、常见构建错误
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) — 运行期问题排查
- [THIRD_PARTY_NOTICES.md](dist/CppFarmer-mod/THIRD_PARTY_NOTICES.md) — 捆绑的 BepInEx 来源、许可证与校验

## 已知限制

- 游戏内所有数字都是浮点数；整型除法映射为向下取整，**负数相除与 C++ 不同**
- 不支持位运算、指针、类与继承、模板、`switch`、`std::set`、`std::pair`、
  `#include <...>`（没有标准库可链接），遇到时给出带行号的明确错误
- 生成脚本仍是 AST 解释执行，操作数消耗不变，**没有性能收益**
- 转译产物与 C++ 源码的坐标系不同：断点、执行高亮、错误位置都已映射回源码，但
  执行高亮的粒度有时只能落到「整行」（生成脚本里没有一一对应的 token 时）
- 需要随游戏更新维护补丁点；补丁失效时插件会尽量降级而不是崩游戏
- 捆绑的 `winhttp.dll` 是 DLL 代理加载器，个别安全软件可能误报；可从
  [BepInEx 官方仓库](https://github.com/BepInEx/BepInEx) 自行下载同版本核对

## 声明 / Notice

**不提供反编译产物。** 本仓库及其 Releases 不包含、不链接、不分发：

- 任何**游戏自身**的程序集、可执行文件、资源文件、文档原文或存档
- 任何反编译源码、IL/字节码反汇编、元数据 dump、字符串堆导出
- 任何逆向工程中间文件或开发期分析脚本
- 任何从游戏代码中摘录的实现片段

`plugin/CppFarmer.csproj` 中的 `Core.dll`、`Utils.dll`、`UnityEngine.*.dll`、`BepInEx.dll`
等只是**编译期引用声明**，指向使用者本机游戏安装目录；仓库里只有路径字符串，没有文件。

### 发行包中的第三方组件

`dist/CppFarmer-mod.zip` 除本项目的两个 DLL 外，还捆绑 **BepInEx 5.4.23.6**
（Windows x64 / Unity Mono）的官方未修改发行文件，让安装器可以开箱即用：

| 项目 | 内容 |
|---|---|
| 官方仓库 | **https://github.com/BepInEx/BepInEx** |
| 版本 / 发行页 | `v5.4.23.6` · https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6 |
| 许可证 | **GNU LGPL-2.1**（或任何更新版本），Copyright (C) 2020 BepInEx Team |
| 许可证全文 | `dist/CppFarmer-mod/LICENSES/BepInEx-LGPL-2.1.txt` |
| 完整声明 | [THIRD_PARTY_NOTICES.md](dist/CppFarmer-mod/THIRD_PARTY_NOTICES.md)（含 SHA-256 与上游库清单） |

包内 `winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx/core/*`
共 21 个文件与官方 `BepInEx_win_x64_5.4.23.6.zip` **逐字节一致**，未作修改。
BepInEx 自带的上游库（UnityDoorstop、HarmonyX、Harmony、MonoMod、Mono.Cecil）
各自归其权利人；完整仓库链接见第三方声明文件。

本包**仍不含**任何游戏程序集、反编译源码、IL 反汇编、游戏资源或逆向中间文件。
包内 `README.md` 与 `install.bat` 均带有存档风险警告；发布 Release 时也应把该警告
写进 Release 说明正文。

本插件在**运行时**通过 Harmony 对已加载的方法打补丁，不覆盖、不修补任何游戏文件；
安装器只会**新增** BepInEx 与插件目录。所有游戏数据、成就、统计与科技树状态仍由
游戏自身管理。插件本身不写存档，但它会改变游戏处理代码文件的方式，使用前请阅读
[存档安全](#存档安全save-safety) 并自行备份。

**使用风险自负 / Use at your own risk.** 模组会改变游戏的解析行为与代码文件的保存方式，
**可能导致存档损坏或丢失**（包括但不限于代码文件消失、保存失败、存档不一致、进度或成就异常）。
作者不对任何存档、进度或成就的损失负责；请在完整备份后再使用，并自行评估风险。
**The mod may corrupt or lose save data. The authors are not responsible for any loss of
saves, progress or achievements. Always keep a full backup.**

本项目是**非官方**爱好者作品，与游戏开发者、发行商、Valve/Steam 无关联。
使用本项目需要你自行**合法拥有**该游戏。请遵守游戏与平台的服务条款；
**若权利方提出要求，本项目会配合调整或下架。**

第三方组件各归其权利人：[BepInEx](https://github.com/BepInEx/BepInEx)（LGPL-2.1，
本包捆绑 5.4.23.6）、Harmony / HarmonyX、UnityDoorstop、MonoMod、Mono.Cecil、
Unity / TextMeshPro、.NET、GCC/MinGW。完整来源与许可证见
[THIRD_PARTY_NOTICES.md](dist/CppFarmer-mod/THIRD_PARTY_NOTICES.md)。


## 许可证

[MIT](LICENSE) — Copyright (c) 2026 C++ Farmer contributors.

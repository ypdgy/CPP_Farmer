# C++ Farmer

在游戏《编程农场》(**The Farmer Was Replaced**) 内置编辑器里，直接用 **C++** 或 **C** 写控制脚本。

> 变更见 [CHANGELOG.md](CHANGELOG.md) · 下载见 [Releases](../../releases)

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

游戏本身只支持一门自研的类 Python 语言，没有 C/C++ 执行路径。C++ Farmer 的做法是：

```
.c / .cpp 源码  ──►  前端编译器（本仓库）  ──►  游戏语言脚本  ──►  游戏自己的解释器执行
```

C 与 C++ 共用同一个前端：`.c` 窗口走 C 方言（printf 会按格式串展开），`.cpp` 窗口走 C++ 方言。
两门语言各有独立的补全词表、着色规则、存档扩展名与说明页。

- 一个用 C++20 写的**单文件前端编译器**：词法 → 递归下降语法 → 语义映射 → 缩进代码生成
- 一个 **BepInEx 5 / Harmony 插件**：在解析入口把生成脚本交给游戏，并接管补全、着色、说明页
- 生成产物仍然由游戏自己的解释器逐节点执行，因此**操作数、成就统计、科技树解锁与手写脚本完全一致**

这不是性能方案：C++ 在这里买到的是表达力（结构体、类型、工程性），不是帧率。

## 功能

| 功能 | 说明 |
|---|---|
| C++ 子集 | 变量/类型、函数、`if/else`、`while`、`for`、范围 for、结构体、枚举、`?:`、`cout <<` |
| C 子集 | 同一个前端 + C 方言：`struct` / `typedef`、数组、枚举、`printf` 占位符在编译期展开 |
| 多语言并存 | `.c` 与 `.cpp` 窗口可以同时打开，各有补全、着色、说明页；存档分别存成 `main.c` / `main.cpp` |
| 科技树门禁 | 编译期按存档实际解锁项检查，未解锁的构造直接给出带行号的错误 |
| 中文输入友好 | 全角标点明确报错；不可见空白（NBSP、BOM、零宽空格）静默忽略 |
| 多文件 | `#include "x.cpp"` 走游戏原生 import 机制，执行高亮与断点可跨文件 |
| 编辑器集成 | C++ 关键字补全、C++ 语法着色、字符串/注释内不弹补全、`{` 回车自动缩进 |
| 词条说明 | 补全列表与代码悬停时，C++ 关键字和标准库写法都有中文说明（不支持的直接标注） |
| 说明页 | 右上角按钮打开**说明总览**，再按语言进入「语言说明」「常见错误」两页；文档首页也有入口 |
| 原生扩展名 | `.cpp` / `.c` 窗口直接存成 `main.cpp` / `main.c`（不再包一层 `.py`），旧存档自动迁移 |
| 外部编辑 | 插件自动把 `cppfarmer_api.hpp` / `cfarmer_api.h` 放进存档目录，编辑器可补全游戏内置函数与枚举 |
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

`.cpp` 窗口现在**直接以窗口名存盘**（`Storage / UseCppExtension`，默认开启），
原生脚本仍按游戏的 `<窗口名>.py` 规则：

| 编辑器里看到的窗口 | 磁盘上的文件 |
|---|---|
| `main.cpp` | `main.cpp` |
| `dfs.cpp` | `dfs.cpp` |
| `main.py`（原生脚本） | `main.py` |

所以：

- 想**手工新增**一个 C++ 文件，直接建 `xxx.cpp` 即可。
- 旧版本留下的 `xxx.cpp.py` 会在**加载时自动改名**为 `xxx.cpp`，代码不会丢；
  若两者同时存在，以 `.cpp` 为准。
- 关掉这个功能（`Storage / UseCppExtension = false`）就退回旧的 `<窗口名>.py` 命名，
  已经迁移的文件会在下次保存时改回。
- 想**改名**，直接改 `.cpp` 文件名；`.py` 窗口仍需连 `.py` 一起改。

插件还会在存档目录放一个**非游戏文件**，供外部编辑器使用，不影响存档内容：

| 文件 | 用途 |
|---|---|
| `cppfarmer_api.hpp` | 游戏内置函数与枚举的声明，编辑器补全用；每次启动按插件版本重写 |

代码里写 `#include "cppfarmer_api.hpp"` 即可（引号形式会搜索当前目录），
**不需要任何编辑器配置**；这行对游戏零影响，前端会专门过滤它。
游戏不加载 `.hpp`，自动存档也只清理 `.py` / `.cpp`，所以它不会被删。
不需要时把 `Storage / ExportEditorHeader` 设为 `false`，或手动删除。

### 自动存档会删文件

游戏的自动存档（选项 `autosave`，默认开启，每 30 秒一次）在写回所有窗口之后，会
**删除存档目录里所有“不是当前打开的窗口”的 `.py` 文件**——这是游戏自身的行为，用来
清理已删除的代码窗口。本模组把同样的清理扩展到 `.cpp`，否则关掉的 C++ 窗口会永远留在磁盘上。

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
├─ native/                    原生前端（一个 DLL，多门语言）
│   ├─ include/cf_api.h       语言无关 ABI
│   ├─ src/registry.cpp       语言分发表、cf_* 实现、旧符号别名
│   └─ frontends/cpp/         C++ 子集：lexer/parser/codegen + CLI + watch
├─ plugin/src/                BepInEx / Harmony 插件（C#）
│   ├─ Plugin.cs              配置项、补丁注册
│   ├─ Core/                  语言无关：源码读取、转译缓存、存档、坐标映射、文档路由
│   ├─ Languages/
│   │   ├─ LanguageProfile.cs 一门语言要实现什么
│   │   ├─ LanguageRegistry.cs 按文件名找语言
│   │   ├─ Cpp/               C++：词表、着色规则、include、说明页、提示
│   │   └─ C/                 C：C 关键字、着色规则、说明页
│   └─ Ui/HelpButton.cs       右上角按钮，打开说明总览
├─ plugin/pages/              游戏内说明页正文（Markdown，随包发布，可直接编辑）
│   ├─ farmer_index.md        总览页：各语言分页的导航
├─ editor/                    外部编辑器用的 API 头（IDE-only）
├─ scripts/                   构建、目录监听与打包（pack.py 生成 Release zip）
├─ examples/cpp/              可直接编译的示例
├─ examples/c/                同上，C 版本
├─ templates/cpp/             起步模板（目录监听用）
├─ tests/cpp/                 特性用例、错误用例、多文件用例
├─ dist/                      CppFarmer-mod/（包内文本源 + 捆绑的 BepInEx 载荷）+ 上传用 zip
└─ docs/                      架构、语言规格、存档安全、构建、排障
```

## 文档

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — 架构、集成点、并发约束、成就与科技树保护
- [docs/languages/cpp.md](docs/languages/cpp.md) — C++ 子集支持矩阵与语义映射
- [docs/languages/c.md](docs/languages/c.md) — C 方言：`printf` 展开、与 C++ 的边界
- [docs/ADDING_A_LANGUAGE.md](docs/ADDING_A_LANGUAGE.md) — 新增一门语言的完整接入点
- [native/README.md](native/README.md) — 原生前端的 ABI 与目录约定
- [docs/SAVE_SAFETY.md](docs/SAVE_SAFETY.md) — 存档安全：备份、自动存档行为、恢复与免责声明
- [docs/BUILD.md](docs/BUILD.md) — 构建前置、路径配置、常见构建错误
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) — 运行期问题排查
- [editor/README.md](editor/README.md) — 外部编辑器（VS Code / clangd / CLion）配置
- [THIRD_PARTY_NOTICES.md](dist/CppFarmer-mod/THIRD_PARTY_NOTICES.md) — 捆绑的 BepInEx 来源、许可证与校验
- [CHANGELOG.md](CHANGELOG.md) — 版本历史与兼容性说明

## 已知限制

- 游戏内所有数字都是浮点数；整型除法映射为向下取整，**负数相除与 C++ 不同**
- C 侧：`printf` 只支持 `%d %i %u %f %s %c` 与 `%%`，宽度/精度不生效；`%x` `%o` `%e` `%g` 会被拒绝而不是打印错数字
- 不支持位运算、指针、类与继承、模板、`switch`、`std::set`、`std::pair`、
  `#include <...>`（没有标准库可链接），遇到时给出带行号的明确错误
- 生成脚本仍是 AST 解释执行，操作数消耗不变，**没有性能收益**
- 转译产物与 C++ 源码的坐标系不同：断点、执行高亮、错误位置都已映射回源码，但
  执行高亮的粒度有时只能落到「整行」（生成脚本里没有一一对应的 token 时）
- 需要随游戏更新维护补丁点；补丁失效时插件会尽量降级而不是崩游戏
- `editor/` 下的两个头文件（`cppfarmer_api.hpp` / `cfarmer_api.h`）只是给 IDE 的声明，
  **不参与游戏内编译**；能写什么仍以 [docs/languages/](docs/languages/README.md) 为准，
  IDE 不会提示前端不支持的那些写法
- `.c` 与 `.cpp` 共用同一个前端，所以 `.c` 里技术上也能写 `std::vector`；不推荐，
  而且跨语言 `#include` 不互认（`.c` 导入 `.cpp` 会按 C 方言编译）
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
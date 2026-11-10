# 架构

> 本文只描述本项目自己的设计与集成点名称。**不包含任何游戏反编译源码或反汇编输出。**
> 集成点名称作为公开 API 事实列出，用于说明补丁挂在哪里；本文不摘录游戏实现代码。

## 分层

```
┌─ 游戏内编辑器 (CodeWindow, 源码文本) ────────────────────┐
│                                                          │
│  BepInEx 插件 (CppFarmer.dll, 托管)                        │
│    Languages/            每门语言一个 profile             │
│    Core/                 语言无关：读源码、缓存、映射      │
│    Ui/                   说明页按钮                       │
│              │ P/Invoke (cf_transpile_ex(language, ...))   │
│              ▼                                            │
│  原生前端 (cppfarmer_native.dll, C++20)                    │
│    src/registry.cpp      按语言 id 分发                    │
│    frontends/<id>/       lexer → parser → AST → codegen    │
│              │                                            │
│              ▼  游戏语言脚本文本                           │
│  游戏自己的 Tokenizer → Parser → AST → 执行器              │
└──────────────────────────────────────────────────────────┘
```

三层的职责边界：

| 层 | 位置 | 知道什么 | 不知道什么 |
|---|---|---|---|
| 语言规则 | `native/frontends/<id>/`、`plugin/src/Languages/<Name>/` | 某门语言的语法、关键字、着色 | 游戏对象模型之外的任何东西 |
| 通用管线 | `plugin/src/Core/` | 游戏 API、存档规则、坐标映射 | 具体是哪门语言 |
| 接线 | `plugin/src/Plugin.cs` | 注册补丁、读配置 | 具体算法 |

**为什么必须转译**：所有世界操作（`move`、`harvest`、`plant`）都由游戏的 C# 解释器执行，
游戏没有原生代码执行路径。转译后的脚本调用的是同一批内置函数，因此统计口径不变。

## 原生前端

`cppfarmer_native.dll` 是**一个 DLL、多个前端**。`native/src/registry.cpp` 维护一张
`CfLanguage[]` 表，插件每次调用都带上语言 id：

```c
int cf_transpile_ex(const char* language, const char* src, const char* unlocks_csv,
                    char** out, char** line_map_csv, char** err);
```

- id 为 `NULL`/空 → 用索引 0 的默认语言
- 未知 id → 返回 10，`err` 写明 `unknown language`
- 旧符号 `cppfarmer_transpile*` 保留为默认语言的别名，供旧插件使用

C++ 前端四个阶段（`native/frontends/cpp/`）：

1. **词法** — 按字节处理 UTF-8；识别 C++ 注释 `//`、字符串（含转义）、预处理行。
   `#include "x"` 会产生一个专用 token；不可见 Unicode 空白按空白吞掉。
2. **语法** — 递归下降，产出 `Program { functions, structs, enums, globals, imports, entry }`。
3. **语义映射** — 把 C++ 构造映射到目标语言：`struct` → 字典构造、`enum` → 前缀常量、
   `cout <<` → `print()`、整型 `/` → `//`、计数 `for` → `range()`、三元 → `if/else`。
   表达式按优先级发射，只在必要时加括号——多余的括号会生成真实节点并消耗操作数。
4. **代码生成** — 输出缩进脚本，同时产出「输出行 → 源行」映射表，供断点与错误定位使用。

ABI 细节与新增语言步骤见 [ADDING_A_LANGUAGE.md](ADDING_A_LANGUAGE.md)。

## 插件结构

```
plugin/src/
├─ Plugin.cs              配置项、补丁注册、科技树词表刷新
├─ Core/
│  ├─ NativeBridge.cs     cf_* P/Invoke 与 UTF-8 内存
│  ├─ CodeSupport.cs      读窗口/磁盘源码、转译缓存、断点与高亮坐标
│  ├─ SaveStorage.cs      存档扩展名、镜像、迁移、文件监听
│  ├─ ExecutionBlink.cs   执行高亮坐标回映射
│  ├─ EditorCompletion.cs 补全候选分发（按 profile）
│  ├─ EditorHighlight.cs  语法着色重绘（按 profile）
│  ├─ EditorScrollGuard.cs 失焦不跳回第一行
│  └─ DocRouter.cs        虚拟文档路由、首页链接
├─ Languages/
│  ├─ LanguageProfile.cs  抽象基类：扩展名、存档后缀、各编辑器钩子
│  ├─ LanguageRegistry.cs 注册表：按文件名找语言（最长后缀优先）
│  └─ Cpp/                C++ 的实现：词表、着色规则、缩进、include、帮助页、提示
└─ Ui/HelpButton.cs       右上角说明页按钮
```

**核心不变量**：`Core/` 里不允许出现某个语言的 id、扩展名或关键字。所有语言差异都经
`LanguageRegistry.ForWindow(window)` 取到 profile 后调用虚方法。新增语言不改 `Core/`。

## 插件的集成点

全部通过 Harmony 运行时补丁，不修改磁盘文件。

### 编译与存档链路（语言无关）

| 集成点 | 用途 |
|---|---|
| `Parser.Parse` | 唯一收口：取 profile → `Prepare` → 转译 → 把 token 流交给游戏解析器 |
| `Tokenizer.Tokenize` | 直接调用（非补丁），用游戏自己的词法器校验生成结果 |
| `Saver.IsValidCodeFileName` | 放行已注册的扩展名，让它们作为普通代码文件参与保存与备份 |
| `Saver.Load` | 前置：`.cpp.py` → `.cpp` 迁移 + 临时镜像；后置：删除镜像、导出编辑器头 |
| `Saver.WriteCodeFiles` | 后置：把 `x.cpp.py` 改名成 `x.cpp`，清理已关闭窗口的文件 |
| `Saver.OnFileChanged` | 文件监听只认 `.py`，自有扩展名的改动由本模组塞进待处理队列 |
| `Saver.WriteTextAtomically` | 保存失败时把真实异常写进日志（诊断用） |
| `CodeWindow.Load` | 读取窗口文本/文件名；注意其字符串参数是**文件内容** |
| `BreakPointPanel.UpdateBreakpoints` | 把源码行号换算成生成脚本的字符偏移 |

### 编辑器 UI

| 集成点 | 用途 | 归属 |
|---|---|---|
| `CodeWindow.GetWordList` / `GetSubWordList` | 补全候选交给 profile 处理 | 通用分发 |
| `CodeWindow.OpenCodeCompleterAtCarret` | 字面量 / 注释内不弹补全 | 通用分发 |
| `CodeWindow.UpdateSearch` / `OnThemeChanged` / `LateUpdate` | 重新应用 profile 的着色 | 通用分发 |
| `CodeWindow.ScrollToStringPosition` | 失焦时跳过「滚到第 0 个字符」 | 通用 |
| `CodeWindow.InsertNewLineIndentation` | `{` 结尾的行回车后多缩进一级 | C++ 专属 |
| `TooltipUtils.GetWordTooltip` | 补全列表 / 悬停时补词条说明 | C++ 专属 |
| `TooltipUtils.UnlockTooltip` | 科技树悬停追加语言能力 | C++ 专属 |
| `Localizer.LoadDoc` | 回答各语言的虚拟文档 id；给文档首页加链接 | 通用分发 |
| `BlinkManager.GetWorldWordRect` / `GetWorldLineRect` | 执行高亮坐标 → 源码坐标 | 通用 |

UI 补丁按「窗口属于哪个 profile」过滤，`.py` 窗口行为完全不变。渲染页面只做**内存内**字符串改写，
不写 `StreamingAssets`，也不触碰游戏文档文件。

两个踩过的值得注意的语义：

- `Tokenizer.Tokenize` 的 `bool` 返回值是「**是否出现无法识别的 token**」，即 true = 失败。
- `CodeWindow.Load` 的字符串参数是**文件内容**，不是文件名；文件名在字段里。

还有一条兜底纪律：读不到窗口文本时**绝不能 `return true` 退回原版解析器**。
那会让游戏把源码原文当成自己的语言解析（其中 `//` 是整除运算符），
报出指向第一行注释的 `error_invalid_expression`。现在的处理是退回读磁盘上的
同名代码文件，再不行才编译成空脚本，并在日志里留下每一阶段的痕迹。

### 存档交互边界

插件不写 `save.json`、不碰物品与科技树字段。但代码窗口会作为普通代码文件参与游戏的保存流程，
并且**扩展名被改成了语言自己的后缀**（`Storage/UseCppExtension`）：

- `.cpp` 窗口存成 `<窗口名>`，例如 `main.cpp` → `main.cpp`；`.py` 窗口仍是 `<窗口名>.py`
- 游戏的加载器只枚举 `*.py`，所以读取时用「临时镜像」：先迁移 `x.cpp.py` → `x.cpp`，
  再为每个 `x.cpp` 写一份 `x.cpp.py`，`Saver.Load` 结束后删除镜像
- 保存时游戏写的是 `x.cpp.py`，后置补丁把它改名成 `x.cpp`，并清理窗口已关闭的文件
- `autosave`（默认每 30 秒）写回窗口后会删除「不属于当前打开窗口」的 `.py` 与自有后缀文件
- `autosave` 与 `file watcher` 互斥，游戏会自动关掉其中一个
- 文件监听只认 `.py`，自有后缀的外部改动由 `SaveStorage.QueueExternalChange` 塞进待处理队列

这些是**游戏自身行为**，但模组改变了哪些文件参与其中，因此使用前必须备份。
详见 [SAVE_SAFETY.md](SAVE_SAFETY.md)。

## 多文件：`#include` 走 import

早期实现把被包含文件**合并进主文件文本**，执行正常，但所有节点都归属主窗口，
执行高亮与断点永远跳不出去。游戏按「节点属于哪个 CodeWindow」定位高亮，而原生
`import` 会去 `workspace.codeWindows` 找到目标窗口并调用它的 `Parse()`，节点自然带那个窗口。

现在的做法：

1. 编译器把 `#include "hello.cpp"` 生成 `from hello import *`
2. 插件在 `Parser.Parse` **之前**改写 `FROM` 后的那个 token 值，把 `hello` 改成真实窗口名 `hello.cpp`
3. 执行时游戏自己的 import 命中原生键，独立解析，节点归属正确

**绝不往 `codeWindows` 里加别名**。游戏保存存档时会枚举 `codeWindows.Values` 并做
`dictionary.Add(fileName, ...)`；别名指向同一窗口会让它被枚举两次，抛重复键异常，
表现为「写入存档失败」。只改 token 值是安全的替代。

这段逻辑由 `LanguageProfile.Prepare` 承担：只有声明了 include 语义的语言才需要实现它。

## 成就与科技树：三条硬约束

1. **统计口径零改动**。成就在动作发生时由游戏记录；转译产物调用同一批内置函数。
2. **`MainSim.RecordsAchievements` 只读不碰**，不调用 `Farm.Unlock` / `UnlockOrUpgrade` /
   `ResetUnlocksToMax`。科技树只读不写。
3. **操作数口径对齐**。生成代码只保留必要括号，计数循环映射为 `range`，
   避免引入额外 AST 节点影响时间/操作数类成就与排行榜的可比性。

编译期的科技树门禁按存档实际解锁项校验，读不到词表时 **fail-open**（不误伤）。

## 线程与并发（重要）

游戏会在**主线程**（编辑器编辑、打开窗口）和**模拟线程**（F5 执行期 import 触发的
`Parse()`）同时进入 `Parser.Parse` 补丁。因此：

- 所有共享容器都用 `ConcurrentDictionary`
- 断点诊断的 `StringBuilder` 是每次调用局部对象
- 原生编译调用用 `NativeLock` 串行化
- 科技树词表由插件的 `Update()` 在**主线程**定期刷新；编译路径只读缓存，
  绝不在模拟线程里取 `MainSim` 的模拟锁

普通 `Dictionary` 被两个线程同时写入会损坏桶链，之后 `TryGetValue` 可能无限循环，
表现为「按 F5 后游戏无响应且日志无异常」。这是一个已经修过的真实故障。

## 目录与产物

| 产物 | 来源 | 说明 |
|---|---|---|
| `cppfarmer.exe` | `native/frontends/cpp/cli.cpp` | 命令行编译器（默认语言） |
| `cppfarmer-watch.exe` | `native/frontends/cpp/watch.cpp` | 目录监听、批量转译 |
| `cppfarmer_native.dll` | `native/src/registry.cpp` + `native/frontends/*` | 全静态链接，供插件 P/Invoke |
| `CppFarmer.dll` | `plugin/` | BepInEx 插件 |

可执行文件名与 `cppfarmer_native.dll` 都保留了历史命名（升级兼容），内部已经是多语言结构。

原生库名必须是 `cppfarmer_native.dll`：Windows 文件名不区分大小写，
`cppfarmer.dll` 会与托管程序集 `CppFarmer.dll` 冲突并把它覆盖掉，导致插件静默不加载。

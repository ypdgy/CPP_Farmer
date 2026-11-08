# 架构

> 本文只描述本项目自己的设计与集成点名称。**不包含任何游戏反编译源码或反汇编输出。**
> 集成点名称作为公开 API 事实列出，用于说明补丁挂在哪里；本文不摘录游戏实现代码。

## 两层

```
┌─ 游戏内编辑器 (CodeWindow, .cpp 文本) ──────────────────┐
│                                                        │
│  BepInEx 插件 (CppFarmer.dll, 托管)                       │
│    · 拦截解析入口，送入原生编译器                        │
│    · 接管补全 / 着色 / 说明页 / 科技树提示 / 断点        │
│              │ P/Invoke (cppfarmer_transpile_ex)             │
│              ▼                                         │
│  原生前端编译器 (cppfarmer_native.dll, C++20)             │
│    lexer → parser → AST → 语义映射 → 缩进代码生成        │
│              │                                        │
│              ▼  游戏语言脚本文本                        │
│  游戏自己的 Tokenizer → Parser → AST → 执行器            │
└────────────────────────────────────────────────────────┘
```

**为什么必须转译**：所有世界操作（`move`、`harvest`、`plant`）都由游戏的 C# 解释器执行，
游戏没有原生代码执行路径。转译后的脚本调用的是同一批内置函数，因此统计口径不变。

## 原生编译器

单文件、无第三方依赖、C++20。四个阶段：

1. **词法** — 按字节处理 UTF-8；识别 C++ 注释 `//`、字符串（含转义）、预处理行。
   `#include "x"` 会产生一个专用 token；不可见 Unicode 空白按空白吞掉。
2. **语法** — 递归下降，产出 `Program { functions, structs, enums, globals, imports, entry }`。
3. **语义映射** — 把 C++ 构造映射到目标语言：`struct` → 字典构造、`enum` → 前缀常量、
   `cout <<` → `print()`、整型 `/` → `//`、计数 `for` → `range()`、三元 → `if/else`。
   表达式按优先级发射，只在必要时加括号——多余的括号会生成真实节点并消耗操作数。
4. **代码生成** — 输出缩进脚本，同时产出「输出行 → 源行」映射表，供断点与错误定位使用。

导出接口（`cpp_front/src/dll.cpp`）：

```c
int  cppfarmer_transpile_ex(const char* src, const char* unlocksCsv,
                       char** out, char** lineMapCsv, char** err);
void cppfarmer_free(char* p);
const char* cppfarmer_version(void);
```

命令行版额外提供 `--check`、`--stdout`、`--line-map`、`--print-features`、`--unlocks`。

## 插件的集成点

全部通过 Harmony 运行时补丁，不修改磁盘文件。下表只列方法名与用途：

### 编译与存档链路

| 集成点 | 用途 |
|---|---|
| `Parser.Parse` | 唯一收口：把生成脚本的 token 流交给游戏解析器 |
| `Tokenizer.Tokenize` | 直接调用（非补丁），用游戏自己的词法器校验生成结果 |
| `Saver.IsValidCodeFileName` | 放行 `.cpp`，让它作为普通代码文件参与保存与备份 |
| `Saver.Load` | 前置：`.cpp.py` → `.cpp` 迁移 + 临时镜像；后置：删除镜像 |
| `Saver.WriteCodeFiles` | 后置：把写出的 `x.cpp.py` 改名成 `x.cpp`，清理已关闭的 `.cpp` |
| `Saver.OnFileChanged` | 文件监听只认 `.py`，`.cpp` 的改动由本模组塞进游戏的待处理队列 |
| `Saver.WriteTextAtomically` | 保存失败时把真实异常写进日志（诊断用） |
| `CodeWindow.Load` | 读取窗口文本/文件名；注意其字符串参数是**文件内容** |
| `BreakPointPanel.UpdateBreakpoints` | 把 C++ 行号换算成生成脚本的字符偏移 |

### 编辑器 UI

| 集成点 | 用途 |
|---|---|
| `CodeWindow.GetWordList` / `GetSubWordList` | 把补全候选换成 C++ 白名单词表 |
| `CodeWindow.OpenCodeCompleterAtCarret` | 字符串 / 字符字面量 / 注释内不弹补全 |
| `CodeWindow.UpdateSearch` / `OnThemeChanged` / `LateUpdate` | 重新应用 C++ 语法着色 |
| `CodeWindow.InsertNewLineIndentation` | `{` 结尾的行回车后多缩进一级 |
| `CodeWindow.ScrollToStringPosition` | 失焦时跳过「滚到第 0 个字符」，保持浏览位置 |
| `TooltipUtils.GetWordTooltip` | 补全列表 / 悬停时补上 C++ 词条说明 |
| `TooltipUtils.UnlockTooltip` | 科技树悬停提示追加 C++ 能力 |
| `Localizer.LoadDoc` | 回答两个虚拟文档 id（`cppfarmer_reference`、`cppfarmer_errors`）；给文档首页加链接 |
| `BlinkManager.GetWorldWordRect` / `GetWorldLineRect` | 执行高亮的生成坐标 → C++ 源码坐标 |

UI 补丁全部按 `.cpp` 文件名过滤，`.py` 窗口行为不变。渲染页面只做**内存内**字符串改写，
不写 `StreamingAssets`，也不触碰游戏文档文件。

两个踩过的值得注意的语义：

- `Tokenizer.Tokenize` 的 `bool` 返回值是「**是否出现无法识别的 token**」，即 true = 失败。
- `CodeWindow.Load` 的字符串参数是**文件内容**，不是文件名；文件名在字段里。

还有一条兜底纪律：读不到窗口文本时**绝不能 `return true` 退回原版解析器**。
那会让游戏把 C++ 原文当成自己的语言解析（其中 `//` 是整除运算符），
报出指向第一行注释的 `error_invalid_expression`。现在的处理是退回读磁盘上的
同名代码文件，再不行才编译成空脚本，并在日志里留下每一阶段的痕迹。

### 存档交互边界

插件不写 `save.json`、不碰物品与科技树字段。但 `.cpp` 窗口会作为普通代码文件
参与游戏的保存流程，并且**扩展名被改成 `.cpp`**（`Storage/UseCppExtension`）：

- `.cpp` 窗口存成 `<窗口名>`，例如 `main.cpp` → `main.cpp`；`.py` 窗口仍是 `<窗口名>.py`
- 游戏的加载器只枚举 `*.py`，所以读取时用「临时镜像」：先迁移 `x.cpp.py` → `x.cpp`，
  再为每个 `x.cpp` 写一份 `x.cpp.py`，`Saver.Load` 结束后删除镜像
- 保存时游戏写的是 `x.cpp.py`，后置补丁把它改名成 `x.cpp`，并清理窗口已关闭的 `.cpp`
- `autosave`（默认每 30 秒）写回窗口后会删除「不属于当前打开窗口」的 `.py` 与 `.cpp`
- `autosave` 与 `file watcher` 互斥，游戏会自动关掉其中一个
- 文件监听只认 `.py`，`.cpp` 的外部改动由 `CppStorage` 塞进游戏自己的待处理队列

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
| `cppfarmer.exe` | `cpp_front/src/main.cpp` | 命令行编译器 |
| `cppfarmer-watch.exe` | `cpp_front/src/watch.cpp` | 目录监听、批量转译 |
| `cppfarmer_native.dll` | `cpp_front/src/dll.cpp` | 全静态链接，供插件 P/Invoke |
| `CppFarmer.dll` | `plugin/` | BepInEx 插件 |

原生库名必须是 `cppfarmer_native.dll`：Windows 文件名不区分大小写，
`cppfarmer.dll` 会与托管程序集 `CppFarmer.dll` 冲突并把它覆盖掉，导致插件静默不加载。

# 新增一门语言

本仓库已经从「一个 C++ 编译器的模组」整理成「多语言前端宿主 + 每语言一个 profile」的结构。
新增语言只需要**新增**文件，不需要改动任何现有语言的代码。本文列出全部接入点。

> 本文面向改代码的人，不面向玩家。玩家视角见 [README](../README.md)。

已实现的四种语言：`cpp`（C++ 子集）、`c`（C 方言）、`java`（Java 子集）与 `csharp`（C# 子集）。
注意 **C 没有单独的解析器**：它复用 `frontends/cpp/`，只在 codegen 里切 `Dialect::C`。
**Java 与 C# 有独立的 parser 与 codegen**（`frontends/java/`、`frontends/csharp/`），但复用 C++ 前端的
lexer 与 AST 定义——如果新语言的语法与现有前端足够接近，优先考虑 C 这条路；语法差异大
（类结构、输出、字符串拼接语义不同）再像 Java / C# 一样独立写 parser/codegen。

---

## 0. 一览：要动的四个地方

| 步骤 | 位置 | 是否必做 |
|---|---|---|
| 1. 原生前端 | `native/frontends/<id>/` | 必做（编译逻辑） |
| 2. 原生注册 | `native/src/registry.cpp` 的 `kLanguages` | 必做 |
| 3. C# profile | `plugin/src/Languages/<Name>/<Name>Language.cs` | 必做 |
| 4. C# 注册 | `plugin/src/Languages/LanguageRegistry.cs` 静态构造 | 必做 |
| 5. 编辑器头（可选） | `editor/` + profile 的 `EditorHeaderFileName` | 可选 |
| 6. 帮助页（可选） | `plugin/pages/<name>_*.md` + profile 的 `ReferenceDocId` / `BuildDoc` | 可选 |
| 7. 示例与测试 | `examples/<id>/`、`tests/<id>/` | 建议 |
| 8. 打包 | `scripts/pack.py`（编辑器目录已自动带上） | 通常无需改 |

下文用「Rust」作为假想例子，实际实现的只有 C++。

---

## 1. 原生前端

原生库是**一个 DLL，多个前端**。每个前端只做一件事：把源码变成游戏语言脚本。

### 1.1 目录与构建

```
native/
├─ include/cf_api.h          语言无关 ABI（一般不用改）
├─ src/registry.cpp          前端注册表（每次加语言改一行）
├─ src/cf_util.{h,cpp}       字符串/CSV 工具
└─ frontends/
   └─ cpp/
      ├─ cpp_frontend.h      声明 cf_rust_language() 之类的描述符函数
      ├─ cpp_frontend.cpp    实现编译入口
      ├─ lexer.hpp parser.hpp codegen.hpp ast.hpp
      └─ cli.cpp watch.cpp   本地命令行工具
```

新增 `native/frontends/rust/`，然后在 `scripts/build.sh` 与 `scripts/build.bat` 的
`INCLUDES` / 源文件列表里加上该目录。Windows 版 `build.bat` 走 g++ 分支时编 DLL，
MSVC 分支目前只编命令行工具——加语言时请一并补上。

### 1.2 ABI 契约

头文件是 [`native/include/cf_api.h`](../native/include/cf_api.h)：

```c
typedef int (*cf_compile_fn)(const char* src, const char* unlocks_csv,
                             char** out, char** line_map_csv, char** err);

typedef struct CfLanguage {
    const char* id;            // 稳定小写 id："rust"
    const char* display_name;  // "Rust"
    const char* extensions;    // ".rs;.rlib"（分号分隔，带点）
    cf_compile_fn compile;
} CfLanguage;
```

**参数顺序必须与 `cf_compile_fn` 完全一致**：`(src, unlocks, out, line_map, err)`。
这是踩过的坑：把 `err` 与 `line_map` 写反时，编译能过、符号也在，但行映射会被当成
错误字符串返回，表现为「断点全部错位」。C 函数指针不做名字检查，编译器不会提醒。

返回约定：

- `0` 成功；非 0 失败，且必须把 `"file:line:col: message"` 写进 `*err`（`cf_dup` 分配）。
- `*out` / `*line_map_csv` 用 `cf_dup` 分配，由插件用 `cf_free` 释放。
- 行映射是**每个输出行一个整数**：该行来自的源码行号，编译器生成的行为 `0`。没有映射时传 `nullptr` 即可，但断点与执行高亮会失效。
- 任何异常都要在 `compile` 内捕获并转成错误字符串，**不要让它穿过 C ABI**。

### 1.3 注册

`native/src/registry.cpp` 已经写好分发、错误格式、旧符号别名，只需加一行：

```cpp
const CfLanguage* const kLanguages[] = {
    &cf_cpp_language(),
    &cf_rust_language(),      // ← 新增
};
```

索引 0 是默认语言（未传 id 时使用），也决定 `/cpp` 之外的旧插件行为。新增语言请**追加**，
不要插到最前面。

### 1.4 兼容别名

`cppfarmer_transpile` / `cppfarmer_transpile_ex` / `cppfarmer_version` 仍导出，指向默认语言。
它们存在只为「新 DLL + 旧插件」这一个组合，不要在新代码里使用。

---

## 2. C# 侧

### 2.1 一个 profile 是什么

[`plugin/src/Languages/LanguageProfile.cs`](../plugin/src/Languages/LanguageProfile.cs)
是抽象基类。默认实现全部是「不做任何事」，所以一门只要求转译的语言只需要覆盖
`Id` / `DisplayName` / `Extensions` 三个属性。

```csharp
internal sealed class RustLanguage : LanguageProfile
{
    public static readonly RustLanguage Instance = new RustLanguage();

    public override string Id => "rust";
    public override string DisplayName => "Rust";
    public override string[] Extensions => new[] { ".rs" };

    public override string StorageSuffix => ".rs";          // 存档里存成 main.rs
}
```

### 2.2 全部钩子

| 成员 | 默认 | 用途 |
|---|---|---|
| `Id` | 抽象 | 必须与原生 `CfLanguage.id` 一致 |
| `DisplayName` | 抽象 | 日志、错误面板、帮助页标题 |
| `Extensions` | 抽象 | 认领哪些文件名（最长后缀优先） |
| `StorageSuffix` | `null` | 存档中的扩展名；`null` = 沿用游戏的 `.py` |
| `EditorHeaderFileName` | `null` | 复制进存档目录的 IDE 头文件名 |
| `ReferenceDocId` / `ErrorsDocId` | `null` | 虚拟文档 id |
| `BuildDoc(docId)` | `null` | 返回该 id 的 Markdown |
| `HomeLinkText` | `null` | 文档首页追加的链接行 |
| `PatchTypes` | 空 | 本语言独占的 Harmony 补丁类 |
| `Prepare(...)` | 原样返回 | 编译前改写源码（如 include 展开） |
| `BuildCompletion` | 原样返回 | 补全候选重排 |
| `FilterCompletion` | 原样返回 | 输入中的候选过滤 |
| `InsideLiteralOrComment` | `false` | 字面量/注释内不弹补全 |
| `Colorize` | `null` | 返回着色后的富文本；`null` = 用游戏自带着色 |
| `DescribeWord` | `null` | 关键字/API 悬停说明 |
| `DescribeUnlock` | `null` | 科技树提示追加内容 |

`Owns(window)` 是便捷方法，等价于 `LanguageRegistry.ForWindow(window) == this`。

### 2.3 注册

`LanguageRegistry` 的静态构造函数里追加：

```csharp
static LanguageRegistry()
{
    Register(new CppLanguage());
    Register(new RustLanguage());   // 索引 1
}
```

`Register` 会检查 id 重复与扩展名冲突并写日志；**索引 0 是默认语言**，帮助按钮和
「无扩展名回退」都用它。

### 2.4 补丁归属

两种补丁要分清：

- **语言无关**（`PatchParserParse`、`PatchStorage*`、`PatchGetWordList`…）已在
  `Plugin.Awake` 里全局注册，内部通过 profile 分发。新增语言**不要**再复制一份。
- **语言独占**（语法着色规则、缩进规则、专属文档）。放进 `Languages/<Name>/`，
  在 `PatchTypes` 里列出即可，`Plugin.Awake` 会自动注册。补丁内部必须用
  `Owns(...)` 提前返回，否则会作用到别的语言的窗口上。

### 2.5 不要重复实现的东西

以下能力已经是语言无关的，直接可用：

- 读窗口文本 / 磁盘兜底（`CodeSupport.GetSource`、`ReadCodeFileFromDisk`）
- 转译缓存、并发串行化（`CodeSupport.TranspileCached`，缓存键含语言 id）
- 断点与执行高亮的坐标映射（`CodeSupport` + `ExecutionBlink`）
- 存档改名、镜像、旧文件迁移、文件监听（`SaveStorage`，按 `StorageSuffix` 循环）
- 文档路由与首页链接（`DocRouter`）
- 编辑器头导出（`SaveStorage.ExportEditorFiles`，按 `EditorHeaderFileName` 循环）

---

## 3. 编辑器资源

外部编辑器支持是**可选**的。需要时：

1. 把声明文件放到 `editor/`（例如 `editor/rustfarmer_api.rs`）。`pack.py` 会把整个
   `editor/` 打进发布包，`install.bat` 会把它复制到 `BepInEx/plugins/CppFarmer/editor/`。
2. profile 的 `EditorHeaderFileName` 返回该文件名，插件在每次加载存档时把它写进存档目录
   （内容相同则不重写）。

**文件必须不是游戏认识的后缀**（不要用 `.rs` 之外的 `.py`），否则会被自动存档当成代码文件清理。
`.hpp` / `.rs` 这类后缀是安全的。

---

## 4. 存档扩展名

`StorageSuffix` 决定窗口在磁盘上的文件名：

| 设置 | 窗口 `main.rs` 落盘 | 说明 |
|---|---|---|
| `null` | `main.rs.py` | 沿用游戏规则，零风险 |
| `".rs"` | `main.rs` | 外部编辑器直接识别；需要迁移/镜像逻辑 |

选 `".rs"` 时，`SaveStorage` 会自动：

- 加载前把旧的 `main.rs.py` 改名为 `main.rs`，并写一份 `main.rs.py` 临时镜像骗过原版加载器；
- 加载后删镜像；
- 保存后把游戏写出的 `main.rs.py` 改名回 `main.rs`，并清理窗口已关闭的文件；
- 把外部编辑产生的 `.rs` 变更塞进游戏的待处理队列。

**同一后缀不要给两个语言**：`LanguageRegistry.Register` 会警告，但迁移与清理会互相打架。

---

## 5. 帮助页

页面正文**不进 DLL**，而是插件目录下的普通 Markdown，随包发布：

```
仓库                                 发布包 / 安装后
plugin/pages/farmer_index.md          →  BepInEx/plugins/CppFarmer/farmer_index.md   ← 总览
plugin/pages/cppfarmer_reference.md   →  BepInEx/plugins/CppFarmer/cppfarmer_reference.md
plugin/pages/cppfarmer_errors.md      →  BepInEx/plugins/CppFarmer/cppfarmer_errors.md
```

profile 只负责两件事：

1. `ReferenceDocId` / `ErrorsDocId` 返回固定 id（如 `"rustfarmer_reference"`）。
2. `BuildDoc(docId)` 从插件目录读对应文件；**读不到时返回一段「页面缺失」提示，
   不要返回 null**——返回 null 会让游戏弹出空白文档，玩家看不出出了什么事。

文件名带语言前缀（`rustfarmer_*`），所以所有页面可以摊平放在同一目录，运行时不必再按语言分子目录。
`pack.py` 的 `EXTRA_DIRS` 已经把 `plugin/pages/*.md` 映射到包的插件目录，`install.bat`
用 `copy *.md` 复制，新增语言无需改这两处；但要在 `pack.py` 的 `required_arcs` 里加上新文件名，
缺页时打包直接失败。

好处：改文案只改 Markdown 并重启游戏，不用重编译；GitHub 上也能直接看 diff。

渲染器只支持标题、粗体、行内代码、链接、`<columns>`、`<unlock=...>`。**行内代码的反引号必须成对**：
游戏的渲染器按反引号整体切分，落单会让后面整页变成代码块。这是踩过的坑。

### 总览页要手动加一节

右上角按钮打开的是 `farmer_index.md`（`ReferenceIndex.DocId`），它**不属于任何语言**，
所以新增语言时要在里面加一节：

```markdown
## Rust（`.rs` 窗口）

- [Rust 语言说明](rustfarmer_reference) — 支持哪些写法
- [Rust 常见错误](rustfarmer_errors) — 会被拒绝的写法
```

按钮和文档首页都不必改：首页只插一条 `ReferenceIndex.HomeLink` 总入口，
再从这里分发。新增分页文件时，记得在 `scripts/pack.py` 的 `required_arcs` 里登记，
缺页会直接打包失败。

插入到文档首页的文本里**不要有反引号**：渲染器按反引号切分整页，落单会让后续全变代码块。

---

## 6. 示例、测试、模板

按语言分目录：

```
examples/<id>/        可直接编译的示例
templates/<id>/       起步模板（scripts/watch_to_save.bat 监听它）
tests/<id>/           正向用例 + err/ 负向用例 + inc/ 多文件用例
```

`tests/README.md` 说明了用例约定与回归脚本，新增语言时复制一份对应目录并在
`docs/BUILD.md` 里补一行命令即可。

---

## 7. 验收清单

新增语言后至少跑完这些：

- [ ] `bash scripts/build.sh` 通过，`objdump -p build/cppfarmer_native.dll | grep cf_` 能看到新 id
- [ ] `python scripts/pack.py` 通过（会预检载荷与两个 DLL）
- [ ] 用 `ctypes` 或命令行直接调 `cf_transpile("<id>", ...)`：成功返回 0，错误返回非 0 且 `err` 可读
- [ ] 行映射非空，且长度等于生成脚本的行数
- [ ] 插件 `dotnet build -c Release` 0 错 0 警
- [ ] 进游戏：日志出现 `language '<id>' (<Name>) ready`
- [ ] 该语言的窗口能执行；`.py` 窗口行为不变
- [ ] 断点、执行高亮落在正确行；失焦不跳回第一行
- [ ] 存档目录里文件名符合预期；关闭窗口后文件被清理
- [ ] 两种语言同时打开时互不干扰（补全、着色、存档各归各的）

---

## 8. 已知边界

这些是多语言改造**没有**解决、需要按需处理的地方：

1. **跨语言 import**：C++ 的 `#include "x.cpp"` 映射到游戏 import，另一门语言若也想跨文件，
   需要自己的 `Prepare`，并且引入的模块必须是同一门语言（游戏按窗口名解析）。
2. **科技树提示**：`PatchUnlockTooltip` 目前只追加 C++ 文案。多语言时它应改为遍历
   `LanguageRegistry.All` 拼接，或在每个 profile 里返回带标题的段落。
3. **帮助按钮**：目前打开默认语言（索引 0）的页面。多语言时应改成菜单。
4. **缩进规则**：`PatchCppIndent` 是 C++ 独占（`{` + `//` 注释）。别的大括号语言可以复用，
   但要把类移到 `Core/` 并改成 profile 钩子，而不是复制一份。
5. **着色**：每个语言一份正则规则表（`CppHighlighter.Rules`）。返回 `null` 就完全交给游戏。
6. **配置项名**：`Storage/UseCppExtension` 这个键名保留了 C++ 的历史命名（改名会重置用户设置），
   但它控制的是「所有带 `StorageSuffix` 的语言」。

# 更新日志

本文件记录用户可见的变化。版本号同时出现在三个地方，发布时必须一致：

| 位置 | 字段 |
|---|---|
| `plugin/src/Plugin.cs` | `PluginVersion`（BepInEx 日志与模组列表） |
| `plugin/CppFarmer.csproj` | `<Version>`（DLL 程序集元数据） |
| `native/src/registry.cpp` | `cf_version()`（插件启动时打印 `native compiler: ...`） |

## v0.6.0 — 2026-11-11

### 新增：C# 语言支持

- 新增 `csharp` 语言（`.cs` 窗口），拥有独立的解析器与代码生成器（复用 C++ 前端的词法器与 AST）。
- 一个文件一个 `class`，类当作**静态容器**——字段与方法都要 `static`，实例成员明确报错。
- 入口 `static void Main(string[] args)`（或无参 `static void Main()`）提升为顶层代码，开局无需「函数定义」。
- 支持类型 `int float double bool char string void var`；一维数组 `int[] a = new int[10]`、`{1,2,3}`、`.Length`。
- 输出 `Console.WriteLine(...)` / `Console.Write(...)` 映射为 `print`；字符串 `+` 拼接自动把数字 `str()` 化。
- 数学 `Math.Abs` `Math.Min` `Math.Max` `Math.Sqrt` `Math.Pow` `Math.Floor` `Math.Ceiling` `Math.Round` 与 `Math.PI`。
- 支持 `foreach (var v in xs)`；`using System;` 整行忽略；`Entities.Tree` 等游戏枚举原样透传。
- `switch`、`do/while`、三元、位运算、对象实例化、属性、字符串插值、泛型、多维数组、`ref`/`out` 均被明确拒绝。
- 独立的补全词表、语法着色、说明页（`csharpfarmer_reference` / `csharpfarmer_errors`）与存档后缀 `.cs`。
- 新增 `docs/languages/csharp.md` 规格说明与 `scripts/test_csharp.py` 回归脚本。

### 兼容性

- 原生 ABI 未变（`cf_*` 接口不变），仅新增语言 id `csharp`；`cf_language_count()` 从 3 变为 4。
- 0.5.x 插件配 0.6.0 DLL 仍能编译 C++/C/Java，但看不到 C#；插件与 DLL 需同版本发布。
- 存档格式、配置项、说明页结构均未变，可直接覆盖安装。

## v0.5.0 — 2026-11-11

### 新增：Java 语言支持

- 新增 `java` 语言（`.java` 窗口），拥有独立的解析器与代码生成器（复用 C++ 前端的词法器与 AST 定义）。
- 一个文件一个 `public class`；类当作**静态容器**——字段与方法都要 `static`，实例成员明确报错。
- 入口 `public static void main(String[] args)` 提升为顶层代码，`args` 被忽略，开局无需解锁「函数定义」。
- 支持类型 `int float double boolean char String void`；一维数组 `int[] a = new int[10]`、`{1,2,3}` 字面量、`.length`。
- 输出 `System.out.println(...)` / `System.out.print(...)` 映射为 `print`；字符串 `+` 拼接自动把数字 `str()` 化。
- 数学 `Math.abs` `Math.min` `Math.max` `Math.sqrt` `Math.pow` `Math.floor` `Math.ceil` `Math.round` 与 `Math.PI`。
- 布尔 `true/false`、`&&` `||` `!` 映射为 `and` / `or` / `not`；`Entities.Tree` 等游戏枚举原样透传。
- `import java.*;` 整行忽略；`switch`、`do/while`、三元、位运算、对象实例化、泛型、多维数组、跨文件导入均被明确拒绝。
- 独立的补全词表、语法着色、说明页（`javafarmer_reference` / `javafarmer_errors`）与存档后缀 `.java`。
- 新增 `docs/languages/java.md` 规格说明与 `scripts/test_java.py` 回归脚本（直接经 `cf_transpile_ex` 调 DLL）。

### 兼容性

- 原生 ABI 未变（`cf_*` 接口不变），仅新增语言 id `java`；`cf_language_count()` 从 2 变为 3。
- 0.4.x 插件配 0.5.0 DLL 仍能编译 C++/C，但看不到 Java；插件与 DLL 需同版本发布。
- 存档格式、配置项、说明页结构均未变，可直接覆盖安装。

## v0.4.1 — 2026-11-11

### 修复：旧插件与新 DLL 混装时崩溃

- **现象**：0.2.x 的 `CppFarmer.dll` 配 0.3.0 / 0.4.0 的 `cppfarmer_native.dll`，
  编译能成功，但第一次输出结果时抛 `EntryPointNotFoundException`（找不到入口点
  `cppfarmer_free`）。
- **影响范围**：只在手动混装两个版本时出现。安装包中的插件与 DLL 始终同版本，
  **正常升级不受影响**，不需要重装。
- **根因**：0.3.0 把导出改名成 `cf_*`，为旧插件保留了 `cppfarmer_transpile` /
  `cppfarmer_transpile_ex` / `cppfarmer_version` 三个别名，漏掉了 `cppfarmer_free`。
  编译侧别名齐全，掩盖了释放侧缺失——所以现象不是一启动就报错，而是跑出结果才崩。
- **修复**：补回 `cppfarmer_free`。导出符号现为 `cf_*` 8 个 + 兼容别名 4 个，共 12 个。

### 兼容性

- 原生 ABI 未变，`cf_*` 与原有别名都保留；本次只补了一个缺失的别名。
- 存档格式、配置项、说明页结构均未变，可直接覆盖安装。

## v0.4.0 — 2026-11-10

### 新增：C 语言支持

- 新增 `c` 语言（`.c` 窗口），与 C++ 共用同一个前端编译器，只在 codegen 上切换 `Dialect`。
  没有复制解析器：C 需要的语法本来就是 C++ 子集的子集。
- `printf` 在编译期按格式串展开成字符串拼接。支持 `%d` `%i` `%u` `%f` `%s` `%c` 与 `%%`；
  `%x` `%o` `%e` `%g` 直接拒绝而不是静默打印十进制。
- 支持 C 的 `typedef struct { ... } Alias;`、`struct Point p;`、`enum`。
  顺带修掉一个 C++ 侧的旧 bug：`typedef` 匿名结构体以前被整体跳过，运行时会 `NameError`。
- `.c` 与 `.cpp` 各有独立的补全词表、语法着色、说明页与存档后缀，可以同时打开。
- 原生库导出 `cf_c_language()`；`cf_language_count()` 返回 2，`cf_transpile_ex("c", ...)` 走 C 方言。

### 修复：开局不可用

- **`main` 现在展开成生成脚本的顶层语句，不再生成 `def main()`**。目标语言的函数定义需要
  科技树「函数定义」解锁，新存档没有，包成函数会让整门语言在开局无法运行。
  末尾 `return` 会被忽略；若 `main` 在 `if` / 循环内提前 `return`，则退回函数形式（那时确实需要解锁）。
- 解锁相关的报错改成中文，并说明 `main` 不需要该科技：
  `这个写法需要先解锁「函数定义」，当前存档还没解锁。main() 会作为顶层代码执行…`

### 修复：语言边界

- `.c` 里写 `cout << ...` 现在会报错并提示改用 `printf` / `print`（以前会被静默翻译成 `print`）。
  `.cpp` 里 `printf` 的占位符仍然被拒绝，两门语言各自只认自己的写法。
- 三元表达式不再因为「顶层缩进为 0」被误判（入口提升后 `main` 就在顶层）。

### 外部编辑器

- 新增 `editor/cfarmer_api.h`（C）与 `editor/cppfarmer_api.hpp`（C++），随包分发，
  进一次游戏后自动复制到当前存档目录。
- `print` / `quick_print` 的声明按语言分开：C++ 用变参模板，C 用变参宏。
  任何函数原型都无法跨 C / C++ / MSVC IntelliSense 表达「任意参数」，宏是唯一不报
  「参数过多」的写法。
- C++ 头补 `#include <iostream>`，写 `std::cout << ... << std::endl;` 不再报未定义。
- `#include "cfarmer_api.h"` / `"cppfarmer_api.hpp"` 在游戏内被完全忽略，
  引号与尖括号、C 与 C++ 四种组合都不会生成 import。

### 文档

- 右上角按钮改为打开**说明总览页** `farmer_index.md`：页内按语言分节，链到 C++ / C
  各自的「语言说明」与「常见错误」。此前按钮固定打开 C++ 页，C 用户看不出还有自己的文档。
  文档首页的两个链接也合并为这一条总入口。
- 游戏内说明页仍是插件目录下的普通 Markdown（`farmer_index.md`、`cfarmer_*.md`、
  `cppfarmer_*.md`），改文案或调整导航都不需要重新编译。
- 新增 `docs/languages/c.md`，`docs/languages/cpp.md` 补充入口提升与解锁说明。
- `docs/ADDING_A_LANGUAGE.md` 记录新增一门语言的全部接入点。
- `tests/README.md` 改为两门语言的目录约定，并加入「开局可用性」回归命令。

### 兼容性

- 原生 ABI 未变（仍是 `cf_*`，`cppfarmer_*` 保留为默认语言别名），0.3.0 的插件可与本版 DLL 共存。
- 存档格式未变：`.cpp` / `.c` 仍直接存成 `main.cpp` / `main.c`；旧 `.cpp.py` 自动迁移。
- 配置项新增 `Storage / ExportEditorHeader` 的行为扩展为「导出每门语言的头文件」，
  `BepInEx/config/cppfarmer.cfg` 不会重置。

## v0.3.0 — 2026-11-10

### 重构：为多语言做准备

- 原生源码 `cpp_front/` → `native/frontends/cpp/`；新增语言无关 ABI `native/include/cf_api.h`
  与分发表 `native/src/registry.cpp`。
- 导出从 `cppfarmer_transpile_ex(src, ...)` 变为 `cf_transpile_ex(language, src, ...)`，
  旧符号保留为默认语言别名，避免旧插件配新 DLL 时缺符号。
- 插件拆成三层：`Languages/`（语言规则）、`Core/`（游戏管线）、`Plugin.cs`（配置与补丁注册）。
  新增 `LanguageProfile` / `LanguageRegistry`，补丁分「通用分发」与「语言独占」两类。
- 说明页正文从 DLL 常量搬到 `plugin/pages/*.md`，随包发布到插件目录，改文案不用重编译。
- `examples/`、`tests/`、`templates/` 按语言分组；`docs/LANGUAGE.md` → `docs/languages/cpp.md`。

### 已知边界

- 跨语言 `#include` 不互认（`.c` 导入 `.cpp` 会按 C 方言编译）。
- 科技树悬停提示只有 C++ 文案；帮助按钮固定打开默认语言页面。

## 更早的版本

`0.1.x` / `0.2.x` 是 C++ 子集的首版与 `.cpp` 存档扩展名、编辑器 API 头的引入，
见仓库的提交历史。

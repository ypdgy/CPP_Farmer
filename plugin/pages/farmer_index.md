# C++ Farmer

**警告**：这个 mod 是 vibe coding 做出的，可能存在大量 bug；若遇到 bug，请到 GitHub 提 issue。

本模组支持四门语言，各自有独立的说明页与错误表。先选你正在写的语言，再进对应分页。

## C++

`.cpp` 窗口：语法、类型、结构体、多文件、执行与断点。

- [C++ 语言说明](cppfarmer_reference)
- [C++ 常见错误](cppfarmer_errors)

## C

`.c` 窗口：与 C++ 的差异、`printf`、`typedef struct`。

- [C 语言说明](cfarmer_reference)
- [C 常见错误](cfarmer_errors)

## Java

`.java` 窗口：类与 `static`、数组、`System.out.println`、字符串拼接。

- [Java 语言说明](javafarmer_reference)
- [Java 常见错误](javafarmer_errors)

## C#

`.cs` 窗口：与 Java 相近，但用 `Console.WriteLine`、`string`/`bool`、`.Length`、`foreach`。

- [C# 语言说明](csharpfarmer_reference)
- [C# 常见错误](csharpfarmer_errors)

## 四门语言共同的部分

- 入口都是 `main`（Java 是 `public static void main`，C# 是 `static void Main`），并被展开成生成脚本的顶层代码，新存档不需要解锁「函数定义」也能运行
- 代码文件直接存进存档目录（`main.cpp` / `main.c` / `main.java` / `main.cs`），可以用 VS Code 等外部编辑器打开
- 操作数消耗、成就与科技树规则与手写脚本完全一致

## 速查

- 想看某门语言支持哪些写法：[C++ 语言说明](cppfarmer_reference) / [C 语言说明](cfarmer_reference) / [Java 语言说明](javafarmer_reference) / [C# 语言说明](csharpfarmer_reference)
- 报错看不懂：[C++ 常见错误](cppfarmer_errors) / [C 常见错误](cfarmer_errors) / [Java 常见错误](javafarmer_errors) / [C# 常见错误](csharpfarmer_errors)
- 按了运行没反应：看安装包 README 的「排障文件」一节
- 存档看起来不对：先退出游戏，再看安装包 README 的「存档安全」

## 换语言

`.c`、`.cpp`、`.java`、`.cs` 窗口可以同时打开，互不影响：补全、着色、存档后缀、说明页都按扩展名区分。
把窗口改名成另一种扩展名，保存时就会换成对应的存储方式与编辑器行为。



# C++ Farmer · C#

**警告**: 这个 mod 用 vibe coding 写成，可能有 bug，遇到问题请到 GitHub 提 issue。

把代码窗口改名为以 `.cs` 结尾，按运行键时会先编译成游戏本身的脚本再执行。
编译产物仍是游戏语言，所以操作数消耗、成就、科技树解锁与手写脚本完全一致。

报错时先看 [常见错误与函数调用](csharpfarmer_errors)。

## 一个类，一个入口

一个 `.cs` 文件是一个 `class`，类名随意（不必等于文件名）：

```csharp
class Farm {
    static void Main(string[] args) {
        for (int i = 0; i < 4; i++) {
            move(North);
        }
        Console.WriteLine("done");
    }
}
```

- 入口是 `static void Main(string[] args)`，也可以写无参的 `static void Main()`；`args` 会被忽略
- `Main` 会被展开成顶层代码（不包成函数），所以**开局不需要解锁「函数定义」也能跑**
- 类内成员**必须加 `static`**：本模组把 class 当作静态容器（字段 + 方法），不支持实例字段/方法
- 自定义 `static` 方法需要解锁「函数定义」

## 支持什么

类型：`int` `float` `double` `bool` `char` `string` `void` `var`

语句：`if` / `else`、`for`、`foreach (var x in arr)`、`while`、`break`、`continue`、`return`

数组（一维）：`int[] a = new int[10]`、`int[] a = {1, 2, 3}`、`a[0]`、`a.Length`

输出：`Console.WriteLine(...)` 和 `Console.Write(...)` 都映射成 `print(...)`（带换行），可以传多个参数

字符串拼接：`"sum=" + n` 会自动把数字转成字符串，等价于 `"sum=" + str(n)`

数学：`Math.Abs` `Math.Min` `Math.Max` `Math.Sqrt` `Math.Pow` `Math.Floor` `Math.Ceiling` `Math.Round`，以及常量 `Math.PI`

布尔：`bool`、`true` / `false`、`&&` `||` `!`，编译成目标语言的 `and` `or` `not`

整数除法：`int / int` 向下取整（和 C++ 一样，负数相除与 C# 不同）

## 被忽略的东西

`using System;` 这类指令会被**整行忽略**，不需要删掉——游戏里没有标准库可链接，写在这里只是让编辑器不报错。

## 不支持什么

- `new` 实例化对象（`new Foo()`）—— 类只是静态容器；`new int[n]` 这种数组创建是支持的
- `switch` / `case` / `default`、`do` / `while`
- 三元表达式 `a ? b : c` —— 请改写为 `if` / `else`
- 属性（`public int X { get; set; }`）、字符串插值（`$"x={n}"`）
- 位运算（`&` `|` `^` `<<` `>>`）、泛型（`List<int>`）、多维数组（`int[][]`）
- 指针、`try` / `catch`、继承、接口、`ref` / `out`、可变参数
- 跨文件：C# 没有对应的 `#include`，多文件请分成多个独立的 `.cs` 窗口

写成这些会得到带行号的明确报错，而不是静默出错。

## 与 C++ / C / Java 的关系

四门语言各自独立编译：`.cs` 有自己的解析器和代码生成器（与 Java 相近，但类型名、入口、输出与 Math 拼写是 C# 的）。
`.cs` 窗口存成 `main.cs`（不是 `main.cs.py`），可以直接用 Visual Studio / VS Code 打开存档目录编辑。

## 存档与备份

本模组不改 `save.json`、不解锁任何科技。但代码文件会参与游戏的保存流程，
自动存档会清理「不属于当前打开窗口」的代码文件。改文件、恢复备份前请先退出游戏。

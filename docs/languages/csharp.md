# C# 子集与语义映射

> 本文只描述本编译器对 C# 的处理，不包含任何游戏文档原文或反编译内容。
> C# 有**独立的解析器与代码生成器**（不是 Java 的方言）；Java 侧见 [java.md](java.md)。

## 程序入口

入口是 `static void Main(string[] args)`（也可以写无参的 `static void Main()`）。
**`Main` 会被展开成生成脚本的顶层语句，不生成 `def`**，所以新存档还没解锁「函数定义」科技时也能直接运行。
`args` 参数被忽略（游戏没有命令行参数）。

其余 `static` 方法仍然生成 `def`，按「先全部定义、最后执行入口」的顺序输出，所以方法顺序无关紧要。
`Main` 里若在 `if` / 循环内部 `return`，会退回 `def Main()` 形式，那时需要「函数定义」解锁。

```csharp
class Farm {
    static int Square(int v) { return v * v; }

    static void Main(string[] args) {
        Console.WriteLine("result=" + Square(3));
    }
}
```

## 类与成员

一个 `.cs` 文件是一个 `class`（类名任意）。本模组把 class 当作**静态容器**：

- 类内字段和方法**必须加 `static`**，实例字段/方法会被拒绝
- 访问修饰符（`public` `private` `protected` `internal`）与 `sealed` `abstract` `partial` 可写、会被忽略
- 不支持实例化：`new Foo()` 会报错；`new int[n]` 这种**数组创建**是支持的

## 支持矩阵

| 类别 | 支持 | 不支持 |
|---|---|---|
| 类型 | `int` `float` `double` `bool` `char` `string` `void` `var` | `byte` `short` `long` `decimal`、泛型 |
| 声明 | 静态字段、局部变量、一维数组 `int[] a` | 实例字段、属性、`int[][]` 多维数组 |
| 方法 | `static` 方法、参数、递归、`void` | 实例方法、`ref` / `out` / `params`、重载 |
| 控制流 | `if/else`、`while`、`for`、`foreach`、`break`、`continue`、`return` | `switch/case`、`do/while`、`try/catch` |
| 表达式 | 算术、比较、`&&` `\|\|` `!`、自增自减、复合赋值、字符串 `+` 拼接 | 三元 `?:`、位运算、`is` / `as`、字符串插值 |
| 数组 | `new int[n]`、`{1,2,3}`、`a[i]`、`a.Length` | `List<T>` 等标准库 |
| 输出 | `Console.WriteLine(...)`、`Console.Write(...)` | `Console.ReadLine`、`string.Format` |
| 数学 | `Math.Abs` `Math.Min` `Math.Max` `Math.Sqrt` `Math.Pow` `Math.Floor` `Math.Ceiling` `Math.Round`、`Math.PI` | 其它 `Math.*` |
| 导入 | `using System;`（整行忽略） | 自定义跨文件导入 |

## 关键映射

| C# 写法 | 生成 |
|---|---|
| `Console.WriteLine(a, b)` | `print(a, b)` |
| `"sum=" + n` | `"sum=" + str(n)`（非字符串操作数自动包 `str`） |
| `int / int` | `//`（向下取整） |
| `a.Length` | `len(a)` |
| `new int[n]` | `cs_list(n, 0)`（列表填充辅助函数） |
| `Math.Sqrt(x)` / `Math.Pow(a,b)` | `cpp_sqrt(x)` / `(a ** b)` |
| `true` / `false` / `null` | `True` / `False` / `None` |
| `&&` / `\|\|` / `!` | `and` / `or` / `not` |
| `Entities.Tree` / `Items.Water` | 原样透传（游戏枚举常量） |
| `foreach (var v in xs)` | `for v in xs:` |

## 与 Java 的差异

C# 与 Java 的生成结果几乎一致，解析器处理的差异是：

- 类型名：`bool` / `string`（Java 是 `boolean` / `String`），并支持 `var` 类型推断
- 入口：`Main`（大写）与无参重载
- 输出：`Console.WriteLine` / `Console.Write`（Java 是 `System.out.println`）
- 导入指令：`using`（Java 是 `import`）
- 循环：`foreach (var v in xs)`（Java 是增强 `for (T v : xs)`）
- 数组长度：`.Length`（Java 是 `.length`）
- 数学：`Math.Sqrt` / `Math.Pow` 等 PascalCase（Java 是 camelCase）

## 已知语义偏差

1. **所有数字都是浮点数**。整型除法按向下取整处理，负数相除与 C# 不同（C# 向零取整）。
2. **字符串 `+` 拼接**依赖 `str()`，与 C# 的 `"a" + 1` 结果一致。
3. **三元表达式、位运算、`switch`、属性、字符串插值会被拒绝**而不是静默给出错误结果。
4. **没有对象**：类只是静态容器，字段和方法靠名字全局可见。
5. **没有跨文件**：`using` 只忽略标准库；多文件请开多个 `.cs` 窗口。
6. **操作数消耗与手写脚本一致**，没有性能收益。

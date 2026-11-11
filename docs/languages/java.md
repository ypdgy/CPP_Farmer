# Java 子集与语义映射

> 本文只描述本编译器对 Java 的处理，不包含任何游戏文档原文或反编译内容。
> Java 有**独立的解析器与代码生成器**（不是 C++ 的方言）；C++ 侧见 [cpp.md](cpp.md)。

## 程序入口

入口是 `public static void main(String[] args)`。**`main` 会被展开成生成脚本的顶层语句，不生成
`def`**，所以新存档还没解锁「函数定义」科技时也能直接运行。`args` 参数被忽略（游戏没有命令行参数）。

其余 `static` 方法仍然生成 `def`，按「先全部定义、最后执行入口」的顺序输出，所以方法顺序无关紧要。
`main` 里若在 `if` / 循环内部 `return`，会退回 `def main()` 形式，那时需要「函数定义」解锁。

```java
public class Farm {
    static int square(int v) { return v * v; }

    public static void main(String[] args) {
        System.out.println("result=" + square(3));
    }
}
```

## 类与成员

一个 `.java` 文件是一个 `public class`（类名任意，不必等于文件名）。本模组把 class 当作**静态容器**：

- 类内字段和方法**必须加 `static`**（`static int x = 0;`、`static int add(...)`），实例字段/方法会被拒绝
- 访问修饰符（`public` `private` `protected`）与 `final` 可写、会被忽略
- 不持实例化：`new Foo()` 会报错；`new int[n]` 这种**数组创建**是支持的

## 支持矩阵

| 类别 | 支持 | 不支持 |
|---|---|---|
| 类型 | `int` `float` `double` `boolean` `char` `String` `void` | `byte` `short` `long`、泛型 |
| 声明 | 静态字段、局部变量、一维数组 `int[] a` | 实例字段、`int[][]` 多维数组 |
| 方法 | `static` 方法、参数、递归、`void` | 实例方法、可变参数、重载（按名区分） |
| 控制流 | `if/else`、`while`、`for`、增强 `for (T v : arr)`、`break`、`continue`、`return` | `switch/case`、`do/while`、`try/catch` |
| 表达式 | 算术、比较、`&&` `\|\|` `!`、自增自减、复合赋值、字符串 `+` 拼接 | 三元 `?:`、位运算、`instanceof` |
| 数组 | `new int[n]`、`{1,2,3}`、`a[i]`、`a.length` | `new Foo()`、`Arrays` 等标准库 |
| 输出 | `System.out.println(...)`、`System.out.print(...)` | `System.in`、`printf` |
| 数学 | `Math.abs` `Math.min` `Math.max` `Math.sqrt` `Math.pow` `Math.floor` `Math.ceil` `Math.round`、`Math.PI` | 其它 `Math.*` |
| 导入 | `import java.*;`（整行忽略） | 自定义跨文件导入 |

## 关键映射

| Java 写法 | 生成 |
|---|---|
| `System.out.println(a, b)` | `print(a, b)` |
| `"sum=" + n` | `"sum=" + str(n)`（非字符串操作数自动包 `str`） |
| `int / int` | `//`（向下取整） |
| `a.length` | `len(a)` |
| `new int[n]` | `java_list(n, 0)`（列表填充辅助函数） |
| `Math.sqrt(x)` / `Math.pow(a,b)` | `cpp_sqrt(x)` / `(a ** b)` |
| `true` / `false` / `null` | `True` / `False` / `None` |
| `&&` / `\|\|` / `!` | `and` / `or` / `not` |
| `Entities.Tree` / `Items.Water` | 原样透传（游戏枚举常量） |

## 与 C++ / C 的关系

Java 有独立的 `native/frontends/java/`（lexer 复用 C++ 前端的，AST 结构也复用，但 parser 与 codegen 是
Java 专属）。三门语言各自独立编译，`.java` 窗口存成 `main.java`，补全、着色、存档后缀、说明页都按扩展名区分。

## 已知语义偏差

1. **所有数字都是浮点数**。整型除法按向下取整处理，负数相除与 Java 不同（Java 向零取整）。
2. **字符串 `+` 拼接**依赖 `str()`，因此 `"a" + 1 + 2` 生成 `"a" + str(1) + str(2)`，与 Java 的
   `"a12"` 一致；但 `1 + 2 + "a"` 里前两个是整数相加（`3`），与 Java 的求值顺序一致。
3. **三元表达式、位运算、`switch` 会被拒绝**而不是静默给出错误结果。
4. **没有对象**：类只是静态容器，字段和方法都靠名字全局可见，同名字段会像 C++ 的全局变量一样冲突。
5. **没有跨文件**：`import` 只忽略标准库；多文件请开多个 `.java` 窗口。
6. **操作数消耗与手写脚本一致**，没有性能收益；Java 买到的是表达力，不是帧率。

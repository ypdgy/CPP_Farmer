# C++ Farmer · Java

**警告**: 这个 mod 用 vibe coding 写成，可能有 bug，遇到问题请到 GitHub 提 issue。

把代码窗口改名为以 `.java` 结尾，按运行键时会先编译成游戏本身的脚本再执行。
编译产物仍是游戏语言，所以操作数消耗、成就、科技树解锁与手写脚本完全一致。

报错时先看 [常见错误与函数调用](javafarmer_errors)。

## 一个类，一个入口

一个 `.java` 文件是一个 `public class`，类名随意（不必等于文件名）：

```java
public class Farm {
    public static void main(String[] args) {
        for (int i = 0; i < 4; i++) {
            move(North);
        }
        System.out.println("done");
    }
}
```

- 入口是 `public static void main(String[] args)`；`args` 参数会被忽略（游戏没有命令行参数）
- `main` 会被展开成顶层代码（不包成函数），所以**开局不需要解锁「函数定义」也能跑**
- 类内成员**必须加 `static`**：本模组把 class 当作静态容器（字段 + 方法），不支持实例字段/方法
- 自定义 `static` 方法需要解锁「函数定义」，和 C++ 的自定义函数一样

## 支持什么

类型：`int` `float` `double` `boolean` `char` `String` `void`

语句：`if` / `else`、`for`、增强 `for`（`for (int x : arr)`）、`while`、`break`、`continue`、`return`

数组（一维）：`int[] a = new int[10]`、`int[] a = {1, 2, 3}`、`a[0]`、`a.length`

输出：`System.out.println(...)` 和 `System.out.print(...)` 都映射成 `print(...)`（带换行），可以传多个参数

字符串拼接：`"sum=" + n` 会自动把数字转成字符串，等价于 `"sum=" + str(n)`

数学：`Math.abs` `Math.min` `Math.max` `Math.sqrt` `Math.pow` `Math.floor` `Math.ceil` `Math.round`，以及常量 `Math.PI`

布尔：`boolean`、`true` / `false`、`&&` `||` `!`，编译成目标语言的 `and` `or` `not`

整数除法：`int / int` 向下取整（和 C++ 一样，负数相除与 Java 不同）

## 被忽略的东西

`import java.util.*;` 这类标准库导入会被**整行忽略**，不需要删掉——游戏里没有标准库可链接，写在这里只是让编辑器不报错。

## 不支持什么

- `new` 实例化对象（`new Foo()`）—— 类只是静态容器，不能当对象用；`new int[n]` 这种数组创建是支持的
- `switch` / `case` / `default`、`do` / `while`
- 三元表达式 `a ? b : c` —— 请改写为 `if` / `else`
- 位运算（`&` `|` `^` `<<` `>>`）、泛型（`List<Integer>`）、多维数组（`int[][]`）
- 指针、`try` / `catch`、继承、接口、`System.in`、可变参数
- 跨文件：Java 没有对应的 `#include`，多文件请分成多个独立的 `.java` 窗口

写成这些会得到带行号的明确报错，而不是静默出错。

## 与 C++ / C 的关系

三门语言各自独立编译：`.java` 有自己的解析器和代码生成器（不是 C++ 的方言）。
`.java` 窗口存成 `main.java`（不是 `main.java.py`），可以直接用 IntelliJ / VS Code 打开存档目录编辑。

## 存档与备份

本模组不改 `save.json`、不解锁任何科技。但代码文件会参与游戏的保存流程，
自动存档会清理「不属于当前打开窗口」的代码文件。改文件、恢复备份前请先退出游戏。

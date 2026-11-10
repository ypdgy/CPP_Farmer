# C 子集与语义映射

> 本文只描述本编译器对 C 的处理，不包含任何游戏文档原文或反编译内容。
> `.c` 与 `.cpp` 共用同一个前端，C++ 侧规则见 [cpp.md](cpp.md)。

## 程序入口

入口是 `main`。**`main` 会被展开成生成脚本的顶层语句，不生成 `def`**，所以新存档还没解锁
「函数定义」科技时也能直接运行。末尾的 `return` 会被丢弃。

其余函数仍然生成 `def`，按「先全部定义、最后执行入口」的顺序输出，所以函数顺序无关紧要，
前置声明也不需要。`main` 里若在 `if` / 循环内部 `return`，会退回 `def main()` 形式，
那时需要「函数定义」解锁。

## 与 C++ 的边界

`.c` 窗口走 C 方言，**没有 `cout` 和 `<<` 输出流**——写了会报
`C 里没有 cout / 输出流`。输出用 `printf(...)` 或 `print(...)`。同理 `.cpp` 窗口里
`printf` 的占位符仍被拒绝，两边各自只认自己的写法。

```c
#include <stdio.h>

int square(int v) { return v * v; }

int main(void) {
    printf("result=%d\n", square(3));
    return 0;
}
```

## 支持矩阵

| 类别 | 支持 | 不支持 |
|---|---|---|
| 类型 | `int` `float` `double` `bool` `char` `void` `long` `short` `unsigned` `signed` `size_t` | 指针、`union`、位域 |
| 声明 | 局部/全局变量、数组 `int a[9]`、`const`、`static`、`typedef` | `register`、`restrict`、`volatile` 语义 |
| 函数 | 定义、参数、递归、`void f(void)` | 可变参数（`printf` 是编译器特例）、函数指针 |
| 控制流 | `if/else`、`while`、`do`、`for`、`break`、`continue`、`return` | `switch/case`、`goto` |
| 表达式 | 算术、比较、`&&` `\|\|` `!`、`?:`（仅作独立语句）、自增自减、复合赋值 | 位运算、`sizeof`、逗号表达式 |
| 复合类型 | `struct X { ... };`、`typedef struct { ... } X;`、`enum` | 嵌套定义、匿名 struct 变量 |
| 预处理 | `#include "x.c"`（真导入）、`#include <...>`（整行忽略） | `#define`、宏、条件编译 |
| 输出 | `printf`（见下）、`print` | `puts`、`putchar`、`sprintf` |

## printf

`printf` 在**编译期**展开成字符串拼接，占位符与参数逐个核对：

```c
printf("x=%d, ok=%s\n", x, "yes");
// 生成: print("x=" + str(x) + ", ok=" + "yes" + "\n")
```

| 转换符 | 行为 |
|---|---|
| `%d` `%i` `%u` | 十进制整数 |
| `%f` | 浮点数（按游戏格式输出） |
| `%s` | 字符串，原样拼接 |
| `%c` | 字符字面量，原样拼接；参数必须是 `'A'` 这种字面量 |
| `%%` | 一个字面量百分号 |
| `%x` `%X` `%o` `%e` `%g` | **编译期拒绝**：游戏没有十六进制/科学计数法输出 |
| `%p` `%n` | **编译期拒绝** |

- 宽度与精度（`%5.2f`）会被解析但**不生效**，数值按游戏自己的格式输出
- 占位符数量与参数数量不一致直接报错，并指出行号
- 第一个参数必须是字符串字面量，否则无法在编译期校验

## 结构体

两种写法等价，生成的都是「构造函数 + 字典字段」：

```c
struct Point { int x; int y; };        /* 名字 Point，用 Point p; */
typedef struct { int x; int y; } Vec;  /* 名字 Vec，用 Vec v; */
```

字段访问用 `p.x`，生成 `p["x"]`。没有指针，所以也没有 `p->x`。

## 多文件

```c
#include "helper.c"
```

被包含的文件必须是**已存在的 `.c` 窗口**（存档目录里的 `helper.c`）。编译器把它生成为
`from helper import *`，再由游戏自己的 import 机制解析——这样断点与执行高亮才能跳进被包含的文件。

尖括号包含（`#include <stdio.h>`）整行忽略，因此标准头写了也不会出错，只是没有标准库。

## 与 C++ 的差异

- `printf` 带占位符：C 支持（编译期展开），C++ 报错并建议改用 `std::cout`
- 补全词表：C 只有 C 关键字，不提供 `class` `template` `vector` 等 C++ 写法
- 着色规则：C 的规则表不含 C++ 专属关键字
- 存档：`.c` 窗口存成 `main.c`，`.cpp` 窗口存成 `main.cpp`，互不影响
- 说明页与编辑器头：`cfarmer_reference.md` / `cfarmer_api.h`，与 C++ 的两套并列

`.c` 里其实也能写 C++ 构造（例如 `std::vector`），因为共用前端；但不推荐，理由是可移植性。

## 已知语义偏差

1. **所有数字都是浮点数**。<dfn>整型除法</dfn>按向下取整处理，负数相除与 C 不同。
2. **`printf` 的宽度/精度不生效**，`%5.2f` 与 `%f` 输出一致。
3. **三元表达式只能作独立语句**，因此 `printf("%s", x ? "a" : "b")` 会报错。
   改法是先算到变量里：`const char *` 不可用（无指针），所以用 `if/else` 分两次 `printf`。
4. **没有指针**，所以没有字符串变量、没有 `char *`、没有 `malloc`。
5. **操作数消耗与手写脚本一致**，没有性能收益；C 买到的是表达力，不是帧率。

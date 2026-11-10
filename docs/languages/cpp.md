# C++ 子集与语义映射

> 本文只描述本编译器的语法与映射规则，不包含任何游戏文档原文或反编译内容。

## 程序入口

入口是 `main`。**`main` 会被展开成生成脚本的顶层语句，不生成 `def`**：目标语言的函数定义
需要科技树「函数定义」解锁，而新存档开局没有，包成 `def main()` 会让整个语言在开局不可用。

其余函数仍然生成 `def`，并且先于入口全部定义，所以函数定义顺序无关紧要，前置声明也不需要。

`main` 末尾的 `return` 会被丢弃（顶层没有返回值）。若 `main` 里出现写在 `if` 或循环内部的
`return`（提前退出），它无法提升到顶层，就会退回 `def main()` + `main()` 形式——那种代码
需要「函数定义」解锁。

```cpp
int square(int v) { return v * v; }

int main() {
    std::cout << square(3) << std::endl;
    return 0;
}
```

## 支持矩阵

| 类别 | 支持 | 不支持 |
|---|---|---|
| 类型 | `int` `float` `double` `bool` `char` `string` `void` `auto`、整数/无符号别名 | 指针、引用语义、模板、`union` |
| 声明 | 局部变量、全局变量、数组 `int a[9]`、`const` | `constexpr` 求值、`static` 语义 |
| 函数 | 定义、参数、默认参数、递归、重载（按名映射） | 类方法、lambda、函数指针 |
| 控制流 | `if/else`、`while`、`do`、`for`、范围 for、`break`、`continue`、`return` | `switch/case`、`goto` |
| 表达式 | 算术、比较、`&&` `||` `!`、`?:`、自增自减（语句/for 步进）、复合赋值 | 位运算、`new`/`delete`、逗号表达式 |
| 容器 | `std::vector`（`push_back`/`pop_back`/`size`/`empty`/下标）、`std::map`、初始化列表 | 迭代器、算法库、`std::set`、`std::pair` |
| 复合类型 | `struct` → 字典构造、`enum` → 前缀常量 | 继承、多态、访问控制 |
| 预处理 | `#include "x.cpp"` | `#include <...>`、`#define`、宏 |
| 注释 | `//`、`/* */` | `#` 注释（那是目标语言的注释符） |

`using namespace std;`、`typedef ...;`、`namespace x { }` 会被**接受并跳过**，
但结尾的 `;` 必须写。少了分号会直接报带行号的错，不会再把后面的代码一起吞掉
（旧行为会让整个程序静默变成空脚本）。

## 语义映射表

| C++ | 生成结果 | 备注 |
|---|---|---|
| `int a = 1;` | `a = 1` | 目标语言无类型声明 |
| `a / b`（两个整型） | `a // b` | **向下取整**，C++ 是向零截断，负数结果不同 |
| `a && b` / `a \|\| b` / `!a` | `a and b` / `a or b` / `not a` | 按优先级发射，只在必要时保留括号 |
| `for (int i=0; i<n; i++)` | `for i in range(n):` | 操作数消耗与手写 `range` 相同 |
| `for (int x : items)` | `for x in items:` | |
| `std::cout << a << b << std::endl;` | `print(a, b)` | 支持多段拼接 |
| `printf("text");` | `print("text")` | **仅纯字符串字面量**；含 `%` 明确报错 |
| `print("text");` | `print("text")` | 直通游戏内置 |
| `v.push_back(x)` / `v.pop_back()` / `v.size()` | 对应内置函数 | |
| `struct P { int x; int y; };` | 返回字典的构造函数 | 字段用 `p.x` 读写 |
| `enum Mode { IDLE, RUN };` | `MODE_IDLE` / `MODE_RUN` | 加前缀避免与内置名冲突 |
| `#include "hello.cpp"` | `from hello import *` | 见下方多文件说明 |
| `None` / `True` / `False` | 原样 | 目标语言的内置常量 |

不支持的构造会给出**带行号的明确错误**，而不是静默生成错误结果。
例如位运算：

```
main.cpp:7: error: bitwise operators are not available in the target language (consider arithmetic instead)
```

**没有效果的语句同样报错**。`printf;`、`x;`、`a + b;` 这类「算出来没人用」的语句，
C++ 编译器只给警告，游戏更是照单全收地什么也不做——所以本模组直接拒绝：

```
main.cpp:2: error: 'printf' 是函数，要写成 printf(...)；只写名字这一行什么也不会做
main.cpp:3: error: 'x' 这一行什么也不会做（少写了赋值或函数调用？）
```

游戏对自己的内建名（`print`、`move` …）本来就会提示「是不是要写 print()？」，
这条规则把同样的照顾给到只有本模组认识的 C++ 名字。

**不支持的写法按名字拒绝**，而不是让语法抛 `expected ';', got '{'`：

| 写法 | 报错 |
|---|---|
| `switch` / `case` / `default` | 不支持 switch，请改用 if / else if |
| `sizeof` `new` `delete` `this` `operator` | 明确标注不支持，并给出替代写法 |
| `template` / `typename` / `virtual` / `override` | 不支持模板、继承与虚函数 |
| `namespace x { }` | 不支持 namespace 块（内容会被整块跳过） |
| `std::set` / `std::pair` | 请改用 `std::vector` / `struct` |

这些词也不会出现在补全列表里。

**指令行必须有结尾的 `;`。** `using namespace std`（漏分号）在旧版会把后面整个文件
吞掉、编译出空脚本，游戏表现为「什么都没发生也不报错」；现在会直接报
`'using' 后面缺少结尾的 ';'`，行号指向 `using` 那一行。`namespace` / `template`
块不再被静默跳过，而是明确拒绝。

## 输入相关

**全角标点显式报错**。中文输入法打出的 `；` `（）` `＝` 不会被静默替换，
而是提示应该改成哪个半角字符——静默改写会掩盖输入法问题。

**不可见空白静默忽略**：U+00A0 不换行空格、U+2000–U+200B、U+2028/2029、
U+202F、U+205F、U+3000、U+FEFF(BOM)。这些字符肉眼无法分辨，报错也无法据此修改。

## 多文件

```cpp
#include "hello.cpp"

int main() {
    hello();
}
```

- 只认引号形式；`<...>` 忽略（没有标准库可链接）
- **例外**：`#include "cppfarmer_api.hpp"` / `<cppfarmer_api.hpp>` 一律忽略，既不导入也不报错
  （它是 IDE 用的 API 声明，游戏没有对应文件）
- 模块名 = 文件名去掉扩展名，必须是合法标识符（`my-lib.cpp` 会报错，请改名）
- 被包含文件应**在编辑器中打开**；插件不会在解析过程中创建窗口
- 生成的是 `from hello import *`，由游戏自己的 import 机制加载，因此执行高亮与断点能跨文件
- 重复包含自动忽略，循环包含由游戏检测

## 游戏内置直通

世界操作在 C++ 里同名调用，枚举沿用点号写法：

```cpp
move(North);
harvest();
can_harvest();
plant(Entities.Bush);
till();
use_item(Items.Water);
get_pos_x();  get_pos_y();  get_world_size();
```

`Entities.Tree`、`Items.Hay`、`Grounds.Soil`、`Unlocks.Carrots` 等照常可用。

编译期会按存档解锁项校验特性，缺少时直接报中文提示：

`这个写法需要先解锁「函数定义」，当前存档还没解锁。main() 会作为顶层代码执行，不需要解锁；…`

`main` 本身不触发「函数定义」；自定义函数、`struct` / `class` 会。有 `#include` 时，
来自其他文件的函数名无法静态验证，交由游戏运行时检查。

## 已知语义偏差

1. **所有数字都是浮点数**，没有真正的整数类型。
2. **整型除法向下取整**，负数与 C++ 不同。
3. **没有性能收益**：产物仍是 AST 逐节点解释执行。
4. `struct` 是字典，不是值类型布局；赋值语义随字典。
5. 生成脚本与 C++ 原文是两个坐标系，错误行号已映射，但游戏自身的高亮/搜索按生成脚本定位。

[<- 返回 C++ 说明](cppfarmer_reference)
---
# 常见错误与函数调用

## 报错怎么读

按运行键时会先编译整个文件，编译不通过就一步都不会执行。报错长这样：

`main.cpp:12: error: ...`

前面的数字是行号，指的是你的 .cpp 源码，不是游戏语言的行号。

## 只有名字的语句

只写一个名字，既没有括号也没有赋值，编译器会当成笔误：

`printf;` 报 `'printf' 是函数，要写成 printf(...)；只写名字这一行什么也不会做`

`x;` 报 `'x' 这一行什么也不会做（少写了赋值或函数调用？）`

改成 `printf("hi");` 或 `use_item(Items.Water);` 就正常了。游戏只对它自己认识的名字提示过这个问题，C++ 独有的名字由本模组来提示。

## printf 的两条限制

- `printf("hello");` 只有在纯字符串时才翻译成游戏的 print
- 带百分号占位符会直接报错，请改用 `std::cout << a << b << std::endl;`

## 解锁相关

新存档开局没有「函数定义」科技，编译器会在报错里点名缺哪一项：

`这个写法需要先解锁「函数定义」，当前存档还没解锁。main() 会作为顶层代码执行，不需要解锁；只有自定义函数、struct / class 才用到这个科技`

- `main` 本身**不会**触发这一项：它被展开成顶层代码
- 自定义函数、`struct` / `class` 会触发，因为它们必须生成真正的函数
- `main` 里如果有写在 `if` 或循环内部的 `return`（提前退出），`main` 会退回函数形式，也会触发

想开局就能跑，把提前退出改成 `if` 包住后面的逻辑，别在分支里 `return`。

## 未知函数

`unknown function 'foo' - add a mapping or a definition`

一般是这三种原因：名字拼错；定义写在了使用之后；函数在另一个文件里却没写 `#include "foo.cpp"`。

注意：文件里一旦出现 include，未知名字会被放行，留给运行时由游戏报告，因为编译器看不到另一个文件里定义了什么。

## 不支持的方法

`v.foo()` 会报 `unsupported method 'foo()'`。

容器支持的方法只有这些：push_back、emplace_back、append、pop_back、size、length、empty、clear、count、front、back、at、substr。

## 不支持的 C++ 写法

- `switch` / `case` / `default`：请改用 if / else if
- `sizeof`、`new`、`delete`、`this`、`operator`
- `template` / `typename`：不支持模板
- `virtual` / `override`：不支持继承与虚函数
- `namespace x { }`：内容会被整块跳过，请写在文件顶层
- `std::set` / `std::pair`：请改用 std::vector 或 struct
- 指针与引用语义、位运算、`#include <...>`、`#define`

这些词不会出现在补全列表里；手写出来会直接报错并说明原因，不会静默跳过。

## 少写分号

`using namespace std` 后面必须补上 `;`，否则报：

`'using' 后面缺少结尾的 ';'（一直扫到文件末尾都没找到）`

`typedef` 同理。直接写完整的 `using namespace std;` 最省事。

## 全角标点

中文输入法打出的 `；` `（）` `＝` 不会被静默替换，而是提示改成对应的半角字符。静默改写会掩盖输入法问题。

## 没解锁的构造

`这个写法需要先解锁「列表」，当前存档还没解锁`

去科技树解锁对应科技即可。`main` 本身不再需要「函数定义」（它被展开成顶层代码），
但自定义函数、`struct` / `class` 仍然需要。本模组从不替你解锁任何东西，
成就与解锁条件仍按游戏原本的规则走。

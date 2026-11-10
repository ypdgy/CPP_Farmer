# C++ Farmer v0.3.0测试版
**警告**: 这个mod是vibe coding做出的，可能存在大量bug，若出现bug请前往github提出issue。

C++ Farmer 让你在游戏内直接用 C++ 写农场脚本。文件名以 `.cpp` 结尾的代码窗口，按下运行键时会先被编译成游戏本身的脚本，然后照常执行。

**重要**：编译产物仍然是游戏语言，所以操作数消耗、成就统计、科技树解锁与手写脚本完全一致。

按运行键报错时，先看 [常见错误与函数调用](cppfarmer_errors)：那里列了所有会被明确拒绝的写法。

## 文件与运行

把代码窗口改名为以 `.cpp` 结尾即可。编译错误显示在同一个错误面板里，行号对应你的 C++ 源码，改完再按运行键，不必重启游戏。

## 函数与入口

函数用返回类型声明，程序入口是 `main`。`main` 会在所有函数定义之后被自动调用。

`int square(int v) { return v * v; }`

`void main() { std::cout << square(3) << std::endl; }`

## 变量与类型

声明时带类型：`int`、`float`、`double`、`bool`、`char`、`std::string`，也支持 `auto`。
数组写成 `int grid[9];`，向量写成 `std::vector<int> v;`。

## 控制流

`if`、`else`、`while`、`for`、`break`、`continue`、`return` 与 C++ 一致。

`for (int i = 0; i < n; i++) { }`

`for (int x : items) { }`

计数循环会被编译成游戏的 `range`，因此操作数消耗与手写 `for i in range(n)` 相同。

## 游戏内置函数

游戏提供的操作在 C++ 里同名直通，调用方式不变。

`move(North);`  `harvest();`  `can_harvest();`

`plant(Entities.Bush);`  `till();`  `use_item(Items.Water);`

`get_water();`  `get_pos_x();`  `get_pos_y();`  `get_world_size();`

枚举沿用点号写法：`Entities.Tree`、`Items.Hay`、`Grounds.Soil`、`Unlocks.Carrots`。
游戏里的 `None`、`True`、`False` 在 C++ 里照常可用。

## 输出

推荐用 `std::cout << "文本" << std::endl;`，可以连续拼接多段。

也可以用游戏内置的 `print("文本");`，或只含纯字符串的 `printf("文本");`。
`printf` 的百分号占位符不支持，会明确报错，请改用 `std::cout`。

## 容器

`std::vector<int> v;`

`v.push_back(1);`  `v.pop_back();`  `int n = v.size();`  `v.empty()`

## 多文件

`#include "hello.cpp"`

被包含的文件一起参与编译，重复包含会自动忽略，可以省略 include guard。
查找顺序：已打开的代码窗口优先，其次是存档目录下的同名文件。

## 结构体与枚举

`struct Point { int x; int y; };`

`enum Mode { IDLE, RUN };`

结构体编译成字典，字段用 `p.x` 读写；枚举成员会加上枚举名前缀，避免与游戏内置名冲突。

## 断点与单步

在行号栏点击即可下断点，跨文件也能命中。单步调试与原生行为一致。

## 已知限制

游戏内所有数字都是浮点数，所以整型除法按向下取整处理，负数相除的结果与 C++ 不同。
位运算、指针、类与继承、模板都不支持，遇到时会给出明确错误。
不支持 `#include <...>` 形式的头文件，这里没有 C++ 标准库可链接。

## 代码提示

`.cpp` 窗口的补全会给出 C++ 关键字、当前文件里的标识符，以及游戏内置函数。

游戏自带的函数说明仍然适用于运行时行为，本页只说明 C++ 的写法差异。

[<- 返回 C 说明](cfarmer_reference)
---
# 常见错误与函数调用

下面按类别列出会被**明确拒绝**的写法。报错里都带行号，并尽量给出替代写法。

## printf 相关

- `printf 的格式串必须是字符串字面量` —— 格式串只能写字面量，不能用变量或拼接结果
- `printf 的占位符比参数多` —— `%d` 有 2 个但只传了 1 个参数；补齐参数或删掉占位符
- `printf 的参数比占位符多` —— 传了参数却没写对应占位符；多出来的参数不会被打印
- `printf 暂不支持 %x 这个转换符` —— 只支持 `%d` `%i` `%u` `%f` `%s` `%c` 和 `%%`；十六进制、科学计数法没有对应输出
- `printf 的格式串以 % 结尾` —— 想输出百分号请写 `%%`

## 语法

- `expected ';', got 'xxx'` —— 上一行漏了分号，常见于 `struct` 定义结尾
- `expected a declaration` —— 顶层出现了没有类型的语句
- `C 里没有 cout / 输出流` —— C 没有 iostream；输出改用 `printf("...", x)` 或 `print(x)`
- `这个写法需要先解锁「函数定义」` —— `main` 不需要（展开为顶层代码）；自定义函数、`struct` 需要科技树解锁
- `printf 只能写在函数里` —— 可执行语句必须在函数内
- `#include 只能写在文件顶层` —— 别把 include 放进函数或代码块

## 不支持的结构

- `switch (x) { case 1: ... }` —— 改成 `if` / `else if` 链
- `sizeof(int)` —— 直接用具体数字，或定义一个常量
- `p->x`、`*p`、`int *p` —— 没有指针；用 `struct` 值或数组下标
- `malloc` / `free` —— 没有堆；用固定大小数组或全局变量
- 按位与、按位或、`a << 2` —— 不支持位运算，改用算术或布尔运算
- `void f(int ...)` —— 不支持可变参数，拆成固定参数重载（按名区分）

## 运行期

- 按运行键没反应 —— 看日志里的 `c parse begin` / `source N chars`；`0 chars` 说明窗口文本没读到
- 报「找不到 #include」 —— 被包含的 `.c` 文件既不在存档目录、也没有作为窗口打开
- 打印出来少了小数位 —— `%5.2f` 的宽度与精度不生效，数值按游戏格式输出
- 断点位置偏了一行 —— 生成脚本有编译器插入的行；断点已按行号映射，若仍偏移请提 issue

## 函数调用速查

世界与移动：`move(North)` `can_move(dir)` `get_pos_x()` `get_pos_y()` `get_world_size()`
`get_entity_type()` `get_ground_type()` `harvest()` `can_harvest()` `plant(Entities.Bush)`
`till()` `water()` `get_water()` `clear()` `use_item(Items.Water)`

输出：`printf("...", ...)` `print(...)` `quick_print(...)`

数学：`abs(x)` `min(a, b)` `max(a, b)` `random()`

方向常量：`North` `East` `South` `West`，可以直接和 `get_pos_x()` 的结果比较。

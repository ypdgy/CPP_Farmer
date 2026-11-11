[<- 返回 Java 说明](javafarmer_reference)

# 常见错误与函数调用

下面按类别列出会被**明确拒绝**的写法。报错里都带行号，并尽量给出替代写法。

## 类结构

- `类内成员请加 static` —— 类内字段和方法必须写 `static`，例如 `static int x = 0;`、`static int add(...)`
- `unsupported type 'xxx'` —— 只支持 `int float double boolean char String void`；自定义类型不能作为变量类型
- `expected 'class'` —— 文件顶层要有一个 `public class` 包住所有代码

## 语法

- `expected ';', got 'xxx'` —— 上一行漏了分号
- `'new' only supports arrays` —— 不能实例化对象，只能用 `new int[n]` 创建数组
- `unsupported member access '.xxx'` —— 数组只有 `.length` 这一个成员；对象字段没有对应机制
- `unsupported type 'xxx'` —— 类型名写错了，或用了不支持的类型

## 不支持的结构

- `switch (x) { case 1: ... }` —— 改成 `if` / `else if` 链
- `do { ... } while (x)` —— 改成 `while`
- `a ? b : c` —— 三元表达式暂不支持，改成 `if` / `else`
- 按位与、按位或、`a << 2` —— 不支持位运算，改用算术或布尔运算
- `new Foo()` —— 不能实例化对象；把需要的东西写成 `static` 字段/方法
- `int[][] a` —— 只支持一维数组
- `import "xxx.java"` —— 不支持跨文件导入；多文件请开多个 `.java` 窗口

## 运行期

- 按运行键没反应 —— 看日志里的 `java parse begin` / `source N chars`；`0 chars` 说明窗口文本没读到
- 断点位置偏了一行 —— 生成脚本有编译器插入的行；断点已按行号映射，若仍偏移请提 issue

## 函数调用速查

世界与移动：`move(North)` `can_move(dir)` `get_pos_x()` `get_pos_y()` `get_world_size()`
`get_entity_type()` `get_ground_type()` `harvest()` `can_harvest()` `plant(Entities.Bush)`
`till()` `water()` `get_water()` `clear()` `use_item(Items.Water)`

输出：`System.out.println(...)` `System.out.print(...)` `quick_print(...)`

数学：`Math.abs(x)` `Math.min(a, b)` `Math.max(a, b)` `Math.sqrt(x)` `Math.PI` `random()`

方向常量：`North` `East` `South` `West`，可以直接和 `get_pos_x()` 的结果比较。

游戏内置函数直接以全局形式调用（`move(...)`），**不要**写成 `javafarmer_api.move(...)` 或 `Farmer.move(...)`。

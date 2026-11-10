# 外部编辑器支持

`cppfarmer_api.hpp`（C++）与 `cfarmer_api.h`（C）是给 **IDE 看的**声明文件，不是游戏代码的一部分。

两份头文件内容同源：游戏的 52 个内置函数、4 个方向常量，以及 `Entities` / `Items` /
`Grounds` / `Hats` / `Leaderboards` / `Unlocks` 六个枚举的全部成员。区别只在语法——
C 版把 `std::string` 换成 `const char*`、把枚举写成 `typedef struct` + 常量实例，
所以 `Entities.Bush` 在 C 里也是合法表达式。

配好之后，VS Code 或 clangd 能对 `move(`、`Entities.`、`Items.` 给出补全和悬停说明；
写错参数时编辑器也会提示。

两个 C 专属的细节：

- `printf` 不是游戏内置函数（前端在编译期把它展开成字符串拼接），所以它由头文件**额外声明**，
  原型与 `<stdio.h>` 一致——只 include 这个头，或者同时 include `<stdio.h>`，都不会冲突或报警告。
- `print` / `quick_print` 接受任意参数，但**没有任何函数原型能跨语言表达这一点**：
  `void print();` 在 C89 里是「参数未指定」，C++ 和 MSVC 的 IntelliSense 却当成**零参数**
  （悬停会显示成 `void print(void)`）；`const char* first, ...` 又会拒绝第一个参数不是字符串的调用。
  所以头文件按 `__cplusplus` 分开：C++ 用 `template <typename... Args>`，C 用变参宏
  `#define print(...) ((void)0)`。两者都接受任意调用，IDE 不会再报「参数过多」。
  代价是 C 模式下悬停 `print` 显示的是宏而不是函数签名。
- C++ 头里 `#include <iostream>`，所以在 `.cpp` 里直接写 `std::cout << ... << std::endl;`
  不需要自己再 include。

> C 头文件只做声明，不定义任何函数；`-fsyntax-only` 不会产生链接需求。
> 想自己校验：`gcc -std=c11 -fsyntax-only -I <存档目录> main.c`

## 文件在哪

插件启动时会自动把对应语言的头文件复制到**当前存档目录**，和你的代码放在一起：

```
Saves\Save0\
├─ main.cpp                 你的代码
├─ cppfarmer_api.hpp        ← 自动复制（IDE 用，游戏不读）
└─ cfarmer_api.h            ← 同上，C 版
```

所以正常流程是：**装完模组、进一次游戏，再用编辑器打开存档目录**。
如果没看到它，检查 `BepInEx\config\cppfarmer.cfg` 里的 `Storage / ExportEditorHeader`
是否为 `true`；也可以手动从 `BepInEx\plugins\CppFarmer\editor\` 复制过去。

## 怎么用

在 `.cpp` / `.c` 顶部写一行即可：

```cpp
#include "cppfarmer_api.hpp"   // C++
```

```c
#include "cfarmer_api.h"       // C
```

引号形式会**优先搜索当前文件所在目录**，而头文件就在同一个存档目录里，所以 VS Code、
clangd、CLion 都**不需要任何额外配置**。

尖括号形式也可以（`#include <cppfarmer_api.hpp>`），但标准 C++ 的尖括号只搜索系统路径与
`includePath`，需要你在编辑器里把存档目录加进 include 路径。因此推荐用引号。

> 这行对游戏没有任何影响：前端会**专门过滤** `*_api.h` / `*_api.hpp`——
> 引号和尖括号都不生成 import、不校验文件、不报错。它只对编辑器生效。

## 当前存档文件格式

从启用 `Storage/UseCppExtension` 起，`.cpp` 窗口在存档里直接写成 **`main.cpp`**
（不再是 `main.cpp.py`）。旧存档里的 `main.cpp.py` 会在加载时自动改名，代码不会丢。

存档目录（Windows 默认）：

```
%USERPROFILE%\AppData\LocalLow\TheFarmerWasReplaced\TheFarmerWasReplaced\Saves\Save0
```

## 编辑器配置

通常不需要。只有两种情况需要额外设置：

- 头文件不在代码所在目录（例如你把 `.cpp` 放到别处编辑）
- 编辑器不搜索「当前文件目录」（少数 clangd 配置）

VS Code 的 `.vscode/c_cpp_properties.json`：

```json
{
  "configurations": [
    {
      "name": "C++ Farmer",
      "includePath": ["${workspaceFolder}"],
      "cppStandard": "c++20",
      "intelliSenseMode": "windows-gcc-x64"
    }
  ],
  "version": 4
}
```

clangd 的 `compile_flags.txt`（放在代码目录）：

```
-std=c++20
-I.
```

## 外部编辑的注意事项

- **改文件前退出游戏**：游戏每 30 秒自动存档，会覆盖磁盘内容
- 想边玩边改：在游戏选项里启用 `file watcher`（它会自动关掉 `autosave`），
  保存文件后游戏会同步进窗口
- 文件名必须与窗口名一致：窗口 `main.cpp` ↔ 文件 `main.cpp`
- 不要给 `.cpp` 文件加 `.py` 后缀，旧格式只在升级时被识别一次
- 标准 C++ 要求 `main` 返回 `int`，所以 IDE 会对 `void main()` 标红；这是 IDE 的规则，
  游戏内两种写法都能跑。想消掉波浪线就写 `int main()`

## 重新生成

游戏更新后内置函数可能变化，重新生成即可：

```bash
python scripts/gen_editor_api.py "<path to game>"
```

脚本从游戏的 `builtins.py` 解析函数与枚举，一次覆盖本目录的 `cppfarmer_api.hpp`
和 `cfarmer_api.h`。
`Unlocks` 里有重名成员（每个分组都有 `Unlock` / `Upgrade`），C++ 不允许重复成员，
脚本只保留首次出现的那个。

# 测试用例

这些 `.cpp` / `.c` 是**给 `build/cppfarmer.exe` 转译的输入**，不是能用 gcc / g++ 直接编译的
标准代码。它们只用本模组支持的子集（见 [docs/languages/](../docs/languages/README.md)）。

## 目录约定

| 路径 | 预期 | 说明 |
|---|---|---|
| `cpp/features.cpp` | 转译成功 | 覆盖大部分 C++ 语法特性的综合用例 |
| `cpp/ternary.cpp` | 转译成功 | `?:` 展开成 `if/else` |
| `cpp/unicode_space.cpp` | 转译成功 | BOM / NBSP / 全角空格等不可见空白应被静默忽略 |
| `cpp/entry_top.cpp` | 转译成功，且**生成结果里没有 `def`** | `main` 必须展开成顶层代码 |
| `cpp/inc/` | 转译成功 | 多文件：`main.cpp` 通过 `#include "hello.cpp"` 导入 |
| `cpp/err/*.cpp` | **必须报错**（退出码非 0） | 全角标点、非法字符、位运算、缺分号、无效果语句等 |
| `c/basic.c` | 转译成功 | C 语法综合：`struct` / `typedef` / 数组 / 枚举 |
| `c/printf.c` | 转译成功 | 各受支持的占位符展开 |
| `c/entry.c` | 转译成功，且**生成结果里没有 `def`** | `int main(void)` 也要提升 |
| `c/inc/` | 转译成功 | 多文件：`#include "helper.c"` 变成模块导入 |
| `c/err/*.c` | **必须报错** | `printf` 占位符不匹配、`%x`、`switch`、以及 C 里写 `cout` |
| `java/entry.java` | 转译成功，且**生成结果里没有 `def`** | `public static void main` 必须提升为顶层代码 |
| `java/basic.java` | 转译成功 | 静态字段/方法、数组、两种循环、字符串拼接、`Math`、布尔综合用例 |
| `java/err/*.java` | **必须报错** | 无 `static` 成员、`new` 对象、`switch`、位运算、三元表达式 |

> 曾有两个正向用例误放在 `err/` 里（三元表达式、Unicode 空白），现已移到 `cpp/`。
> 如果新增用例，请先确认它到底应该成功还是失败，再决定放哪。

## 跑一遍

```bash
# 先构建
bash scripts/build.sh

# 正向用例：全部应为 rc=0
for f in tests/cpp/features.cpp tests/cpp/ternary.cpp tests/cpp/unicode_space.cpp \
         tests/cpp/entry_top.cpp tests/cpp/inc/main.cpp \
         tests/c/entry.c \
         examples/cpp/farm.cpp examples/cpp/maze.cpp examples/c/farm.c; do
    ./build/cppfarmer.exe "$f" --stdout >/dev/null 2>&1
    rc=$?                      # 先存 rc，再执行 printf / basename
    printf '%-34s rc=%s\n' "$f" "$rc"
done

# 负向用例：全部应为 rc!=0，且错误信息里带源码行号
for f in tests/cpp/err/*.cpp tests/c/err/*.c; do
    msg=$(./build/cppfarmer.exe "$f" --check 2>&1)
    rc=$?
    printf '%-34s rc=%s  %s\n' "$f" "$rc" "$(echo "$msg" | head -1)"
done

# Java：C++ CLI 只认 cpp/c，Java 用例直接走 DLL 的 cf_transpile_ex 回归
python scripts/test_java.py
```

## 开局可用性（重要）

`def` 在目标语言里需要科技树「函数定义」解锁，新存档没有。所以入口相关用例要额外验证一次
**模拟开局**的编译：

```bash
# 只有开局默认拥有的解锁项，没有 functions
./build/cppfarmer.exe tests/c/entry.c --unlocks "variables,operators,loops,lists" --check
./build/cppfarmer.exe tests/cpp/entry_top.cpp --unlocks "variables,operators,loops,lists" --check
# Java 入口同样要在无 functions 时能编译（scripts/test_java.py 内已覆盖）
```

两条（cpp/c）都应成功。若哪天入口又变回 `def main()`，这两条会立刻失败。

注意 Bash 里 `printf ... "$(basename "$f")" "$?"` 会让命令替换把 `$?` 覆盖成 0，
**必须先把退出码存进变量**（上面的 `rc=$?`）。

`--stdout` 打印生成脚本，`--check` 只做语法/语义校验，`--line-map` 打印
「输出行 → 源行」映射表。命令行完整参数见 [docs/BUILD.md](../docs/BUILD.md)。

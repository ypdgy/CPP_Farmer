# 测试用例

这些 `.cpp` 是**给 `build/cppfarmer.exe` 转译的输入**，不是能用 g++ 直接编译的标准 C++。
它们只用本模组支持的 C++ 子集（见 [docs/LANGUAGE.md](../docs/LANGUAGE.md)）。

## 目录约定

| 路径 | 预期 | 说明 |
|---|---|---|
| `features.cpp` | 转译成功 | 覆盖大部分语法特性的综合用例 |
| `ternary.cpp` | 转译成功 | `?:` 展开成 `if/else` |
| `unicode_space.cpp` | 转译成功 | BOM / NBSP / 全角空格等不可见空白应被静默忽略 |
| `inc/` | 转译成功 | 多文件：`main.cpp` 通过 `#include "hello.cpp"` 导入 |
| `err/*.cpp` | **必须报错**（退出码非 0） | 错误诊断：全角标点、非法字符、位运算、缺分号、无效果语句、不支持的写法等 |

> 曾有两个正向用例误放在 `err/` 里（三元表达式、Unicode 空白），现已移到 `tests/`。
> 如果新增用例，请先确认它到底应该成功还是失败，再决定放哪。

## 跑一遍

```bash
# 先构建
bash scripts/build.sh

# 正向用例：全部应为 rc=0
for f in tests/features.cpp tests/ternary.cpp tests/unicode_space.cpp \
         tests/inc/main.cpp examples/farm.cpp examples/maze.cpp; do
    ./build/cppfarmer.exe "$f" --stdout >/dev/null 2>&1
    rc=$?                      # 先存 rc，再执行 printf / basename
    printf '%-30s rc=%s\n' "$f" "$rc"
done

# 负向用例：全部应为 rc!=0，且错误信息里带源码行号
for f in tests/err/*.cpp; do
    msg=$(./build/cppfarmer.exe "$f" --check 2>&1)
    rc=$?
    printf '%-30s rc=%s  %s\n' "$f" "$rc" "$(echo "$msg" | head -1)"
done
```

注意 Bash 里 `printf ... "$(basename "$f")" "$?"` 会让命令替换把 `$?` 覆盖成 0，
**必须先把退出码存进变量**（上面的 `rc=$?`）。

`--stdout` 打印生成脚本，`--check` 只做语法/语义校验，`--line-map` 打印
「输出行 → 源行」映射表。命令行完整参数见 [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)。

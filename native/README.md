# native/ — 原生前端

一个 DLL 承载多门语言。插件通过 `cf_*` 接口按语言 id 调用，看不到任何前端内部结构。

```
native/
├─ include/cf_api.h      插件与前端之间的唯一契约（CfLanguage、cf_compile_fn）
├─ src/
│  ├─ registry.cpp       语言分发表 + cf_* 实现 + 旧 cppfarmer_* 别名
│  └─ cf_util.{h,cpp}    字符串复制、CSV 拆分
└─ frontends/
   └─ cpp/               第一门语言：C++ 子集
      ├─ cpp_frontend.{h,cpp}   描述符 + 编译入口
      ├─ lexer.hpp parser.hpp codegen.hpp ast.hpp
      └─ cli.cpp watch.cpp      本地命令行工具（不进发布包）
```

## 构建

```bash
bash scripts/build.sh          # build/cppfarmer.exe, cppfarmer-watch.exe, cppfarmer_native.dll
```

包含目录固定为 `native/include`、`native/src`、`native/frontends/<id>`。
新增语言：加 `frontends/<id>/`，在 `registry.cpp` 的 `kLanguages` 加一行，
再更新 `scripts/build.sh` / `build.bat` 的源文件列表。

## 约定

- **导出符号**：只有 `extern "C"`，全部经 `CF_EXPORT`。语言 id 是稳定小写串。
- **错误格式**：`"1:<line>:<col>: message"`，插件据此在编辑器里定位。
- **行映射**：每个输出行一个整数（该行来自的源码行号，编译器生成的行填 0）。
- **内存**：返回给插件的字符串用 `cf_dup` 分配，由插件 `cf_free`。前端不得持有。
- **异常**：必须在编译入口内捕获，转成错误字符串；不要跨 C ABI 抛出。
- **兼容**：`cppfarmer_transpile*` 别名只为「新 DLL + 旧插件」保留，新代码只用 `cf_*`。

## 与插件的关系

插件只认识 `native/include/cf_api.h` 里的四个函数加语言枚举；它自己的
`LanguageProfile` 决定哪些窗口送到哪个 id。两边靠 id 字符串对齐，
启动时会比对 `cf_language_*` 与已注册 profile，缺失的一方会在日志里告警。

详细接入步骤见 [../docs/ADDING_A_LANGUAGE.md](../docs/ADDING_A_LANGUAGE.md)。

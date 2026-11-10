# 从源码构建

> [!IMPORTANT]
> 构建需要**你本机已合法安装的游戏**提供编译期引用。
> 本仓库**不提供**游戏程序集、反编译源码或任何游戏文件；只包含路径配置方式。
> 详见主 README 的「声明」。

## 前置

| 工具 | 版本 | 用途 |
|---|---|---|
| g++ / MinGW-w64 | 支持 C++20 | 编译原生编译器（`cppfarmer.exe`、`cppfarmer-watch.exe`、`cppfarmer_native.dll`） |
| .NET SDK | 6+ | 编译 C# 插件（**不需要 MSVC**） |
| 游戏 | 已安装 | 提供 `Core.dll`、`Utils.dll`、`UnityEngine.*.dll` 等编译期引用 |
| BepInEx | 5.4.x x64 / Mono | 提供 `BepInEx.dll`、`0Harmony.dll`；需先解压到游戏目录并启动过一次游戏 |

目标框架固定为 `netstandard2.1`：游戏使用 Unity 6，其模块程序集引用
`netstandard 2.1.0.0`；设为 `netstandard2.0` 会报 `CS1705`。

## 指定游戏目录

仓库里**没有写死任何机器路径**。任选一种：

### 方式 1：命令行参数（推荐）

```bat
dotnet build plugin\CppFarmer.csproj -c Release -p:GameDir="<path to game>"
plugin\build_and_install.bat "<path to game>"
```

### 方式 2：环境变量

```bash
export CPPFARMER_GAME="<path to game>"
bash scripts/build.sh
dotnet build plugin/CppFarmer.csproj -c Release
```

```powershell
$env:CPPFARMER_GAME = "<path to game>"
plugin\build_and_install.bat
```

### 方式 3：本地配置文件（git-忽略）

创建 `plugin/GameDir.local.props`：

```xml
<Project>
  <PropertyGroup>
    <GameDir><path to game></GameDir>
  </PropertyGroup>
</Project>
```

或创建 `plugin/GameDir.local.txt`，内容为一行游戏目录路径（供 `build_and_install.bat` 读取）。

这两个文件都在 `.gitignore` 中，不会进仓库。

## 构建命令

```bash
# 原生侧：一次生成三个产物到 build/
bash scripts/build.sh          # Windows Git Bash / MSYS2
# 或
scripts\build.bat

# 托管侧
dotnet build plugin/CppFarmer.csproj -c Release -p:GameDir="..."

# 一键：编译 + 安装到 BepInEx/plugins/CppFarmer/
plugin\build_and_install.bat "..."
```

`build.sh` 的编译器可用 `CXX` 覆盖：

```bash
CXX=clang++ bash scripts/build.sh
```

原生 DLL 使用 `-shared -static` 全静态链接，只依赖系统 `KERNEL32` / UCRT，
不需要随附 `libstdc++` 或 `libwinpthread`。

## 验证

```bash
# 单文件转译
build/cppfarmer.exe examples/cpp/farm.cpp --stdout
build/cppfarmer.exe examples/cpp/maze.cpp --check

# 特性清单与行映射
build/cppfarmer.exe tests/cpp/features.cpp --print-features
build/cppfarmer.exe tests/cpp/features.cpp --line-map

# 批量转译 + 语法近似校验
mkdir -p /tmp/cppfarmer && ./build/cppfarmer-watch.exe examples /tmp/cppfarmer --once
python -m py_compile /tmp/cppfarmer/*.py

# 正向用例应 rc=0，tests/err/ 负向用例应 rc!=0
for f in tests/cpp/features.cpp tests/cpp/inc/main.cpp; do
    build/cppfarmer.exe "$f" --stdout >/dev/null; echo "$f rc=$?"
done
for f in tests/cpp/err/*.cpp; do
    build/cppfarmer.exe "$f" --check >/dev/null 2>&1; rc=$?; echo "$f rc=$rc"
done
```

用例目录约定与完整回归脚本见 [tests/README.md](../tests/README.md)。
`tests/cpp/err/` 是故意写错、必须报错的用例（全角标点、非法字符、位运算、缺分号、
无效果语句等）；`tests/cpp/unicode_space.cpp` 等正向用例放在 `tests/cpp/` 下。

## 产物

| 文件 | 输出位置 |
|---|---|
| `cppfarmer.exe` | `build/` |
| `cppfarmer-watch.exe` | `build/` |
| `cppfarmer_native.dll` | `build/` |
| `CppFarmer.dll` | `plugin/bin/Release/` |

发布包在 `dist/CppFarmer-mod.zip`，内含 `install.bat` / `uninstall.bat` / `README.md`
以及两个 DLL。可直接上传到 GitHub Releases。

## 常见构建错误

| 现象 | 原因 |
|---|---|
| `GameDir is not set` | 未指定游戏目录，见上文三种方式 |
| `CS1705: netstandard 2.1.0.0 高于 2.0.0.0` | 目标框架被改成了 `netstandard2.0`，改回 `2.1` |
| `CS0012: 类型 MonoBehaviour 在未引用的程序集中` | 缺少 `UnityEngine.dll` 门面引用（不只是 `CoreModule`） |
| 一堆「找不到类型」 | `GameDir` 指向错误目录，或 BepInEx 未安装 |
| 原生 DLL 加载失败 | 用 `objdump -p` 检查是否意外依赖 `libstdc++-6.dll` / `libwinpthread-1.dll`；应加 `-static` |

## 文件名注意事项

原生库必须叫 `cppfarmer_native.dll`。Windows 文件名不区分大小写，
如果叫 `cppfarmer.dll`，安装时会与托管程序集 `CppFarmer.dll` 视为同一文件并互相覆盖，
结果是插件静默不加载。

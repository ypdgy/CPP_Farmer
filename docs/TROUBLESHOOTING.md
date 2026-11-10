# 排障

> 所有运行时诊断文件都写在 `BepInEx/plugins/CppFarmer/` 下，不会写进游戏存档目录。
>
> [!CAUTION]
> 涉及代码文件丢失、保存失败、存档异常的问题，请先**退出游戏**并阅读
> [存档安全](SAVE_SAFETY.md)。本模组可能导致存档损坏或丢失，恢复前不要继续保存。

## 快速回退

`BepInEx/config/cppfarmer.cfg`：

```ini
[General]
Enabled = false
```

重启游戏即可**完全关闭所有补丁**，游戏回到原生行为。这是判断「问题是否由模组引起」的最快方式。
确认后改回 `true` 即可，不需要重装。

或直接运行 `uninstall.bat`（只删除插件目录，不动 BepInEx、游戏文件与存档；
若想连 BepInEx 一起移除，需手动删除 `winhttp.dll`、`doorstop_config.ini`、
`.doorstop_version` 和 `BepInEx\`，见包内 README 的卸载一节）。

---

## 插件没有加载

`BepInEx/LogOutput.log` 里应出现：

```
[Info   : C++ Farmer] native compiler: cppfarmer 0.2
[Info   : C++ Farmer] harmony patches applied
[Info   : C++ Farmer] C++ Farmer button added to the toolbar
```

| 现象 | 处理 |
|---|---|
| 完全没有 `C++ Farmer` | `CppFarmer.dll` 不在 `BepInEx/plugins/CppFarmer/`；或 BepInEx 本身没加载 |
| 根本没有 `LogOutput.log` | BepInEx 未启动：检查游戏根目录的 `winhttp.dll`、`doorstop_config.ini`、`BepInEx\core\BepInEx.Preloader.dll`。用发布包的 `install.bat` 会自动装好这三项；但它**不会覆盖**已有的同名文件，若你之前手动改过配置需自行核对 |
| 有 `native compiler` 但没有 `harmony patches` | 补丁初始化抛异常，查看日志中的错误行 |
| 安装目录里只剩一个 3MB 的 DLL | 原生库与托管程序集重名导致覆盖。原生库必须叫 `cppfarmer_native.dll` |
| 日志提示 `not a valid .NET assembly` | BepInEx 尝试把原生 DLL 当托管插件加载，这是**正常提示**，会被跳过 |

> BepInEx 默认日志级别是 Info，`LogDebug` 不会显示。诊断信息因此同时写入下面的文件。

## 编译错误

带 `C++ L<行号>:` / `C L<行号>:` 前缀的错误来自本编译器，行号对应你的源码：

| 错误 | 含义 |
|---|---|
| `这个写法需要先解锁「xxx」` | 该构造对应的科技尚未解锁；`main` 不需要「函数定义」，自定义函数 / `struct` 才需要 |
| `全角字符「；」…请改用半角「;」` | 中文输入法标点，切换输入法即可 |
| `无法识别的字符「变」…` | 标识符请使用英文 |
| `bitwise operators are not available` | 目标语言没有位运算，改用算术 |
| `printf 的格式化占位符不受支持` | 改用 `std::cout << ... << std::endl;` |
| `'using' 后面缺少结尾的 ';'` | `using namespace std` 漏写分号；旧版会吞掉后面整个文件，现在直接报错 |
| `'printf' 是函数，要写成 printf(...)` | 只写了函数/变量名字的语句没有任何效果，补上调用或赋值 |
| `不支持 switch / sizeof / new / template / std::set ...` | 这些写法没有对应实现，报错里会给出替代方案 |
| `找不到 #include「x.cpp」` | 该文件不在存档目录，且未在编辑器中打开 |
| `expected a declaration, got 'xxx'` | 顶层有未写完的标识符或语句 |

游戏内的 **C++** 按钮 → 说明页 → 「常见错误与函数调用」也列了这些报错和改法。

**不带 `C++ L` 前缀的英文小写错误**来自游戏自己的解析器，说明生成的脚本被拒绝。
把 `last_generated.py` 发出来即可定位。

## 按 F5 后游戏无响应

先看日志中的 `cpp parse ...` 步骤行，它们标记了编译阶段：

| 最后一行 | 卡在 |
|---|---|
| 没有 `cpp parse begin` | 没进补丁，问题在别处 |
| `cpp parse begin` | 读取窗口文本；若下一行是 `text unavailable`，会退回读磁盘文件或编译成空 |
| `cpp parse: source N chars` | `#include` 处理（找不到被引用的窗口） |
| `cpp parse: includes prepared` | 原生转译 / 编译缓存 |
| `cpp parse: transpiled N chars` | 分词（游戏词法器校验） |
| `cpp parse: tokenizing` | 模块名改写、token 流构造 |
| `cpp parse: calling game parser` | 游戏解析器 / 执行器 |

`cpp parse rejected:` 后面跟的是前端主动拒绝的报错（已转成游戏内错误气泡），
`C++ front-end crashed, falling back:` 则是插件异常，此时会退回原版解析器，
通常需要把完整日志发出来。

历史故障：插件曾被主线程与模拟线程同时进入，共享的普通 `Dictionary` 被并发写坏，
之后 `TryGetValue` 无限循环（无异常、CPU 打满）。现已全部改为 `ConcurrentDictionary`，
并把科技树词表刷新移到主线程。若仍遇到，请附上完整日志。

## 代码文件丢失 / 存档异常

**先退出游戏再动存档目录。** 游戏的自动存档每 30 秒写回一次，并会删除
「不属于当前打开窗口」的 `.py` 文件；游戏运行时放回去的文件可能马上又被清掉。

| 现象 | 处理 |
|---|---|
| 手工新增的 `xxx.cpp` 不见了 | 运行时被 `autosave` 清理；退出游戏后重新复制。C++ 文件直接叫 `<窗口名>.cpp` |
| 只建了 `xxx.cpp` 却没出现窗口 | 确认 `Storage/UseCppExtension = true`；旧版需要 `xxx.cpp.py`，新版读 `.cpp` |
| 存档里同时有 `xxx.cpp` 和 `xxx.cpp.py` | 旧格式残留；退出游戏后删掉 `.py` 那份，以 `.cpp` 为准 |
| 窗口名对不上 / 出现两个窗口 | 改名时连同 `.py` 后缀一起改 |
| 世界状态被脚本改坏 | 从自建备份或 `Backup/Save0_backupNNN` 恢复；这是脚本真实执行的后果 |
| 想彻底回退 | `uninstall.bat` 只删插件目录；再按 [存档安全](SAVE_SAFETY.md) 恢复存档 |

完整说明（备份命令、恢复步骤、历史故障、免责声明）见
[docs/SAVE_SAFETY.md](SAVE_SAFETY.md)。

## 保存失败（弹「写入存档文件失败」）

游戏保存时会枚举打开的代码窗口并把文件名加进字典。任何让**同一窗口出现两次**的改动
都会触发重复键异常，显示成这句笼统的提示。

- 确认没有两个同名代码窗口
- 确认没有外部编辑器 / 记事本开着 `Saves/Save0/*.py`
- `autosave` 与 `file watcher` 互斥，游戏会自动关掉其中一个；不要同时指望两者
- `WriteTextAtomically` 补丁会把真实异常写进 `LogOutput.log`，搜索 `save write failed`
- 早期版本 `#include` 曾注册窗口别名导致本错误，已改为只改写 token，升级即可

> 顺带一提：**Steam 验证游戏完整性不会删除 `BepInEx/`**，也不会修复文档页显示问题。
> 文档是运行时渲染的文本，不是磁盘文件损坏。

## 补全与着色

| 现象 | 处理 |
|---|---|
| 提示里还有 Python 关键字 / `__name__` | 应已被白名单过滤；确认 `General/Enabled = true` 且是最新版 |
| 在字符串里打字还弹提示 | 检查 `UI/NoCompleteInStrings = true`；仅对 `.cpp` 窗口生效 |
| 补全/悬停没有说明 | 游戏只给内建词配了文案；C++ 关键字与 `push_back`、`std::cout` 等由本模组补，检查 `UI/WordTooltips = true` |
| `#include` 仍是灰色注释 | 着色补丁挂在主线程更新路径，确认 DLL 为最新版；与 `UI/HelpButton` 配置无关 |
| `.py` 窗口被影响 | 不应发生，所有 UI 补丁都按 `.cpp` 文件名过滤。请反馈 |
| 点别的窗口后内容滚回开头 | 已修：失焦时跳过「滚到第 0 个字符」的调用；只对 `.cpp` 生效，若 `.py` 也这样请反馈 |
| 输入 `void main() {` 后回车不缩进 | 检查 `UI/SmartIndent = true`；游戏原生只认 Python 的 `:` |
| 输入 `}` 不自动回退一级 | 目前不做，和原版一样用 Shift+Tab |

## 断点

`bp_debug.txt` 记录每次重映射的完整链路：窗口名、断点行号、映射到的输出行与字符偏移。

| 现象 | 处理 |
|---|---|
| 断点完全不生效 | 看 `bp_debug.txt` 是否有内容；`unit=none` 说明该窗口还没编译过 |
| 停在附近行而非精确行 | 一个 C++ 语句可能展开成多行脚本，取首个匹配；空行/花括号会吸附到下一条语句 |
| 跨文件不命中 | 确认被包含文件的窗口是打开的，且 `#include` 走的是 import（生成 `from x import *`） |

单步调试（F11）走执行器自己的暂停点，不受映射影响。

## 执行高亮

游戏画「当前执行位置」用的是**生成脚本**里的字符下标，而编辑器显示 C++ 源码。
`ExecutionBlink.cs` 在绘制入口把这两个坐标换算回源码行；`CodeSupport` 的行映射为 0 的
helper/前导行会向后吸附到最近的有映射行。

| 现象 | 处理 |
|---|---|
| 光块落在文件开头的注释里 | 旧版没有坐标换算；升级到最新版 |
| 光块只高亮整行而不是某个词 | 生成脚本里没有一一对应的 token 时退化为整行，属预期行为 |
| `pop_back` 里高亮了 `pop` 前缀 | 已改为整词匹配；若仍出现请附上行号 |
| 跨文件执行时光块不跳过去 | 确认 `#include` 生成的是 `from x import *`，且被包含窗口已打开 |

坐标换算只改绘制用的实参，**不改节点上的 `wordStart`**（断点比较用的是生成坐标）。

## 说明页 / 首页链接 / 科技树提示

- 右上角 **C++** 按钮：若被游戏 UI 遮挡，可用配置微调；克隆失败会退回 IMGUI 按钮
- 文档首页链接：只在 `docs/home.md` 上生效，幂等，不写磁盘文件
- 科技树提示：悬停节点时追加，无对应 C++ 能力的科技（纯物品/帽子）不显示
- 说明页正文是插件目录下的两个普通 Markdown：`cppfarmer_reference.md`、`cppfarmer_errors.md`，随包发布。改文案直接编辑这两个文件并重启游戏，**不用重新编译**；删掉它们会显示「说明页缺失」提示
- 说明页内指向 `cppfarmer_errors` 的「常见错误与函数调用」链接走游戏的文档跳转，`<-` 与后退按钮都能用（这是游戏虚拟文档 id，不是磁盘文件）

## 配置项一览

`BepInEx/config/cppfarmer.cfg`

| 键 | 默认 | 说明 |
|---|---|---|
| `General / Enabled` | true | 总开关 |
| `General / EnforceUnlocks` | true | 编译期科技树门禁；读不到词表时自动放行 |
| `General / VerboseLog` | false | 转译细节日志 |
| `UI / HelpButton` | true | 右上角 C++ 按钮 |
| `UI / HelpButtonRightMargin` | 10 | 仅影响 IMGUI 兜底按钮 |
| `UI / HelpButtonTopMargin` | 8 | 同上 |
| `UI / HomeLink` | true | 文档首页的 C++ 说明链接 |
| `UI / UnlockTooltips` | true | 科技树悬停提示 |
| `UI / NoCompleteInStrings` | true | 字符串/字符字面量/注释内不弹补全 |
| `UI / SmartIndent` | true | `.cpp` 窗口里 `{` 结尾的行回车后多缩进一级 |
| `UI / WordTooltips` | true | 补全列表与编辑器悬停时的 C++ 词条说明 |
| `Storage / UseCppExtension` | true | `.cpp` / `.c` 窗口存成 `<名字>.cpp` / `<名字>.c`；false 退回游戏的 `.py` 命名（旧文件会在保存时改回） |
| `Storage / ExportEditorHeader` | true | 把 `cppfarmer_api.hpp` 放进当前存档目录，供外部编辑器补全 |

## 诊断文件

| 文件 | 内容 |
|---|---|
| `bp_debug.txt` | 断点映射链路（每次覆盖） |
| `last_generated.py` | 生成脚本被游戏词法器拒绝时的转储 |
| `cpp_unlocks.txt` | 观测到的科技树词表，游戏改版后用于校准门禁 |
| `cppfarmer_reference.md` | 说明页正文（随包发布，可编辑） |
| `cppfarmer_errors.md` | 「常见错误」页正文（随包发布，可编辑） |

这些文件都可以安全删除，插件会按需重新生成。

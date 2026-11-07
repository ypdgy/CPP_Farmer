# dist/ — 发布包

`CppFarmer-mod.zip` 是上传到 GitHub Releases 的**构建产物**，不是源文件。

## 目录

| 路径 | 说明 |
|---|---|
| `CppFarmer-mod/` | 包内容的**版本化源**：说明、安装/卸载脚本、第三方声明、许可证，以及捆绑的 BepInEx 载荷 |
| `CppFarmer-mod/README.md` | 玩家读的说明（含存档风险警告与 BepInEx 声明） |
| `CppFarmer-mod/THIRD_PARTY_NOTICES.md` | BepInEx 官方仓库地址、版本、LGPL-2.1 许可、SHA-256、上游库清单 |
| `CppFarmer-mod/LICENSES/` | BepInEx LGPL-2.1 许可证全文 + 官方 changelog |
| `CppFarmer-mod/winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`BepInEx/core/` | BepInEx 5.4.23.6 官方未修改文件 |
| `CppFarmer-mod.zip` | 发布包；以上全部内容 + 两个本项目编译出的 DLL |

只有 **`CppFarmer.dll` 与 `cppfarmer_native.dll`** 不在版本控制里（根目录
`.gitignore` 的 `*.dll`）：它们由 `pack.py` 从构建输出注入。BepInEx 的二进制
通过 `.gitignore` 末尾的例外规则**刻意纳入版本控制**，这样从全新 clone
也能一键重建完整 Release。例外只作用于 `dist/CppFarmer-mod/`，其它目录的
`*.dll` / `BepInEx/` 仍被忽略。

> `install.bat` / `uninstall.bat` 保持**纯 ASCII + CRLF**：Windows 命令行默认
> 代码页不保证正确显示 UTF-8 中文，中文说明一律放在 `README.md` 里。

## 重新打包

```bash
# 1. 编译原生库与插件（见 docs/BUILD.md）
bash scripts/build.sh
dotnet build plugin/CppFarmer.csproj -c Release -p:GameDir="<path to game>"

# 2. 生成 zip
python scripts/pack.py
```

`pack.py` 会：

- 遍历 `dist/CppFarmer-mod/` 的全部文件（含 BepInEx 载荷），条目名使用正斜杠
- 跳过源树中 `BepInEx/plugins/CppFarmer/` 下的文件，改从
  `build/cppfarmer_native.dll` 与 `plugin/bin/Release/CppFarmer.dll` 注入，
  避免把过期 DLL 带进 Release
- 预检 `install.bat`、`THIRD_PARTY_NOTICES.md`、`LICENSES/BepInEx-LGPL-2.1.txt`、
  `winhttp.dll`、`BepInEx/core/BepInEx.dll` 等关键文件是否齐全

> Windows 下**不要**用 `Compress-Archive` 代替：它会写入反斜杠条目名，
> 某些解压工具会据此创建带反斜杠的字面文件名。

## 发布前检查

- 包内约 29 个文件：本项目 2 个 DLL + BepInEx 官方 21 个文件（逐字节一致）
  + 说明/许可证/脚本 6 个
- 用 `sha256sum` 或官方发行页核对 `winhttp.dll`、`BepInEx/core/BepInEx.dll`；
  哈希清单见 `THIRD_PARTY_NOTICES.md`
- `README.md` 顶部有中英双语的**存档风险警告**、备份路径与免责声明
- `install.bat` / `uninstall.bat` 运行后**交互询问游戏目录**（可拖拽粘贴，`q` 退出；
  传参数可跳过询问，无其它隐藏配置），安装前打印备份提示；检测到已有 BepInEx 时跳过
- 包内**不含**任何游戏程序集、反编译源码、IL/反汇编、游戏资源或逆向中间文件；
  捆绑的 BepInEx 是第三方开源框架，来源与许可证已单独声明
- 「可能损坏存档」的完整说明见 [docs/SAVE_SAFETY.md](../docs/SAVE_SAFETY.md)

发布 Release 时，请把存档风险警告与「含 BepInEx 5.4.23.6（LGPL-2.1）」也写进
Release 说明正文——很多玩家只看 Release 页面，不会打开 zip 里的 README。

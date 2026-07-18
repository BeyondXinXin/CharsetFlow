# CharsetFlow

CharsetFlow 是一个面向 Windows 11 的本地批量文本编码转换器，目标框架为 `net10.0-windows`。

## 功能

- 智能识别、扩展名过滤、不过滤三种文件纳入方式
- 文件、文件夹和拖放添加；文件夹递归扫描与排除规则
- BOM、无 BOM Unicode、GB18030、Big5、SHIFT-JIS、EUC、Windows 和 ISO 系列编码
- UTF-8 / UTF-8 BOM / GB18030 等批量互转
- CRLF / LF 换行符批量转换
- 严格解码、严格编码和 Unicode 往返校验，避免静默丢失字符
- 原位转换或输出到指定目录；原位转换可自动创建 `.bak` 备份
- 前 100 KB 异步探测、内容预览、手动指定源编码和中途取消
- 配置自动保存到 `%LocalAppData%\CharsetFlow\settings.json`
- 与 SmartCharsetConverter 兼容的基础命令行参数

探测顺序为：BOM → 严格 UTF 校验 → UTF.Unknown（uchardet 系复合探测）→ 严格往返校验 → 二进制排除。

## 构建与发布

```bat
dotnet build CharsetFlow.csproj -c Debug
dotnet publish CharsetFlow.csproj -c Release -r win-x64 --self-contained true
```

发布配置默认为 `win-x64`、`SelfContained`、单文件和 ReadyToRun。

## 命令行

```bat
CharsetFlow --help
CharsetFlow --help charset
CharsetFlow --input D:\input --target_charset "UTF-8 BOM" --target_linebreak LF --output_dir D:\output
CharsetFlow --input D:\a.txt --target_charset GB18030 --output_origin
```

不带 `--` 参数时启动图形界面；将文件或文件夹拖到程序图标上也会在图形界面中加载。

## 探测参考

- [SmartCharsetConverter](https://github.com/tomwillow/SmartCharsetConverter)
- [UTF.Unknown](https://github.com/CharsetDetector/UTF-unknown)

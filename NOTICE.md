# 来源与许可说明

本项目为 Desk Top Racer 的第三方维护助手，界面、部署、统计、库存保护与发布整理署名 by Awan。既有代码与依赖遵循各自的版权与许可，本仓库发布不表示取得游戏或第三方内容的全部版权。

新增原创维护代码使用 LICENSE 中的 MIT 条款。该条款仅覆盖 Awan 有权授权的新增代码；既有代码、依赖、游戏内容与第三方数据遵循各自的版权与许可。

| 组件 / 内容 | 来源 |
| --- | --- |
| GDRE 工具 | gdsdecomp v2.7.0，用于生成游戏接口文件；https://github.com/GDRETools/gdsdecomp |
| .NET 10 运行时 | 随发布包自包含分发；https://dotnet.microsoft.com/ |
| 游戏接口脚本 | `Assets/game-plugin/helper_bridge.gd`，由本项目维护 |
| 游戏文件与资源 | Desk Top Racer 游戏内容，权利归对应权利人 |
| 市场参考数据 | Steam 社区市场公开报价接口；https://steamcommunity.com/market/ |
| 发电与 Star 图 | 使用内置 imagegen 制作，说明见 docs/star-card-generation.md |

源码仓库不分发游戏本体、生成的接口程序集、个人配置、库存与开箱记录或完整游戏日志。

## v0.8.19 运行库

本版 EXE 通过 Frida 按 PID 连接游戏；上表中的 GDRE 与桥接脚本用于仓库保留的历史实现。

| 组件 | 来源与许可 |
| --- | --- |
| Node.js 24.19.0 | 助手缓存中的 worker 运行环境，遵循 MIT 与随附第三方许可；https://nodejs.org/ |
| Frida 17.24.3 | 进程内动态适配，遵循 LGPL-2.0 与 WxWindows-exception-3.1；https://github.com/frida/frida |

Node.js、Frida 及 JavaScript 依赖的许可文件随内置运行库保留，首次连接时解压至 `%LOCALAPPDATA%\DTRControlAssistant\frida`。Frida 源码和构建说明见 https://github.com/frida/frida/tree/17.24.3 。

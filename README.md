<p align="center">
  <img src="docs/afdian-support.jpg" alt="扫码为阿丸好饿发电" width="280">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 DTR 助手点个 Star" width="280"></a>
</p>

# DTR 助手 · by Awan

Desk Top Racer 的 Windows 桌面助手。助手在本机通过游戏内接口检查并领取补给箱、整理仓库、按品质策略处理物品，并显示道具图片、Steam 市场参考价和掉落统计。

**[下载 v0.7.18](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/tag/v0.7.18)** · [部署说明](docs/DEPLOYMENT-REVIEW.md) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## 功能

| 页面 | 功能 |
| --- | --- |
| 总览 | 游戏连接状态；一键开启持续检查补给箱与仓库，也可立即执行单项检查 |
| 仓库 | 读取储物箱道具，图片展示、重复合并、名称搜索、品质与类型筛选、按参考价排序 |
| 统计 | 补给箱开箱记录、24 小时与累计数量、品质分布、可交易道具记录 |
| 品质策略 | 收藏、拆解、出售的品质规则；仓库使用率达到阈值后开始整理 |
| 设置 | 检查周期、共用间隔、领取后立即整理、防掉线开关与检测间隔 |
| 环境 | 一键部署游戏接口，保存原版备份，可校验并恢复 |
| 运行日志 | 逐条带时间的运行记录与保留时间设置 |

## 工作原理

助手把接口程序集部署到 Steam 启动的游戏 EXE，并在游戏目录保存原版备份；游戏运行期间无法替换 EXE，退出后助手完成接入。接口启动后助手按当前游戏进程 PID 探测 `127.0.0.1:19110–19120`，连接后通过本机回环地址调用游戏内部入口执行动作，界面负责显示状态与保存设置。

价格查询使用 Steam 市场参考报价，并按配置的货币换算；参考估值不等于可提现金额，助手不执行市场挂单或交易。

## 安装与使用

环境为 Windows 10/11 64 位、Steam 版 Desk Top Racer。

1. 在 Steam 安装 Desk Top Racer，并至少启动一次。
2. 从 Releases 下载 `DesktopRacerHelper-v0.7.18-win-x64.zip`，完整解压到当前用户可写的目录。发布包为 .NET 10 自包含程序，目标电脑无需另装运行时，`Assets` 目录需与 EXE 一起保留。
3. 启动 `DesktopRacerHelper.exe`，在“设置”页选择这台电脑上的 `Desk Top Racer.exe`。
4. 在“环境”页点“一键部署”。首次部署需要联网下载 GDRE v2.7.0，助手会在解压前校验固定 SHA-256。
5. 点顶部“启动游戏”，或从 Steam 启动游戏；等助手显示游戏已连接后再点“一键开启”。

游戏目录需要当前用户可写。Steam 库位于受保护目录时，请使用具有该目录写入权限的 Windows 账户运行部署。助手每次启动都会核对部署记录与文件指纹，校验通过时复用现有环境。

## 源码与构建

WinForms 助手源码位于 `DesktopRacerHelper/`，游戏侧接口脚本位于 `Assets/game-plugin/`。

```powershell
dotnet publish DesktopRacerHelper.csproj -c Release -r win-x64 --self-contained true `
  -p:UseAppHost=true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None
```

需要 .NET 10 SDK。本仓库不包含游戏本体、生成的接口文件、个人配置、统计记录或完整游戏日志。源码来源与许可范围见 [NOTICE](NOTICE.md)。

## 支持作者

可以扫描页首的发电图支持阿丸好饿，也可以给仓库点 Star。问题反馈请带上助手版本、游戏版本和发生时间。

自动化操作可能受到游戏或平台规则限制，请先阅读相关规则并自行评估账号风险。

**by Awan**

# DTR 助手 0.7.18 部署审阅

## 结论

发布包面向 Windows x64。项目使用 .NET 10 WinForms，并以自包含方式发布；新电脑无需单独安装 .NET。项目目标系统版本为 Windows 10 build 19041，Windows 11 x64 是推荐部署环境。微软的 [.NET 支持策略](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)列出 .NET 10 LTS 支持至 2028 年 11 月 14 日；[Windows 安装说明](https://learn.microsoft.com/en-us/dotnet/core/install/windows)列出 .NET 10 支持的 Windows 版本和版本渠道。

助手支持 Godot 4.7.1 游戏构建，部署前会检查游戏文件指纹、接口版本 1.0.2 和协议 3。Windows 版本或游戏引擎超出当前支持范围时，部署校验会返回错误。

## 新电脑部署

1. 在 Steam 安装 Desk Top Racer，并至少启动一次。
2. 解压完整发布包到当前 Windows 用户可写的目录。发布包包含 `DesktopRacerHelper.exe` 和 `Assets` 目录。
3. 启动助手，在“设置”页选择这台电脑上的 `Desk Top Racer.exe`。
4. 在“环境”页点击“一键部署”。首次部署需要联网下载 GDRE v2.7.0；助手会在解压前检查固定 SHA-256。
5. 助手把游戏接口部署到 Steam 使用的游戏 EXE，并在游戏目录保存原始 EXE 备份。游戏运行期间无法替换 EXE；退出后助手会完成接入。
6. 点击助手顶部“启动游戏”，或从 Steam 启动游戏。接口启动后，助手按当前游戏进程 PID 探测 `127.0.0.1:19110–19120`，并连接游戏 API。

游戏目录需要允许当前用户写入。Steam 库位于受保护目录时，请使用具有该目录写入权限的 Windows 账户运行部署。

## 后续启动与更新

- 助手每次启动都会核对部署记录和文件指纹；校验通过时复用现有环境，不会重复生成游戏文件。
- Steam 更新替换游戏 EXE 后，助手会检查新文件。Godot 版本仍为 4.7.1 时可按新指纹重新部署；运行中的游戏退出后才能替换 EXE。
- “清除全部环境”会校验备份并恢复 Steam 游戏 EXE，再删除助手创建的接口文件和记录。备份或当前 EXE 校验失败时，助手会保留相关文件并提示重新检测。
- 助手设置、日志、库存缓存、接口状态与 GDRE 下载保存在 `%LOCALAPPDATA%\DesktopRacerHelper`。
- 游戏 API 端口只绑定回环地址。助手优先读取状态文件，并在状态文件过期、进程不匹配或缺失时主动探测端口及 PID。

## 当前代码审阅

- 0.7.18 使用 .NET 10 自包含发布，目标电脑无需安装运行时。微软支持策略列出的 .NET 10 LTS 结束日期为 2028-11-14。
- 发布使用单文件应用宿主；`Assets` 保留为发布目录中的独立文件，必须与 EXE 一起分发。
- 游戏通过 Steam 注册的 `Desk Top Racer.exe` 启动。助手在本机缓存生成后的部署文件，用哈希校验并更新 Steam EXE。
- API 发现根据运行中游戏 PID 验证端口响应，能处理游戏进程无法更新 `%LOCALAPPDATA%` 状态文件的情况。
- 价格查询只请求可交易物品，先规范化游戏物品名；界面显示有报价、无报价和请求失败的数量。
- 页面切换会释放旧页面控件并复用主题字体；事件补记会合并统计写盘与统计页刷新。
- 补给箱领取持续到游戏没有可领取箱子或游戏确认物品栏已满。
- API 暂不可达时，助手继续探测并保留仍在运行的游戏。防掉线自动重启只处理接口已响应但游戏操作持续失效的情况。
- Steam 游戏目录需要写权限；首次部署需要访问 GitHub 下载并验证 GDRE。离线电脑或受保护安装目录需要先解决这两项条件。
- 项目源代码由 v0.7.10 的助手程序集经 ILSpy 重建后继续修改。当前源码可在本机 .NET SDK 10 下重新发布。

## 构建

需要 .NET 10 SDK。Windows x64 自包含发布命令：

```powershell
dotnet publish DesktopRacerHelper.csproj -c Release -r win-x64 --self-contained true `
  -p:UseAppHost=true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugSymbols=false -p:DebugType=None
```

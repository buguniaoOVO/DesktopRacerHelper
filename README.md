<p align="center">
  <img src="docs/afdian-support.jpg" alt="爱发电赞助" width="250">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 GitHub 仓库点个 Star" width="250"></a>
</p>

# DTR 中控助手 v0.8.19

Windows x64 桌面助手，通过 Frida 按 PID 连接《Desk Top Racer》主机和 Sandboxie 多开，界面固定为简约样式。

[下载 v0.8.19 EXE](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.19/DTRAssistant-v0.8.19-win-x64.exe) · [历史版本](https://github.com/buguniaoOVO/DesktopRacerHelper/releases) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## v0.8.19 更新公告

- 重建游戏连接逻辑，现无需部署桥接或游戏目录插件。
- 支持主机及 Sandboxie 多开按进程接入，并可运行中重连。
- 功能初步测试大致正常，尚未经长时间稳定性测试，欢迎反馈。

连接方式为 Frida 按 PID 附加，在内存中加载适配脚本。沙盒账号使用对应沙盒内的独立 worker。

## 使用说明

更新时完全退出助手，将下载文件改名为 `DTR中控助手.exe` 后覆盖原 EXE，保留原有配置与 `Assets` 文件夹。游戏可保持运行。

1. 通过 Steam 或 Sandboxie 启动 Desk Top Racer，进入游戏主界面。
2. 启动助手，等待自动发现并附加各游戏进程；首次连接会解压内置运行库。
3. 在“总览”查看账号状态。若账号已显示在线而页面数据未更新，点击“刷新”。
4. 连接异常时点击“重连进程”，再点击“刷新”查看状态。未知游戏引擎构建会停止附加并提示等待适配。

路径自动识别失败时，打开“设置 → 游戏路径与进程连接”，填写或浏览选择 `Desk Top Racer.exe` 完整路径后保存。沙盒账号要求 Sandboxie 可启动对应沙盒内的 worker。

此连接方式会向游戏进程内加载代码，需要保持游戏客户端运行。程序会验证游戏文件名、引擎指纹和目标 PID；游戏目录不部署插件。请自行确认游戏和平台规则。

## 功能

- 多账号状态、资源、装备零件、背包与 Steam 可交易物品展示。
- 补给箱领取、品质策略整理、Steam 物品转移、自动换装、零件升级、精通升级、自动维修、幽灵捕捉及离线奖励检测。
- 按账号设置不限帧、10、15 或 30 FPS；设置中可调整检测周期与操作间隔。
- 统计页按品质着色并展示掉落概率，掉落记录可导出 CSV。

游戏内“发送到 Steam”负责转移物品，不会自动创建市场挂牌。

**by 阿丸Awan**

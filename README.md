<p align="center">
  <img src="docs/afdian-support.jpg" alt="爱发电赞助" width="250">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 GitHub 仓库点个 Star" width="250"></a>
</p>

# DTR 中控助手 v0.8.18

Windows x64 桌面助手，通过本机桥接管理《Desk Top Racer》主机和 Sandboxie 多开，界面固定为简约样式。

[下载 v0.8.18 EXE](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.18/DTRAssistant-v0.8.18-win-x64.exe) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## v0.8.18 更新公告

- 重做游戏连接流程，优化桥接完成后的账号发现和状态刷新。
- 多账户管理页显示各账号已装备零件，以及已发送至 Steam 背包的可交易物品。

## 使用说明

1. 先退出所有 Desk Top Racer 游戏，再启动助手。
2. 等待助手顶部显示“等待游戏进程”后，通过 Steam 或 Sandboxie 启动游戏。
3. 在“总览”查看账号状态。若账号已显示在线而页面数据未更新，点击“刷新”。
4. 如果频繁连接失败，尝试刷新并重新连接游戏。

首次部署桥接需保持所有游戏进程退出。路径自动识别失败时，打开“设置 → 游戏路径与桥接”，填写或浏览选择 `Desk Top Racer.exe` 完整路径后保存。

## 功能

- 多账号状态、资源、装备零件、背包与 Steam 可交易物品展示。
- 补给箱领取、品质策略整理、Steam 物品转移、自动换装、零件升级、精通升级、自动维修、幽灵捕捉及离线奖励检测。
- 按账号设置不限帧、10、15 或 30 FPS；设置中可调整检测周期与操作间隔。
- 统计页按品质着色并展示掉落概率，掉落记录可导出 CSV。

游戏内“发送到 Steam”负责转移物品，不会自动创建市场挂牌。

**by 阿丸Awan**

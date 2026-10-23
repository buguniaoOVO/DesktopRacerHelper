<p align="center">
  <img src="docs/afdian-support.jpg" alt="爱发电赞助" width="250">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 GitHub 仓库点个 Star" width="250"></a>
</p>

# DTR 中控助手 v0.8.17

Windows x64 桌面助手，用于管理《Desk Top Racer》本机与 Sandboxie 游戏进程，并支持 SteamKit 直连账号。

[下载 v0.8.17 EXE](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.17/DTRAssistant-v0.8.17-win-x64.exe) · [上一公开版 v0.8.6](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.6/DTRAssistant-v0.8.6-win-x64.exe) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## v0.8.17 更新公告

与上次公开版 v0.8.6 相比，本版主要增加和调整：

- 调整连接逻辑，增加 SteamKit 直连账号模式，可按 Steam Guard 要求完成认证；刷新令牌和 MAFile 认证字段使用当前 Windows 用户 DPAPI 保护。
- 直连模式显示 Steam 钱包余额和币种，支持设置网络代理；账号首次连接只读取状态，挂机功能由用户手动开启。
- 在线账号每 5 分钟检查离线奖励，只有游戏快照明确报告待领取且桥接支持时才提交，并等待游戏状态确认。
- 改进背包及 Steam 仓库可交易物品识别，在物品名称显示 `[M]` 标记；统计表按品质着色。

> **连接提示：**连接状态偶尔异常时，可多次点击“刷新”并重新连接游戏。助手顶部若持续显示“桥接中”，但“总览”或“账号管理”中账号已显示在线，点击“刷新”即可更新显示。若频繁仍无法连接，请尝试重新刷新并连接游戏；持续无法连接时，可改用[上一公开版 v0.8.6](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.6/DTRAssistant-v0.8.6-win-x64.exe)。

## 首次使用

1. 运行 `DTR中控助手.exe`；覆盖升级前请退出旧助手。
2. 本机/Sandboxie 模式下，助手会在游戏进程全部退出时部署本机桥接模块。首次部署完成后，从 Steam 或 Sandboxie 重新启动游戏。
3. 在总览检查账号状态，按账号启用自动化任务。
4. 使用无客户端直连时，打开“设置 → 无客户端直连”并添加 Steam 账号；该模式不启动 Desk Top Racer 或 Steam 桌面客户端。

如果日志提示未配置有效的游戏 EXE 路径，打开“设置 → 游戏路径与桥接”，填写或浏览选择 `Desk Top Racer.exe`，点击“保存路径并部署桥接”。

## 功能

- 多账号总览、装备与背包信息、补给箱和比赛状态。
- 补给箱领取、品质策略整理、Steam 物品转移、自动换装、零件升级、精通升级、自动维修和幽灵捕捉。
- 按账号设置不限帧、10、15 或 30 FPS；设置只作用于对应的 DTR 游戏进程。
- 离线奖励自动检测与领取；连接中断时助手不会自动重启游戏。
- SteamKit 钱包余额显示及 Steam 交易 URL 复制。

Steam 社区库存浏览、待确认列表及市场挂牌尚未接入；游戏内“发送到 Steam”只转移物品，不创建市场挂牌。

**by 阿丸Awan**

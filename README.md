<p align="center">
  <img src="docs/afdian-support.jpg" alt="爱发电赞助" width="250">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 GitHub 仓库点个 Star" width="250"></a>
</p>

# DTR 中控助手 v0.8.6

Windows x64 桌面助手，通过本机桥接管理《Desk Top Racer》的主机与 Sandboxie 游戏进程。

[下载 v0.8.6 EXE](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.8.6/DTRAssistant-v0.8.6-win-x64.exe) · [首次使用](#首次使用) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## v0.8.6 更新

- 新增手动填写或浏览选择游戏 EXE 路径，改善部分电脑无法识别游戏目录时的桥接部署。
- 保留本机与 Sandboxie 多账号管理及游戏内自动化功能。

## 首次使用

1. 下载并运行 EXE。覆盖升级时退出旧助手，再把新文件改名为 `DTR中控助手.exe` 并覆盖原 EXE；保留安装目录中的 `Assets` 文件夹。
2. 首次部署桥接时，请退出游戏进程。助手会更新主机和现有 Sandboxie 游戏副本，并保留原版 EXE 备份。
3. 等待运行日志提示桥接部署完成，再通过 Steam 或 Sandboxie 启动游戏。
4. 若日志提示未配置有效的游戏 EXE 路径，打开“设置 → 游戏路径与桥接”，填写或浏览选择 `Desk Top Racer.exe`，点击“保存路径并部署桥接”。

助手为每个游戏进程动态分配本机端口，并按桥接响应中的 PID 匹配账号。若顶部仍显示“桥接中”，但总览或多账户管理已显示在线，点击“刷新”更新显示。连接异常时可刷新并重新连接游戏。

## 功能

- 总览显示账号状态、资源、背包、补给箱和比赛状态。
- 支持品质策略、开箱、背包整理、Steam 物品转移、自动换装、零件升级、精通升级、自动维修和幽灵捕捉。
- 统计页面显示品质概率；掉落记录可导出 CSV。
- 按账号设置不限帧、10、15 或 30 FPS。
- 简约界面；设置可调整检测周期与操作间隔。

游戏内“发送到 Steam”负责转移物品，不会自动创建市场挂牌。

**by 阿丸Awan**
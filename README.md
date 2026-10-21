<p align="center">
  <img src="docs/afdian-support.jpg" alt="爱发电赞助" width="250">
  <a href="https://github.com/buguniaoOVO/DesktopRacerHelper"><img src="docs/star-support.png" alt="给 GitHub 仓库点个 Star" width="250"></a>
</p>

# DTR 中控助手 v0.85

Windows x64 桌面客户端，用于管理《Desktop Racer》的本机与 Sandboxie 游戏进程。

[下载 v0.85 EXE](https://github.com/buguniaoOVO/DesktopRacerHelper/releases/download/v0.85/DTRAssistant-v0.85-win-x64.exe) · [首次使用](#首次使用) · [问题反馈](https://github.com/buguniaoOVO/DesktopRacerHelper/issues)

## v0.85 更新

- 优化连接逻辑与限帧表现，降低多开资源占用。
- 修复多开幽灵捕捉；活动期间仍可能偶发遗漏。
- 更新桥接时请退出游戏，部署完成后重新启动对应游戏。连接状态异常时，可多刷新或重试连接。

## 首次使用

1. 下载并运行 EXE。覆盖升级时先退出助手，再将下载的文件改名为 `DTR中控助手.exe` 并覆盖原文件；保留安装目录中的 `Assets` 文件夹。
2. 首次部署桥接时，请退出游戏进程。助手会把本机桥接部署到主机和现有 Sandboxie 游戏副本，并保留原版 EXE 备份。
3. 等待运行日志显示部署完成，再从 Steam 或 Sandboxie 启动游戏。
4. 等待总览显示账号在线，然后按账号启用自动化任务。

桥接更新可点击助手顶部的“更新桥接”。助手会处理连接异常或版本过期的游戏进程，部署对应副本并重新启动该进程。

## 功能

- 总览显示每个账号的资源、背包、补给箱和比赛状态，并管理账号自动化开关。
- 品质策略支持收藏、拆解、出售与自动换装。
- 统计和掉落记录显示补给箱数量、物品品质及品质概率。
- 万圣节页面显示各账号幽灵捕捉状态；每个账号每日上限为 100 次，活动期间仍可能出现遗漏。
- 帧率上限支持不限帧、10、15 和 30 FPS，可按游戏进程单独设置。
- 设置可调整主题、检测周期、操作间隔和更新提示。

助手通过游戏进程内的本机桥接协议连接游戏。桥接为每个进程动态分配本机端口，助手按协议响应中的 PID 匹配账号，支持主机和 Sandboxie 多开。

## 支持作者

可以扫描页首的爱发电图片支持作者，也可以给仓库点 Star。问题反馈请附上助手版本、游戏版本和发生时间。

自动化操作请遵守游戏与平台规则，并自行评估账号风险。

**by 阿丸Awan**
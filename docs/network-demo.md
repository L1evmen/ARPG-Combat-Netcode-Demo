# 局域网联机 Demo

## 编辑器启动

1. 打开 `Assets/Scenes/02-NetworkGameScene.unity`。
2. 进入 Play Mode，一端点击 `Host`。
3. 另一端输入 Host 的局域网 IPv4 地址并点击 `Client`。
4. 同一台电脑测试时使用 `127.0.0.1`。

默认使用 UDP `7777` 端口；跨设备测试时需要允许 Windows 防火墙放行该端口。

## 构建

使用 Unity 菜单 `Tools/ARPG/Build Network Demo/Windows Player`，输出目录为：

`Builds/NetworkDemo/ARPGNetwork.exe`

也可以使用命令行直接启动：

```text
ARPGNetwork.exe -host
ARPGNetwork.exe -client 192.168.1.10
```

## 同步范围

- 玩家：位置、朝向、动画状态、装备外观、生命、死亡与复活。
- Boss：位置、朝向、动画状态、生命、格挡耐力、激活与死亡状态。
- 战斗：玩家攻击请求由 Host 校验；Boss AI、命中与伤害由 Host 裁决。
- IK：远端角色在本地根据同步姿态执行程序化 IK，不传输骨骼数据。

背包内容、任务进度、存档和设置仍为本地系统，不在本 Demo 的同步范围内。

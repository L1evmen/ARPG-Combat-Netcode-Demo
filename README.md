# 3D 动作 RPG Demo（Unity 客户端）

> Unity 2023.1.1f1 · 单人战斗 + 双人局域网联机 · [演示视频（B 站）](https://www.bilibili.com/video/BV1SwEy6sEEe/)

独立设计并开发的第三人称动作 RPG 演示项目，包含连招战斗、Boss AI、目标锁定、背包装备、任务存档、设置与音频、局域网联机等模块。项目采用模块化、事件驱动的组织方式，背包 UI 使用 MVC 分层，可分别构建和运行单机与联机演示场景。

核心业务代码与系统实现由本人独立完成。项目中的部分字体、角色、动作、音频及美术资源来自第三方，未取得或尚未核实公开再分发授权，仅用于学习与求职作品展示，不作为本人原创成果；详见[素材与版权说明](ASSET_NOTICE.md)。

![项目演示画面](docs/images/cover.png)

[▶ 观看完整演示](https://www.bilibili.com/video/BV1SwEy6sEEe/) · [架构说明](docs/architecture.md) · [联机验证记录](docs/verification.md)

## 技术亮点

### 局域网状态同步

基于 Netcode for GameObjects 与 Unity Transport 实现双人 Host / Client 联机：

- 以 **20 Hz** 同步玩家与 Boss 的位姿、动画参数、动作状态和装备信息；
- 远端副本使用 **100 ms 插值缓冲**，处理乱序快照并按序号去重，在包间隙内进行最长 **120 ms 的受限外推**；
- Boss AI、生命状态、攻击请求与伤害结算由 Host 裁决；Host 同时校验客户端提交的移动快照、攻击距离与请求频率；
- 编写本机双进程自动冒烟测试，验证双向移动、旋转、移动/攻击动画与 Boss 同步；成功帧记录的副本位置误差为 **0.040～0.076 m**。

测试实现见 `Assets/Scripts/Network/NetworkSyncSmokeTest.cs`，环境、阈值与复现方式见[验证记录](docs/verification.md)。上述误差为一次本机双进程测试结果，不代表网络 RTT 或统计学平均值。

### 连招战斗系统

- 以策略模式抽象 `ComboAction`，派生五段轻击、三级蓄力重击、升龙斩、空中连招、砸地与闪避等动作；
- `ComboManager` 统一管理输入采样、短时输入缓冲、动作切换与闪避取消；
- 每种招式实例缓存在字典中复用，命中盒只在有效攻击窗口开启；
- 输入、动作规则、动画表现与伤害路由相互分离，新增招式时无需修改既有动作类。

### Boss AI

- 使用层级有限状态机组织 `Dormant`、`Alive`、`Death` 流程；`Alive` 组合状态内部管理 `Observe`、`Chase`、`Combo`、`Block`、`HitStun`、`Stagger`、`Recover`、`Retreat` 等行为；
- 组合状态统一处理 Enter / Update / Exit，并将受击等事件沿当前状态分支下发；
- 实现格挡耐力、破防硬直与恢复，根据 **70% / 40%** 血量阈值选择不同连招表现；
- 使用 NavMesh 驱动追击与走位，并限制 Boss 的有效战斗区域。

### 角色与敌人系统

- `TargetLockManager` 在锥形范围内按屏幕空间距离选择目标，目标持续被遮挡后自动解锁；
- `EnemyController` 使用 FSM 管理巡逻、追逐、攻击、受击与死亡，攻击窗口由 Animation Event 驱动；
- 程序化双脚 IK 根据地面法线调整脚部与骨盆，缓解斜坡、台阶上的悬脚和穿模。

### 数据与 UI

- `InventoryService` 管理物品堆叠、排序与筛选，通过事件驱动装备、快捷栏与背包 UI 刷新；
- 背包系统将数据、操作入口与显示层分离，UI 子系统采用 MVC 组织；
- `SaveManager` 使用四槽位 JSON 存档，持久化角色、背包、装备与任务数据；
- 支持音量、显示设置与 Input System 键位覆盖。

![连招 Animator 与设置界面](docs/images/combat-and-settings.png)

## 项目结构

```text
Assets/
├─ Scenes/
│  ├─ 00-Mainmeun.unity          # 主菜单
│  ├─ 01-GameScene.unity         # 单机演示场景
│  └─ 02-NetworkGameScene.unity  # 联机演示场景
├─ Scripts/
│  ├─ Combat / CombatV2 / Weapon # 连招、伤害与武器
│  ├─ Boss / BossHPBar           # Boss HFSM 与血条
│  ├─ Enemy / Interactable       # 敌人 FSM、拾取与交互
│  ├─ Network                    # 网络同步、Host 裁决与冒烟测试
│  ├─ Backpack / Inventory       # 背包、装备与快捷栏
│  ├─ Save / Settings            # 存档与设置
│  ├─ HUD / UI / Audio           # HUD、界面与音频
│  └─ Manager / SO / Other       # 事件、数据与角色控制
└─ Editor/
   └─ NetworkDemoSetup.cs        # 联机场景校验与构建入口
```

更完整的模块边界、数据流和设计取舍见[架构说明](docs/architecture.md)。

## 如何运行

### 环境

- Unity `2023.1.1f1`；
- Windows 10 / 11；
- URP `15.0.6`、Input System `1.7.0`、Netcode for GameObjects `1.13.0`、Unity Transport `1.4.0`。

建议使用相同 Unity 版本打开项目，避免场景和 Prefab 被升级重写。首次导入时需等待 Unity Package Manager 完成依赖解析。

### 单机演示

1. 使用 Unity Hub 添加仓库根目录；
2. 打开 `Assets/Scenes/00-Mainmeun.unity`；
3. 进入 Play Mode，从主菜单进入单机战斗。

常用操作：`WASD` 移动、`Space` 跳跃、`Left Shift` 冲刺/闪避、鼠标左键轻击、鼠标右键蓄力重击、`W + 右键` 上挑、`F` 特殊技、鼠标中键锁定、`E` 交互、`Tab` 背包、`1～4` 快捷栏。

### 联机演示

1. 打开 `Assets/Scenes/02-NetworkGameScene.unity`；
2. 使用菜单 `Tools/ARPG/Build Network Demo/Windows Player` 构建 Windows Player；
3. 一端选择 Host，另一端输入 Host 的局域网 IPv4 地址并选择 Client；同机双开使用 `127.0.0.1`。

默认使用 UDP `7777` 端口。跨设备测试时需要允许 Windows 防火墙放行该端口。

## 素材与版权声明

- 核心业务代码与系统实现由本人独立完成；第三方 Shader、插件及素材不属于本人原创成果；
- Layer Lab、LowPolyMegapolis、DynamicBone 等资源来源于 Unity Asset Store，其使用与再分发应遵循各自许可条款；
- `UnityURPToonLitShaderExample` 来源于 GitHub 开源项目，仓库内保留原始 MIT License；
- 部分字体、角色、动作、音乐和音效未取得或尚未核实公开再分发授权，仅作为演示素材；“学习、非商业、求职展示”声明不构成权利方授权；
- 本项目不提供覆盖全部仓库内容的统一开源许可，也不授予任何第三方素材的使用权；
- 战斗体验设计参考《黑神话：悟空》《艾尔登法环》等动作游戏，系统与玩法代码为独立实现。

如素材权利方认为本仓库的使用方式不妥，请联系 `liyiwei0110@163.com`，确认后将及时移除相关内容。详细清单与公开发布建议见[素材与版权说明](ASSET_NOTICE.md)。

## 联系方式

李奕威 · `liyiwei0110@163.com`

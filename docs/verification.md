# 验证记录

## 环境

| 项目 | 值 |
| --- | --- |
| 日期 | 2026-08-15 |
| 操作系统 | Windows 11 64-bit |
| Unity | 2023.1.1f1 (46620eadcc07) |
| Render Pipeline | URP 15.0.6 |
| Netcode | Netcode for GameObjects 1.13.0 |
| Transport | Unity Transport 1.4.0，UDP 7777 |
| 构建 | Windows x64 Development Build |

## 构建与静态校验

Unity 使用仓库内 `NetworkDemoSetup.BuildWindowsPlayer` 完成以下动作：

1. 校验 NetworkPlayer Prefab、NetworkObject Hash、DefaultNetworkPrefabs 注册和 Build Settings。
2. 校验网络场景中 NetworkManager、UnityTransport、NetworkSessionController、NetworkBossSynchronizer 与 Lobby UI。
3. 以 `Assets/Scenes/02-NetworkGameScene.unity` 构建 Development Player。

命令：

```powershell
& 'E:\Unity\Unity 2023.1.1f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'E:\UnityProjects\ARPG2' `
  -executeMethod NetworkDemoSetup.BuildWindowsPlayer `
  -logFile 'network-build.log'
```

结果：编辑器脚本编译通过，`Network demo validation passed`，Player 构建成功。

## 双进程冒烟测试

Development Build 传入 `-sync-smoke` 后会自动执行固定流程：

- Client 移动 1.5m、旋转 90°并设置跑步/攻击 Animator 参数；Host 验证远端副本。
- Host 执行同样动作；Client 验证远端副本。
- Host 驱动 Boss 移动 1.5m并旋转 90°；Client 验证 Boss 副本。
- 任一步骤在 12 秒内不满足阈值即输出 `FAIL` 并以退出码 2 结束。

复现命令：

```powershell
$exe = 'Builds\NetworkDemo\ARPGNetwork.exe'
$hostProcess = Start-Process $exe -WindowStyle Hidden -PassThru -ArgumentList `
  '-batchmode','-nographics','-host','-sync-smoke','-logFile','host-smoke.log'
Start-Sleep -Seconds 2
$clientProcess = Start-Process $exe -WindowStyle Hidden -PassThru -ArgumentList `
  '-batchmode','-nographics','-client','127.0.0.1','-sync-smoke','-logFile','client-smoke.log'
```

### 验收条件

- 玩家源对象和远端副本位移均至少 1m。
- 玩家源对象和远端副本旋转均至少 45°；Client 无源对象时检查副本即可。
- 同步位置误差不超过 0.35m。
- 跑步参数与攻击标志均被远端 Animator 接收。
- Boss 位移至少 1m、旋转至少 45°、同步误差不超过 0.35m。

### 结果

| 日志阶段 | 位移/旋转 | 成功帧误差 | 结论 |
| --- | --- | ---: | --- |
| `HOST_RECEIVE_CLIENT` | source 1.50m / replica 1.46m；90.0° / 87.6° | 0.040m | PASS |
| `CLIENT_RECEIVE_HOST` | replica 1.40m；84.2° | 0.076m | PASS |
| `CLIENT_RECEIVE_BOSS` | 1.00m；60.1° | 0.076m | PASS |
| 进程 | `HOST_PASS` / `CLIENT_PASS` | — | PASS |

日志摘录：

```text
[SyncSmoke] HOST_RECEIVE_CLIENT_PASS ... replicaError=0.040m locomotion=true attack=true
[SyncSmoke] CLIENT_RECEIVE_HOST_PASS ... replicaError=0.076m locomotion=true attack=true
[SyncSmoke] CLIENT_RECEIVE_BOSS_PASS ... error=0.076m
[SyncSmoke] HOST_PASS
[SyncSmoke] CLIENT_PASS
```

误差是 `CharacterStateSynchronizer.LastPositionError` 在满足全部验收条件的成功帧取值，不是 RTT、均值、P95 或跨网络环境的性能承诺。

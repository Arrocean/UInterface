# UInterface
This project serves as a foundational, component-based mod for Uboat, designed to facilitate the integration of all new submarines.

## 本地开发

当前支持 UBOAT 2026.1 Patch 21（Steam build 24913406）。框架为内容包提供艇型注册、开局和港口装载、设备挂点及玩家艇切换通知。

```powershell
dotnet build -c Release -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
dotnet build -c Release -t:DeployMod -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
```

`UboatGamePath` 也可通过环境变量 `UBOAT_GAME_PATH` 提供。部署目标默认是 `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\uboat-submarine-framework`，可用 `-p:LocalModPath=...` 指定其他位置。部署后在启动器的 Mods 列表启用 **UInterface**。UBOAT 使用部署包中的 `Source` 自行编译脚本。

## 为战役注册艇型

在依赖 UInterface 的内容包中添加 `Data Sheets/Sandbox.xlsx`，按以下格式创建 `UInterfaceSubmarines` 工作表。

| / | Base Type |
| --- | --- |
| example.submarine | Type VIIC |

给新艇型 ID 加上你自己的命名空间前缀，避免包之间重名。把这个 ID 填进战役 `Campaigns` 工作表的 `Type` 列，即可由框架在开局时解析。`Base Type` 使用游戏原有艇型名称，使用 Type IX 系列时需要相应 DLC。

艇型定义、开局条目及预览资源由内容包提供。潜艇内容包随艇发布预览资源。UBOAT 按开局 ID 查找 `Icons/Scenario/<开局 ID>` 对应的图标和 `Images/Scenario/<开局 ID>/` 下的预览图。

## 艇型、挂点与设备

艇体包可为 `UInterfaceSubmarines` 添加以下可选列。仅设置 `Base Type` 的旧配置仍可作为原版艇型别名使用。

| 列 | 用途 |
| --- | --- |
| `Entity Type` | 内容包在 UBOAT 实体数据库注册的唯一玩家实体名，后缀为 ` (Player)`。与 `Interior Scene` 一起填写。 |
| `Interior Scene` | 内容包提供的舱内场景资源名。场景根对象需要原版 `PlayerShip` 组件。 |
| `Port Ship` | 在港口换艇菜单显示的艇名。此列有值时，独立艇型进入港口列表。 |
| `Port Prerequisite` | 可选的解锁前置。用原版艇型名或去掉 ` (Player)` 后缀的自定义实体名。 |

战役开局通过艇型 ID 选艇。自定义实体开局和港口换艇都交给原版玩家艇加载器，读档继续使用原版存档路径。构建内容时请保留实体名、场景名和它们所在的 Mod。港口卡片和详情预览沿用原版资源键，以去掉 ` (Player)` 后缀的实体名为键。

同一个 `Sandbox.xlsx` 可包含以下工作表。首列的表头都是 `/`。

| 工作表 | 首列的键 | 其他列 |
| --- | --- | --- |
| `UInterfaceMounts` | `example.submarine:radar-mast` | `Transform` 填艇体或舱内根对象下的相对层级路径，例如 `Exterior/Masts/Radar`。 |
| `UInterfaceEquipment` | `example.radar` | `Mount` 填 `radar-mast`。 |

同一设备包可以在提供同名挂点的多艘艇上使用。使用原版艇或原版别名时，挂点键使用原版实际艇型，例如 `Type VIIC:radar-mast`。

## C# 接口

`UInterface.SubmarineApi` 的 `ApiVersion` 为 1。以下接口在游戏主线程使用。数据表定义优先于脚本注册，同一 ID 不应由多个内容包声明。

| 接口 | 职责 |
| --- | --- |
| `RegisterSubmarine(id, baseType, entityType, interiorScene)` / `TryGetSubmarine(id, out baseType, out entityType, out interiorScene)` | 注册或查询艇型。原版别名的实体名和场景名都传 `null`。 |
| `RegisterMount(submarineId, mountId, transformPath)` / `TryGetMount(mountId, out mount)` | 注册挂点，并从当前玩家艇查找 `Transform`。 |
| `RegisterEquipment(equipmentId, mountId)` / `TryGetEquipmentMount(equipmentId, out mount)` | 声明设备所需的挂点，并用设备 ID 找到安装位置。 |
| `TryGetCurrentSubmarine(out submarineId, out ship)` | 获取当前艇和实际艇型 ID。模块初始化后可调用一次，用于接管已经加载的艇。 |
| `OnPlayerSubmarineChanged` | 参数为 `(previousShip, newShip)`。卸载时 `newShip` 为 `null`，此时释放设备绑定。 |

UBOAT 运行时分别编译各个源码 Mod。第三方脚本可在 `IUserMod.OnLoaded` 中用 `AccessTools.TypeByName("UInterface.SubmarineApi")` 查找公共类型，检查 `ApiVersion`，再为需要的方法创建委托。需要在退出艇场景时解绑的脚本可以订阅上述事件。

设备挂点提供资源定位。设备包可通过 `DeploymentApi` 接入桅杆收放和状态。探测模块可通过 `SensorsApi` 获取原版探测器和观测结果。

## 舱内工位

[舱内机制盘点与框架架构](docs/舱内机制与框架架构.md)记录了六个艇型的实测盘点、依赖边界和后续接入顺序。

`UInterface.StationApi` 的 `ApiVersion` 为 1。它在游戏主线程使用，并可按上节方式通过委托跨 Mod 调用。下表列出两个带稳定动作 ID 的原版工位。其余工位可枚举并查询原版动作。

| 工位 ID | 动作 ID | 原版动作 |
| --- | --- | --- |
| `uinterface.helm` | `uinterface.helm.work` | 到舵手工位值班，沿用原版船员管理设置 |
| `uinterface.trim-pump` | `uinterface.trim-pump.toggle` | 派船员操作配平泵开关，使用设备本身的配平和排水逻辑 |

自制艇在 `Sandbox.xlsx` 的 `UInterfaceStations` 工作表中添加绑定。首列为 `/`，键例为 `example.submarine:uinterface.helm`，`Mount` 列填在 `UInterfaceMounts` 中注册的挂点 ID。挂点应指向当前艇内舱的原版 `Activator` 或其关联设备所在的对象。一处挂点需能唯一定位操作入口。

| 接口 | 职责 |
| --- | --- |
| `RegisterStation(submarineId, stationId, mountId)` | 脚本注册工位绑定，已有数据表定义优先 |
| `RegisterAction(stationId, actionId, actionType)` | 给工位上的现有 `CharacterAction` 类型绑定动作 ID |
| `TryGetStation(stationId, out activator)` | 找到当前艇上的原版操作入口 |
| `QueryActions(stationId, actor)` / `TryGetActionState(stationId, actionId, actor, out state)` | 查询角色在该工位可见的动作及原版可用状态 |
| `RequestAction(stationId, actionId, actor)` | 通过原版角色动作入口请求执行。返回值表示请求是否进入动作流程，任务完成状态由原版系统持有 |
| `TryGetState(stationId, out hasAssignedCrew, out switchedOn, out operating)` | 读取设备工位的人员指派、开关和实际启用状态 |
| `SubscribeStateChanged(stationId, callback)` | 监听原版工位状态和人员指派变化，返回可释放的订阅。换艇时自动解除，内容包可重新查询和订阅 |

## 机制接口

[机制接口接入说明](docs/机制接口接入说明.md)列出 12 类机制在框架中的入口、原版与自定义组件的接入方式，以及统一验收范围。原版 TDC 为可选组件，鱼雷管接口独立提供。

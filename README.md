# UInterface
This project serves as a foundational, component-based mod for Uboat, designed to facilitate the integration of all new submarines.

## 本地开发

当前支持 UBOAT 2026.1 Patch 21（Steam build 24913406）。框架为内容包提供艇型注册、开局和港口装载、设备挂点及玩家艇生命周期通知。

```powershell
dotnet build -c Release -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
dotnet build -c Release -t:DeployMod -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
```

`UboatGamePath` 也可通过环境变量 `UBOAT_GAME_PATH` 提供。每次构建都会生成 `Workshop/UInterface`。该目录的根包含 `Manifest.json`、`readme.txt`、`preview.jpg` 和 `Source`。C# 源码由 UBOAT 模组管理器编译。版本库保留 `Mod` 内的源码和发布素材，`Workshop`、`bin` 和 `obj` 是生成目录。

`DeployMod` 默认复制发布包到 `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\uboat-submarine-framework`，也可用 `-p:LocalModPath=...` 指向其他 UInterface 安装目录。部署后，在 UBOAT 启动器的 Mods 页面找到 UInterface，使用其 Steam Workshop 上传入口。首次上传成功后，启动器会把条目 ID 写入该安装目录的清单。后续更新前，把该 ID 同步到 `Mod/Manifest.json`，这样下一次部署仍关联同一条目。

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
| `UInterfaceMounts` | `example.submarine:radar-mast` | `Transform` 填艇体、舱内或已加载依赖场景根对象下的相对层级路径，例如 `Exterior/Masts/Radar`。 |
| `UInterfaceEquipment` | `example.radar` | `Mount` 填 `radar-mast`。 |

同一设备包可以在提供同名挂点的多艘艇上使用。使用原版艇或原版别名时，挂点键使用原版实际艇型，例如 `Type VIIC:radar-mast`。

## C# 接口

`UInterface.SubmarineApi` 的 `ApiVersion` 为 1。以下接口在游戏主线程使用。数据表定义优先于脚本注册，同一 ID 不应由多个内容包声明。

| 接口 | 职责 |
| --- | --- |
| `RegisterSubmarine(id, baseType, entityType, interiorScene)` / `TryGetSubmarine(id, out baseType, out entityType, out interiorScene)` | 注册或查询艇型。原版别名的实体名和场景名都传 `null`。 |
| `RegisterMount(submarineId, mountId, transformPath)` / `TryGetMount(mountId, out mount)` | 注册挂点，按艇体、舱内、已加载的本艇依赖场景顺序查找 `Transform`。依赖场景中有多处匹配时返回 `false`。 |
| `RegisterEquipment(equipmentId, mountId)` / `TryGetEquipmentMount(equipmentId, out mount)` | 声明设备所需的挂点，并用设备 ID 找到安装位置。 |
| `TryGetCurrentSubmarine(out submarineId, out ship)` | 获取当前艇和实际艇型 ID。 |
| `OnPlayerSubmarineChanged` | 参数为 `(previousShip, newShip)`。原版切换阶段发出，卸载时 `newShip` 为 `null`。 |
| `TryGetReadySubmarine(out submarineId, out ship)` / `OnPlayerSubmarineReady` | 查询已初始化的当前艇，或在开局、换艇和读档后接收 `PlayerShip`，用于绑定工位和设备。 |
| `OnPlayerSubmarineRestored` | 原版读档流程完成后，在本次就绪通知之后发出，参数为恢复后的 `PlayerShip`。 |
| `OnPlayerSubmarineDependencyReady` | 依赖场景在玩家艇就绪后加载时，提供当前艇和依赖场景根 `Transform`，用于补绑设备。 |

UBOAT 运行时分别编译各个源码 Mod。依赖方可注入游戏的 `IModManager`，通过 `GetMod("UInterface")?.Assembly` 从实际启用的框架程序集查找公共类型，再为所需方法创建委托。基础签名版本由 `ApiVersion` 表示。`GetFeatureVersion(featureId)` 返回当前已安装的能力版本，`0` 表示暂不可用。依赖方应在艇就绪时取得当前运行时绑定，允许可选能力稍后出现。完整示例见[跨 Mod 调用](docs/机制接口接入说明.md#由游戏模组管理器加载的内容包)。

设备包可通过 `DeploymentApi` 接入桅杆收放和状态，通过 `SensorsApi` 注册自定义传感器并提供状态通知。探测模块也可以获取原版探测器和观测结果。

## 舱内工位

[舱内机制盘点与框架架构](docs/舱内机制与框架架构.md)记录了六个艇型的实测盘点、依赖边界和后续接入顺序。

`UInterface.StationApi` 的 `ApiVersion` 为 1。它在游戏主线程使用，并可按上节方式通过委托跨 Mod 调用。下表列出两个带稳定动作 ID 的原版工位。其余工位可枚举并查询原版动作。

| 工位 ID | 动作 ID | 原版动作 |
| --- | --- | --- |
| `uinterface.helm` | `uinterface.helm.work` | 到舵手工位值班，沿用原版船员管理设置 |
| `uinterface.trim-pump` | `uinterface.trim-pump.toggle` | 派船员操作配平泵开关，使用设备本身的配平和排水逻辑 |

自制艇在 `Sandbox.xlsx` 的 `UInterfaceStations` 工作表中添加绑定。首列为 `/`，键例为 `example.submarine:uinterface.helm`。`Mount` 填单个挂点 ID，`Mounts` 可填逗号分隔的多个挂点 ID。挂点应指向当前艇内舱的原版 `Activator` 或其关联设备所在的对象。

| 接口 | 职责 |
| --- | --- |
| `RegisterStation(submarineId, stationId, mountId)` | 脚本注册工位绑定，已有数据表定义优先 |
| `RegisterStationGroup(submarineId, stationId, mountIds)` | 一次注册同组的多个挂点 |
| `RegisterAction(stationId, actionId, actionType)` | 给工位上的现有 `CharacterAction` 类型绑定动作 ID |
| `RegisterActionSelector(stationId, actionId, actionType, selector)` | 用筛选器区分同类型的原版动作模板 |
| `RegisterSubmarineAction(submarineId, stationId, actionId, actionType, selector)` | 为指定艇型绑定动作，优先于同名全局默认绑定 |
| `GetStations(stationId)` / `TryGetStation(stationId, out activator)` | 枚举当前艇的工位，或在唯一匹配时取回原版操作入口 |
| `QueryActions(stationId, actor)` / `TryGetActionState(stationId, actionId, actor, out state)` | 查询角色在该工位可见的动作及原版可用状态 |
| `RequestAction(stationId, actionId, actor)` | 通过原版角色动作入口请求执行。返回值表示任务已接收，或已适配的即时动作已完成 |
| `RequestTrackedAction` / `RequestTrackedNativeAction` / `TryCancelTrackedAction` | 取得原版动作实例，读取进度和阶段，并按原版停止条件取消指定动作 |
| `QueryQueuedNativeActions` / `RequestQueuedNativeAction` / `TryCancelQueuedNativeAction` | 追加到个人队列，或在未指定船员时提交全艇队列 |
| `GetActiveQueuedNativeActions` / `TryTrackQueuedNativeAction` | 玩家艇就绪后枚举并接管原版队列中的玩家任务，包括读档恢复的任务 |
| `QueryNativeUtilityActions` / `RequestNativeUtilityAction` | 查询并触发原版辅助交互，例如打开设备帮助或储物界面 |
| `TryGetState` / `TryGetStationState` | 按唯一 ID 或工位实例读取人员指派、开关和实际启用状态 |
| `TryGetStationUsers(station, out users)` | 获取正在使用工位的船员，也适用于没有原版设备关联的工位 |
| `SubscribeStateChanged` / `SubscribeStationStateChanged` | 按唯一 ID 或工位实例监听状态。换艇、卸载和读档时解除订阅 |

## 机制接口

[机制接口接入说明](docs/机制接口接入说明.md)列出 12 类机制在框架中的入口、原版与自定义组件的接入方式，以及统一验收范围。原版 TDC 为可选组件，鱼雷管接口独立提供。

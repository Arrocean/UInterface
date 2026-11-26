# UInterface

[简体中文](README-zh_CN.md)

UInterface is a modular framework for adding player submarines and onboard equipment to UBOAT.

## Local development

UInterface supports UBOAT 2026.1 Patch 21 (Steam build 24913406). It provides submarine registration, loading at campaign start and in port, equipment mounts, and player submarine lifecycle events.

```powershell
dotnet build -c Release -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
dotnet build -c Release -t:DeployMod -p:UboatGamePath="D:\Steam\steamapps\common\UBOAT"
```

You can also set `UBOAT_GAME_PATH` to specify the UBOAT installation. Each build generates `Workshop/UInterface` with `Manifest.json`, `readme.txt`, `preview.jpg`, and the `Source` directory. UBOAT's mod manager compiles the C# source. The repository keeps the source and release assets in `Mod`. `Workshop`, `bin`, and `obj` are generated directories.

`DeployMod` copies the package to `%USERPROFILE%\AppData\LocalLow\Deep Water Studio\UBOAT\Mods\uboat-submarine-framework` by default. Use `-p:LocalModPath=...` to deploy to a different UInterface installation. After deploying, find UInterface on the Mods page of the UBOAT launcher and use its Steam Workshop upload option. On the first successful upload, the launcher writes the Workshop item ID to the local manifest. Copy that ID to `Mod/Manifest.json` before deploying another update so the package stays linked to the same item.

## Registering a submarine type for a campaign

In a content mod that depends on UInterface, add `Data Sheets/Sandbox.xlsx` with a `UInterfaceSubmarines` sheet like the one below.

| / | Base Type |
| --- | --- |
| example.submarine | Type VIIC |

Prefix your submarine type ID with your content mod's namespace to avoid collisions. Put that ID in the `Type` column of the campaign's `Campaigns` sheet so UInterface can resolve it when the scenario starts. `Base Type` takes a stock submarine type name. The Type IX series requires the corresponding DLC.

Content mods provide submarine definitions, campaign start entries, and preview assets. Submarine mods ship their previews with the boat. UBOAT uses the scenario start ID to look up the icon at `Icons/Scenario/<start ID>` and the preview images in `Images/Scenario/<start ID>/`.

## Submarine types, mounts, and equipment

Submarine mods can add these optional columns to `UInterfaceSubmarines`. Entries that specify only `Base Type` can still serve as aliases for stock submarine types.

| Column | Purpose |
| --- | --- |
| `Entity Type` | The unique player entity name registered in the UBOAT entity database, including the ` (Player)` suffix. Set this together with `Interior Scene`. |
| `Interior Scene` | The interior scene asset name supplied by the mod. The scene root must have a stock `PlayerShip` component. |
| `Port Ship` | The boat name shown in the port submarine selector. Set this column to list the custom type in port. |
| `Port Prerequisite` | An optional unlock prerequisite. Use a stock submarine type name or the custom entity name with the ` (Player)` suffix removed. |

The campaign start entry selects the boat by type ID. UBOAT's player submarine loader handles custom entities at campaign start and when changing submarines in port. Saves use the stock loading path. Keep entity names, scene names, and the mods that supply them stable across content builds. Port cards and detail previews use the stock resource keys, based on the entity name with the ` (Player)` suffix removed.

The same `Sandbox.xlsx` can include the sheets below. The first column of each sheet is named `/`.

| Sheet | First-column key | Other columns |
| --- | --- | --- |
| `UInterfaceMounts` | `example.submarine:radar-mast` | Set `Transform` to a relative path under the hull, interior, or a loaded dependency scene root, such as `Exterior/Masts/Radar`. |
| `UInterfaceEquipment` | `example.radar` | Set `Mount` to `radar-mast`. |

An equipment mod can work with multiple boats that provide the same mount ID. For a stock boat or a stock alias, use the actual stock type in the mount key, such as `Type VIIC:radar-mast`.

## C# API

`UInterface.SubmarineApi` has an `ApiVersion` of 1. Use these APIs on the game thread. Data sheet definitions take precedence over script registrations. Assign each ID to a single content mod.

| API | Purpose |
| --- | --- |
| `RegisterSubmarine(id, baseType, entityType, interiorScene)` / `TryGetSubmarine(id, out baseType, out entityType, out interiorScene)` | Register or look up a submarine type. Pass `null` for the entity and scene names when registering a stock alias. |
| `RegisterMount(submarineId, mountId, transformPath)` / `TryGetMount(mountId, out mount)` | Register a mount and find its `Transform`. The lookup checks the hull, interior, and loaded dependency scenes for the current boat in that order. It returns `false` when a dependency scene lookup yields multiple matches. |
| `RegisterEquipment(equipmentId, mountId)` / `TryGetEquipmentMount(equipmentId, out mount)` | Declare the mount an equipment item needs and resolve it by equipment ID. |
| `TryGetCurrentSubmarine(out submarineId, out ship)` | Get the current player boat and its actual submarine type ID. |
| `OnPlayerSubmarineChanged` | Fires during a stock submarine transition with `(previousShip, newShip)`. `newShip` is `null` during unload. |
| `TryGetReadySubmarine(out submarineId, out ship)` / `OnPlayerSubmarineReady` | Get the initialized player boat or receive its `PlayerShip` after campaign start, a submarine change, or save loading. Bind stations and equipment at this point. |
| `OnPlayerSubmarineRestored` | Fires after the stock load and the ready event, with the restored `PlayerShip`. |
| `OnPlayerSubmarineDependencyReady` | Provides the current boat and the dependency scene's root `Transform` when the scene loads after the player boat is ready. |

UBOAT compiles source mods separately at runtime. Inject the game's `IModManager`, use `GetMod("UInterface")?.Assembly` to find the active framework's public types, and create delegates for the methods you need. `ApiVersion` identifies the API signature version. `GetFeatureVersion(featureId)` returns the installed feature version. A value of `0` means the feature is currently unavailable. Set up runtime bindings when the player boat is ready and account for optional features that may become available later. See the [cross-mod calling example](docs/机制接口接入说明.md#由游戏模组管理器加载的内容包) in Chinese.

Equipment mods can use `DeploymentApi` for mast deployment, retraction, and state. `SensorsApi` registers custom sensors and provides state notifications. Detection mods can also retrieve stock detectors and observations.

## Interior stations

The [interior systems and framework architecture guide](docs/舱内机制与框架架构.md) records the six submarine types surveyed, the dependency boundaries, and the integration order. The guide is in Chinese.

`UInterface.StationApi` has an `ApiVersion` of 1. Use it on the game thread and bind it across mods with delegates as described above. The table shows two stock stations with stable action IDs. You can enumerate other stations and query their stock actions.

| Station ID | Action ID | Stock action |
| --- | --- | --- |
| `uinterface.helm` | `uinterface.helm.work` | Assign a crew member to the helm using the stock crew management settings. |
| `uinterface.trim-pump` | `uinterface.trim-pump.toggle` | Assign a crew member to switch the trim pump on or off using its stock trim and drainage logic. |

Custom boats can add station bindings in the `UInterfaceStations` sheet of `Sandbox.xlsx`. The first column is `/`, with keys such as `example.submarine:uinterface.helm`. Set `Mount` to one mount ID or `Mounts` to a comma-separated list. Each mount should point to the stock `Activator` inside the current boat or to the object that holds its associated equipment.

| API | Purpose |
| --- | --- |
| `RegisterStation(submarineId, stationId, mountId)` | Register a station binding from code. Existing data sheet definitions take precedence. |
| `RegisterStationGroup(submarineId, stationId, mountIds)` | Register multiple mounts as a single station group. |
| `RegisterAction(stationId, actionId, actionType)` | Bind an action ID to an existing `CharacterAction` type at the station. |
| `RegisterActionSelector(stationId, actionId, actionType, selector)` | Select among stock action templates that share a type. |
| `RegisterSubmarineAction(submarineId, stationId, actionId, actionType, selector)` | Bind an action for a specific boat type. It takes precedence over the global binding with the same name. |
| `GetStations(stationId)` / `TryGetStation(stationId, out activator)` | Enumerate the current boat's stations or get the stock activator when exactly one station matches. |
| `QueryActions(stationId, actor)` / `TryGetActionState(stationId, actionId, actor, out state)` | Query actions visible to a crew member at that station and check their stock availability. |
| `RequestAction(stationId, actionId, actor)` | Submit a request through the stock crew action entry point. The return value indicates that the game accepted the task or completed a supported immediate action. |
| `RequestTrackedAction` / `RequestTrackedNativeAction` / `TryCancelTrackedAction` | Get the stock action instance, read progress and stage, and cancel the action using the game's stop conditions. |
| `QueryQueuedNativeActions` / `RequestQueuedNativeAction` / `TryCancelQueuedNativeAction` | Work with personal action queues or route tasks to the boat-wide queue when no crew member is specified. |
| `GetActiveQueuedNativeActions` / `TryTrackQueuedNativeAction` | Once the boat is ready, enumerate and track player-assigned tasks in stock queues, including tasks restored from a save. |
| `QueryNativeUtilityActions` / `RequestNativeUtilityAction` | Query and trigger stock utility actions, such as opening equipment help or storage. |
| `TryGetState` / `TryGetStationState` | Read crew assignment, switch position, and operating state by unique ID or station instance. |
| `TryGetStationUsers(station, out users)` | Get the crew members using a station. This also works for stations with no associated stock equipment. |
| `SubscribeStateChanged` / `SubscribeStationStateChanged` | Listen for state changes by unique ID or station instance. Subscriptions are released when changing boats, unloading, or loading a save. |

## Onboard systems

The [systems integration guide](docs/机制接口接入说明.md) covers 12 areas, explains how to use stock and custom components, and records the scope of in-game verification. It is in Chinese. The stock TDC is optional. The torpedo tube API is available independently.

Propulsion mods can use `PropulsionApi.RegisterProvider` to supply propeller power and an optional target speed. Stock telegraph and port workflows require two `PlayerShipEngine` components on the boat. See [propulsion output](docs/机制接口接入说明.md#推进输出) for callback parameters, lifecycle, and a cross-mod example.

Generators and electrical equipment can use `PowerApi.RegisterSource` and `PowerApi.RegisterLoad` to contribute to the stock battery's charge and discharge. A propulsion mod can implement its own generator and use the stock motor and battery. See [power integration](docs/机制接口接入说明.md#电力接入) for rate units and a cross-mod example.

Use `PowerApi.RegisterInterlockedLoad` for equipment that follows the stock compartment power supply. Contacts reported through `SensorContactsApi` appear on the stock standard and tactical charts.

Equipment mods can also use [save state](docs/机制接口接入说明.md#设备状态随档保存), [first-person controls](docs/机制接口接入说明.md#自定义控件桅杆与子系统), [custom station actions](docs/机制接口接入说明.md#工位与原版动作), [shared sensor contacts](docs/机制接口接入说明.md#共享传感器接触), and [torpedo tube requests](docs/机制接口接入说明.md#火控与弹药的职责).

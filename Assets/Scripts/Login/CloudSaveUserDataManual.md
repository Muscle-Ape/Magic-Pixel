# MagicPixel 用户数据云存储操作手册

本文档基于当前登录模块、`MPUser` 本地 ES3 存档结构，以及已经接入的 Unity Cloud Save SDK 整理。目标是在不破坏现有本地存档的前提下，把用户数据逐步同步到 Unity Cloud Save。

当前实现补充：自定义关卡配置数据已经从用户主快照拆分到独立 Player Data Key `mp_custom_level_snapshot_v1`。用户主快照 `mp_user_snapshot_v1` 只保存资产、设置、主线关卡、大图关卡和宠物等常规数据；自定义关卡图片仍然使用 Cloud Save Files。

## 1. 当前状态

### 已具备的能力

- `Packages/manifest.json` 已接入 `com.unity.services.cloudsave`，当前版本为 `3.4.1`。
- 登录模块已经通过 `MPLoginManager` 对外提供统一登录入口。
- Unity Services 初始化在 `MPUnityAuthenticationApi.InitializeAsync()` 内完成。
- 当前 Unity Services Environment 选择规则：
  - Unity Editor：`development`
  - 发布构建：`production`
- 当前用户身份以 Unity Authentication 的 `PlayerId` 为准，可通过 `MPLoginManager.Instance.PlayerId` 获取。
- 当前游戏用户数据主要由 `MPUser` 使用 ES3 保存在本地。

### 关键约束

Cloud Save 玩家数据是挂在 Unity Authentication 玩家身份上的。也就是说，必须先完成登录并拿到稳定的 `PlayerId`，再执行云端读取或保存。

当前项目支持游客登录。游客账号也可以使用 Cloud Save，但游客账号的可恢复性依赖 Unity Authentication 本地 SessionToken 和当前设备。如果玩家清理数据、换设备、卸载后凭据丢失，可能会生成新的游客 `PlayerId`，从而读取不到旧 `PlayerId` 下的云存档。因此，重要数据上线前建议引导玩家绑定账号密码、Google Play Games、Apple 或 Facebook。

## 2. 推荐总体方案

当前阶段不要用 Cloud Save 直接替代 ES3，而是采用“本地优先 + 云端快照同步”的方式：

1. 游戏所有业务仍先读写 `MPUser` 和 ES3。
2. 登录成功后，基于 `PlayerId` 拉取 Cloud Save 快照。
3. 若云端没有快照，则把本地 ES3 数据生成快照并上传。
4. 若云端有快照，则与本地快照比较时间戳、版本和账号信息，再决定覆盖本地、上传本地，或进入冲突处理。
5. 游戏过程中本地数据变化后标记为 dirty，延迟批量上传，避免每次 ES3.Save 都请求网络。
6. 退出游戏、切换账号、登出前尝试 flush 云端保存。

这样做的好处是：离线时游戏仍可正常运行；网络失败不影响本地进度；Cloud Save 只负责跨设备恢复和云备份。

## 3. 当前数据盘点

| 模块 | 本地脚本 | 本地 ES3 Key | 数据内容 | 云同步建议 |
| --- | --- | --- | --- | --- |
| 资源 | `MPUser.Assets.cs` | `key_coins` | 金币，默认 200 | 需要同步，但正式上线前建议服务端校验或 Cloud Code 兜底 |
| 资源 | `MPUser.Assets.cs` | `key_home_reward_ready_at_utc_ticks` | 主页三小时奖励下一次可领取的 UTC 时间 | 需要同步 |
| 资源 | `MPUser.Assets.cs` | `m_ket_diamond` | 钻石 | 需要同步，属于高价值数据 |
| 资源 | `MPUser.Assets.cs` | `key_hint_props` | 提示道具数量 | 需要同步 |
| 资源 | `MPUser.Assets.cs` | `key_love_recover_props` | 生命恢复道具数量 | 需要同步 |
| 设置 | `MPUser.Setting.cs` | `key_isMusic` | 音乐开关 | 可同步，也可按设备本地保存 |
| 设置 | `MPUser.Setting.cs` | `key_isSound` | 音效开关 | 可同步，也可按设备本地保存 |
| 设置 | `MPUser.Setting.cs` | `key_isVibration` | 震动开关 | 可同步，也可按设备本地保存 |
| 主线关卡 | `MPUser.MainLevel.cs` | `key_mainlevel_pass_index` | 主线当前通关下标 | 需要同步 |
| 主线关卡 | `MPUser.MainLevel.cs` | `key_mainlevel_unlocklist` | 主线已解锁关卡 ID | 需要同步 |
| 主线关卡 | `MPUser.MainLevel.cs` | `key_mainlevel_passlist` | 主线已通关关卡 ID | 需要同步 |
| 主线关卡 | `MPUser.MainLevel.cs` | `key_mainlevel_stars` | 主线关卡最高星数 | 需要同步 |
| 大图关卡 | `MPUser.LargeImage.cs` | `m_key_largeimagelevel_pass_index` | 大图模式当前通关下标 | 需要同步 |
| 大图关卡 | `MPUser.LargeImage.cs` | `m_key_largeimagelevel_unlocklist` | 大图已解锁关卡 ID | 需要同步 |
| 大图关卡 | `MPUser.LargeImage.cs` | `m_key_largeimagelevel_passlist` | 大图已通关关卡 ID | 需要同步 |
| 大图关卡 | `MPUser.LargeImage.cs` | `key_largeimagelevel_stars` | 大图关卡最高星数 | 需要同步 |
| 大图关卡 | `MPUser.LargeImage.cs` | `key_largeimagelevel_coin_award_claimed` | 已领取金币奖励的关卡 ID | 需要同步，避免重复领奖 |
| 自定义关卡 | `MPUser.CustomLevel.cs` | `key_customlevel_json` | 自定义关卡配置 JSON | 需要同步 |
| 自定义关卡 | `MPUser.CustomLevel.cs` | `key_customlevel_passlist` | 自定义关卡通关列表 | 需要同步 |
| 自定义关卡图片 | `MPUser.CustomLevel.cs` | `Application.persistentDataPath/CustomLevels` | 自定义关卡 PNG/Icon 文件 | 建议使用 Cloud Save Files，不建议塞进 Player Data |
| 关卡进度缓存 | `MPUser.LevelProgressCache.cs` | `key_mainlevel_progress_cache_{id}` | 主线未完成局内进度 | 可选同步，建议先不同步 |
| 关卡进度缓存 | `MPUser.LevelProgressCache.cs` | `key_largeimagelevel_progress_cache_{id}` | 大图未完成局内进度 | 可选同步，建议先不同步 |
| 宠物 | `MPUser.Pets.cs` | `key_selected_pet_id` | 当前选中宠物 ID | 需要同步 |

## 4. 推荐 Cloud Save Key 设计

初期建议使用一个主快照 Key，减少多 Key 同步时的冲突面：

| Cloud Save Key | 类型 | Access Class | 用途 |
| --- | --- | --- | --- |
| `mp_user_snapshot_v1` | Player Data | Default | 保存除图片文件外的完整用户快照 |
| `mp_user_snapshot_meta_v1` | Player Data | Default | 可选，保存最近同步时间、客户端版本、调试信息 |
| `mp_custom_level_image_{levelId}` | Player Files | Player Files | 自定义关卡原图 |
| `mp_custom_level_icon_{levelId}` | Player Files | Player Files | 自定义关卡列表图标 |

后续如果数据量变大，可以把 `mp_user_snapshot_v1` 拆成多个 Key，例如 `mp_assets_v1`、`mp_level_progress_v1`、`mp_pets_v1`。当前项目阶段建议先保持单快照，后续迁移更简单。

容量边界：

- Cloud Save Player Data 是键值对存储，默认、公开、受保护 3 种访问类型各自都有容量限制。
- 官方当前限制是每种访问类型最多 2000 个 Key，每种访问类型总量最多 5 MiB。
- 因此 `mp_user_snapshot_v1` 只适合保存结构化 JSON 数据，不适合保存 PNG、截图、关卡缩略图等二进制文件。
- 自定义关卡图片应使用 Cloud Save Files；如果后续自定义关卡数量明显增加，自定义关卡配置也可以从主快照拆成单独 Key 或文件。

## 5. 推荐快照结构

建议新增 DTO，例如 `MPUserCloudSnapshot`，用于隔离 Cloud Save 数据结构和 `MPUser` 内部字段：

```csharp
public class MPUserCloudSnapshot
{
    public int schemaVersion;
    public string playerId;
    public string unityEnvironment;
    public string lastLoginProvider;
    public bool hasBoundIdentity;
    public long updatedAtUtcTicks;
    public string clientVersion;

    public MPUserAssetsSnapshot assets;
    public MPUserSettingsSnapshot settings;
    public MPUserMainLevelSnapshot mainLevel;
    public MPUserLargeImageLevelSnapshot largeImageLevel;
    public MPUserCustomLevelSnapshot customLevel;
    public MPUserPetsSnapshot pets;
}
```

字段说明：

- `schemaVersion`：存档结构版本，后续结构变化时做迁移。
- `playerId`：保存时的 Unity Authentication PlayerId，用于排查账号切换问题。
- `unityEnvironment`：`development` 或 `production`，方便后台核对数据写入环境。
- `lastLoginProvider`：最近登录方式，用于排查游客/绑定账号同步问题。
- `hasBoundIdentity`：当前账号是否绑定正式身份。
- `updatedAtUtcTicks`：快照最后更新时间，冲突处理的基础字段。
- `clientVersion`：`Application.version`，用于判断旧版本客户端上传的数据。

## 6. 登录系统联动流程

当前启动链路：

1. `MPLauncher.LaunchAsync()`
2. 初始化 YooAsset。
3. 初始化 `UIManager`。
4. `yield return MPLoginManager.Instance.Initialize()`。
5. 登录流程返回 `EnterGame` 后进入 `MPLauncher.EnterGame()`。
6. `MPDataManager.Instance.Initialize()` 初始化静态配置。
7. `MPUser.instance.Initialization()` 从 ES3 加载用户本地数据。
8. 打开 `MPHomeView`。

推荐云存档接入点：

```text
MPLauncher.EnterGame()
    -> MPDataManager.Instance.Initialize()
    -> MPUser.instance.Initialization()
    -> MPCloudSaveManager.Instance.InitializeAfterUserLoadedAsync()
    -> UIManager.Inst.ShowWindow<MPHomeView>()
```

如果担心云端拉取耗时影响启动体验，可以先进入首页，但加一个同步中遮罩或首页顶部状态提示。不要在 `MPUser.instance.Initialization()` 之前应用云端数据，因为主线、大图和宠物数据初始化依赖 `MPDataManager` 的静态配置。

## 7. 标准操作流程

### 首次登录并上传本地数据

1. 玩家启动游戏。
2. 登录模块完成匿名登录或正式账号登录。
3. 获取 `MPLoginManager.Instance.PlayerId`。
4. 初始化 `MPUser` 本地数据。
5. Cloud Save 读取 `mp_user_snapshot_v1`。
6. 如果云端不存在：
   - 从 `MPUser` 构建本地快照。
   - 写入 `mp_user_snapshot_v1`。
   - 保存返回的 `writeLock` 到本地云同步元数据。

### 有云端数据时恢复

1. 登录成功后读取 `mp_user_snapshot_v1`。
2. 如果云端快照 `playerId` 与当前 `PlayerId` 不一致，停止自动应用并记录错误。
3. 比较云端与本地 `updatedAtUtcTicks`。
4. 云端更新：应用云端快照到本地 ES3 和 `MPUser`。
5. 本地更新：上传本地快照。
6. 双端都有变化且无法判断时：进入冲突处理 UI。

### 游戏过程中保存

1. 业务代码继续调用 `MPUser` 的方法，例如 `AddCoins`、`MainLevelPass`、`PetUnlock`。
2. 这些方法成功写入 ES3 后，通知云同步层 `MarkDirty()`。
3. 云同步层做 5 到 10 秒 debounce。
4. 网络可用且已登录时批量上传最新快照。
5. 网络失败时保留 dirty 标记，下次登录、回到前台或定时重试。

### 登出或切换账号

1. 调用 `MPLoginManager.Instance.LogoutAsync()` 前，先尝试 flush 当前账号云存档。
2. 登出完成后清理云同步层内存状态，包括当前 `PlayerId`、dirty 标记、writeLock。
3. 打开登录页面。
4. 玩家选择新账号登录后，重新按当前 `PlayerId` 拉取对应云存档。

## 8. Cloud Save SDK 常用调用

保存普通玩家数据：

```csharp
using System.Collections.Generic;
using Unity.Services.CloudSave;

Dictionary<string, object> data = new Dictionary<string, object>
{
    { "mp_user_snapshot_v1", snapshot }
};

Dictionary<string, string> writeLocks = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
string newWriteLock = writeLocks["mp_user_snapshot_v1"];
```

读取玩家数据：

```csharp
using System.Collections.Generic;
using Unity.Services.CloudSave;

Dictionary<string, Unity.Services.CloudSave.Models.Item> result =
    await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string>
    {
        "mp_user_snapshot_v1"
    });

if (result.TryGetValue("mp_user_snapshot_v1", out var item))
{
    MPUserCloudSnapshot snapshot = item.Value.GetAs<MPUserCloudSnapshot>();
    string writeLock = item.WriteLock;
}
```

使用写锁保存，避免覆盖其他设备刚写入的数据：

```csharp
using System.Collections.Generic;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;

Dictionary<string, SaveItem> data = new Dictionary<string, SaveItem>
{
    { "mp_user_snapshot_v1", new SaveItem(snapshot, lastWriteLock) }
};

Dictionary<string, string> writeLocks = await CloudSaveService.Instance.Data.Player.SaveAsync(data);
lastWriteLock = writeLocks["mp_user_snapshot_v1"];
```

保存自定义关卡图片文件：

```csharp
using System.IO;
using Unity.Services.CloudSave;

byte[] imageBytes = File.ReadAllBytes(localImagePath);
await CloudSaveService.Instance.Files.Player.SaveAsync($"mp_custom_level_image_{levelId}", imageBytes);
```

## 9. 冲突处理规则

Cloud Save 提供 `writeLock`。读取和保存数据时都可以拿到新的写锁；保存时带上旧写锁，如果云端已经被其他设备更新，服务会返回错误，提示当前写入会覆盖别人改过的数据。

推荐处理方式：

1. 捕获写锁冲突。
2. 重新读取云端 `mp_user_snapshot_v1`。
3. 比较本地快照和云端快照。
4. 根据模块选择合并或让玩家选择。

模块级合并建议：

| 模块 | 冲突策略 |
| --- | --- |
| 主线关卡 | 解锁列表、通关列表取并集；星数取每关最大值；通关下标取最大值 |
| 大图关卡 | 解锁列表、通关列表、奖励领取列表取并集；星数取每关最大值；通关下标取最大值 |
| 设置 | 使用当前设备设置，或让玩家选择是否跟随云端 |
| 资源货币 | 不建议简单取最大值；初期可按更新时间选择，正式上线建议服务端校验 |
| 自定义关卡 | 按关卡 ID 合并；同 ID 冲突时需要引入单关卡 `updatedAtUtcTicks` |
| 宠物 | 仅同步当前选中宠物 ID；冲突时使用更新时间较新的选择 |
| 关卡进度缓存 | 建议不参与跨设备冲突合并，必要时按更新时间选择 |

## 10. 游客账号和数据安全说明

### 游客模式不会天然丢云存档

Cloud Save 的数据挂在当前 Unity Authentication `PlayerId` 下。只要本地会话能恢复到同一个 `PlayerId`，游客账号也能读到之前的云存档。

### 游客模式的风险

游客没有绑定外部身份。一旦本地 SessionToken 丢失，玩家可能拿到新的游客 `PlayerId`。这时旧云存档仍在 Unity 后台，但客户端无法通过新游客身份直接读到旧玩家的数据。

### 推荐做法

- 游客可以先玩，系统自动云同步。
- 当玩家产生重要进度或付费资产时，引导绑定账号。
- 绑定成功后继续沿用同一个 Unity Authentication PlayerId，云存档不会迁移到另一个玩家下面。
- 设置页的 `LogIn` 按钮应优先用于绑定，而不是强制创建新账号。

## 11. Dashboard 检查流程

### Editor development 环境

1. 在 Unity Editor 运行游戏。
2. 通过游客登录进入游戏。
3. 控制台记录当前 `MPLoginManager.Instance.PlayerId`。
4. 修改一项容易识别的数据，例如金币、关卡通关或宠物选择。
5. 触发云端上传。
6. 打开 Unity Dashboard。
7. 选择当前 Unity Project。
8. 切换到 `development` Environment。
9. 在 Authentication / Player Management 中搜索当前 `PlayerId`，确认玩家存在。
10. 在 Cloud Save 的 Player Data 或 Data Explorer 中查看 `mp_user_snapshot_v1`。

### 发布 production 环境

1. 构建真机包或发布包。
2. 使用同样的登录方式进入游戏。
3. 记录 `PlayerId`。
4. 上传云存档。
5. Unity Dashboard 切换到 `production` Environment。
6. 检查 Authentication 玩家和 Cloud Save 数据。

如果 Editor 数据出现在 production，或发布包数据出现在 development，优先检查 `MPUnityAuthenticationApi.GetUnityEnvironmentName()` 和 Unity Dashboard 当前 Environment。

## 12. 测试用例清单

| 用例 | 操作 | 预期结果 |
| --- | --- | --- |
| 首次游客登录 | 清空本地数据后运行 Editor | 登录成功，Cloud Save 创建 `mp_user_snapshot_v1` |
| 二次游客登录 | 不清理本地数据，再次运行 | 恢复同一个 PlayerId，读取同一份云存档 |
| 本地进度上传 | 增加金币或通关关卡后上传 | Dashboard 中快照内容更新 |
| 云端恢复 | 另一台设备或清理本地 ES3 后，用同一绑定账号登录 | 拉取云端快照并恢复本地数据 |
| 游客丢凭据 | 清理 Unity Authentication 本地 Session 后匿名登录 | 可能产生新 PlayerId，读取不到旧游客云存档 |
| 绑定账号 | 游客进度存在时绑定 Google Play Games 或账号密码 | PlayerId 不变，原云存档继续可读写 |
| 写锁冲突 | 两台设备同时修改同一账号数据 | 后保存的一端触发冲突，重新拉取并合并或提示 |
| 断网保存 | 断网时修改本地数据 | ES3 正常保存，云同步 dirty 保留 |
| 恢复网络 | 断网后恢复网络 | dirty 数据自动重试上传 |
| 登出切号 | 当前账号修改数据后登出，再登录另一个账号 | 旧账号先 flush，新账号拉取自己的云存档 |

## 13. 推荐代码落点

后续实现云同步功能时，建议新增独立模块，不要把 Cloud Save SDK 调用散落在 UI 或 `MPUser` 里。

推荐结构：

| 类型 | 建议路径 | 职责 |
| --- | --- | --- |
| `IMPCloudSaveApi` | `Assets/Scripts/CloudSave/Abstractions` | 抽象 Cloud Save SDK 的保存、读取、文件上传能力 |
| `MPUnityCloudSaveApi` | `Assets/Scripts/CloudSave/Api` | 封装 `CloudSaveService.Instance` |
| `MPCloudSaveManager` | `Assets/Scripts/CloudSave/Core` | 云同步门面，负责初始化、拉取、上传、dirty、flush、登出清理 |
| `MPUserCloudSnapshot` | `Assets/Scripts/CloudSave/Models` | 云端用户快照 DTO |
| `MPUserCloudSnapshotBuilder` | `Assets/Scripts/CloudSave/Core` | 负责 `MPUser` 与快照之间的转换 |
| `MPCloudSaveConflictResolver` | `Assets/Scripts/CloudSave/Core` | 负责本地和云端快照的合并策略 |
| `MPCloudSaveLocalMetaRepository` | `Assets/Scripts/CloudSave/Persistence` | 使用 ES3 保存 writeLock、最后同步时间、dirty 标记 |

推荐事件绑定：

- `MPLoginManager.Instance.LoginSucceeded`：登录成功后准备云同步。
- `MPLoginManager.Instance.LoggedOut`：清理当前云同步状态。
- `Application.pause` / `OnApplicationQuit`：flush dirty 数据。
- `MPUser` 内部每次 ES3 保存后：调用 `MPCloudSaveManager.Instance.MarkDirty(MPCloudSaveDirtyReason reason)`。

`MPUser` 当前字段大多是 private，后续实现快照导入导出时建议给 `MPUser` 增加专门的快照方法，而不是让云同步模块反射私有字段：

```csharp
public MPUserCloudSnapshot CreateCloudSnapshot();
public void ApplyCloudSnapshot(MPUserCloudSnapshot snapshot);
```

这两个方法可以写成 `MPUser.CloudSave.cs` partial 文件，保持和现有 `MPUser.Assets.cs`、`MPUser.MainLevel.cs` 等文件风格一致。

## 14. 异常和降级策略

| 场景 | 处理方式 |
| --- | --- |
| 未登录 | 不执行 Cloud Save，等待 `LoginSucceeded` |
| Unity Services 未初始化 | 由登录模块初始化；云同步层只检查状态，不重复抢初始化流程 |
| 网络异常 | 保留本地 ES3 数据和 dirty 标记，延迟重试 |
| 限流 | 增加退避时间，不要连续上传 |
| 数据反序列化失败 | 不覆盖本地数据，记录日志，提示玩家或上传本地快照到备用 Key |
| schemaVersion 过旧 | 执行迁移；不能迁移时保留本地数据 |
| playerId 不一致 | 停止自动应用云端数据，记录严重日志 |
| 写锁冲突 | 重新读取云端并进入合并策略 |

## 15. 上线前检查清单

- Unity Dashboard 中 `development` 和 `production` Environment 都已创建。
- Authentication 的登录提供商已按发布平台配置。
- Cloud Save 已启用。
- Editor 写入 `development`，真机包写入 `production`。
- 匿名登录、账号密码登录、Google Play Games 登录都能拿到 PlayerId。
- 游客绑定正式账号后 PlayerId 不变。
- 云端快照包含 `schemaVersion`、`playerId`、`updatedAtUtcTicks`。
- 资源货币类数据有防刷策略或后续 Cloud Code 规划。
- 自定义关卡图片走 Cloud Save Files，不塞进 Player Data。
- 断网、限流、写锁冲突都有日志和降级处理。
- 登出、切换账号、重新登录不会把 A 账号数据写到 B 账号。

## 16. 官方参考

- Unity Cloud Save Get started：<https://docs.unity.com/en-us/cloud-save/get-started>
- Unity Cloud Save 玩家数据：<https://docs.unity.com/zh-cn/cloud-save/concepts/player-data>
- Unity Cloud Save Unity SDK 教程：<https://docs.unity.com/zh-cn/cloud-save/tutorials/unity-sdk>
- Unity Cloud Save 写锁：<https://docs.unity.com/zh-cn/cloud-save/concepts/write-locks>
- Unity Authentication 工作机制：<https://docs.unity.com/zh-cn/authentication/how-authentication-works>

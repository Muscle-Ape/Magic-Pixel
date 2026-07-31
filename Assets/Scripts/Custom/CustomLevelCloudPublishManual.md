# MagicPixel 自定义关卡云发布操作文档

本文档基于当前项目的自定义模式、登录系统和 Unity Cloud Save 接入情况，整理一套“玩家上传自定义关卡到 Unity Cloud，其他玩家浏览体验，玩家点赞，作者撤销发布”的实现方案。

当前项目已经具备个人自定义关卡云存储能力，但“公共发布”不是同一个问题。个人云存储只服务当前玩家自己；公共发布需要让其他玩家可查询、可读取、可点赞，因此必须引入服务端逻辑和公开目录数据。

## 1. 当前项目已有能力

### 1.1 本地自定义关卡

当前自定义关卡主要由以下脚本负责：

| 脚本 | 作用 |
| --- | --- |
| `MPCustomView` | 自定义关卡编辑、保存、生成图片 |
| `MPCustomLevelInfo` | 自定义关卡结构数据 |
| `MPCustomLevelColorInfo` | 自定义关卡颜色格子数据 |
| `MPUser.CustomLevel.cs` | 本地 ES3 保存、读取、删除、通关状态 |
| `MPUser.CloudSave.cs` | 将本地自定义关卡转换成 Cloud Save 快照 |
| `MPCloudSaveManager` | 登录后同步个人云存档 |

当前本地自定义关卡核心数据：

```json
{
  "id": "level_custom_1",
  "title": "Undefined",
  "size": 5,
  "block": [0, 1, 2],
  "colors": [
    {
      "index": 0,
      "color": "#FF8E64FF"
    }
  ]
}
```

### 1.2 个人云存储

当前 Cloud Save 已经拆分为：

| Key | 类型 | 用途 |
| --- | --- | --- |
| `mp_user_snapshot_v1` | Player Data | 玩家资产、设置、主线关卡、大图关卡、宠物 |
| `mp_custom_level_snapshot_v1` | Player Data | 当前玩家自己的自定义关卡数据 |
| `mp_custom_level_image_{levelId}` | Player Files | 当前玩家自己的自定义关卡大图 |
| `mp_custom_level_icon_{levelId}` | Player Files | 当前玩家自己的自定义关卡图标 |

注意：当前这些数据都是“玩家个人数据”。其他玩家不能直接通过客户端读取另一个玩家的 Player Data 或 Player Files。

## 2. 为什么公共发布不能直接复用当前个人云存储

当前 `mp_custom_level_snapshot_v1` 挂在 Unity Authentication 的 `PlayerId` 下，它适合做跨设备恢复和个人备份。

公共发布需要满足：

1. 玩家 A 上传关卡后，玩家 B 可以看到。
2. 玩家 B 可以读取关卡结构并进入体验。
3. 玩家 B 可以点赞。
4. 玩家 A 可以撤销自己发布的关卡。
5. 客户端不能伪造点赞数、不能撤销别人的关卡。

因此推荐使用：

| 服务 | 用途 |
| --- | --- |
| Authentication | 识别上传者、点赞者、撤销者 |
| Cloud Code | 服务端校验、写入公开数据、处理点赞、处理撤销 |
| Cloud Save Custom Data 或 Game Data | 保存公共关卡目录、关卡详情、点赞记录 |
| Cloud Save Player Data | 继续保存玩家自己的草稿和个人备份 |

官方文档说明 Cloud Code 可以使用 service token 访问跨玩家数据和 Game Data，并可查询 Cloud Save 中配置索引的数据。Cloud Save Files 适合玩家自己的大文件备份，但玩家文件不能被其他玩家直接访问，所以公共关卡不要依赖 Player Files 作为展示来源。

## 3. 推荐总体架构

```text
玩家客户端
    |
    | 调用 Cloud Code
    v
Cloud Code
    | 校验身份、校验关卡、处理写锁、处理点赞、防重复
    v
Cloud Save 公共数据
    | public level record
    | like record
    | owner index
    v
其他玩家客户端
```

### 3.1 客户端职责

客户端只做：

1. 编辑和本地保存自定义关卡。
2. 点击上传时调用 Cloud Code。
3. 展示公共关卡列表。
4. 点击公共关卡后进入游戏体验。
5. 点击点赞时调用 Cloud Code。
6. 作者点击撤销时调用 Cloud Code。

客户端不直接修改公共关卡数据，不直接增加点赞数。

### 3.2 Cloud Code 职责

Cloud Code 负责：

1. 校验玩家是否登录。
2. 校验关卡数据是否合法。
3. 生成公共关卡 ID。
4. 写入公共关卡数据。
5. 查询公共关卡列表。
6. 查询公共关卡详情。
7. 处理点赞，保证同一玩家不能重复点赞。
8. 处理撤销发布，保证只有作者能撤销。

### 3.3 Cloud Save 职责

Cloud Save 保存：

1. 公共关卡详情。
2. 公共关卡列表查询字段。
3. 点赞记录。
4. 作者发布索引。

## 4. 公共关卡数据结构设计

### 4.1 公共关卡状态

```csharp
public enum MPCustomLevelPublishStatus
{
    Published = 0,
    Revoked = 1,
    Deleted = 2
}
```

推荐先只做 `Published` 和 `Revoked`。

`Revoked` 表示作者撤销发布，普通列表不再展示，但后台仍可保留数据，方便排查和恢复。

### 4.2 公共关卡记录

```csharp
public class MPCustomLevelPublicRecord
{
    public int schemaVersion;
    public string publicLevelId;
    public string sourceLocalLevelId;
    public string ownerPlayerId;
    public string ownerDisplayName;
    public string title;
    public int size;
    public List<int> block;
    public List<MPCustomLevelColorInfo> colors;
    public int likeCount;
    public int playCount;
    public int status;
    public long createdAtUtcTicks;
    public long updatedAtUtcTicks;
    public string clientVersion;
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `publicLevelId` | 公共关卡 ID，不直接使用本地 `level_custom_1`，避免不同玩家冲突 |
| `sourceLocalLevelId` | 作者本地关卡 ID，便于排查 |
| `ownerPlayerId` | 上传者 PlayerId |
| `ownerDisplayName` | 展示名，可后续接入昵称系统 |
| `title` | 关卡标题 |
| `size` | 5 或 10 |
| `block` | 需要玩家填充的格子 |
| `colors` | 颜色格子 |
| `likeCount` | 点赞数，只能由 Cloud Code 修改 |
| `playCount` | 体验次数，只能由 Cloud Code 修改 |
| `status` | 发布状态 |
| `createdAtUtcTicks` | 发布时间 |
| `updatedAtUtcTicks` | 最后更新时间 |

### 4.3 点赞记录

```csharp
public class MPCustomLevelLikeRecord
{
    public string publicLevelId;
    public string playerId;
    public long createdAtUtcTicks;
}
```

点赞记录建议使用单独 Key：

```text
mp_custom_level_like_{publicLevelId}_{playerId}
```

这样可以判断某个玩家是否已经点赞，避免重复点赞。

## 5. Cloud Save Key 设计

### 5.1 公共关卡详情

```text
mp_public_custom_level_{publicLevelId}
```

保存 `MPCustomLevelPublicRecord`。

### 5.2 点赞记录

```text
mp_custom_level_like_{publicLevelId}_{playerId}
```

保存 `MPCustomLevelLikeRecord`。

### 5.3 作者发布索引

可选：

```text
mp_player_published_custom_levels_v1
```

保存当前玩家已经发布的公共关卡 ID 列表。

如果使用 Cloud Save 查询并建立 `ownerPlayerId` 索引，则这个 Key 可以不做。

## 6. Cloud Save 索引建议

为了支持公共关卡列表，需要在 Unity Dashboard 的 Cloud Save 中提前创建索引。

推荐索引：

| 字段 | 排序 | 用途 |
| --- | --- | --- |
| `status` | Ascending | 只查询已发布关卡 |
| `createdAtUtcTicks` | Descending | 最新发布 |
| `likeCount` | Descending | 热门排序 |
| `ownerPlayerId` | Ascending | 查询某个玩家发布的关卡 |

注意：Cloud Save 查询通常只会对索引创建之后写入的数据生效，所以索引应在正式测试前先建好。

## 7. Cloud Code 接口设计

### 7.1 发布关卡

```text
PublishCustomLevel(levelInfo)
```

输入：

```json
{
  "sourceLocalLevelId": "level_custom_2",
  "title": "Undefined",
  "size": 5,
  "block": [],
  "colors": []
}
```

流程：

1. 检查 `context.playerId` 是否存在。
2. 校验 `size` 只能是 5 或 10。
3. 校验 `colors` 不超过 `size * size`。
4. 校验 `colors.index` 不重复、不越界。
5. 校验 `block` 不重复、不越界。
6. 生成 `publicLevelId`。
7. 写入公共关卡详情。
8. 返回 `publicLevelId`。

返回：

```json
{
  "success": true,
  "publicLevelId": "public_level_xxx"
}
```

客户端操作：

1. 玩家点击上传。
2. 读取本地 `MPCustomLevelInfo`。
3. 调用 `PublishCustomLevel`。
4. 成功后把本地关卡标记为“已上传”。

### 7.2 获取公共关卡列表

```text
GetPublishedCustomLevels(sortType, pageSize, cursor)
```

排序建议：

| sortType | 说明 |
| --- | --- |
| `Latest` | 按 `createdAtUtcTicks` 倒序 |
| `Popular` | 按 `likeCount` 倒序 |

返回字段建议只包含列表展示需要的数据：

```json
{
  "items": [
    {
      "publicLevelId": "public_level_xxx",
      "title": "Undefined",
      "ownerDisplayName": "Guest",
      "size": 5,
      "likeCount": 12,
      "playCount": 31,
      "createdAtUtcTicks": 639209967361431000
    }
  ],
  "nextCursor": "..."
}
```

### 7.3 获取公共关卡详情

```text
GetPublishedCustomLevel(publicLevelId)
```

流程：

1. 读取公共关卡详情。
2. 检查 `status == Published`。
3. 返回完整结构数据。

客户端收到后可以转换为当前项目已有的 `MPCustomLevelInfo`，然后复用现有游戏入口进行体验。

### 7.4 体验关卡

```text
PlayPublishedCustomLevel(publicLevelId)
```

最小实现：

1. 客户端先调用 `GetPublishedCustomLevel` 获取详情。
2. 客户端打开游戏界面体验。

进阶实现：

1. 玩家点击体验时调用 `PlayPublishedCustomLevel`。
2. Cloud Code 将 `playCount + 1`。
3. 返回关卡详情。

### 7.5 点赞关卡

```text
LikePublishedCustomLevel(publicLevelId)
```

流程：

1. 检查玩家是否登录。
2. 读取关卡详情。
3. 检查关卡是否 `Published`。
4. 检查点赞记录 `mp_custom_level_like_{publicLevelId}_{playerId}` 是否存在。
5. 不存在则创建点赞记录。
6. 将关卡 `likeCount + 1`。
7. 使用写锁处理并发点赞。

返回：

```json
{
  "success": true,
  "liked": true,
  "likeCount": 13
}
```

如果已经点过赞：

```json
{
  "success": true,
  "liked": true,
  "likeCount": 12,
  "message": "Already liked"
}
```

### 7.6 撤销发布

```text
RevokePublishedCustomLevel(publicLevelId)
```

流程：

1. 检查玩家是否登录。
2. 读取公共关卡详情。
3. 检查 `ownerPlayerId == context.playerId`。
4. 将 `status` 改为 `Revoked`。
5. 更新 `updatedAtUtcTicks`。
6. 返回成功。

普通公共列表只查询 `status == Published`，撤销后的关卡不会再显示。

## 8. 客户端界面操作流程

### 8.1 作者上传

```text
MPCustomLevelView
    -> 选择本地自定义关卡
    -> 点击 Upload
    -> MPCustomLevelPublishManager.PublishAsync(levelInfo)
    -> Cloud Code PublishCustomLevel
    -> 成功后刷新本地状态
```

建议 UI 状态：

| 状态 | 展示 |
| --- | --- |
| 未上传 | 显示 `Upload` |
| 上传中 | 按钮禁用，显示 loading |
| 已上传 | 显示 `Uploaded` 和 `Revoke` |
| 已撤销 | 显示 `Upload Again` |

### 8.2 其他玩家体验

```text
PublicCustomLevelView
    -> 拉取公共关卡列表
    -> 点击某个关卡
    -> GetPublishedCustomLevel
    -> 转换成 MPCustomLevelInfo
    -> 打开 MPGameView
```

现有 `MPGameView` 已经支持 `isCustomLevel` 和 `customLevelInfo`，因此可以复用当前自定义关卡游玩流程。

### 8.3 点赞

```text
PublicCustomLevelDetailView
    -> 点击 Like
    -> LikePublishedCustomLevel
    -> 刷新 likeCount
```

点赞按钮应避免连续点击：

1. 点击后立即禁用按钮。
2. 等 Cloud Code 返回。
3. 返回已点赞后保持选中态。

### 8.4 作者撤销

```text
MyPublishedCustomLevelView
    -> 点击 Revoke
    -> 二次确认
    -> RevokePublishedCustomLevel
    -> 本地状态改为 Revoked
```

撤销不建议直接物理删除，先做软删除。

## 9. 客户端新增代码建议

### 9.1 新增模型

建议路径：

```text
Assets/Scripts/Custom/CloudPublish/Models
```

建议类：

| 类 | 作用 |
| --- | --- |
| `MPCustomLevelPublicRecord` | 公共关卡详情 |
| `MPCustomLevelPublishResult` | 上传返回结果 |
| `MPCustomLevelListResult` | 公共列表返回结果 |
| `MPCustomLevelLikeResult` | 点赞返回结果 |
| `MPCustomLevelPublishStatus` | 发布状态枚举 |

### 9.2 新增 API 抽象

建议路径：

```text
Assets/Scripts/Custom/CloudPublish/Abstractions
```

接口：

```csharp
public interface IMPCustomLevelPublishApi
{
    Task<MPCustomLevelPublishResult> PublishAsync(MPCustomLevelInfo levelInfo, CancellationToken cancellationToken);
    Task<MPCustomLevelListResult> GetListAsync(string sortType, int pageSize, string cursor, CancellationToken cancellationToken);
    Task<MPCustomLevelPublicRecord> GetDetailAsync(string publicLevelId, CancellationToken cancellationToken);
    Task<MPCustomLevelLikeResult> LikeAsync(string publicLevelId, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(string publicLevelId, CancellationToken cancellationToken);
}
```

### 9.3 新增管理器

建议路径：

```text
Assets/Scripts/Custom/CloudPublish/Core/MPCustomLevelPublishManager.cs
```

职责：

1. 统一调用 Cloud Code。
2. 处理 loading 状态。
3. 处理错误提示。
4. 维护本地发布状态缓存。
5. 通知 UI 刷新。

### 9.4 UI 新增入口

建议：

| 页面 | 新增内容 |
| --- | --- |
| `MPCustomLevelView` | 本地关卡 Upload/Revoke 按钮 |
| `MPHomeView` 或自定义入口 | Public Levels 入口 |
| `MPPublicCustomLevelView` | 公共关卡列表 |
| `MPPublicCustomLevelDetailView` | 详情、体验、点赞 |
| `MPMyPublishedCustomLevelView` | 我上传的关卡、撤销 |

## 10. 服务端校验规则

Cloud Code 发布时必须校验：

| 校验项 | 规则 |
| --- | --- |
| 登录状态 | 必须有 `context.playerId` |
| 标题 | 非空，长度建议 1 到 24 |
| size | 只能是 5 或 10 |
| block | 索引不重复，范围为 `[0, size * size)` |
| colors | 索引不重复，范围为 `[0, size * size)` |
| color | 必须是 `#RRGGBBAA` 或项目认可格式 |
| 数据量 | colors 最多 `size * size` 条 |
| 发布频率 | 建议限制，例如每分钟最多 3 次 |
| 撤销权限 | 只能作者本人撤销 |
| 点赞 | 同一玩家同一关卡只能点赞一次 |

## 11. 图片与图标处理

当前项目会把自定义关卡图片保存到 Player Files：

```text
mp_custom_level_image_{levelId}
mp_custom_level_icon_{levelId}
```

但 Player Files 是玩家个人文件，不适合作为公共关卡图片来源。

推荐公共关卡列表图标这样处理：

1. 公共关卡只保存 `size` 和 `colors`。
2. 客户端拉取列表或详情后，根据 `colors` 本地生成预览图。
3. 复用当前 `CreateCustomLevelImageTextureFromConfig` 的思路生成 Texture2D。

这样不需要公共图片存储，也避免跨玩家读取 Player Files 的限制。

## 12. 推荐上线前检查清单

- Unity Dashboard 已启用 Authentication、Cloud Save、Cloud Code。
- Cloud Save 公共关卡查询索引已经提前创建。
- Cloud Code 函数已部署到 `development` 环境并通过测试。
- Editor 使用 `development`，发布包使用 `production`。
- 客户端不能直接写公共关卡数据。
- 点赞由 Cloud Code 执行，不由客户端直接加数字。
- 撤销发布必须校验作者。
- 公共列表只显示 `status == Published`。
- 自定义关卡数据会过滤重复 index。
- 10x10 最大 colors 数量为 100，数据体积在可控范围内。
- 游客账号允许上传前，需要确认产品策略；正式上线建议引导绑定账号。

## 13. 测试用例

| 用例 | 操作 | 预期 |
| --- | --- | --- |
| 发布 5x5 关卡 | 创建 5x5 并上传 | Cloud Save 出现公共关卡记录 |
| 发布 10x10 关卡 | 创建 10x10 并上传 | colors 不超过 100 |
| 其他玩家体验 | 另一个账号打开公共列表并进入 | 能正常打开自定义关卡 |
| 点赞 | 玩家 B 点赞玩家 A 的关卡 | likeCount +1 |
| 重复点赞 | 玩家 B 再次点赞同一关卡 | likeCount 不重复增加 |
| 作者撤销 | 玩家 A 撤销自己的关卡 | 公共列表不再显示 |
| 非作者撤销 | 玩家 B 尝试撤销玩家 A 的关卡 | Cloud Code 返回失败 |
| 越界数据 | 上传 index 超过范围的数据 | Cloud Code 拒绝 |
| 重复 index | 上传重复 colors index | Cloud Code 清洗或拒绝 |
| 断网 | 上传过程中断网 | UI 显示失败，本地数据不丢失 |

## 14. 分阶段实施建议

### 第一阶段：最小可用版本

1. 新增 Cloud Code：发布、列表、详情、点赞、撤销。
2. 客户端新增 `MPCustomLevelPublishManager`。
3. `MPCustomLevelView` 增加 Upload/Revoke。
4. 新增公共列表页。
5. 公共关卡详情复用现有 `MPGameView` 进入体验。

### 第二阶段：体验优化

1. 增加热门排序。
2. 增加我的发布列表。
3. 增加点赞状态缓存。
4. 增加上传冷却和错误提示。
5. 增加内容举报。

### 第三阶段：安全与运营

1. 接入内容审核。
2. 限制游客上传数量。
3. 增加后台下架状态。
4. 增加 Cloud Code 日志和指标。
5. 增加排行榜或推荐池。

## 15. 官方参考

- Unity Cloud Save 概览：https://docs.unity.org.cn/ugs/en-us/manual/cloud-save/manual
- Unity Cloud Save with Cloud Code：https://docs.unity.com/en-us/cloud-save/tutorials/cloud-code
- Unity Cloud Save Files：https://docs.unity.com/en-us/cloud-save/concepts/files
- Unity Cloud Code 概览：https://docs.unity.org.cn/ugs/manual/cloud-code/manual

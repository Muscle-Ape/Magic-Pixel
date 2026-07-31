# 自定义关卡公开发布模块配置清单

本文档对应当前项目已经实现的 C# Cloud Code Module 版本。

## 已在项目中实现的内容

### 客户端

| 路径 | 作用 |
| --- | --- |
| `Assets/Scripts/Custom/CloudPublish/Models` | 公开关卡 DTO、返回结果、本地发布状态缓存 |
| `Assets/Scripts/Custom/CloudPublish/Abstractions/IMPCustomLevelPublishApi.cs` | 公开关卡云端访问接口 |
| `Assets/Scripts/Custom/CloudPublish/Api/MPUnityCloudCodeCustomLevelPublishApi.cs` | 使用 `CloudCodeService.Instance.CallModuleEndpointAsync` 调用 C# Module |
| `Assets/Scripts/Custom/CloudPublish/Core/MPCustomLevelPublishManager.cs` | 发布、列表、详情、体验、点赞、撤销的客户端门面 |
| `Assets/Scripts/Custom/MPCustomLevelItem.cs` | 本地自定义关卡列表 Upload 按钮接入上传/撤销 |

### Cloud Code C# Module

| 路径 | 作用 |
| --- | --- |
| `Assets/CloudCode/MagicPixelCustomLevelPublish.ccmr` | Unity Cloud Code C# Module Reference |
| `CloudCodeModules/MagicPixelCustomLevelPublish` | 独立 .NET Cloud Code Module 工程 |
| `CustomLevelPublishModule.cs` | 服务端发布、列表、详情、体验、点赞、撤销逻辑 |
| `Models.cs` | 服务端 DTO |
| `ModuleSetup.cs` | 注入 `IGameApiClient`，用于访问 Cloud Save |

客户端调用的模块名固定为：

```text
MagicPixelCustomLevelPublish
```

模块函数名：

```text
PublishCustomLevel
GetPublishedCustomLevels
GetPublishedCustomLevel
PlayPublishedCustomLevel
LikePublishedCustomLevel
RevokePublishedCustomLevel
```

## 你需要配置的内容

### 1. Unity Services 项目绑定

在 Unity Editor 中打开：

```text
Edit > Project Settings > Services
```

确认当前项目已经绑定 Unity Cloud Project。

需要启用：

```text
Authentication
Cloud Save
Cloud Code
```

### 2. 配置 Cloud Code C# Module 的 .NET 路径

项目已经新增 Editor 辅助菜单：

```text
MagicPixel > Cloud Code > Set .NET Path
```

点击后会把 Cloud Code 包使用的 `.NET` 路径写入 Unity EditorPrefs：

```text
C:\Program Files\dotnet\dotnet.exe
```

也可以在 Unity Editor 中手动查看：

```text
Edit > Preferences > Cloud Code
```

当前本机命令行检测到：

```text
.NET SDK 10.0.302
```

模块工程目标框架是：

```text
net9.0
```

### 3. 安装 Deployment 包

Cloud Code C# Module 通过 Unity Deployment 窗口部署。

如果项目还没有安装，需要在 Package Manager 中安装：

```text
com.unity.services.deployment
```

当前项目已经安装：

```text
com.unity.services.cloudcode
com.unity.services.cloudsave
com.unity.services.authentication
com.unity.services.deployment
```

### 4. 部署 C# Module

项目已经新增一键入口：

```text
MagicPixel > Cloud Code > Deploy Custom Level Module
```

该菜单会依次执行：

```text
1. 设置 .NET 路径
2. Release 构建 C# Module
3. 打开 Deployment 窗口
4. 选中并部署 Assets/CloudCode/MagicPixelCustomLevelPublish.ccmr
```

如果只想打开 Deployment 窗口并选中模块，可以使用：

```text
MagicPixel > Cloud Code > Open Custom Level Module In Deployment
```

也可以使用 Unity 原生路径：

```text
Services > Deployment
```

选择环境：

```text
development
```

找到：

```text
Assets/CloudCode/MagicPixelCustomLevelPublish.ccmr
```

执行部署。

部署成功后，Cloud Code 后台应出现模块：

```text
MagicPixelCustomLevelPublish
```

如果部署窗口报错：

```text
Failed to retrieve main project - Could not find a Publish Profile.
```

需要确认模块工程下存在且仅存在一个 `.pubxml`：

```text
CloudCodeModules/MagicPixelCustomLevelPublish/MagicPixelCustomLevelPublish/Properties/PublishProfiles/FolderProfile.pubxml
```

该文件用于让 Unity Cloud Code Deployment 从 solution 中定位主 `.csproj`。当前项目已经补齐。

如果下一步报 NuGet / `api.nuget.org` / `linux-x64` runtime 相关错误，需要先确保当前电脑能访问 NuGet，因为 Unity 部署 C# Module 时会执行 `dotnet publish -c Release -r linux-x64`。

### 5. Cloud Save 数据位置与验证

当前 C# Module 使用 Cloud Save Private Custom Data 存储公开关卡。

Custom ID：

```text
mp_public_custom_levels
```

目录 Key：

```text
mp_public_custom_level_catalog_v1
```

单个关卡 Key：

```text
mp_public_custom_level_{publicLevelId}
```

当前开发期列表功能通过目录 Key 分页读取，不强制依赖 Cloud Save Query Index。

后台验证步骤：

```text
Unity Dashboard > 当前 Project > 当前 Environment > Cloud Save > Data
```

查看 Custom Data，确认存在：

```text
Custom ID: mp_public_custom_levels
Key: mp_public_custom_level_catalog_v1
Key: mp_public_custom_level_{publicLevelId}
```

### 6. Access Control 建议

公开关卡数据应只允许 Cloud Code 服务端写入。

操作建议：

- 不要禁止 Player Data 的普通读写，否则会影响当前已有的个人云存档。
- 只针对公开自定义关卡使用的 Custom Data 做限制。
- 客户端只保留 Cloud Code Module 调用入口，不直接写公开关卡 Custom Data。
- Cloud Code Module 使用服务端上下文写入 `mp_public_custom_levels`。

Dashboard 操作路径：

```text
Unity Dashboard > 当前 Project > 当前 Environment > Access Control
```

创建或检查 Cloud Save 相关规则时，只限制公开关卡 Custom Data 资源，不要全局拦截 Cloud Save Player Data。

### 7. 环境配置

当前项目 Unity Services 初始化逻辑已经区分：

```text
Editor -> development
Build  -> production
```

因此你需要分别在 Unity Dashboard 中确认：

- `development` 环境已部署 `MagicPixelCustomLevelPublish`。
- 正式发包前，`production` 环境也需要部署同名模块。

环境部署顺序建议：

```text
1. 在 Unity Editor 的 Services/Deployment 窗口选择 development。
2. 部署 MagicPixelCustomLevelPublish。
3. 使用编辑器登录并上传一个测试关卡。
4. 在 Dashboard 的 development 环境确认 Cloud Code Module 和 Cloud Save 数据。
5. 正式发包前切换到 production。
6. 重新部署同一个 MagicPixelCustomLevelPublish。
7. 在 production 环境做一次独立冒烟测试。
```

### 8. 当前开发期限制

当前版本为了先跑通流程，点赞玩家列表暂存在关卡记录内：

```text
likedPlayerIds
```

这适合开发环境和小规模测试。

正式上线、点赞量变大后，建议把点赞记录拆成独立 Key：

```text
mp_custom_level_like_{publicLevelId}_{playerId}
```

并用 Cloud Save Query Index 优化公开关卡列表排序。

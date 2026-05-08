# VRWorld Toolkit（汉化版）

> 📢 本仓库为 [VRWorld Toolkit](https://github.com/oneVR/VRWorldToolkit) 的社区中文汉化分支。原项目由 [oneVR](https://oneVR.dev/) 开发，基于 MIT 许可证发布。

<img src="https://github.com/oneVR/VRWorldToolkit/assets/4764355/0672bef5-0aa4-42b4-b388-1a47bc1ba998">

<div align="center">

[![GitHub stars](https://img.shields.io/github/stars/oneVR/VRWorldToolkit?style=for-the-badge)](https://github.com/oneVR/VRWorldToolkit/stargazers)
[![GitHub all releases](https://img.shields.io/github/downloads/oneVR/VRWorldToolkit/total?style=for-the-badge)](https://github.com/oneVR/VRWorldToolkit/releases)
[![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/oneVR/VRWorldToolkit?sort=semver&style=for-the-badge)](https://github.com/oneVR/VRWorldToolkit/releases/latest)
[![Project License](https://img.shields.io/badge/license-MIT-brightgreen?style=for-the-badge)](https://github.com/oneVR/VRWorldToolkit/blob/master/LICENSE)
![GitHub repo size](https://img.shields.io/github/repo-size/oneVR/VRWorldToolkit?style=for-the-badge)

</div>

**VRWorld Toolkit** 是一款 Unity Editor 扩展，旨在降低 VRChat 世界创作的门槛，让创建性能良好的世界变得更加容易。主要支持 VRChat 世界项目，对角色项目和未安装 VRChat SDK 的项目也有有限支持。

如需报告问题，你可以加入我的 [Discord 服务器](https://discord.com/invite/FCm28DM) 或创建 [新的 Issue](https://github.com/oneVR/VRWorldToolkit/issues/new/choose)。也欢迎提交 Pull Request。

## 安装

### 环境要求
* Unity 2022.3.x

### 快速开始
* 如果你未使用 VRChat Creator Companion，可以从[这里](https://github.com/oneVR/VRWorldToolkit/releases)下载最新版本的 Unity Package 导入到你的 Unity 项目中
* 如果你使用 VRChat Creator Companion，可以在内置的 Curated 仓库中找到 VRWorld Toolkit
* 导入后，工具栏中会出现 VRWorld Toolkit 下拉菜单。如果没有出现，请查看[故障排除](#故障排除)

### 故障排除
> [!IMPORTANT]
> 首先，如果你正在开发 VRChat 项目，请确保运行的是最新版 SDK，如不是请[更新](https://creators.vrchat.com/sdk/updating-the-sdk/)。本项目保持与最新 SDK 版本的同步更新，不保证对旧版本的支持。

首先打开 Unity Console，可以通过 `Ctrl + Shift + C` 或 `Window > General > Console` 打开。然后确保窗口右上角的红色错误已启用。最后点击左上角的 `Clear`，这样只会显示导致编译失败的错误。

如果剩余的错误提到了 Post Processing 或 Bakery，但你的项目*并没有*安装它们，请参考以下说明。

最常见的问题是项目之前安装了 `Post Processing` 或 `Bakery` 但后来被移除。这会在项目中留下这些资源自动添加的 Scripting Define Symbol，导致 VRWorld Toolkit 认为它们仍然存在于项目中。

可以手动从 `Edit > Project Settings > Player > Other Settings > Scripting Define Symbols` 中移除：

* 对应 Bakery：`BAKERY_INCLUDED`
* 对应 Post Processing：`UNITY_POST_PROCESSING_STACK_V2`

这些符号的主要作用是在项目中设置它们时才加载相应部分的代码，但它们不会随添加它们的资源一起被自动移除。

还有一种罕见情况：如果你的项目全局命名空间中存在 `Bloom.cs` 脚本或名为 `Bloom` 的类，会与 Post Processing 产生冲突。通常表现为控制台中反复出现 VRWorld Toolkit 脚本无法访问 Post Processing Bloom 的错误。最简单的解决方法是找到并删除有问题的脚本，通常在资源中搜索 `Bloom` 就能找到。

## 主要功能

<img align="right" width="400" margin="20" src="https://github.com/oneVR/VRWorldToolkit/assets/4764355/52c0c25c-c3e9-4b73-8e88-b4e10c884040">

### 世界调试器 (World Debugger)
遍历场景，检查常见问题，并给出改进建议。包含超过 90 条不同的提示、警告、错误和常规消息！

它还允许查看 SDK 最近构建的统计数据，方便快速了解构建内容的概况。同时会分别保存最新的 Windows 和 Android 构建数据，便于两者对比。

### 构建时禁用 (Disable On Build)
通过 `VRWorld Toolkit > 构建时功能 > 构建时禁用 > 设置` 运行设置后，会添加一个新标签 `DisableOnBuild`。所有标记了此标签的游戏对象将在构建前被自动禁用。最典型的用例是更方便地管理基于触发器的遮挡剔除。

### 后期处理 (Post Processing)
提供一键配置后期处理的方案，附带一个简单的示例配置文件供进一步编辑。

### 快捷功能 (Quick Functions)

#### 复制世界 ID
帮你快速将当前场景的世界 ID 复制到剪贴板，省去翻找 Scene Descriptor 的麻烦。

#### 批量纹理导入器 (Mass Texture Importer)
批量处理纹理，快速将 Crunch 压缩和其他设置应用到当前场景中的所有纹理或项目中的所有资源。

### 自定义编辑器 (Custom Editors)
为现有的 VRChat 组件添加更多功能，使其更易用并提供体验优化。如果不需要，也可以通过 `VRWorld Toolkit > 自定义编辑器 > 禁用` 轻松关闭。

增强内容包括：

* VRC 镜子反射
 * 一键设置常用反射层
 * 针对镜子常见问题的警告和提示
 * VRChat 专属层的说明
* VRC 角色展台
 * 支持多选时批量复制和设置展台 ID
 * 选中带展台组件的游戏对象时，会在场景中绘制展台图片在实际游戏中的显示区域轮廓

## 特别感谢

* [Pumkin](https://github.com/rurre/PumkinsAvatarTools) - 在项目初期给了我大量帮助，他创作的 Disable On Upload 功能是这个项目的起点
* [Silent](http://s-ilent.gitlab.io/index.html) - 帮我润色文字表达，并在后期处理功能上提供帮助
* [Metamaniac (Table)](https://twitter.com/Metamensa) - 逐字检查了我的文字，揪出了所有愚蠢的拼写错误

**免责声明：** 此扩展仍在持续开发中。尽管我尽力进行全面测试，但仍可能出现问题。*请务必做好项目备份，使用风险自负！*

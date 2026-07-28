# Illustrator 智能输入法

> v1.0.0 免费版，由 [mootop.top](https://mootop.top/) 发布。

- [使用帮助](docs/HELP.md)
- [开发手记](docs/DEVELOPMENT-NOTES.md)
- [免费使用许可](LICENSE)

这是一个面向 Windows 和 Adobe Illustrator 2026 的输入法状态管理工具。

它由两个部分组成：

- `IllustratorTypeFlow.exe`：常驻系统托盘，识别 Illustrator 界面焦点并控制 IME 的开关状态。
- `IllustratorTypeFlow.aip`：很小的 Illustrator 原生插件，只读取 `HasTextFocus`，将画布文字编辑状态发送给托盘程序。

程序不会修改 Illustrator 文档，也不会模拟 Shift、Win+Space 等按键。运行时不需要管理员权限。

## 已实现的规则

| Illustrator 状态 | 输入法 |
| --- | --- |
| 画布文字插入点或文字选择 | 中文 |
| 图层、画板重命名 | 中文 |
| 插件中的普通文本框 | 中文 |
| 宽高、坐标、字号、透明度、角度等参数框 | 英文 |
| 画布非文字状态 | 英文，单键快捷键可用 |
| 切换到其他应用 | 恢复进入 Illustrator 前的状态 |

程序沿用当前激活的中文输入法，因此可以在 WeType 和微软拼音之间自行切换。它只控制该输入法的打开/关闭状态，不会偷偷发送键盘事件。

普通 `Document`/网页面板不再被当成输入框：只有出现真正的 `Edit` 输入焦点、画布文字插入点/文字选区，或用户明确标记的第三方控件才会开启中文。状态防抖约为 15 毫秒。

## 直接构建托盘程序

仓库优先使用项目内的 `.tools\dotnet`，也可以使用系统安装的 .NET 8 SDK：

```powershell
.\scripts\build.ps1
```

输出：

```text
artifacts\app\IllustratorTypeFlow.exe
```

该 EXE 是自包含的 Windows x64 单文件程序，目标电脑无需预装 .NET。

## 构建 Illustrator 插件

Adobe Illustrator SDK 不能随源码分发。需要准备：

1. 从 Adobe Developer Console 下载与 Illustrator 2026 匹配的 Windows SDK。
2. 安装 Visual Studio 2022 的“使用 C++ 的桌面开发”和 CMake 组件。
3. 安装 Python；Adobe 的 `create_pipl.py` 会用到它。

然后运行：

```powershell
.\scripts\build.ps1 -IllustratorSdkRoot 'D:\SDK\Adobe Illustrator 2026 SDK'
```

输出：

```text
artifacts\plugin\IllustratorTypeFlow.aip
artifacts\plugin\plugin.pipl
```

插件只在 Illustrator 主线程调用 Adobe SDK。后台线程只发送已缓存的布尔状态，不会从非主线程访问 Illustrator 套件。

## 安装

构建完成后：

```powershell
.\scripts\install.ps1
```

文件会安装到：

```text
%LOCALAPPDATA%\IllustratorTypeFlow
```

安装脚本会启动托盘程序，并注册当前用户级开机启动，不需要管理员权限。

第一次安装原生插件后，需要在 Illustrator 中执行一次：

1. 打开“首选项 → 增效工具和暂存盘”（英文界面为 “Plug-ins & Scratch Disks”）。
2. 启用“其他增效工具文件夹”。
3. 选择 `%LOCALAPPDATA%\IllustratorTypeFlow\plugin`。
4. 重启 Illustrator。

如果暂时没有 `.aip`，托盘程序会启用无插件校正：监听 Illustrator 的文字工具、画布点击和退出操作来判断画布文字编辑。常用的 `T → 点击文字`、双击文字、`Esc`、`Ctrl+Enter` 和切换其他工具均可自动切换输入法；安装 `.aip` 后则优先采用插件的精确状态。

## 托盘菜单

- **启用智能切换**：临时暂停或恢复。
- **开机启动**：设置当前用户级启动项。
- **无插件校正：正在编辑画布文字**：在少数第三方工具造成误判时手动校正当前状态。
- **将当前输入框设为文字框**：纠正第三方插件没有正确公开控件类型的情况。
- **将当前输入框设为参数框**：排除被误识别的数值字段。
- **清除当前输入框规则**：恢复自动分类。
- **复制当前诊断信息**：复制 UI Automation 控件信息，便于排查误判。
- **打开日志目录**：日志和设置位于 `%LOCALAPPDATA%\IllustratorTypeFlow`。

## 测试

运行自动化测试：

```powershell
.\.tools\dotnet\dotnet.exe test .\tests\IllustratorTypeFlow.Tests\IllustratorTypeFlow.Tests.csproj -c Release
```

测试覆盖状态决策、画布状态优先级、无插件画布状态机、中英文命名字段、数值参数排除、用户覆盖规则和管道协议。

建议安装后手工验证：

1. 分别用 WeType 和微软拼音测试点文字、区域文字、路径文字。
2. 在画布文字编辑和选择工具之间连续切换 50 次。
3. 验证非文字状态下 `V`、`A`、`P`、`T` 快捷键。
4. 验证图层/画板重命名和常用第三方插件文字框。
5. 验证宽高、字号、透明度等参数框保持英文。
6. 验证 Alt+Tab、退出 Illustrator 和退出托盘程序时恢复输入法。

## 卸载

关闭 Illustrator 后运行：

```powershell
.\scripts\uninstall.ps1
```

保留设置和日志：

```powershell
.\scripts\uninstall.ps1 -KeepSettings
```

若 Illustrator 的“其他增效工具文件夹”仍指向安装目录，可在首选项中取消该选项。

# WebViewHub (AI Agent Collaboration Hub)

[English Version](./README_en.md) | [中文版](./README.md)

WebViewHub 是一个基于 Windows Presentation Foundation (WPF) 和 WebView2 构建的多窗口 AI 代理协作中心。它允许用户在一个集中的工作台上同时管理和交互多个 AI 模型（如 ChatGPT、Gemini、豆包、Kimi 等），实现高效的跨 AI 协同。

![WebViewHub](app.ico) <!-- 请替换为实际截图路径 -->

## 🎯 项目目标 (Project Goal)

天下苦来回切各种 AI 网页久矣！本项目旨在打造一个极其丝滑的多模态 AI 协同终极桌面极客工具。它不是调用昂贵的官方 API，而是直接驱动原生的各家 AI 网页端，实现同屏无缝多开。通过底层 DOM 注入自动化操作，打通各大模型之间的壁垒，实现一套提示词 (Prompt) 自动群发、跨模型接龙生成等协同功能，大幅提升 AI 使用效率和体验。

## ✨ 核心特性

- **同屏多阵列排版**：支持双边布局、磁吸瀑布流、分屏叠加等灵活的窗口阵列模式。
- **一键中央群发**：中间悬浮指令中心，敲击一句 Prompt 即可向同屏的所有 AI 自动打字并发送。
- **跨模型角色提取 (@接龙)**：借助 `@角色名`，可以让 ChatGPT 生成框架，随即提取给 Kimi 继续完成正文。
- **系统级原生会话保持**：相互隔离的 WebView 实例，免密免配置 Cookie 缓存，实时保存布局参数 (SQLite)。
- **极致无边框沉浸外观**：支持 Glassmorphism（毛玻璃）、极限标题栏收缩、伪装移动端 UA (User-Agent) 视图。

## 🏗️ 技术架构 (Technical Architecture)

本项目遵循轻量级客户端设计原则，主要技术栈如下：
- **前端/UI**: WPF (Windows Presentation Foundation), XAML, 支持流畅的毛玻璃动画和多层叠放。
- **后端逻辑**: C#, .NET 8.0
- **浏览器内核**: Microsoft Edge WebView2, 提供原生 Web 渲染与丰富的宿主交互 (Host-Web 互操作)。
- **MVVM 框架**: CommunityToolkit.Mvvm，用于实现数据绑定和UI状态剥离。
- **本地存储**: Microsoft.Data.Sqlite (SQLite)，持久化保存窗口布局、角色配置和系统设置。
- **打包部署**: .NET Publish (单文件/框架依赖), Inno Setup 6 安装向导。

**核心工作原理**：
WebViewHub 直接驱动官方 Web UI。系统利用 WebView2 的 `ExecuteScriptAsync` 注入原生 JavaScript 脚本与分析 DOM 树，自动实现：找输入框、粘贴文字、点击发送，以及自动逆向抓取出最新的回答纯文本，将其完美整合到了中央通讯协议中。完美绕开高防和风控验证。

## � 启动方式 (Getting Started)

### 方式 1：下载免安装纯净版 (推荐)
前往 [Releases](https://github.com/yourusername/WebViewHub/releases) 页面下载最新的 `WebViewHub_Clean.zip`。解压后直接双击 `WebViewHub.exe` 即可运行，完全不依赖环境配置。

### 方式 2：使用安装向导 (EXE)
下载并运行 `WebViewHub_Install_v1.0.0.exe` 安装包。根据向导完成安装，系统将自动创建桌面快捷方式。

### 方式 3：自行源码编译
1. 安装环境：确保你的系统已安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 及 Visual Studio 2022。
2. 克隆本仓库：
   ```bash
   git clone https://github.com/yourusername/WebViewHub.git
   ```
3. 编译运行：
   ```bash
   cd WebViewHub
   dotnet run
   ```

## � 目录说明 (Directory Structure)

```text
WebViewHub-AI/
├── App.xaml / App.xaml.cs          # WPF 应用程序入口和全局事件
├── MainWindow.xaml / .cs           # 主窗口视图，包含底层容器逻辑
├── Controls/                       # 自定义复合 UI 控件 (UserControls)
│   ├── CentralCommandPanel         # 悬浮中央群发控制台
│   ├── WebViewContainer            # 管理多个 WebView 实例的容器
│   ├── WebViewUnit                 # 单个基于 WebView2 的 AI 窗口封装
│   ├── AppleRoleDialog             # 角色提取/@功能 对话框
│   ├── AppleUrlDialog              # 添加新 URL/自定义 AI 网站 对话框
│   └── AppleMessageBox             # 统一定制化提示弹窗
├── Services/                       # 后台业务逻辑和服务
│   └── LayoutService.cs            # 处理布局排版、SQLite 数据加载和窗口状态
├── Styles/                         # 全局样式资源和主题设计
│   └── AppStyles.xaml              # 控件外观定义、色彩令牌
└── WebViewHub.csproj               # .NET 8 WPF 项目配置文件
```

## 📄 授权协议 (License)

本项目采用 **CC BY-NC 4.0 (知识共享 署名-非商业性使用 4.0 国际许可协议)** 进行授权。
您可以自由共享、演绎本作品，但**绝不可用于任何商业目的**。如果您需要用于商业包装或内部盈利项目，请联系原作者进行单独的商业授权。
详情请参阅项目根目录下的 [LICENSE](LICENSE) 文件。

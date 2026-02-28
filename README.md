# WebViewHub (AI Agent Collaboration Hub)

WebViewHub 是一个基于 Windows Presentation Foundation (WPF) 和 WebView2 构建的多窗口 AI 代理协作中心。它允许用户在一个集中的工作台上同时管理和交互多个 AI 模型（如 ChatGPT、Gemini、豆包、Kimi 等），实现高效的跨 AI 协同。

![WebViewHub](app.ico) <!-- 请替换为实际截图路径 -->

## ✨ 核心特性

- **多标签/多窗口聚合**：在一个可自由排列的画布上并排运行多个 AI 网页端。
- **动态布局排版**：支持双边布局、瀑布流、分屏叠加等多种窗口阵列模式。
- **中央调度控制台**：中间悬浮指令中心，可一键将提示词群发给指定的所有 AI 窗口。
- **角色路由提取 (Role Tag)**：支持通过 `@角色名` 将特定要求发给对应的 AI，或者实时抓取其他 AI 生成的答案。
- **沉浸式无边框交互**：极致缩小的标题栏、悬浮操作按钮以及半透明毛玻璃材质 (Glassmorphism)。
- **持久化隔离环境**：所有 WebView 实例相互隔离并支持 Cookie 缓存缓存自动免密登录，且配置实时持久化到 SQLite。
- **移动/桌面端 UA 切换**：一键切换网页加载的 User-Agent (浏览器标识) 适配各家 AI 的手机端或电脑端最佳 UI。

## 📦 安装指南

### 方式 1：下载免安装纯净版 (推荐)
下载 [Releases](https://github.com/yourusername/WebViewHub/releases) 页面中提供的 `WebViewHub_Clean.zip`。解压后直接双击 `WebViewHub.exe` 即可运行，完全不依赖本地环境配置。

### 方式 2：使用安装向导 (EXE)
下载并运行 `WebViewHub_Install_v1.0.0.exe` 安装包。根据向导完成安装，系统将自动创建桌面快捷方式。

### 方式 3：自行编译
1. 确保已安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. 克隆本仓库：
   ```bash
   git clone https://github.com/yourusername/WebViewHub.git
   ```
3. 编译运行：
   ```bash
   cd WebViewHub
   dotnet run
   ```

## 🛠️ 技术栈

- **前端/UI**: WPF (Windows Presentation Foundation), XAML
- **后端逻辑**: C#, .NET 8.0
- **浏览器内核**: Microsoft Edge WebView2
- **MVVM 框架**: CommunityToolkit.Mvvm
- **本地存储**: Microsoft.Data.Sqlite (SQLite)
- **打包工具**: .NET Publish, Inno Setup 6

## 🚀 核心工作原理

WebViewHub 并不是调用官方昂贵且有次数限制的 API，而是**直接驱动原生的网页端 (Web UI)**。
通过注入原生 JavaScript 脚本与分析 DOM 树，自动实现自动寻框框、粘贴文字、点击发送，以及自动逆向抓取出最新的回答纯文本，完美整合到了中央通讯协议中。

## 📄 授权协议 (License)

本项目采用 **CC BY-NC 4.0 (知识共享 署名-非商业性使用 4.0 国际许可协议)** 进行授权。

这意味着您可以自由地：
- **共享** — 在任何媒介以任何形式复制、发行本作品
- **演绎** — 修改、转换或以本作品为基础进行创作

**惟须遵守下列条件：**
- **非商业性使用** — 您**不得**将本作品或其衍生作品用于**任何商业目的**。如果您需要将本应用或其核心代码用于任何商业包装、售卖或企业内部盈利项目，请联系原作者进行单独的商业授权。
- **署名** — 您必须给出适当的署名，提供指向本许可协议的链接，同时标明是否（对原作品）作了修改。

详情请参阅项目根目录下的 [LICENSE](LICENSE) 文件。

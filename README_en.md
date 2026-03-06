# WebViewHub (AI Agent Collaboration Hub)

[English Version](./README_en.md) | [中文版](./README.md)

WebViewHub is an elegant, multi-window AI agent aggregation workspace built with Windows Presentation Foundation (WPF) and WebView2. It allows users to concurrently manage and interact with multiple AI models (e.g., ChatGPT, Gemini, Claude, Kimi) in a centralized workbench, enabling highly efficient cross-AI collaboration.

![WebViewHub](app.ico) <!-- Replace with actual screenshot path -->

---

## 💖 Sponsors & Partners

Special thanks to the following individuals or organizations for their generous support:

> **[Your spot here – waiting for you!]**
> If you are an organization or company and wish to display your logo or advertisement link here, please feel free to contact the author.

### ☕ Support & Donation

**Development is not easy. If you find WebViewHub helpful, feel free to buy the author a coffee! ☕**

Your support is the greatest motivation for me to continue updating and optimizing this tool. Every donation will be used to cover server costs, improve software quality, and support the development of subsequent new features.

<table align="center">
  <tr>
    <td align="center">
      <img src="Assets/wechat_pay.jpg" width="200" /><br />
      <b>WeChat Pay</b>
    </td>
    <td align="center">
      <img src="Assets/alipay.jpg" width="200" /><br />
      <b>Alipay</b>
    </td>
  </tr>
</table>

---

## 🎯 Project Goal

People have long suffered from endlessly switching tabs between various AI web pages! This project aims to create extremely smooth multi-modal AI synergy via an ultimate geek desktop tool. Instead of calling expensive official APIs, it directly drives the native web UI of the various AI providers, enabling seamless multi-instance windows on the same screen. Through underlying DOM injection and automated operations, it breaks the barriers between major models, offering synchronized prompt broadcasting and cross-model generation cascades, significantly boosting AI usage efficiency and experience.

## ✨ Core Features

- **Multi-display Arrays Layout**: Flexible window layouts such as side-by-side, magnetic waterfall, and layered split-screen.
- **One-Click Central Broadcasting**: A centralized floating command panel. Type one prompt, and it will automatically type and send it to all AI windows on the screen simultaneously.
- **Cross-Model Role Fetching (@Cascade)**: By using `@RoleName`, you can ask ChatGPT to generate an outline, and immediately fetch it into Kimi or Claude to flesh out the main text.
- **Native Session Persistence**: Completely isolated WebView instances inherently cache cookies (password-free logins) and save UI layout sizing in real-time (via SQLite).
- **Immersive Borderless UI**: Supports Glassmorphism effects, minimalist title bars, and mobile User-Agent (UA) view spoofing.
- **High-Fidelity Markdown Extraction**: Built-in Turndown engine for automatic restoration of tables, code blocks, and math formulas.
- **Smart Zoom Persistence**: Remembers and restores individual window zoom factors across sessions.

## 🆕 v1.1.0 Change Log

- **[Major Upgrade] Extraction Engine**: Migrated to `TurndownService` for genuine HTML-to-Markdown conversion.
  - Full support for paragraphs, complex tables, and fenced code blocks with language detection.
  - Fixed extraction gaps in ChatGPT and Gemini's long conversations.
- **[Interaction] Adaptive Input**:
  - Automatically enables `Ctrl + Enter` sending for Gemini and Grok.
- **[Visuals] UI Cleanup**:
  - **Doubao**: Physically removed "Suggested Questions" and recommendation widgets.
  - **Grok**: Filtered out status noise (e.g., "1.5s Fast") for cleaner results.
- **[Persistence] Zoom memory**: Persistent storage for manual zoom factors; your preferred view scale is now kept across restarts.
- **[Kernel] Security & Stability**: Bypassed TrustedHTML CSP restrictions, preventing crashes on high-security models.

## 🏗️ Technical Architecture

This project strictly adheres to lightweight client design principles. The main tech stack includes:
- **Frontend/UI**: WPF (Windows Presentation Foundation) and XAML, supporting fluid blur animations and deep layering.
- **Backend Logic**: C#, .NET 8.0
- **Browser Engine**: Microsoft Edge WebView2, providing native web rendering and rich Host-Web interoperability.
- **MVVM Framework**: CommunityToolkit.Mvvm, used for data binding and decoupling UI states.
- **Local Storage**: Microsoft.Data.Sqlite (SQLite), persistently saving window layouts, roles, and system configuration.
- **Packaging & Deployment**: .NET Publish (Single File/Framework Dependent) and Inno Setup 6 for the installer.

**Core Working Principle**: 
WebViewHub directly controls the official Web UI rather than integrating paid APIs. The system leverages WebView2's `ExecuteScriptAsync` to inject native JavaScript, traverse the DOM tree, automatically locate input boxes, paste text, trigger send buttons, and automatically reverse-scrape the latest plain text answers, integrating everything perfectly into the central communication protocol. This gracefully bypasses rigid CAPTCHAs and bot protection systems.

## 🚀 Getting Started

### Method 1: Portable Clean Version (Recommended)
Navigate to the [Releases](https://github.com/yourusername/WebViewHub/releases) page and download the latest `WebViewHub_Clean.zip`. Extract the files and double-click `WebViewHub.exe` to run immediately, with zero dependencies required.

### Method 2: Use the Installer (EXE)
Download and run the `WebViewHub_Install_v1.0.0.exe` installer. Follow the wizard steps, and the system will automatically create a desktop shortcut for you.

### Method 3: Build from Source
1. Prerequisites: Ensure you have built-in [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Visual Studio 2022.
2. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/WebViewHub.git
   ```
3. Build and run:
   ```bash
   cd WebViewHub
   dotnet run
   ```

## 📂 Directory Structure

```text
WebViewHub-AI/
├── App.xaml / App.xaml.cs          # WPF application entry point and global events
├── MainWindow.xaml / .cs           # Main window view, holding bottom-layer container logic
├── Controls/                       # Custom compound UI Controls (UserControls)
│   ├── CentralCommandPanel         # Floating central command broadcasting panel
│   ├── WebViewContainer            # Container managing multiple WebView instances
│   ├── WebViewUnit                 # Wrapping of a single WebView2-based AI window
│   ├── AppleRoleDialog             # Role extraction / @ function dialog
│   ├── AppleUrlDialog              # Add new URL / Custom AI sites dialog
│   └── AppleMessageBox             # Unified custom alert/message dialogs
├── Services/                       # Application business logic and services
│   └── LayoutService.cs            # Handles layout positioning, SQLite data, and window states
├── Styles/                         # Global styling resources and theming
│   └── AppStyles.xaml              # UI aesthetics definition, color tokens
└── WebViewHub.csproj               # .NET 8 WPF Project configuration file
```

## 💬 Feedback & Communication

If you encounter any issues during use, or have great suggestions and ideas, please feel free to reach out:

- **Email**: [zhangqiang88vip@163.com](mailto:zhangqiang88vip@163.com) (I will respond to your messages as soon as possible)
- **GitHub Discussions**: Visit [Discussions](https://github.com/zhangqiang8vip/WebViewHub-AI/discussions) to join the conversation or suggest new features.
- **Bug Reports**: If you find a specific bug, please submit it via [Issues](https://github.com/zhangqiang8vip/WebViewHub-AI/issues).

Every piece of feedback is invaluable to this project!

---

## 📄 License

This project is licensed under **CC BY-NC 4.0 (Creative Commons Attribution-NonCommercial 4.0 International)**.
You are free to share and adapt the material, but **under no circumstances can you use it for commercial purposes**. If you need to use this tool or its core code for business, packaging, or internal corporate profit-generating projects, please contact the original author for a separate commercial license.
See the [LICENSE](LICENSE) file in the repository root for more details.

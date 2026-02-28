using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WebViewHub.Controls
{
    public partial class WebViewContainer : UserControl
    {
        #region 依赖属性

        public static readonly DependencyProperty ProfileIDProperty =
            DependencyProperty.Register(
                nameof(ProfileID),
                typeof(string),
                typeof(WebViewContainer),
                new PropertyMetadata(string.Empty, OnProfileIDChanged));

        public string ProfileID
        {
            get => (string)GetValue(ProfileIDProperty);
            set => SetValue(ProfileIDProperty, value);
        }

        public static readonly DependencyProperty ProfileNameProperty =
            DependencyProperty.Register(
                nameof(ProfileName),
                typeof(string),
                typeof(WebViewContainer),
                new PropertyMetadata("Profile"));

        public string ProfileName
        {
            get => (string)GetValue(ProfileNameProperty);
            set => SetValue(ProfileNameProperty, value);
        }

        public static readonly DependencyProperty RoleTagProperty =
            DependencyProperty.Register(
                nameof(RoleTag),
                typeof(string),
                typeof(WebViewContainer),
                new PropertyMetadata(string.Empty));

        public string RoleTag
        {
            get => (string)GetValue(RoleTagProperty);
            set => SetValue(RoleTagProperty, value);
        }

        public string CurrentUrl
        {
            get => WebView.CurrentUrl;
            set => WebView.CurrentUrl = value;
        }

        #endregion

        #region 事件

        public event EventHandler<CustomDragDeltaEventArgs>? CustomDragDelta;
        public event EventHandler<CustomResizeDeltaEventArgs>? CustomResizeDelta;
        public event EventHandler<WebViewContainer>? DeleteRequested;

        #endregion

        public WebViewContainer()
        {
            InitializeComponent();
            SizeChanged += WebViewContainer_SizeChanged;
        }

        private void WebViewContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 当容器大小变化时计算缩放比例并应用
            // 以 1000 作为基准宽度，低于这个宽度就按比例缩小
            double baseWidth = 1000.0;
            double currentWidth = e.NewSize.Width;
            
            double zoomFactor = Math.Min(1.0, currentWidth / baseWidth);
            // 限制最小缩放比例为 30% 防止看不清
            zoomFactor = Math.Max(0.3, zoomFactor);

            var coreWebView = WebView.GetCoreWebView2();
            if (coreWebView != null)
            {
                WebView.SetZoomFactor(zoomFactor);
            }
        }

        private static void OnProfileIDChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WebViewContainer container && !string.IsNullOrEmpty(e.NewValue as string))
            {
                container.ProfileName = (string)e.NewValue;
            }
        }

        #region 拖拽与调整大小逻辑 (原生 Thumb)

        private void DragThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            CustomDragDelta?.Invoke(this, new CustomDragDeltaEventArgs(e.HorizontalChange, e.VerticalChange));
        }

        private void ResizeRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var newWidth = Math.Max(300, Width + e.HorizontalChange);
            var args = new CustomResizeDeltaEventArgs(newWidth, Height);
            CustomResizeDelta?.Invoke(this, args);
            if (args.Handled) return;
            Width = newWidth;
        }

        private void ResizeBottom_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var newHeight = Math.Max(200, Height + e.VerticalChange);
            var args = new CustomResizeDeltaEventArgs(Width, newHeight);
            CustomResizeDelta?.Invoke(this, args);
            if (args.Handled) return;
            Height = newHeight;
        }

        private void ResizeBottomRight_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var newWidth = Math.Max(300, Width + e.HorizontalChange);
            var newHeight = Math.Max(200, Height + e.VerticalChange);
            var args = new CustomResizeDeltaEventArgs(newWidth, newHeight);
            CustomResizeDelta?.Invoke(this, args);
            if (args.Handled) return;
            Width = newWidth;
            Height = newHeight;
        }

        #endregion

        #region 地址配置与删除

        private void EditRole_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AppleRoleDialog(RoleTag)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                RoleTag = dialog.RoleTag;
                (Window.GetWindow(this) as MainWindow)?.SaveLayout();
            }
        }

        private void EditUrl_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AppleUrlDialog(CurrentUrl)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                var newUrl = dialog.Url;
                if (CurrentUrl != newUrl)
                {
                    CurrentUrl = newUrl;
                    WebView.Navigate(newUrl);
                    (Window.GetWindow(this) as MainWindow)?.SaveLayout();
                }
            }
        }

        private bool _isMobileMode = false;

        // 手机版 UA（iPhone Safari）
        private const string MobileUserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
            "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

        // 桌面版 UA（Chrome Windows）
        private const string DesktopUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";

        private async void ToggleMobile_Click(object sender, RoutedEventArgs e)
        {
            var coreWebView = WebView.GetCoreWebView2();
            if (coreWebView == null) return;

            _isMobileMode = !_isMobileMode;

            // 切换 User-Agent
            coreWebView.Settings.UserAgent = _isMobileMode ? MobileUserAgent : DesktopUserAgent;

            // 更新按钮提示和颜色
            ToggleMobileButton.ToolTip = _isMobileMode ? "当前：手机版（点击切换桌面版）" : "切换手机版/桌面版";
            ToggleMobileButton.Background = _isMobileMode
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 255))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 191, 191));

            // 刷新页面以应用新 UA
            await coreWebView.ExecuteScriptAsync("location.reload();");
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, this);
        }

        #endregion

        #region 注入与中转机制 (AI 群聊总线)

        /// <summary>
        /// 针对主流大模型网页（豆包、Grok、Gemini 等），寻找文本输入框并模拟输入和回车发送
        /// 策略：找到正确输入框 → 填入文本 → 触发 React/Vue 事件 → 模拟 Enter 发送（不找按钮）
        /// </summary>
        public async Task InjectAndSendAsync(string text)
        {
            var coreWebView = WebView.GetCoreWebView2();
            if (coreWebView == null) return;

            // 1. 第一步：先将窗口焦点系统级赋予给 WebView，这对底层渲染引擎接收按键至关重要。
            await Application.Current.Dispatcher.InvokeAsync(() => WebView.Focus());
            await Task.Delay(100);

            // 2. 第二步：在前端寻找到输入框 -> 赐予前端焦点光标 -> 执行清空操作
            string scriptFocus = @"
                (function() {
                    let el = document.querySelector('.ql-editor[contenteditable=""true""]') ||
                             document.querySelector('div[aria-label=""Enter a prompt for Gemini""]') ||
                             document.querySelector('div[contenteditable=""true""][aria-label]') ||
                             document.querySelector('div#prompt-textarea') ||
                             document.querySelector('div.ProseMirror') ||
                             document.querySelector('textarea[data-testid=""chat_input_input""]') ||
                             document.querySelector('textarea[aria-label=""Enter a prompt for Gemini""]') ||
                             document.querySelector('textarea[placeholder*=""消息""]') ||
                             document.querySelector('textarea[placeholder*=""输入""]') ||
                             document.querySelector('textarea[aria-label*=""message"" i]') ||
                             document.querySelector('textarea');
                             
                    if (el) {
                        el.focus();
                        el.click(); // 骗过某些绑在 click 上的激活状态
                        if (el.isContentEditable) {
                            const sel = window.getSelection();
                            const range = document.createRange();
                            range.selectNodeContents(el);
                            sel.removeAllRanges();
                            sel.addRange(range);
                            document.execCommand('delete', false); // 清空
                        } else {
                            el.value = ''; // text area 清空
                            el.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                        return 'true';
                    }
                    return 'false';
                })();
            ";
            
            var focusResult = await coreWebView.ExecuteScriptAsync(scriptFocus);
            // 这里返回 ""true""，带双引号。如果是 false 就不再注入。
            if (focusResult != "\"true\"") return;

            // 等待前端生命周期和清空动画完结
            await Task.Delay(200);

            // 3. 第三步：绝杀！调用 Chromium 引擎底层的开发者协议 (CDP) 进行注入。
            // 这种方式直接绕过页面的 JS 环境，相当于直接在浏览器内核挂载了物理钩子敲击键盘。
            // 将文本安全地转为 JSON 字符串如 "你好\n世界"
            var safeText = System.Text.Json.JsonSerializer.Serialize(text);
            string insertTextJson = $"{{\"text\": {safeText}}}";
            await coreWebView.CallDevToolsProtocolMethodAsync("Input.insertText", insertTextJson);

            // 让 React / Angular 完全消化这些“键盘敲击”
            await Task.Delay(300);

            // 4. 第四步：CDP 模拟完美的实体 Enter 回车键按下与抬起
            string keyDownJson = "{\"type\": \"keyDown\", \"windowsVirtualKeyCode\": 13, \"key\": \"Enter\", \"code\": \"Enter\", \"text\": \"\\r\"}";
            string keyUpJson = "{\"type\": \"keyUp\", \"windowsVirtualKeyCode\": 13, \"key\": \"Enter\", \"code\": \"Enter\"}";
            
            await coreWebView.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyDownJson);
            await Task.Delay(50);
            await coreWebView.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent", keyUpJson);
        }

        /// <summary>
        /// 抓取该网页内最后一条 AI 回答气泡的文本内容。
        /// 策略：先聚焦 WebView → JS 用 Selection+execCommand('copy') 将最后回复内容推入剪贴板 → C# 读剪贴板
        /// 完全绕过 clipboard API 权限问题，无需用户手势。
        /// </summary>
        public async Task<string> FetchLastResponseAsync()
        {
            var coreWebView = WebView.GetCoreWebView2();
            if (coreWebView == null) return string.Empty;

            // 步骤 1：先聚焦 WebView 控件（以获得 Document Focus，execCommand 需要）
            await Application.Current.Dispatcher.InvokeAsync(() => WebView.Focus());
            await Task.Delay(100);

            // 步骤 2：保存当前剪贴板内容（用于之后对比）
            string oldClipboard = string.Empty;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { oldClipboard = Clipboard.GetText(); } catch { }
            });

            // 步骤 3：JS 找最后一条 AI 回复元素，用 Selection 选中并 execCommand('copy')
            string script = @"
                (function() {
                    let el = null;

                    // 豆包
                    let doubaoMsgs = document.querySelectorAll('[data-testid=""receive_message""]');
                    if (doubaoMsgs.length > 0) el = doubaoMsgs[doubaoMsgs.length - 1];

                    // ChatGPT
                    if (!el) {
                        let gptMsgs = document.querySelectorAll('[data-message-author-role=""assistant""]');
                        if (gptMsgs.length > 0) el = gptMsgs[gptMsgs.length - 1];
                    }

                    // Gemini
                    if (!el) {
                        let gemMsgs = document.querySelectorAll('model-response');
                        if (gemMsgs.length > 0) {
                            let lastMsgArea = gemMsgs[gemMsgs.length - 1];
                            el = lastMsgArea.querySelector('.message-content, .markdown, .response-container-content, [data-test-id=""message-content""]') || lastMsgArea;
                        }
                    }
                    if (!el) {
                        let gemMsgs2 = document.querySelectorAll('.response-container-markdown');
                        if (gemMsgs2.length > 0) el = gemMsgs2[gemMsgs2.length - 1];
                    }

                    // Grok（xAI）：回复块常见特征
                    if (!el) {
                        let grokMsgs = document.querySelectorAll(
                            '.message-bubble, [class*=""AssistantMessage""], [class*=""assistant-message""], ' +
                            '[class*=""BotMessage""], [class*=""ai-message""]'
                        );
                        if (grokMsgs.length > 0) el = grokMsgs[grokMsgs.length - 1];
                    }

                    // 通用 markdown / prose
                    if (!el) {
                        let mdMsgs = document.querySelectorAll('.markdown, div.prose, [class*=""markdown""]');
                        if (mdMsgs.length > 0) el = mdMsgs[mdMsgs.length - 1];
                    }

                    // 终极兜底：找页面上文字最多的非输入框 div（通用扫描器）
                    if (!el) {
                        let candidates = Array.from(document.querySelectorAll('div, article, section'));
                        // 排除输入框和导航
                        candidates = candidates.filter(d => {
                            if (d.querySelector('textarea, input')) return false;
                            if (d.tagName === 'NAV' || d.role === 'navigation') return false;
                            let txt = d.innerText || '';
                            return txt.trim().length > 100;
                        });
                        if (candidates.length > 0) {
                            // 找最后一个文字量适中（50-3000字）的候选块
                            for (let i = candidates.length - 1; i >= 0; i--) {
                                let len = (candidates[i].innerText || '').trim().length;
                                if (len > 50 && len < 3000) { el = candidates[i]; break; }
                            }
                        }
                    }

                    if (!el) return 'not_found';

                    // 用 Selection + execCommand('copy') 把内容写入系统剪贴板
                    try {
                        const range = document.createRange();
                        range.selectNodeContents(el);
                        const sel = window.getSelection();
                        sel.removeAllRanges();
                        sel.addRange(range);
                        document.execCommand('copy');
                        sel.removeAllRanges();
                        return 'ok:' + el.tagName;
                    } catch (e) {
                        return 'error:' + e.message;
                    }
                })();
            ";

            await coreWebView.ExecuteScriptAsync(script);

            // 步骤 4：等复制完成，然后从 C# 读剪贴板
            await Task.Delay(400);

            string result = string.Empty;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try { result = Clipboard.GetText(); } catch { }
            });

            if (!string.IsNullOrWhiteSpace(result) && result != oldClipboard)
                return result.Trim();

            // 步骤 5：兜底 —— 直接读 DOM innerText
            string fallbackScript = @"
                (function() {
                    let doubaoAll = document.querySelectorAll('[data-testid=""receive_message""]');
                    if (doubaoAll.length > 0) return doubaoAll[doubaoAll.length - 1].innerText.trim();

                    let gptMsgs = document.querySelectorAll('[data-message-author-role=""assistant""]');
                    if (gptMsgs.length > 0) return gptMsgs[gptMsgs.length - 1].innerText.trim();

                    let geminiMsgs = document.querySelectorAll('model-response');
                    if (geminiMsgs.length > 0) {
                        let lastArea = geminiMsgs[geminiMsgs.length - 1];
                        let contentNodes = lastArea.querySelector('.message-content, .markdown, .response-container-content, [data-test-id=""message-content""]');
                        return (contentNodes || lastArea).innerText.trim();
                    }
                    let geminiMsgs2 = document.querySelectorAll('.response-container-markdown');
                    if (geminiMsgs2.length > 0) return geminiMsgs2[geminiMsgs2.length - 1].innerText.trim();

                    let mdMsgs = document.querySelectorAll('.markdown, div.prose');
                    if (mdMsgs.length > 0) return mdMsgs[mdMsgs.length - 1].innerText.trim();

                    return '';
                })();
            ";

            var resultRaw = await coreWebView.ExecuteScriptAsync(fallbackScript);
            if (!string.IsNullOrEmpty(resultRaw) && resultRaw != "null")
            {
                try { return System.Text.Json.JsonSerializer.Deserialize<string>(resultRaw) ?? string.Empty; }
                catch { return resultRaw.Trim('"', '\''); }
            }

            return string.Empty;
        }

        #endregion

        #region 清理

        public void Cleanup()
        {
            WebView?.Cleanup();
        }

        #endregion
    }

    #region 事件参数

    public class CustomDragDeltaEventArgs : EventArgs
    {
        public double HorizontalChange { get; set; }
        public double VerticalChange { get; set; }

        public CustomDragDeltaEventArgs(double horizontalChange, double verticalChange)
        {
            HorizontalChange = horizontalChange;
            VerticalChange = verticalChange;
        }
    }

    public class CustomResizeDeltaEventArgs : EventArgs
    {
        public double NewWidth { get; set; }
        public double NewHeight { get; set; }
        public bool Handled { get; set; }

        public CustomResizeDeltaEventArgs(double newWidth, double newHeight)
        {
            NewWidth = newWidth;
            NewHeight = newHeight;
        }
    }

    #endregion
}

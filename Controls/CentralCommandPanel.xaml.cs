using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace WebViewHub.Controls
{
    /// <summary>
    /// 回复卡片数据模型
    /// </summary>
    public class ResponseItem
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public partial class CentralCommandPanel : UserControl
    {
        public MainWindow MainWindowReference { get; set; }
        private bool _isInsertingTag = false;

        // --- 回复展示集合 ---
        private readonly ObservableCollection<ResponseItem> _responses = new();

        // --- 发送历史记录管理 ---
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private string _draftCurrent = string.Empty;

        public CentralCommandPanel()
        {
            InitializeComponent();
            ResponseBoard.ItemsSource = _responses;
        }

        private void AddResponse(string role, string content)
        {
            // 如果同一角色已有卡片，更新内容而不是新增
            var existing = _responses.FirstOrDefault(r => r.Role == role);
            if (existing != null)
            {
                existing.Content = content;
                // 因为没有实现 INotifyPropertyChanged，直接替换整个 item
                int idx = _responses.IndexOf(existing);
                _responses[idx] = new ResponseItem { Role = role, Content = content, Time = DateTime.Now.ToString("HH:mm:ss") };
            }
            else
            {
                _responses.Add(new ResponseItem
                {
                    Role = role,
                    Content = content,
                    Time = DateTime.Now.ToString("HH:mm:ss")
                });
            }
        }

        private void ClearResponsesButton_Click(object sender, RoutedEventArgs e)
        {
            _responses.Clear();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string fullText = new TextRange(CommandInput.Document.ContentStart, CommandInput.Document.ContentEnd).Text.Trim();
            if (string.IsNullOrEmpty(fullText))
                return;

            if (MainWindowReference == null)
            {
                MessageBox.Show("未设定 MainWindow 引用，无法分发指令。");
                return;
            }

            var webViews = MainWindowReference.GetAllWebViews();
            if (webViews == null || webViews.Count == 0)
            {
                MessageBox.Show("当前没有开启任何 AI 网页。");
                return;
            }
            
            // 记录历史（不重复记录连续一样的指令）
            if (_history.Count == 0 || _history.Last() != fullText)
            {
                _history.Add(fullText);
            }
            _historyIndex = _history.Count; // 指向最新之后
            _draftCurrent = string.Empty;

            // 支持换行切分指令，每行如果以 @ 开头，就算一个新指令段
            // 采用 Multiline 使得 ^ 匹配每行的开头，Singleline 使得 . 包括换行符
            var sectionMatches = Regex.Matches(fullText, @"^@(\w+)\s+(.*?)(?=(^@|\z))", RegexOptions.Singleline | RegexOptions.Multiline);

            if (sectionMatches.Count == 0)
            {
                AddResponse("系统", "未找到有效的目标角色指令。请在行首使用: @角色名 内容");
                return;
            }

            foreach (Match sec in sectionMatches)
            {
                string targetRole = sec.Groups[1].Value.Trim();
                string commandBody = sec.Groups[2].Value.Trim();

                if (string.IsNullOrEmpty(commandBody)) continue;

                var targetViews = webViews.Where(v => string.Equals(v.RoleTag, targetRole, StringComparison.OrdinalIgnoreCase)).ToList();

                if (targetViews.Count == 0)
                {
                    AddResponse("系统", $"未找到匹配角色标签 [{targetRole}] 的窗口，已跳过。");
                    continue;
                }

                // --- 跨 AI 上下文交互逻辑（内置 @引用）---
                var embeddedRoleMatches = Regex.Matches(commandBody, @"@(\w+)");

                foreach (Match embeddedMatch in embeddedRoleMatches)
                {
                    string sourceRole = embeddedMatch.Groups[1].Value;
                    var sourceView = webViews.FirstOrDefault(v => string.Equals(v.RoleTag, sourceRole, StringComparison.OrdinalIgnoreCase));

                    if (sourceView != null && !string.Equals(sourceRole, targetRole, StringComparison.OrdinalIgnoreCase))
                    {
                        AddResponse("系统", $"👉 正在从 [{sourceRole}] 抓取上下文给 [{targetRole}]...");
                        string lastReply = await sourceView.FetchLastResponseAsync();
                        if (!string.IsNullOrEmpty(lastReply))
                        {
                            string replacement = $"\n\n【以下是来自 {sourceRole} 的内容】:\n{lastReply}\n\n";
                            commandBody = commandBody.Replace(embeddedMatch.Value, replacement);
                        }
                        else
                        {
                            commandBody = commandBody.Replace(embeddedMatch.Value, $"【尝试提取 {sourceRole} 失败】");
                        }
                    }
                }

                foreach (var view in targetViews)
                {
                    AddResponse(targetRole, $"👉 已分发指令，等待 {targetRole} 回答中...");
                    await view.InjectAndSendAsync(commandBody);
                    // 启动一个后台独立监控任务，用于在 AI 回答结束后自动抓取答案回显
                    _ = StartAutoFetchResponseTask(view, targetRole);
                }
            }
            
            // 成功分发后清空富文本输入框
            CommandInput.Document.Blocks.Clear();
            CommandInput.Document.Blocks.Add(new Paragraph());
        }

        private async Task StartAutoFetchResponseTask(WebViewContainer view, string targetRole)
        {
            try
            {
                // 等待页面反应，避免立刻抓取到上一条的旧回复
                await Task.Delay(2000); 

                string stableReply = string.Empty;
                int unchangedCount = 0;
                int maxRetries = 60; // 最多轮询监控 60 秒 (20次 * 3秒)

                for (int i = 0; i < maxRetries; i++)
                {
                    string currentReply = await view.FetchLastResponseAsync();
                    
                    if (!string.IsNullOrEmpty(currentReply))
                    {
                        if (currentReply == stableReply)
                        {
                            unchangedCount++;
                            // 如果连续 2 次（约 3 秒内）文本长度无变化，则判定回答已完成
                            if (unchangedCount >= 2) break;
                        }
                        else
                        {
                            stableReply = currentReply;
                            unchangedCount = 0; // 字数有变动，重置计数器
                        }
                    }
                    
                    await Task.Delay(1500); // 每 1.5 秒抽查一次
                }

                if (!string.IsNullOrEmpty(stableReply))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AddResponse(targetRole, $"✅ 收到了来自 {targetRole} 的回复：\n" + stableReply);
                    });
                }
                else
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AddResponse(targetRole, $"⚠ 等待 {targetRole} 的回复超时或未获取到内容。");
                    });
                }
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AddResponse(targetRole, $"❌ 监听提取异常：{ex.Message}");
                });
            }
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindowReference == null) return;

            var webViews = MainWindowReference.GetAllWebViews().Where(v => !string.IsNullOrEmpty(v.RoleTag)).ToList();

            if (webViews.Count == 0)
            {
                AddResponse("系统", "没有找到配置了角色标签的窗口。");
                return;
            }

            FetchButton.IsEnabled = false;

            // 并发抓取所有 AI 的回复
            var tasks = webViews.Select(async view =>
            {
                try
                {
                    string reply = await view.FetchLastResponseAsync();
                    AddResponse(
                        view.RoleTag,
                        string.IsNullOrEmpty(reply) ? "⚠ 未抓取到有效回复（AI 可能还在思考）" : reply
                    );
                }
                catch (Exception ex)
                {
                    AddResponse(view.RoleTag, $"❌ 抓取失败: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
            FetchButton.IsEnabled = true;
        }

        #region @自动补全逻辑与富文本格式化

        private void CommandInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInsertingTag || MainWindowReference == null) return;

            var caret = CommandInput.CaretPosition;
            if (caret == null) return;

            // 往回读取同一 Run 里的内容，检测输入的 @
            string textBeforeCaret = caret.GetTextInRun(LogicalDirection.Backward);
            if (string.IsNullOrEmpty(textBeforeCaret))
            {
                RoleTagPopup.IsOpen = false;
                return;
            }

            int lastAt = textBeforeCaret.LastIndexOf('@');
            if (lastAt >= 0)
            {
                string typed = textBeforeCaret.Substring(lastAt + 1);
                // 确保触发时中途没有换行或空格
                if (!typed.Contains(" ") && !typed.Contains("\n") && !typed.Contains("\r"))
                {
                    var webViews = MainWindowReference.GetAllWebViews();
                    var tags = webViews.Select(v => v.RoleTag)
                                       .Where(t => !string.IsNullOrEmpty(t) && t.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                                       .Distinct()
                                       .ToList();
                    
                    if (tags.Count > 0)
                    {
                        RoleTagListBox.ItemsSource = tags;
                        RoleTagListBox.SelectedIndex = 0;
                        
                        // 让 Popup 跟随光标位置
                        var rect = caret.GetCharacterRect(LogicalDirection.Backward);
                        RoleTagPopup.PlacementRectangle = rect;
                        RoleTagPopup.IsOpen = true;
                        return;
                    }
                }
            }

            RoleTagPopup.IsOpen = false;
        }

        private void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (RoleTagPopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    if (RoleTagListBox.SelectedIndex < RoleTagListBox.Items.Count - 1)
                        RoleTagListBox.SelectedIndex++;
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (RoleTagListBox.SelectedIndex > 0)
                        RoleTagListBox.SelectedIndex--;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    InsertSelectedTag();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    RoleTagPopup.IsOpen = false;
                    e.Handled = true;
                }
            }
            else
            {
                // 无弹窗时，只依靠 Enter 派发；Shift+Enter 实现换行输入。
                if (e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // 放行，让 RichTextBox 自己处理换行
                        return;
                    }
                    else
                    {
                        e.Handled = true;
                        SendButton_Click(this, new RoutedEventArgs());
                    }
                }
                else if (e.Key == Key.Up)
                {
                    e.Handled = NavigateHistory(-1);
                }
                else if (e.Key == Key.Down)
                {
                    e.Handled = NavigateHistory(1);
                }
                else if (e.Key == Key.Left)
                {
                    e.Handled = JumpOverTagRun(LogicalDirection.Backward);
                }
                else if (e.Key == Key.Right)
                {
                    e.Handled = JumpOverTagRun(LogicalDirection.Forward);
                }
                else if (e.Key == Key.Back)
                {
                    // Backspace：如果光标前面紧贴着蓝色 @标签，整体删除该标签
                    e.Handled = DeleteTagRun();
                }
            }
        }

        private void RoleTagListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (RoleTagListBox.SelectedItem != null)
            {
                InsertSelectedTag();
            }
        }

        private void InsertSelectedTag()
        {
            if (RoleTagListBox.SelectedItem is string tag)
            {
                _isInsertingTag = true;
                try
                {
                    var caret = CommandInput.CaretPosition;
                    string textBeforeCaret = caret.GetTextInRun(LogicalDirection.Backward);
                    if (string.IsNullOrEmpty(textBeforeCaret)) return;

                    int lastAt = textBeforeCaret.LastIndexOf('@');
                    if (lastAt >= 0)
                    {
                        int charsToDelete = textBeforeCaret.Length - lastAt;
                        var startPos = caret.GetPositionAtOffset(-charsToDelete, LogicalDirection.Backward);
                        
                        if (startPos != null)
                        {
                            // 清除刚才敲入的带 @ 的半成品长字符
                            var rangeToDelete = new TextRange(startPos, caret);
                            rangeToDelete.Text = "";

                            // 插入带有蓝色高亮和加粗格式的成品标签，例如 "@gemini"
                            var tagRange = new TextRange(startPos, startPos);
                            tagRange.Text = "@" + tag;
                            tagRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(2, 132, 199)));
                            tagRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                            
                            // 更新光标到高亮区块之后
                            CommandInput.CaretPosition = tagRange.End;
                            
                            // 插入跟随的黑色空格，恢复正常输入样式
                            var spaceRange = new TextRange(CommandInput.CaretPosition, CommandInput.CaretPosition);
                            spaceRange.Text = " ";
                            spaceRange.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51)));
                            spaceRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                            
                            CommandInput.CaretPosition = spaceRange.End;
                        }
                    }
                }
                finally
                {
                    RoleTagPopup.IsOpen = false;
                    _isInsertingTag = false;
                    CommandInput.Focus();
                }
            }
        }

        private bool NavigateHistory(int direction)
        {
            if (_history.Count == 0) return false;

            // 如果刚开始翻历史，保存当前打了一半的草稿
            if (_historyIndex == _history.Count)
            {
                _draftCurrent = new TextRange(CommandInput.Document.ContentStart, CommandInput.Document.ContentEnd).Text.TrimEnd();
            }

            int nextIndex = _historyIndex + direction;
            if (nextIndex < 0 || nextIndex > _history.Count) return false;

            _historyIndex = nextIndex;
            string textToSet = _historyIndex == _history.Count ? _draftCurrent : _history[_historyIndex];

            // 恢复内容到 RichTextBox，这里可以粗略使用纯文本，如果用户要重发带颜色的 @ 也无妨（因为上面正则支持纯文本匹配 @）
            CommandInput.Document.Blocks.Clear();
            CommandInput.Document.Blocks.Add(new Paragraph(new Run(textToSet)));
            CommandInput.CaretPosition = CommandInput.Document.ContentEnd;
            return true;
        }

        private bool JumpOverTagRun(LogicalDirection direction)
        {
            var caret = CommandInput.CaretPosition;
            if (caret == null) return false;

            // 尝试获取接下来将要移动到的邻接 TextPointer
            var nextPos = caret.GetNextInsertionPosition(direction);
            if (nextPos == null) return false;

            // 获取该位置所在的段内对象
            var run = nextPos.Parent as Run;
            if (run != null)
            {
                // 检测是否是我们之前标记的标签的特点（蓝字，加粗）
                if (run.FontWeight == FontWeights.Bold && run.Foreground is SolidColorBrush brush && brush.Color == Color.FromRgb(2, 132, 199))
                {
                    // 把光标直接甩过这个标签 Run
                    CommandInput.CaretPosition = direction == LogicalDirection.Forward ? run.ElementEnd : run.ElementStart;
                    return true;
                }
            }

            return false;
        }

        private bool DeleteTagRun()
        {
            var caret = CommandInput.CaretPosition;
            if (caret == null) return false;

            // 向后（Backward = 左边）探测邻接位置所在的 Run
            var prevPos = caret.GetNextInsertionPosition(LogicalDirection.Backward);
            if (prevPos == null) return false;

            var run = prevPos.Parent as Run;
            if (run != null)
            {
                // 判断是否是蓝色加粗的 @标签（与插入时的格式相同）
                if (run.FontWeight == FontWeights.Bold &&
                    run.Foreground is SolidColorBrush brush &&
                    brush.Color == Color.FromRgb(2, 132, 199))
                {
                    // 选中整个 Run 并删除
                    var range = new TextRange(run.ElementStart, run.ElementEnd);
                    range.Text = string.Empty;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}

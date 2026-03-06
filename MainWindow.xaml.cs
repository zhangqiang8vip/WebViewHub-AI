using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using WebViewHub.Controls;
using WebViewHub.Services;
using System.Windows.Forms;

namespace WebViewHub
{
    public partial class MainWindow : Window
    {
        private readonly List<WebViewContainer> _webViews = new();
        private readonly LayoutService _layoutService;
        private readonly DispatcherTimer _saveTimer;
        private int _counter = 0;
        private int _currentZIndex = 1;

        // 独立浮动窗口实例 (新 Window 方案)
        private FloatingIslandWindow? _floatingIslandWindow;

        // 系统托盘
        private NotifyIcon? _notifyIcon;
        private bool _isExiting = false;

        // 全局快捷键
        private const int HOTKEY_ID_SHOW_FLOATING = 1;
        private const int HOTKEY_ID_SHOW_CHAT = 2;
        private const int MOD_NONE = 0;
        private const int MOD_CTRL = 2;
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 配置路径
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebViewHub", "settings.txt");
        private int _hotkeyShowFloating = (int)Keys.F10; // 默认 F10 显示/隐藏悬浮框
        private int _hotkeyShowChat = (int)Keys.F9;      // 默认 F9 展开/收起聊天窗口

        private bool _isPrayModeEnabled = true;

        private const int MaxWebViews = 10;
        private const double DefaultWidth = 600;
        private const double DefaultHeight = 400;
        private const double DefaultMargin = 6; // 自动排版边距(12px缝隙)
        private const int SnapThreshold = 20; // 磁吸阈值 20 像素
        private const int GridSize = 10; // 网格大小

        public MainWindow()
        {
            InitializeComponent();
            _layoutService = new LayoutService();

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _saveTimer.Tick += (s, e) => SaveLayout();

            Loaded += MainWindow_Loaded;
            Closing += Window_Closing;

            // 初始化系统托盘
            InitNotifyIcon();

            // 加载快捷键配置
            LoadHotkeyConfig();
        }

        // 初始化系统托盘图标
        private ContextMenu? _contextMenu;
        
        private void InitNotifyIcon()
        {
            // 创建 WPF ContextMenu (Win11 风格)
            _contextMenu = new ContextMenu();
            _contextMenu.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));
            _contextMenu.Foreground = System.Windows.Media.Brushes.White;
            _contextMenu.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60));
            _contextMenu.BorderThickness = new Thickness(1);
            _contextMenu.Padding = new Thickness(4);
            
            // 创建菜单项辅助方法
            MenuItem CreateMenuItem(string header, RoutedEventHandler handler)
            {
                var item = new MenuItem 
                { 
                    Header = header,
                    FontSize = 13,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    Foreground = System.Windows.Media.Brushes.White,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(12, 8, 12, 8),
                    BorderThickness = new Thickness(0)
                };
                item.Click += handler;
                item.MouseEnter += (s, e) => { if (s is MenuItem mi) mi.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)); };
                item.MouseLeave += (s, e) => { if (s is MenuItem mi) mi.Background = System.Windows.Media.Brushes.Transparent; };
                return item;
            }
            
            // 菜单项
            _contextMenu.Items.Add(CreateMenuItem("显示主窗口", (s, e) => ShowMainWindow()));
            _contextMenu.Items.Add(CreateMenuItem("显示/隐藏悬浮框", (s, e) => ToggleFloatingWindow()));
            
            // Dock 开关（带勾选标记）
            var dockMenuItem = new MenuItem 
            { 
                Header = "启用吸附",
                FontSize = 13,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Foreground = System.Windows.Media.Brushes.White,
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(12, 8, 12, 8),
                BorderThickness = new Thickness(0),
                IsCheckable = true,
                IsChecked = _floatingIslandWindow?.EnableDock ?? true
            };
            dockMenuItem.Click += (s, e) => 
            {
                bool newValue = !dockMenuItem.IsChecked;
                dockMenuItem.IsChecked = newValue;
                _floatingIslandWindow.EnableDock = newValue;
            };
            _contextMenu.Items.Add(dockMenuItem);
            
            _contextMenu.Items.Add(CreateMenuItem("贴顶部", (s, e) => SnapFloatingToTop()));
            _contextMenu.Items.Add(CreateMenuItem("贴主窗口", (s, e) => SnapFloatingToMainWindow()));
            
            // 分隔线
            var sep1 = new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)), Margin = new Thickness(8, 4, 8, 4) };
            _contextMenu.Items.Add(sep1);
            
            _contextMenu.Items.Add(CreateMenuItem("设置快捷键", (s, e) => ShowHotkeySettings()));
            
            var sep2 = new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)), Margin = new Thickness(8, 4, 8, 4) };
            _contextMenu.Items.Add(sep2);
            
            _contextMenu.Items.Add(CreateMenuItem("退出", (s, e) => ExitApplication()));
            
            _notifyIcon = new NotifyIcon
            {
                Text = "WebViewHub",
                Visible = true,
                ContextMenuStrip = null
            };
            
            // 拦截右键点击显示 WPF ContextMenu
            _notifyIcon.MouseDown += (s, e) => 
            {
                if (e.Button == MouseButtons.Right && _contextMenu != null)
                {
                    _contextMenu.PlacementTarget = null;
                    _contextMenu.IsOpen = true;
                }
            };
            
            // 加载应用图标作为托盘图标
            try {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            } catch { }
            
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ShowHotkeySettings()
        {
            // 简单的输入对话框
            var dialog = new System.Windows.Window
            {
                Title = "设置快捷键",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var label1 = new TextBlock 
            { 
                Text = "显示/隐藏悬浮框快捷键（F10）：", 
                Margin = new Thickness(0, 0, 0, 5),
                FontSize = 14
            };
            var input1 = new System.Windows.Controls.TextBox 
            { 
                Text = ((Keys)_hotkeyShowFloating).ToString(),
                FontSize = 14,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var label2 = new TextBlock 
            { 
                Text = "展开/收起聊天窗口快捷键（F9）：", 
                Margin = new Thickness(0, 0, 0, 5),
                FontSize = 14
            };
            var input2 = new System.Windows.Controls.TextBox 
            { 
                Text = ((Keys)_hotkeyShowChat).ToString(),
                FontSize = 14,
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var hint = new TextBlock
            {
                Text = "提示：输入如 F9, F10, Ctrl+Shift+A 等\n修改后重启生效",
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 11
            };

            var btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
            var okBtn = new System.Windows.Controls.Button { Content = "保存", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelBtn = new System.Windows.Controls.Button { Content = "取消", Width = 80 };

            okBtn.Click += (s, e) =>
            {
                if (Enum.TryParse<Keys>(input1.Text, true, out var k1))
                    _hotkeyShowFloating = (int)k1;
                if (Enum.TryParse<Keys>(input2.Text, true, out var k2))
                    _hotkeyShowChat = (int)k2;
                SaveHotkeyConfig();
                dialog.DialogResult = true;
                dialog.Close();
            };
            cancelBtn.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);

            panel.Children.Add(label1);
            panel.Children.Add(input1);
            panel.Children.Add(label2);
            panel.Children.Add(input2);
            panel.Children.Add(hint);
            panel.Children.Add(btnPanel);

            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ToggleFloatingWindow()
        {
            AppLogger.Debug("[MainWindow] ToggleFloatingWindow() called");
            _floatingIslandWindow?.Toggle();
        }

        private void SnapFloatingToTop()
        {
            _floatingIslandWindow?.SnapToTop();
        }

        private void SnapFloatingToMainWindow()
        {
            _floatingIslandWindow?.SnapToMainWindow(this);
        }

        private void ExitApplication()
        {
            _isExiting = true;
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        // 加载快捷键配置
        private void LoadHotkeyConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 2)
                    {
                        if (Enum.TryParse<Keys>(lines[0], true, out var key1))
                            _hotkeyShowFloating = (int)key1;
                        if (Enum.TryParse<Keys>(lines[1], true, out var key2))
                            _hotkeyShowChat = (int)key2;
                    }
                }
            }
            catch { }
        }

        // 保存快捷键配置
        private void SaveHotkeyConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir!);
                File.WriteAllLines(ConfigPath, new[] {
                    ((Keys)_hotkeyShowFloating).ToString(),
                    ((Keys)_hotkeyShowChat).ToString()
                });
            }
            catch { }
        }

        // 注册全局快捷键
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);

            // 注册快捷键
            RegisterHotKey(helper.Handle, HOTKEY_ID_SHOW_FLOATING, MOD_NONE, _hotkeyShowFloating);
            RegisterHotKey(helper.Handle, HOTKEY_ID_SHOW_CHAT, MOD_NONE, _hotkeyShowChat);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                AppLogger.Debug($"[MainWindow] HwndHook - Hotkey received: ID={hotkeyId}");
                if (hotkeyId == HOTKEY_ID_SHOW_FLOATING)
                {
                    // 显示/切换悬浮框
                    AppLogger.Debug("[MainWindow] HwndHook - Triggering ToggleFloatingWindow()");
                    ToggleFloatingWindow();
                    handled = true;
                }
                else if (hotkeyId == HOTKEY_ID_SHOW_CHAT)
                {
                    // F9 展开/收起聊天窗口（切换聊天面板）
                    AppLogger.Debug("[MainWindow] HwndHook - Triggering ToggleChatPanel()");
                    if (_floatingIslandWindow != null)
                    {
                        _floatingIslandWindow.Show();
                        _floatingIslandWindow.ToggleChatPanel();
                    }
                    else
                    {
                        AppLogger.Warning("[MainWindow] HwndHook - _floatingIslandWindow is null!");
                    }
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_SHOW_FLOATING);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_SHOW_CHAT);
            _notifyIcon?.Dispose();
            base.OnClosed(e);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLayout();
            UpdateCountText();

            // 启动独立 FloatingIslandWindow 窗口
            StartFloatingIslandWindow();
        }

        /// <summary>
        /// 启动独立的灵动岛悬浮窗口 (新 Window 方案 - 解决 Popup 事件丢失问题)
        /// Popup 没有独立窗口句柄，无法正确接收所有系统事件，
        /// 且无法真正置顶到其他应用程序之上。
        /// 使用独立的 WPF Window 可以解决这些问题。
        /// </summary>
        private void StartFloatingIslandWindow()
        {
            AppLogger.Debug("[MainWindow] StartFloatingIslandWindow() - Initializing...");
            _floatingIslandWindow = new FloatingIslandWindow
            {
                MainWindowReference = this,
                EnableDock = true  // 启用 Dock 功能
            };

            // 设置初始位置：屏幕右下角
            var workArea = SystemParameters.WorkArea;
            _floatingIslandWindow.Left = workArea.Width - 80;
            _floatingIslandWindow.Top = workArea.Height - 80;

            // 预加载：先显示窗口让WPF完成布局，然后隐藏
            // 这样第一次展开时就不会卡了
            AppLogger.Debug("[MainWindow] StartFloatingIslandWindow - Pre-loading window");
            _floatingIslandWindow.Show();
            _floatingIslandWindow.PreLoad();  // 预渲染所有控件
            _floatingIslandWindow.Hide();
            
            // 再次显示（图标模式）
            _floatingIslandWindow.Show();
            AppLogger.Debug($"[MainWindow] StartFloatingIslandWindow - Window ready at ({_floatingIslandWindow.Left}, {_floatingIslandWindow.Top})");
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果不是退出程序，则最小化到托盘
            if (!_isExiting)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            // 关闭时一并关闭浮动窗口
            _floatingIslandWindow?.Close();

            SaveLayout();
            foreach (var container in _webViews)
            {
                container.Cleanup();
            }
        }

        #region WebView 管理

        public IReadOnlyList<WebViewContainer> GetAllWebViews() => _webViews;
        
        // 供容器提升层级用的全局 Z 轴计数器
        public int GetNextZIndex()
        {
            return ++_currentZIndex;
        }

        private void AddWebView_Click(object sender, RoutedEventArgs e)
        {
            if (_webViews.Count >= MaxWebViews)
            {
                AppleMessageBox.Show(this, $"最多支持 {MaxWebViews} 个浏览器窗口", "提示");
                return;
            }

            _counter++;
            var profileId = $"Profile_{_counter}";
            
            // 计算新窗口的默认位置（默认排版）
            var (x, y) = CalculateDefaultPosition(_counter - 1);
            
            var container = CreateWebViewContainer(profileId, "https://www.baidu.com", string.Empty, false, x, y, DefaultWidth, DefaultHeight, 1.0);
            AddWebViewToCanvas(container);
            
            Dispatcher.InvokeAsync(() => {
                int cols = (int)Math.Ceiling(Math.Sqrt(_webViews.Count));
                ApplyGridLayout(Math.Max(2, cols), Math.Max(2, cols));
            });
        }

        private (double x, double y) CalculateDefaultPosition(int index)
        {
            var canvasWidth = LayoutCanvas.ActualWidth > 0 ? LayoutCanvas.ActualWidth : 1600;
            var canvasHeight = LayoutCanvas.ActualHeight > 0 ? LayoutCanvas.ActualHeight : 900;

            // 默认排版：2x2 网格，超出后换行
            var cols = Math.Ceiling((index + 1) / 2.0);
            var row = Math.Floor(index / 2.0);

            var x = DefaultMargin + (DefaultWidth + DefaultMargin) * (index % 2);
            var y = DefaultMargin + (DefaultHeight + DefaultMargin) * row;

            // 确保在画布范围内，并且不会超出去
            if (x + DefaultWidth > canvasWidth)
                x = Math.Max(0, canvasWidth - DefaultWidth);
            if (y + DefaultHeight > canvasHeight)
                y = Math.Max(0, canvasHeight - DefaultHeight);

            return (x, y);
        }

        private WebViewContainer CreateWebViewContainer(string profileId, string url, string roleTag, bool isMobileMode, double x, double y, double width, double height, double zoomFactor = 1.0)
        {
            var container = new WebViewContainer
            {
                ProfileID = profileId,
                CurrentUrl = string.IsNullOrEmpty(url) ? "https://www.baidu.com" : url,
                RoleTag = roleTag ?? string.Empty,
                IsMobileModeContent = isMobileMode,
                Width = width,
                Height = height,
                ZoomFactor = zoomFactor
            };

            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);

            container.CustomDragDelta += OnWebViewDragDelta;
            container.CustomResizeDelta += OnWebViewResizeDelta;
            container.DeleteRequested += OnWebViewDeleteRequested;
            container.PreviewMouseLeftButtonDown += (s, e) => BringToFront(container);

            BringToFront(container); // 新建窗口直接置顶
            return container;
        }

        private void BringToFront(WebViewContainer container)
        {
            _currentZIndex++;
            Canvas.SetZIndex(container, _currentZIndex);
        }

        private void AddWebViewToCanvas(WebViewContainer container)
        {
            LayoutCanvas.Children.Add(container);
            _webViews.Add(container);
            UpdateCountText();
        }

        private void RemoveWebView(WebViewContainer container)
        {
            LayoutCanvas.Children.Remove(container);
            _webViews.Remove(container);
            container.Cleanup();
            UpdateCountText();
            
            Dispatcher.InvokeAsync(() => {
                if (_webViews.Count > 0)
                {
                    int cols = (int)Math.Ceiling(Math.Sqrt(_webViews.Count));
                    ApplyGridLayout(Math.Max(2, cols), Math.Max(2, cols));
                }
            });
        }

        private void OnWebViewDeleteRequested(object? sender, WebViewContainer e)
        {
            if (sender is WebViewContainer container)
            {
                var result = AppleMessageBox.ShowConfirm(this, $"确定要删除 {container.ProfileName} 吗？", "确认删除");
                if (result)
                {
                    RemoveWebView(container);
                }
            }
        }

        #endregion

        #region 拖拽和调整大小（带磁吸）

        private void OnWebViewDragDelta(object? sender, CustomDragDeltaEventArgs e)
        {
            if (sender is not WebViewContainer container) return;

            var currentX = Canvas.GetLeft(container);
            var currentY = Canvas.GetTop(container);

            if (double.IsNaN(currentX)) currentX = 0;
            if (double.IsNaN(currentY)) currentY = 0;

            var newX = currentX + e.HorizontalChange;
            var newY = currentY + e.VerticalChange;

            // 限制新坐标在画布范围内
            var canvasWidth = LayoutCanvas.ActualWidth;
            var canvasHeight = LayoutCanvas.ActualHeight;

            // 考虑窗口的实际宽高等于 ActualWidth/ActualHeight。最大能移动的坐标就是画布宽高减去自己
            newX = Math.Max(0, Math.Min(newX, canvasWidth - container.ActualWidth));
            newY = Math.Max(0, Math.Min(newY, canvasHeight - container.ActualHeight));

            // 应用磁吸到其他窗口边缘
            (newX, newY) = ApplySnapToEdges(container, newX, newY);

            Canvas.SetLeft(container, newX);
            Canvas.SetTop(container, newY);
        }

        private void OnWebViewResizeDelta(object? sender, CustomResizeDeltaEventArgs e)
        {
            if (sender is not WebViewContainer container) return;

            var currentX = Canvas.GetLeft(container);
            var currentY = Canvas.GetTop(container);

            if (double.IsNaN(currentX)) currentX = 0;
            if (double.IsNaN(currentY)) currentY = 0;

            var canvasWidth = LayoutCanvas.ActualWidth;
            var canvasHeight = LayoutCanvas.ActualHeight;

            // 限制新宽高不超过画板边界，即起点 current + new 必须 <= canvas
            var newWidth = Math.Min(e.NewWidth, canvasWidth - currentX);
            var newHeight = Math.Min(e.NewHeight, canvasHeight - currentY);

            // 原有的宽度和高度，用于计算差值，分配给被推挤的窗口
            var deltaWidth = newWidth - container.Width;
            var deltaHeight = newHeight - container.Height;

            // 应用磁吸到其他窗口边缘 (联动缩放机制)
            // 如果 A (当前) 的右边缘刚好贴着 B (other) 的左边缘，那么 A 变宽时，B 也要向右挪并变窄
            foreach (var other in _webViews)
            {
                if (other == container) continue;
                var otherX = Canvas.GetLeft(other);
                var otherY = Canvas.GetTop(other);
                var otherWidth = other.ActualWidth;
                var otherHeight = other.ActualHeight;

                // 判断是否 Y 轴有交集（只有相邻或正对着才能联动左右）
                bool isYIntersecting = (currentY < otherY + otherHeight) && (currentY + container.Height > otherY);
                // 判断是否 X 轴有交集（只有相邻或正对着才能联动上下）
                bool isXIntersecting = (currentX < otherX + otherWidth) && (currentX + container.Width > otherX);

                // --- 处理右边缘推挤/拉扯 (联动相邻元素的左边缘) ---
                if (isYIntersecting && newWidth != container.Width)
                {
                    double distanceX = Math.Abs((currentX + container.Width) - otherX);

                    // 1. 如果之前已经贴合，则互相挤压联动
                    if (distanceX <= 2)
                    {
                        var proposedOtherWidth = Math.Max(300, otherWidth - deltaWidth);
                        var proposedOtherX = otherX + deltaWidth;
                        
                        // 满足最小宽度才能挤动，并且不出画板右边界
                        if (proposedOtherWidth >= 300 && proposedOtherX >= 0 && (proposedOtherX + proposedOtherWidth) <= canvasWidth)
                        {
                            Canvas.SetLeft(other, proposedOtherX);
                            other.Width = proposedOtherWidth;
                            
                            // A 磁吸严格贴在 B 调整后的左边缘
                            newWidth = proposedOtherX - currentX;
                        }
                        else
                        {
                            // A 也不能越过推不动的边界
                            newWidth = otherX - currentX;
                        }
                    }
                    // 2. 如果未贴合，但是在磁吸抓取范围内，则自动贴靠消除缝隙
                    else if (Math.Abs(currentX + newWidth - otherX) < SnapThreshold)
                    {
                        newWidth = otherX - currentX;
                    }
                }

                // --- 处理下边缘推挤/拉扯 (联动相邻元素的上边缘) ---
                if (isXIntersecting && newHeight != container.Height)
                {
                    double distanceY = Math.Abs((currentY + container.Height) - otherY);

                    // 1. 如果之前已经贴合，则互相挤压联动
                    if (distanceY <= 2)
                    {
                        var proposedOtherHeight = Math.Max(200, otherHeight - deltaHeight);
                        var proposedOtherY = otherY + deltaHeight;
                        
                        if (proposedOtherHeight >= 200 && proposedOtherY >= 0 && (proposedOtherY + proposedOtherHeight) <= canvasHeight)
                        {
                            Canvas.SetTop(other, proposedOtherY);
                            other.Height = proposedOtherHeight;

                            newHeight = proposedOtherY - currentY;
                        }
                        else
                        {
                            newHeight = otherY - currentY;
                        }
                    }
                    // 2. 如果未贴合，但是在磁吸抓取范围内，则自动贴靠消除缝隙
                    else if (Math.Abs(currentY + newHeight - otherY) < SnapThreshold)
                    {
                        newHeight = otherY - currentY;
                    }
                }
            }

            // 最终如果由于各种运算超出画板边界也要兜底
            currentX = Canvas.GetLeft(container);
            currentY = Canvas.GetTop(container);
            container.Width = Math.Min(newWidth, LayoutCanvas.ActualWidth - currentX);
            container.Height = Math.Min(newHeight, LayoutCanvas.ActualHeight - currentY);
            
            e.Handled = true; // 由 MainWindow 接管，Container 不再直接改宽高
        }

        #endregion

        #region 磁吸逻辑

        private (double x, double y) ApplySnapToEdges(WebViewContainer currentContainer, double x, double y)
        {
            var snappedX = x;
            var snappedY = y;

            foreach (var other in _webViews)
            {
                if (other == currentContainer) continue;

                var otherX = Canvas.GetLeft(other);
                var otherY = Canvas.GetTop(other);
                var otherWidth = other.ActualWidth;
                var otherHeight = other.ActualHeight;
                var currentWidth = currentContainer.ActualWidth;
                var currentHeight = currentContainer.ActualHeight;

                // 水平对齐检查
                var rightEdgeX = x + currentWidth;
                var centerX = x + currentWidth / 2;
                var otherRightEdgeX = otherX + otherWidth;
                var otherCenterX = otherX + otherWidth / 2;

                if (Math.Abs(x - otherX) < SnapThreshold)
                    snappedX = otherX;
                else if (Math.Abs(x - otherRightEdgeX) < SnapThreshold)
                    snappedX = otherRightEdgeX;
                else if (Math.Abs(x - otherCenterX) < SnapThreshold)
                    snappedX = otherCenterX;
                else if (Math.Abs(rightEdgeX - otherX) < SnapThreshold)
                    snappedX = otherX - currentWidth;
                else if (Math.Abs(rightEdgeX - otherRightEdgeX) < SnapThreshold)
                    snappedX = otherRightEdgeX - currentWidth;
                else if (Math.Abs(rightEdgeX - otherCenterX) < SnapThreshold)
                    snappedX = otherCenterX - currentWidth / 2;

                // 垂直对齐检查
                var bottomEdgeY = y + currentHeight;
                var centerY = y + currentHeight / 2;
                var otherBottomEdgeY = otherY + otherHeight;
                var otherCenterY = otherY + otherHeight / 2;

                if (Math.Abs(y - otherY) < SnapThreshold)
                    snappedY = otherY;
                else if (Math.Abs(y - otherBottomEdgeY) < SnapThreshold)
                    snappedY = otherBottomEdgeY;
                else if (Math.Abs(y - otherCenterY) < SnapThreshold)
                    snappedY = otherCenterY;
                else if (Math.Abs(bottomEdgeY - otherY) < SnapThreshold)
                    snappedY = otherY - currentHeight;
                else if (Math.Abs(bottomEdgeY - otherBottomEdgeY) < SnapThreshold)
                    snappedY = otherBottomEdgeY - currentHeight;
                else if (Math.Abs(bottomEdgeY - otherCenterY) < SnapThreshold)
                    snappedY = otherCenterY - currentHeight / 2;
            }

            return (snappedX, snappedY);
        }

        private double SnapToGrid(double value)
        {
            return Math.Round(value / GridSize) * GridSize;
        }

        #endregion

        #region 布局保存和加载

        private void SaveLayout_Click(object sender, RoutedEventArgs e)
        {
            SaveLayout();
            AppleMessageBox.Show(this, "布局已保存", "成功");
        }

        public void SaveLayout()
        {
            var layout = _webViews.Select(w => new LayoutData
            {
                ProfileID = w.ProfileID,
                Url = w.CurrentUrl,
                RoleTag = w.RoleTag,
                IsMobileMode = w.IsMobileModeContent,
                X = Canvas.GetLeft(w),
                Y = Canvas.GetTop(w),
                Width = w.Width,
                Height = w.Height,
                ZoomFactor = w.WebView.GetZoomFactor() / (Math.Max(0.3, Math.Min(1.0, w.ActualWidth / 1000.0)))
            }).ToList();

            _layoutService.SaveLayout(layout);
        }

        private void LoadLayout()
        {
            var layout = _layoutService.LoadLayout();
            if (layout == null || layout.Count == 0) return;

            LayoutCanvas.Children.Clear();
            _webViews.Clear();

            int maxId = 0;

            foreach (var item in layout)
            {
                var container = CreateWebViewContainer(item.ProfileID, item.Url, item.RoleTag, item.IsMobileMode, item.X, item.Y, item.Width, item.Height, item.ZoomFactor);
                AddWebViewToCanvas(container);

                if (!string.IsNullOrEmpty(item.ProfileID) && item.ProfileID.StartsWith("Profile_"))
                {
                    if (int.TryParse(item.ProfileID.Substring(8), out int id))
                    {
                        if (id > maxId) maxId = id;
                    }
                }
            }

            _counter = maxId;

            Dispatcher.InvokeAsync(() => {
                if (_webViews.Count > 0)
                {
                    int cols = (int)Math.Ceiling(Math.Sqrt(_webViews.Count));
                    ApplyGridLayout(Math.Max(2, cols), Math.Max(2, cols));
                }
            }, DispatcherPriority.Loaded);
        }

        #endregion

        #region 辅助方法

        private void UpdateCountText()
        {
            CountText.Text = $"{_webViews.Count}/{MaxWebViews}";
        }

        private void PrayModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (PrayModeToggle.IsChecked == true)
            {
                _isPrayModeEnabled = true;
                PrayModeToggle.Content = "小神仙保佑";
            }
            else
            {
                _isPrayModeEnabled = false;
                PrayModeToggle.Content = "停止保佑";
                
                // 停止时立即收回所有现存的神仙
                foreach (var container in _webViews)
                {
                    var coreWebView = container.WebView.GetCoreWebView2();
                    _ = HidePrayingAnimationForWebView(coreWebView);
                }
            }
        }

        public async Task ShowPrayingAnimationForWebView(CoreWebView2? coreWebView)
        {
            if (!_isPrayModeEnabled || coreWebView == null) return;
            string base64Image = GetPrayingImageBase64();
            if (string.IsNullOrEmpty(base64Image)) return;

            string script = $@"
                (function() {{
                    let styleId = 'my-praying-pet-style';
                    if (!document.getElementById(styleId)) {{
                        let style = document.createElement('style');
                        style.id = styleId;
                        style.innerHTML = `
                            @keyframes petFloat {{
                                0% {{ transform: translate(-50%, -50%) scale(1); }}
                                50% {{ transform: translate(-50%, calc(-50% - 15px)) scale(1); }}
                                100% {{ transform: translate(-50%, -50%) scale(1); }}
                            }}
                        `;
                        document.head.appendChild(style);
                    }}

                    let pet = document.getElementById('my-praying-pet');
                    if (!pet) {{
                        pet = document.createElement('img');
                        pet.id = 'my-praying-pet';
                        pet.src = '{base64Image}';
                        pet.style.position = 'fixed';
                        pet.style.top = '50%';
                        pet.style.left = '50%';
                        pet.style.transform = 'translate(-50%, -50%) scale(0.5)';
                        pet.style.width = '160px';
                        pet.style.height = '160px';
                        pet.style.zIndex = '2147483647';
                        pet.style.cursor = 'pointer';
                        pet.style.transition = 'opacity 0.3s, transform 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275)';
                        pet.style.opacity = '0';
                        pet.style.filter = 'drop-shadow(0px 10px 15px rgba(0,0,0,0.15))';
                        
                        pet.onclick = function() {{
                            this.style.animation = 'none';
                            void this.offsetWidth;
                            this.style.opacity = '0';
                            this.style.pointerEvents = 'none';
                            this.style.transform = 'translate(-50%, -50%) scale(0.5)';
                            setTimeout(() => {{ if (this.parentNode) this.parentNode.removeChild(this); }}, 400);
                        }};
                        
                        document.body.appendChild(pet);
                    }}
                    
                    pet.style.pointerEvents = 'auto'; 
                    setTimeout(() => {{
                        pet.style.opacity = '1';
                        pet.style.transform = 'translate(-50%, -50%) scale(1)';
                        setTimeout(() => {{
                            if (pet.style.opacity === '1') {{
                                pet.style.animation = 'petFloat 3s ease-in-out infinite';
                            }}
                        }}, 400);
                    }}, 50);
                }})();
            ";

            try { await coreWebView.ExecuteScriptAsync(script); } catch { }
        }

        public async Task HidePrayingAnimationForWebView(CoreWebView2? coreWebView)
        {
            if (coreWebView == null) return;
            string script = $@"
                (function() {{
                    let pet = document.getElementById('my-praying-pet');
                    if (pet) {{
                        pet.style.animation = 'none';
                        void pet.offsetWidth;
                        pet.style.opacity = '0';
                        pet.style.transform = 'translate(-50%, -50%) scale(0.5)';
                        pet.style.pointerEvents = 'none';
                        setTimeout(() => {{ if (pet.parentNode) pet.parentNode.removeChild(pet); }}, 400);
                    }}
                }})();
            ";
            try { await coreWebView.ExecuteScriptAsync(script); } catch { }
        }

        private string _prayingImageBase64 = string.Empty;
        private string GetPrayingImageBase64()
        {
            if (!string.IsNullOrEmpty(_prayingImageBase64)) return _prayingImageBase64;
            
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/praying_character.png");
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null)
                {
                    using var memoryStream = new System.IO.MemoryStream();
                    streamInfo.Stream.CopyTo(memoryStream);
                    _prayingImageBase64 = "data:image/png;base64," + Convert.ToBase64String(memoryStream.ToArray());
                }
            }
            catch { }

            return _prayingImageBase64;
        }

        #endregion

        #region 自动化布局方案

        private void ApplyLayout2x2_Click(object sender, RoutedEventArgs e)
        {
            ApplyGridLayout(2, 2);
        }

        private void ApplyLayout3x3_Click(object sender, RoutedEventArgs e)
        {
            ApplyGridLayout(3, 3);
        }

        private void ApplyLayoutSplitVertical_Click(object sender, RoutedEventArgs e)
        {
            ApplyGridLayout(2, 1);
        }

        private void ApplyLayoutSplitHorizontal_Click(object sender, RoutedEventArgs e)
        {
            ApplyGridLayout(1, 2);
        }

        private void ApplyLayoutWaterfall_Click(object sender, RoutedEventArgs e)
        {
            var count = _webViews.Count;
            if (count == 0) return;

            var totalW = LayoutCanvas.ActualWidth;
            var totalH = LayoutCanvas.ActualHeight;
            double gutter = 12.0;

            double w = totalW - gutter * 2;
            double h = (totalH - gutter * (count + 1)) / count;

            for (int i = 0; i < count; i++)
            {
                double x = gutter;
                double y = gutter + i * (h + gutter);
                AnimateToPosition(_webViews[i], x, y, w, h);
            }
        }

        /// <summary>
        /// 核心网格满屏布局：重新构建以绝对缝隙边界无死角平铺，彻底排除宽度侧漏和相互覆盖
        /// </summary>
        private void ApplyGridLayout(int targetCols, int targetRows)
        {
            var count = _webViews.Count;
            if (count == 0) return;

            var totalW = LayoutCanvas.ActualWidth;
            var totalH = LayoutCanvas.ActualHeight;
            double gutter = 12.0;

            int cols = Math.Min(targetCols, count);
            int rows = (int)Math.Ceiling((double)count / cols);

            // 精确计算扣除全部外围缝隙和内部缝隙之后每个块体的基准宽高
            double cellW = (totalW - gutter * (cols + 1)) / cols;
            double cellH = (totalH - gutter * (rows + 1)) / rows;

            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                
                double w = cellW;
                // 对于最后一行没满的情况，把本行其余未分配的空间平分扩展（可选）
                if (r == rows - 1) 
                {
                    int itemsInLastRow = count - (r * cols);
                    if (itemsInLastRow > 0 && itemsInLastRow < cols)
                    {
                        w = (totalW - gutter * (itemsInLastRow + 1)) / itemsInLastRow;
                        c = i - (r * cols);
                    }
                }

                double x = gutter + c * (w + gutter);
                double y = gutter + r * (cellH + gutter);

                AnimateToPosition(_webViews[i], x, y, w, cellH);
            }
        }

        private void AnimateToPosition(WebViewContainer container, double x, double y, double width, double height)
        {
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);
            container.Width  = Math.Max(200, width);
            container.Height = Math.Max(150, height);
        }

        #endregion
    }
}

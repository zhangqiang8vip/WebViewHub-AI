using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WebViewHub
{
    public partial class FloatingIslandWindow : Window
    {
        private DispatcherTimer _inactiveTimer;
        private bool _isExpanded = false;
        
        public MainWindow MainWindowReference { get; set; }

        // ========== Dock 功能相关属性 ==========
        private bool _enableDock = false;
        public bool EnableDock 
        { 
            get => _enableDock;
            set 
            {
                _enableDock = value;
                if (_enableDock)
                {
                    StartDockMonitoring();
                }
                else
                {
                    StopDockMonitoring();
                }
                // 更新取消吸附按钮显示状态
                UpdateUndockButtonVisibility();
            }
        }

        // Dock 位置：top/bottom/left/right/null（未吸附）
        private string? _dockPosition = null;
        private const double DOCK_THRESHOLD = 80; // 吸附阈值（像素），增加到80

        // 配置保存
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WebViewHub", "floating_island.txt");

        // 记录的位置和大小
        private double _savedIconLeft;
        private double _savedIconTop;
        private double _savedExpandedWidth = 400;
        private double _savedExpandedHeight = 300;
        private bool _hasSavedIconPosition = false;

        // 拖拽状态
        private bool _isDragging = false;
        private Point _dragStartScreenPos;  // 屏幕坐标
        private double _dragStartLeft;
        private double _dragStartTop;

        // 缩放状态
        private bool _isResizing = false;
        private string _resizeDirection = "";
        private Point _resizeStartScreenPos;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _resizeStartLeft;
        private double _resizeStartTop;

        // 点击判定
        private bool _hasMoved = false;

        public FloatingIslandWindow()
        {
            InitializeComponent();
            
            // 键盘事件 - ESC 隐藏
            KeyDown += FloatingIslandWindow_KeyDown;
            
            _inactiveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _inactiveTimer.Tick += InactiveTimer_Tick;

            LoadConfig();

            // 鼠标悬停定时器 - 悬停2秒展开
            _hoverTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _hoverTimer.Tick += HoverTimer_Tick;
        }

        // 预加载方法 - 强制渲染所有控件，避免首次展开时卡顿
        public void PreLoad()
        {
            // 强制布局更新
            UpdateLayout();
            
            // 预先设置展开状态并强制渲染
            if (FloatingPanel.Visibility != Visibility.Visible)
            {
                FloatingPanel.Visibility = Visibility.Visible;
                FloatingPanel.UpdateLayout();
                FloatingPanel.Visibility = Visibility.Collapsed;
            }
            
            // 强制渲染图标
            IconGrid.UpdateLayout();
        }

        // 鼠标悬停定时器
        private DispatcherTimer? _hoverTimer;
        private bool _isHovering = false;

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer?.Stop();
            if (_isHovering && !_isExpanded)
            {
                // 悬停2秒后自动展开
                Expand();
            }
        }

        // 加载配置
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 4)
                    {
                        double.TryParse(lines[0], out _savedIconLeft);
                        double.TryParse(lines[1], out _savedIconTop);
                        double.TryParse(lines[2], out _savedExpandedWidth);
                        double.TryParse(lines[3], out _savedExpandedHeight);
                        _hasSavedIconPosition = true;
                    }
                }
            }
            catch { }
        }

        // 保存配置
        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllLines(ConfigPath, new[] {
                    _savedIconLeft.ToString(),
                    _savedIconTop.ToString(),
                    _savedExpandedWidth.ToString(),
                    _savedExpandedHeight.ToString()
                });
            }
            catch { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommandPanel.MainWindowReference = MainWindowReference;

            // 恢复图标位置
            if (_hasSavedIconPosition)
            {
                Left = _savedIconLeft;
                Top = _savedIconTop;
                // 确保在屏幕内
                double sw = SystemParameters.WorkArea.Width;
                double sh = SystemParameters.WorkArea.Height;
                Left = Math.Max(-25, Math.Min(Left, sw - 25));
                Top = Math.Max(0, Math.Min(Top, sh - 50));
            }

            _inactiveTimer.Start();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_isExpanded) return;
            
            // 获取当前激活的窗口句柄
            IntPtr activeWindow = GetForegroundWindow();
            
            // 获取主窗口句柄
            if (MainWindowReference != null)
            {
                var mainWindowHandle = new System.Windows.Interop.WindowInteropHelper(MainWindowReference).Handle;
                
                // 如果激活的是主窗口，不隐藏聊天框
                if (activeWindow == mainWindowHandle)
                {
                    return;
                }
            }
            
            // 其他情况：收起聊天框
                Collapse();
            }
        
        // 获取前景窗口句柄的 API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // ========== 拖拽处理 - 使用屏幕坐标 ==========
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            // 停止所有动画
            BeginAnimation(Window.LeftProperty, null);
            BeginAnimation(Window.TopProperty, null);

            if (_isExpanded)
            {
                // 展开状态下，点击标题栏区域才开始拖拽
                var pos = e.GetPosition(FloatingPanel);
                if (pos.Y <= 24) // 标题栏区域内
        {
            _isDragging = true;
                    _hasMoved = false;
                    _dragStartLeft = Left;
                    _dragStartTop = Top;
                    _dragStartScreenPos = PointToScreen(e.GetPosition(this));
                    CaptureMouse();
                    e.Handled = true;
                }
                return;
            }

            // 收起状态
            if (e.OriginalSource is System.Windows.Shapes.Rectangle)
            {
                return;
            }

            _isDragging = true;
            _hasMoved = false;
            _dragStartLeft = Left;
            _dragStartTop = Top;
            _dragStartScreenPos = PointToScreen(e.GetPosition(this));
            
            CaptureMouse();
            RestartInactiveTimer();
            ResetOpacity();
            e.Handled = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                // 使用屏幕坐标计算 delta
                Point currentScreenPos = PointToScreen(e.GetPosition(this));
                double deltaX = currentScreenPos.X - _dragStartScreenPos.X;
                double deltaY = currentScreenPos.Y - _dragStartScreenPos.Y;

                if (Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1)
            {
                    _hasMoved = true;
                }

                Left = _dragStartLeft + deltaX;
                Top = _dragStartTop + deltaY;
            }
            else if (_isResizing && e.LeftButton == MouseButtonState.Pressed)
            {
                // Resize 也使用屏幕坐标
                Point currentScreenPos = PointToScreen(e.GetPosition(this));
                HandleResize(currentScreenPos);
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (_isDragging)
        {
            _isDragging = false;
                ReleaseMouseCapture();
                
                // 保存图标位置
                _savedIconLeft = Left;
                _savedIconTop = Top;
                _hasSavedIconPosition = true;
                SaveConfig();

                // Dock 功能：检测是否应该吸附到主窗口
                CheckDockOnDrag();

                // 收起状态：没有移动=点击，有移动=拖拽
                if (!_isExpanded && !_hasMoved)
            {
                    ExpandAndFocus();
                }
                else if (!_isExpanded && _hasMoved)
                {
                    SnapToEdge();
                }
                RestartInactiveTimer();
                e.Handled = true;
            }
            else if (_isResizing)
            {
                _isResizing = false;
                ReleaseMouseCapture();

                // 保存缩放后的大小
                _savedExpandedWidth = Width;
                _savedExpandedHeight = Height;
                SaveConfig();

            RestartInactiveTimer();
                e.Handled = true;
            }
            else if (_isDragging && _isExpanded)
            {
                // 展开状态下拖拽结束后，也要保存当前位置和尺寸
                _savedIconLeft = Left;
                _savedIconTop = Top;
                _savedExpandedWidth = Width;
                _savedExpandedHeight = Height;
                _hasSavedIconPosition = true;
                SaveConfig();
            }
        }

        // ========== 缩放处理 - 使用屏幕坐标 ==========
        private void ResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Shapes.Rectangle rect && _isExpanded)
            {
                _isResizing = true;
                _resizeDirection = rect.Tag?.ToString() ?? "SE";

                // 使用屏幕坐标
                _resizeStartScreenPos = PointToScreen(e.GetPosition(this));
                _resizeStartWidth = Width;
                _resizeStartHeight = Height;
                _resizeStartLeft = Left;
                _resizeStartTop = Top;

                // 停止动画
                BeginAnimation(Window.LeftProperty, null);
                BeginAnimation(Window.TopProperty, null);
                BeginAnimation(Window.WidthProperty, null);
                BeginAnimation(Window.HeightProperty, null);

                CaptureMouse();
                e.Handled = true;
            RestartInactiveTimer();
                ResetOpacity();
            }
        }

        private void HandleResize(Point currentScreenPos)
        {
            double minWidth = 200;
            double minHeight = 150;
            double maxWidth = SystemParameters.WorkArea.Width;
            double maxHeight = SystemParameters.WorkArea.Height;

            double deltaX = currentScreenPos.X - _resizeStartScreenPos.X;
            double deltaY = currentScreenPos.Y - _resizeStartScreenPos.Y;

            switch (_resizeDirection)
            {
                case "SE":
                    Width = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth + deltaX));
                    Height = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight + deltaY));
                    break;
                case "SW":
                    double newWidthSW = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth - deltaX));
                    if (newWidthSW != Width)
                    {
                        Left = _resizeStartLeft + (_resizeStartWidth - newWidthSW);
                        Width = newWidthSW;
                    }
                    Height = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight + deltaY));
                    break;
                case "NE":
                    Width = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth + deltaX));
                    double newHeightNE = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight - deltaY));
                    if (newHeightNE != Height)
                    {
                        Top = _resizeStartTop + (_resizeStartHeight - newHeightNE);
                        Height = newHeightNE;
                    }
                    break;
                case "NW":
                    double newWidthNW = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth - deltaX));
                    if (newWidthNW != Width)
                    {
                        Left = _resizeStartLeft + (_resizeStartWidth - newWidthNW);
                        Width = newWidthNW;
                    }
                    double newHeightNW = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight - deltaY));
                    if (newHeightNW != Height)
                    {
                        Top = _resizeStartTop + (_resizeStartHeight - newHeightNW);
                        Height = newHeightNW;
                    }
                    break;
                case "E":
                    Width = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth + deltaX));
                    break;
                case "W":
                    double newWidthW = Math.Max(minWidth, Math.Min(maxWidth, _resizeStartWidth - deltaX));
                    if (newWidthW != Width)
                    {
                        Left = _resizeStartLeft + (_resizeStartWidth - newWidthW);
                        Width = newWidthW;
                    }
                    break;
                case "S":
                    Height = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight + deltaY));
                    break;
                case "N":
                    double newHeightN = Math.Max(minHeight, Math.Min(maxHeight, _resizeStartHeight - deltaY));
                    if (newHeightN != Height)
                    {
                        Top = _resizeStartTop + (_resizeStartHeight - newHeightN);
                        Height = newHeightN;
                    }
                    break;
            }
        }

        // ========== 标题栏拖拽处理 ==========
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isExpanded)
            {
                _isDragging = true;
                _hasMoved = false;
                _dragStartLeft = Left;
                _dragStartTop = Top;
                _dragStartScreenPos = PointToScreen(e.GetPosition(this));
                
                // 停止动画
                BeginAnimation(Window.LeftProperty, null);
                BeginAnimation(Window.TopProperty, null);
                
                CaptureMouse();
                e.Handled = true;
                RestartInactiveTimer();
                ResetOpacity();
            }
        }

        // ========== 闲置处理 ==========
        private void FloatingIsland_MouseEnter(object sender, MouseEventArgs e)
        {
            ResetOpacity();
            _inactiveTimer.Stop();
            
            // 启动悬停定时器
            _isHovering = true;
            _hoverTimer?.Stop();
            _hoverTimer?.Start();
        }

        private void FloatingIsland_MouseLeave(object sender, MouseEventArgs e)
        {
            // 停止悬停定时器
            _isHovering = false;
            _hoverTimer?.Stop();
            
            if (!_isExpanded)
            {
                RestartInactiveTimer();
            }
        }

        private void RestartInactiveTimer()
        {
            _inactiveTimer.Stop();
            if (!_isExpanded)
            {
                _inactiveTimer.Start();
            }
        }

        private void InactiveTimer_Tick(object? sender, EventArgs e)
        {
            _inactiveTimer.Stop();
            if (!_isExpanded && !IsMouseOver)
            {
                // 闲置状态降透明度
                var anim = new DoubleAnimation(0.3, new Duration(TimeSpan.FromMilliseconds(180)));
                BeginAnimation(OpacityProperty, anim);

                // 根据所在屏幕吸附一半身位
                double cx = Left + Width / 2;
                double sw = SystemParameters.WorkArea.Width;
                double targetX = cx < sw / 2 ? -25 : sw - 25;
                
                var moveAnim = new DoubleAnimation(targetX, new Duration(TimeSpan.FromMilliseconds(180)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(LeftProperty, moveAnim);
            }
        }

        private void ResetOpacity()
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;
            
            double sw = SystemParameters.WorkArea.Width;
            if (Left < 0)
            {
                var moveAnim = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(120))) 
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                BeginAnimation(LeftProperty, moveAnim);
            }
            else if (Left > sw - 50)
            {
                var moveAnim = new DoubleAnimation(sw - 50, new Duration(TimeSpan.FromMilliseconds(120))) 
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                BeginAnimation(LeftProperty, moveAnim);
            }
        }

        // 拖拽结束时自动吸附
        private void SnapToEdge()
        {
            // 如果启用了 Dock 功能，先尝试吸附到主窗口
            if (_enableDock && _dockPosition != null)
            {
                // 已经通过 CheckDockOnDrag 确定了吸附位置，直接更新位置
                UpdateDockPosition();
                return;
            }

            // 否则吸附到屏幕边缘（原始逻辑）
            double sw = SystemParameters.WorkArea.Width;
            double targetX = (Left + Width / 2) < (sw / 2) ? 0 : (sw - 50);
            
            var animX = new DoubleAnimation(targetX, new Duration(TimeSpan.FromMilliseconds(180)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(LeftProperty, animX);

            // 保存吸附后的位置
            _savedIconLeft = targetX;
            SaveConfig();
        }

        // ========== 展开/收缩动作 ==========
        public void Expand()
        {
            AppLogger.Debug($"[FloatingIsland] Expand() called - _isExpanded={_isExpanded}, Width={Width}, Height={Height}");
            
            if (_isExpanded) 
            {
                AppLogger.Debug("[FloatingIsland] Expand() skipped - already expanded");
                return; 
            }

            // 兜底：确保窗口可见并激活
            AppLogger.Debug("[FloatingIsland] Calling EnsureVisibleAndActivated()");
            EnsureVisibleAndActivated();
            AppLogger.Debug($"[FloatingIsland] After EnsureVisibleAndActivated - Visibility={Visibility}, Left={Left}, Top={Top}");

            // 最终尺寸保护：确保有有效的展开尺寸
            if (_savedExpandedWidth <= 50) _savedExpandedWidth = 500;
            if (_savedExpandedHeight <= 50) _savedExpandedHeight = 400;
            
            // 强制保存当前尺寸为有效的展开尺寸
            _savedExpandedWidth = Width > 50 ? Width : 500;
            _savedExpandedHeight = Height > 50 ? Height : 400;
            AppLogger.Debug($"[FloatingIsland] Saved sizes - _savedExpandedWidth={_savedExpandedWidth}, _savedExpandedHeight={_savedExpandedHeight}");

            // 如果启用了 Dock 功能，根据主窗口状态决定展开方向
            if (_enableDock && MainWindowReference != null)
            {
                var mainState = MainWindowReference.WindowState;
                AppLogger.Debug($"[FloatingIsland] Dock enabled, mainState={mainState}");
                if (mainState == WindowState.Maximized)
                {
                    AppLogger.Debug("[FloatingIsland] Calling ExpandTowardsScreen()");
                    ExpandTowardsScreen();
                    return;
                }
                else
                {
                    AppLogger.Debug("[FloatingIsland] Calling ExpandTowardsOutside()");
                    ExpandTowardsOutside();
                    return;
                }
            }

            // 默认展开逻辑
            AppLogger.Debug("[FloatingIsland] Calling ExpandDefault()");
            ExpandDefault();
        }

        // 默认展开方式
        public void ExpandDefault()
        {
            _isExpanded = true;
            _inactiveTimer.Stop();
            _hoverTimer?.Stop();
            ResetOpacity();

            IconGrid.Visibility = Visibility.Collapsed;
            UndockButton.Visibility = Visibility.Collapsed;
            FloatingPanel.Visibility = Visibility.Visible;

            // 停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // 确保有默认展开尺寸（防止 0 尺寸问题）
            if (_savedExpandedWidth <= 0) _savedExpandedWidth = 500;
            if (_savedExpandedHeight <= 0) _savedExpandedHeight = 400;

            // 斜对角扩展：从图标位置同时向四周扩展
            double iconCenterX = _savedIconLeft + 25;
            double iconCenterY = _savedIconTop + 25;

            // 计算最终位置（保持中心相对位置）
            double newLeft = iconCenterX - _savedExpandedWidth / 2;
            double newTop = iconCenterY - _savedExpandedHeight / 2;
            newLeft = Math.Max(0, Math.Min(newLeft, SystemParameters.WorkArea.Width - _savedExpandedWidth));
            newTop = Math.Max(0, Math.Min(newTop, SystemParameters.WorkArea.Height - _savedExpandedHeight));

            // 设置初始值（图标大小）
            Width = 50;
            Height = 50;
            Left = _savedIconLeft;
            Top = _savedIconTop;

            // 斜对角扩展动画：同时改变位置和大小，产生从中心向外扩展的效果
            var widthAnim = new DoubleAnimation(_savedExpandedWidth, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            
            var heightAnim = new DoubleAnimation(_savedExpandedHeight, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            FloatingIsland.CornerRadius = new CornerRadius(16);

            // 同时动画位置，产生斜对角效果
            var leftAnim = new DoubleAnimation(newLeft, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var topAnim = new DoubleAnimation(newTop, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            BeginAnimation(WidthProperty, widthAnim);
            BeginAnimation(HeightProperty, heightAnim);
            BeginAnimation(LeftProperty, leftAnim);
            BeginAnimation(TopProperty, topAnim);
            
            Activate();
            CommandPanel.Focus();
        }

        // 展开并聚焦输入框（快捷键使用）
        public void ExpandAndFocus()
        {
            AppLogger.Debug("[FloatingIsland] ExpandAndFocus() called");
            Expand();
            // 聚焦到输入框
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppLogger.Debug("[FloatingIsland] ExpandAndFocus - Focusing input");
                CommandPanel.FocusInput();
            }), DispatcherPriority.Input);
        }

        public void Collapse()
        {
            AppLogger.Debug($"[FloatingIsland] Collapse() called - _isExpanded={_isExpanded}");
            if (!_isExpanded) 
            {
                AppLogger.Debug("[FloatingIsland] Collapse() skipped - already collapsed");
                return;
            }
            _isExpanded = false;

            IconGrid.Visibility = Visibility.Visible;
            FloatingPanel.Visibility = Visibility.Collapsed;

            // 更新取消吸附按钮显示状态
            UpdateUndockButtonVisibility();

            // 停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // 斜对角收缩：同时向中心收缩
            FloatingIsland.CornerRadius = new CornerRadius(25);

            double newLeft = _savedIconLeft;
            double newTop = _savedIconTop;
            newLeft = Math.Max(0, Math.Min(newLeft, SystemParameters.WorkArea.Width - 50));
            newTop = Math.Max(0, Math.Min(newTop, SystemParameters.WorkArea.Height - 50));

            // 同步动画位置和大小，产生斜对角收缩效果
            var widthAnim = new DoubleAnimation(50, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            
            var heightAnim = new DoubleAnimation(50, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            var leftAnim = new DoubleAnimation(newLeft, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var topAnim = new DoubleAnimation(newTop, new Duration(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            BeginAnimation(WidthProperty, widthAnim);
            BeginAnimation(HeightProperty, heightAnim);
            BeginAnimation(LeftProperty, leftAnim);
            BeginAnimation(TopProperty, topAnim);

            RestartInactiveTimer();
        }

        // 切换聊天面板展开/收起（F9 快捷键使用）
        public void ToggleChatPanel()
        {
            // 记录初始状态
            bool wasHidden = Visibility != Visibility.Visible;
            AppLogger.Debug($"[FloatingIsland] ToggleChatPanel() called - wasHidden={wasHidden}, _isExpanded={_isExpanded}");
            
            // 确保窗口显示
            if (wasHidden)
            {
                var workArea = SystemParameters.WorkArea;
                AppLogger.Debug($"[FloatingIsland] ToggleChatPanel - Restoring position. Left={Left}, Top={Top}, WorkArea={workArea.Width}x{workArea.Height}");
                if (Left < 0 || Left > workArea.Width - 50)
                    Left = workArea.Width - 80;
                if (Top < 0 || Top > workArea.Height - 50)
                    Top = workArea.Height - 80;
                Show();
                Activate();
                AppLogger.Debug($"[FloatingIsland] ToggleChatPanel - Window shown and activated. New Pos: ({Left}, {Top})");
            }

            // 切换展开/收起状态
            if (_isExpanded)
            {
                Collapse();
                
                // 如果初始状态是隐藏的，折叠后也隐藏整个窗口
                if (wasHidden)
                {
                    Hide();
                }
            }
            else
            {
                ExpandAndFocus();
            }
        }

        // 切换显示/隐藏悬浮框（仅隐藏/显示窗口，不展开聊天面板）
        public void Toggle()
        {
            AppLogger.Debug($"[FloatingIsland] Toggle() called - Visibility={Visibility}");
            if (Visibility != Visibility.Visible)
            {
                // 窗口隐藏，显示窗口
                // 确保窗口位置在屏幕内
                var workArea = SystemParameters.WorkArea;
                AppLogger.Debug($"[FloatingIsland] Toggle - Showing window. Current Pos: ({Left}, {Top})");
                if (Left < 0 || Left > workArea.Width - 50)
                    Left = workArea.Width - 80;
                if (Top < 0 || Top > workArea.Height - 50)
                    Top = workArea.Height - 80;
                    
                Show();
                Activate();
                AppLogger.Debug($"[FloatingIsland] Toggle - Window shown. New Pos: ({Left}, {Top})");
            }
            else
            {
                // 窗口显示，隐藏窗口
                AppLogger.Debug("[FloatingIsland] Toggle - Hiding window");
                Hide();
            }
        }

        // ESC 键隐藏
        private void FloatingIslandWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_isExpanded)
                {
                    Collapse();
                }
                else
                {
                    Hide();
                }
                e.Handled = true;
            }
        }

        // 贴到屏幕顶部
        public void SnapToTop()
        {
            AppLogger.Debug($"[FloatingIsland] SnapToTop() called - _isExpanded={_isExpanded}");
            // 停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            var workArea = SystemParameters.WorkArea;
            
            if (_isExpanded)
            {
                // 展开状态贴顶
                Top = 0;
                Left = (workArea.Width - Width) / 2; // 居中
            }
            else
            {
                // 收起状态贴顶
                Top = 0;
                Left = workArea.Width - 80;
                _savedIconLeft = Left;
                _savedIconTop = Top;
            }
            SaveConfig();
        }

        // 贴到主窗口
        public void SnapToMainWindow(MainWindow mainWindow)
        {
            AppLogger.Debug($"[FloatingIsland] SnapToMainWindow() called - _isExpanded={_isExpanded}");
            if (mainWindow == null) 
            {
                AppLogger.Warning("[FloatingIsland] SnapToMainWindow - mainWindow is null!");
                return;
            }

            // 停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            var mainLeft = mainWindow.Left;
            var mainTop = mainWindow.Top;
            var mainWidth = mainWindow.Width;
            var mainHeight = mainWindow.Height;

            // 贴到主窗口右侧
            Left = mainLeft + mainWidth + 10;
            Top = mainTop;

            // 保持可见区域内
            var workArea = SystemParameters.WorkArea;
            if (Left + 50 > workArea.Width)
                Left = mainLeft - 60;

            if (_isExpanded)
            {
                Width = _savedExpandedWidth;
                Height = _savedExpandedHeight;
            }
            else
            {
                _savedIconLeft = Left;
                _savedIconTop = Top;
            }
            SaveConfig();
        }

        // ========== Dock 功能实现 ==========

        // 开始监听主窗口事件
        private void StartDockMonitoring()
        {
            if (MainWindowReference == null) return;

            MainWindowReference.LocationChanged += MainWindow_LocationChanged;
            MainWindowReference.SizeChanged += MainWindow_SizeChanged;
            MainWindowReference.StateChanged += MainWindow_StateChanged;
        }

        // 停止监听主窗口事件
        private void StopDockMonitoring()
        {
            if (MainWindowReference == null) return;

            MainWindowReference.LocationChanged -= MainWindow_LocationChanged;
            MainWindowReference.SizeChanged -= MainWindow_SizeChanged;
            MainWindowReference.StateChanged -= MainWindow_StateChanged;
            _dockPosition = null;
        }

        // 主窗口移动时同步移动
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (!_enableDock || _dockPosition == null || MainWindowReference == null) return;
            UpdateDockPosition();
        }

        // 主窗口大小改变时同步调整
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_enableDock || _dockPosition == null || MainWindowReference == null) return;
            UpdateDockPosition();
        }

        // 主窗口状态改变（最大化/还原）时调整展开方向
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (!_enableDock || MainWindowReference == null) return;

            var mainState = MainWindowReference.WindowState;
            if (mainState == WindowState.Maximized)
            {
                // 最大化：向屏幕内部展开
                if (_isExpanded)
                {
                    ExpandTowardsScreen();
                }
            }
            else
            {
                // 普通状态：向主窗口外侧展开
                if (_isExpanded)
                {
                    ExpandTowardsOutside();
                }
            }
        }

        // 更新 Dock 位置
        private void UpdateDockPosition()
        {
            if (MainWindowReference == null) return;

            // 停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            var mainWindow = MainWindowReference;
            var mainLeft = mainWindow.Left;
            var mainTop = mainWindow.Top;
            var mainRight = mainLeft + mainWindow.Width;
            var mainBottom = mainTop + mainWindow.Height;

            switch (_dockPosition)
            {
                case "Top":
                    Left = mainLeft;
                    Top = mainTop - Height;
                    // 确保不超出屏幕
                    if (Top < 0) Top = 0;
                    break;
                case "Bottom":
                    Left = mainLeft;
                    Top = mainBottom;
                    break;
                case "Left":
                    Left = mainLeft - Width;
                    Top = mainTop;
                    // 确保不超出屏幕
                    if (Left < 0) Left = 0;
                    break;
                case "Right":
                    Left = mainRight;
                    Top = mainTop;
                    break;
            }

            // 边界检查
            EnsureWithinScreen();
            
            // 更新取消吸附按钮的显示状态
            UpdateUndockButtonVisibility();
        }

        // 更新取消吸附按钮的显示状态
        private void UpdateUndockButtonVisibility()
        {
            // 只在未展开且已吸附时显示按钮
            if (!_isExpanded && _dockPosition != null && _enableDock)
            {
                UndockButton.Visibility = Visibility.Visible;
            }
            else
            {
                UndockButton.Visibility = Visibility.Collapsed;
            }
        }

        // 取消吸附按钮点击事件
        private void UndockButton_Click(object sender, RoutedEventArgs e)
        {
            // 取消吸附
            _dockPosition = null;
            
            // 将窗口从主窗口边缘移开
            if (MainWindowReference != null)
            {
                var mainWindow = MainWindowReference;
                var mainRight = mainWindow.Left + mainWindow.Width;
                var mainBottom = mainWindow.Top + mainWindow.Height;
                
                // 移到底部右侧区域
                Left = mainRight - Width - 20;
                Top = mainBottom + 20;
                
                // 确保在屏幕内
                EnsureWithinScreen();
                
                // 保存新位置
                _savedIconLeft = Left;
                _savedIconTop = Top;
                SaveConfig();
            }
            
            // 隐藏按钮
            UndockButton.Visibility = Visibility.Collapsed;
            
            e.Handled = true;
        }

        // 兜底：确保窗口可见并激活
        private void EnsureVisibleAndActivated()
        {
            AppLogger.Debug($"[FloatingIsland] EnsureVisibleAndActivated() - Visibility={Visibility}, Left={Left}, Top={Top}");
            try
            {
                // 如果窗口隐藏，先显示
                if (Visibility != Visibility.Visible)
                {
                    var workArea = SystemParameters.WorkArea;
                    AppLogger.Debug("[FloatingIsland] EnsureVisible - Window is hidden, showing...");
                    if (Left < 0 || Left > workArea.Width - 50)
                        Left = workArea.Width - 80;
                    if (Top < 0 || Top > workArea.Height - 50)
                        Top = workArea.Height - 80;
                    Show();
                }

                // 强制激活窗口
                AppLogger.Debug("[FloatingIsland] EnsureVisible - Activating and Topmost toggle");
                Activate();
                Topmost = true;
                Topmost = false;
                Activate();

                // 强制更新布局
                UpdateLayout();

                // 确保面板可见
                AppLogger.Debug($"[FloatingIsland] EnsureVisible - Setting Panel/Grid visibility. FloatingPanel={FloatingPanel.Visibility}, IconGrid={IconGrid.Visibility}");
                if (FloatingPanel.Visibility != Visibility.Visible)
                {
                    FloatingPanel.Visibility = Visibility.Visible;
                }
                if (IconGrid.Visibility != Visibility.Collapsed)
                {
                    IconGrid.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("[FloatingIsland] EnsureVisibleAndActivated error", ex);
                System.Diagnostics.Debug.WriteLine($"EnsureVisibleAndActivated error: {ex.Message}");
            }
        }

        // 检查并确保窗口在屏幕内
        private void EnsureWithinScreen()
        {
            var workArea = SystemParameters.WorkArea;

            if (Left < workArea.Left) Left = workArea.Left;
            if (Top < workArea.Top) Top = workArea.Top;
            if (Left + Width > workArea.Right) Left = workArea.Right - Width;
            if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
        }

        // 检测拖拽时是否应该吸附到主窗口边缘
        public void CheckDockOnDrag()
        {
            if (!_enableDock || MainWindowReference == null) return;

            var mainWindow = MainWindowReference;
            var mainLeft = mainWindow.Left;
            var mainTop = mainWindow.Top;
            var mainRight = mainLeft + mainWindow.Width;
            var mainBottom = mainTop + mainWindow.Height;

            // 获取悬浮窗口的位置
            var floatLeft = Left;
            var floatTop = Top;
            var floatRight = floatLeft + Width;
            var floatBottom = floatTop + Height;
            var floatCenterX = floatLeft + Width / 2;
            var floatCenterY = floatTop + Height / 2;

            // 简化检测：判断悬浮窗口是否靠近主窗口的任意边缘
            // 使用更大的范围来检测
            
            // 检查是否在主窗口左侧附近（悬浮框在主窗口左边）
            bool nearLeft = floatRight >= mainLeft - DOCK_THRESHOLD * 2 && floatRight <= mainLeft + DOCK_THRESHOLD 
                            && floatCenterY >= mainTop - DOCK_THRESHOLD && floatCenterY <= mainBottom + DOCK_THRESHOLD;
            
            // 检查是否在主窗口右侧附近（悬浮框在主窗口右边）
            bool nearRight = floatLeft <= mainRight + DOCK_THRESHOLD * 2 && floatLeft >= mainRight - DOCK_THRESHOLD 
                             && floatCenterY >= mainTop - DOCK_THRESHOLD && floatCenterY <= mainBottom + DOCK_THRESHOLD;
            
            // 检查是否在主窗口上侧附近（悬浮框在主窗口上方）
            bool nearTop = floatBottom >= mainTop - DOCK_THRESHOLD * 2 && floatBottom <= mainTop + DOCK_THRESHOLD 
                           && floatCenterX >= mainLeft - DOCK_THRESHOLD && floatCenterX <= mainRight + DOCK_THRESHOLD;
            
            // 检查是否在主窗口下侧附近（悬浮框在主窗口下方）
            bool nearBottom = floatTop <= mainBottom + DOCK_THRESHOLD * 2 && floatTop >= mainBottom - DOCK_THRESHOLD 
                              && floatCenterX >= mainLeft - DOCK_THRESHOLD && floatCenterX <= mainRight + DOCK_THRESHOLD;

            // 优先检测上下边缘
            if (nearTop)
            {
                _dockPosition = "Top";
                UpdateDockPosition();
            }
            else if (nearBottom)
            {
                _dockPosition = "Bottom";
                UpdateDockPosition();
            }
            else if (nearLeft)
            {
                _dockPosition = "Left";
                UpdateDockPosition();
            }
            else if (nearRight)
            {
                _dockPosition = "Right";
                UpdateDockPosition();
            }
            else
            {
                _dockPosition = null;
            }
        }

        // 向屏幕内部展开（主窗口最大化时使用）
        private void ExpandTowardsScreen()
        {
            if (MainWindowReference == null) return;

            var workArea = SystemParameters.WorkArea;
            var mainWindow = MainWindowReference;

            // 先停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // 计算目标尺寸（确保有默认值）
            double targetWidth = Math.Min(_savedExpandedWidth > 0 ? _savedExpandedWidth : 500, workArea.Width * 0.8);
            double targetHeight = Math.Min(_savedExpandedHeight > 0 ? _savedExpandedHeight : 400, workArea.Height * 0.8);
            double targetLeft = (workArea.Width - targetWidth) / 2;
            double targetTop = (workArea.Height - targetHeight) / 2;

            // 记录当前尺寸（用于动画起点）
            double startWidth = Width;
            double startHeight = Height;
            double startLeft = Left;
            double startTop = Top;

            _isExpanded = true;
            FloatingPanel.Visibility = Visibility.Visible;
            IconGrid.Visibility = Visibility.Collapsed;
            UndockButton.Visibility = Visibility.Collapsed;
            FloatingIsland.CornerRadius = new CornerRadius(16);

            // 显示动画 - 使用当前值作为起点
            var widthAnimation = new DoubleAnimation
            {
                From = startWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnimation = new DoubleAnimation
            {
                From = startHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var leftAnimation = new DoubleAnimation
            {
                From = startLeft,
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var topAnimation = new DoubleAnimation
            {
                From = startTop,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(WidthProperty, widthAnimation);
            BeginAnimation(HeightProperty, heightAnimation);
            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(TopProperty, topAnimation);

            RestartInactiveTimer();
        }

        // 向主窗口外侧展开（主窗口普通状态时使用）
        private void ExpandTowardsOutside()
        {
            if (MainWindowReference == null) return;

            var mainWindow = MainWindowReference;
            var mainLeft = mainWindow.Left;
            var mainTop = mainWindow.Top;
            var mainRight = mainLeft + mainWindow.Width;
            var mainBottom = mainTop + mainWindow.Height;

            // 先停止所有动画
            BeginAnimation(WidthProperty, null);
            BeginAnimation(HeightProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);

            // 计算目标尺寸（确保有默认值）
            double targetWidth = _savedExpandedWidth > 0 ? _savedExpandedWidth : 500;
            double targetHeight = _savedExpandedHeight > 0 ? _savedExpandedHeight : 400;

            // 记录当前尺寸（用于动画起点）
            double startWidth = Width;
            double startHeight = Height;
            double startLeft = Left;
            double startTop = Top;

            // 计算目标位置
            double targetLeft, targetTop;
            if (_dockPosition != null)
            {
                switch (_dockPosition)
                {
                    case "Top":
                        targetLeft = mainLeft;
                        targetTop = mainTop - targetHeight;
                        break;
                    case "Bottom":
                        targetLeft = mainLeft;
                        targetTop = mainBottom;
                        break;
                    case "Left":
                        targetLeft = mainLeft - targetWidth;
                        targetTop = mainTop;
                        break;
                    case "Right":
                    default:
                        targetLeft = mainRight + 10;
                        targetTop = mainTop;
                        break;
                }
            }
            else
            {
                targetLeft = mainRight + 10;
                targetTop = mainTop;
            }

            // 边界检查
            var workArea = SystemParameters.WorkArea;
            targetLeft = Math.Max(0, Math.Min(targetLeft, workArea.Width - targetWidth));
            targetTop = Math.Max(0, Math.Min(targetTop, workArea.Height - targetHeight));

            _isExpanded = true;
            FloatingPanel.Visibility = Visibility.Visible;
            IconGrid.Visibility = Visibility.Collapsed;
            UndockButton.Visibility = Visibility.Collapsed;
            FloatingIsland.CornerRadius = new CornerRadius(16);

            // 显示动画 - 使用当前值作为起点
            var widthAnimation = new DoubleAnimation
            {
                From = startWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var heightAnimation = new DoubleAnimation
            {
                From = startHeight,
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var leftAnimation = new DoubleAnimation
            {
                From = startLeft,
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var topAnimation = new DoubleAnimation
            {
                From = startTop,
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(WidthProperty, widthAnimation);
            BeginAnimation(HeightProperty, heightAnimation);
            BeginAnimation(LeftProperty, leftAnimation);
            BeginAnimation(TopProperty, topAnimation);

            RestartInactiveTimer();
        }

        // 解除 Dock（拖离主窗口时调用）
        public void Undock()
        {
            _dockPosition = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WebViewHub.Controls;
using WebViewHub.Services;

namespace WebViewHub
{
    public partial class MainWindow : Window
    {
        private readonly List<WebViewContainer> _webViews = new();
        private readonly LayoutService _layoutService;
        private readonly DispatcherTimer _saveTimer;
        private int _counter = 0;
        private int _currentZIndex = 1;

        private const int MaxWebViews = 10;
        private const double DefaultWidth = 600;
        private const double DefaultHeight = 400;
        private const double DefaultMargin = 0; // 自动排版零缝隙
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
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CommandPanel.MainWindowReference = this;
            LoadLayout();
            UpdateCountText();


        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveLayout();
            foreach (var container in _webViews)
            {
                container.Cleanup();
            }
        }

        #region WebView 管理

        public IReadOnlyList<WebViewContainer> GetAllWebViews() => _webViews;

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
            
            var container = CreateWebViewContainer(profileId, "https://www.baidu.com", string.Empty, x, y, DefaultWidth, DefaultHeight);
            AddWebViewToCanvas(container);
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

        private WebViewContainer CreateWebViewContainer(string profileId, string url, string roleTag, double x, double y, double width, double height)
        {
            var container = new WebViewContainer
            {
                ProfileID = profileId,
                CurrentUrl = string.IsNullOrEmpty(url) ? "https://www.baidu.com" : url,
                RoleTag = roleTag ?? string.Empty,
                Width = width,
                Height = height
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
                X = Canvas.GetLeft(w),
                Y = Canvas.GetTop(w),
                Width = w.Width,
                Height = w.Height
            }).ToList();

            _layoutService.SaveLayout(layout);
        }

        private void LoadLayout()
        {
            var layout = _layoutService.LoadLayout();
            if (layout == null || layout.Count == 0) return;

            LayoutCanvas.Children.Clear();
            _webViews.Clear();

            foreach (var item in layout)
            {
                var container = CreateWebViewContainer(item.ProfileID, item.Url, item.RoleTag, item.X, item.Y, item.Width, item.Height);
                AddWebViewToCanvas(container);
            }

            _counter = layout.Count;
        }

        #endregion

        #region 辅助方法

        private void UpdateCountText()
        {
            CountText.Text = $"{_webViews.Count}/{MaxWebViews}";
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
            // 左右各半，中间留调度台位置
            ApplyBilateralLayout();
        }

        private void ApplyLayoutSplitHorizontal_Click(object sender, RoutedEventArgs e)
        {
            // 上下分栏（全画布）
            var count = _webViews.Count;
            if (count == 0) return;
            var h = LayoutCanvas.ActualHeight - DefaultMargin * 2;
            var w = LayoutCanvas.ActualWidth - DefaultMargin * 2;
            var itemH = h / count;
            for (int i = 0; i < count; i++)
                AnimateToPosition(_webViews[i], DefaultMargin, DefaultMargin + itemH * i,
                                  w - DefaultMargin, itemH - DefaultMargin);
        }

        private void ApplyLayoutWaterfall_Click(object sender, RoutedEventArgs e)
        {
            ApplyBilateralLayout();
        }

        /// <summary>
        /// 核心布局：将 WebView 均匀分配到中央调度台两侧
        /// </summary>
        private void ApplyBilateralLayout()
        {
            var count = _webViews.Count;
            if (count == 0) return;

            const double panelWidth = 340; // 调度台宽度 + 间距
            var totalW  = LayoutCanvas.ActualWidth;
            var totalH  = LayoutCanvas.ActualHeight;
            var m       = DefaultMargin;

            // 中央调度台水平区域
            var panelLeft  = (totalW - panelWidth) / 2;
            var panelRight = panelLeft + panelWidth;

            // 左右可用宽度
            var leftW  = panelLeft  - m * 2;
            var rightW = totalW - panelRight - m * 2;
            var itemH  = totalH - m * 2;

            // 把窗口分成左右两组
            int leftCount  = count / 2;
            int rightCount = count - leftCount;

            // 左侧（前 leftCount 个）
            for (int i = 0; i < leftCount; i++)
            {
                double w = leftCount > 0 ? (leftW / leftCount) - m : leftW;
                double x = m + w * i + m * i;
                AnimateToPosition(_webViews[i], x, m, w, itemH - m);
            }

            // 右侧（后 rightCount 个）
            for (int i = 0; i < rightCount; i++)
            {
                double w = rightCount > 0 ? (rightW / rightCount) - m : rightW;
                double x = panelRight + m + (w + m) * i;
                AnimateToPosition(_webViews[leftCount + i], x, m, w, itemH - m);
            }
        }

        private void ApplyGridLayout(int cols, int rows)
        {
            // 网格布局也采用双侧模式，忽略 cols/rows，自动计算
            ApplyBilateralLayout();
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

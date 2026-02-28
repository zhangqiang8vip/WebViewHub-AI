using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace WebViewHub.Controls
{
    public partial class WebViewUnit : UserControl
    {
        private CoreWebView2Environment? _environment;
        private bool _isInitialized = false;
        private string _currentUrl = "https://www.baidu.com";

        public string CurrentUrl
        {
            get => _currentUrl;
            set
            {
                _currentUrl = value;
                if (_isInitialized)
                {
                    Navigate(_currentUrl);
                }
            }
        }

        #region 依赖属性

        public static readonly DependencyProperty ProfileIDProperty =
            DependencyProperty.Register(
                nameof(ProfileID),
                typeof(string),
                typeof(WebViewUnit),
                new PropertyMetadata(string.Empty, OnProfileIDChanged));

        public string ProfileID
        {
            get => (string)GetValue(ProfileIDProperty);
            set => SetValue(ProfileIDProperty, value);
        }

        #endregion

        public WebViewUnit()
        {
            InitializeComponent();
            Loaded += WebViewUnit_Loaded;
            Unloaded += WebViewUnit_Unloaded;
        }

        private async void WebViewUnit_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                await InitializeWebViewAsync();
            }
        }

        private void WebViewUnit_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private static void OnProfileIDChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Profile ID 更新时重新初始化
        }

        private async Task InitializeWebViewAsync()
        {
            if (string.IsNullOrEmpty(ProfileID))
            {
                return;
            }

            try
            {
                _isInitialized = true;

                var userDataFolder = GetUserDataFolderPath(ProfileID);
                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }

                _environment = await CoreWebView2Environment.CreateAsync(
                    options: new CoreWebView2EnvironmentOptions()
                    {
                        AdditionalBrowserArguments = "--disable-blink-features=AutomationControlled"
                    },
                    userDataFolder: userDataFolder
                );

                await WebView2Control.EnsureCoreWebView2Async(_environment);
                ConfigureWebView();

                // 导航到当前的地址 (默认是百度，或者由配置文件加载恢复的 Url)
                Navigate(_currentUrl);
            }
            catch (Exception)
            {
                _isInitialized = false;
            }
        }

        private void ConfigureWebView()
        {
            if (WebView2Control.CoreWebView2 == null) return;

            var settings = WebView2Control.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.AreDevToolsEnabled = true;

            // 开启密码存保留和表单记忆以维持登录态
            settings.IsPasswordAutosaveEnabled = true;
            settings.IsGeneralAutofillEnabled = true;

            settings.AreHostObjectsAllowed = true;

            WebView2Control.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            WebView2Control.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
        }

        private void CoreWebView2_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (WebView2Control.CoreWebView2 != null)
            {
                _currentUrl = WebView2Control.CoreWebView2.Source;
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // 处理来自 WebView 的消息
        }

        private string GetUserDataFolderPath(string profileID)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebViewHub",
                "Profiles",
                profileID
            );
            return appDataPath;
        }

        #region 公共 API

        public void Navigate(string url)
        {
            if (_isInitialized && WebView2Control.CoreWebView2 != null)
            {
                WebView2Control.CoreWebView2.Navigate(url);
            }
        }

        public CoreWebView2? GetCoreWebView2()
        {
            return _isInitialized ? WebView2Control.CoreWebView2 : null;
        }

        public void Cleanup()
        {
            WebView2Control?.Dispose();
            _isInitialized = false;
        }

        public void SetZoomFactor(double zoomFactor)
        {
            if (_isInitialized && WebView2Control != null)
            {
                WebView2Control.ZoomFactor = zoomFactor;
            }
        }

        #endregion
    }
}

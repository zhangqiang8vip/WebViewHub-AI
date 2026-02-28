using System.Windows;

namespace WebViewHub.Controls
{
    public partial class AppleUrlDialog : Window
    {
        public string Url { get; private set; } = string.Empty;

        public AppleUrlDialog(string currentUrl)
        {
            InitializeComponent();
            UrlTextBox.Text = currentUrl;
            
            UrlTextBox.SelectAll();
            UrlTextBox.Focus();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            var input = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                DialogResult = false;
            }
            else
            {
                // 简单处理无需 http 的情况
                if (!input.StartsWith("http://") && !input.StartsWith("https://"))
                {
                    input = "https://" + input;
                }
                Url = input;
                DialogResult = true;
            }
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

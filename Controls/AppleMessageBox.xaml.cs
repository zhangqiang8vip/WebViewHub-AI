using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WebViewHub.Controls
{
    public partial class AppleMessageBox : Window
    {
        public bool Result { get; private set; }

        public AppleMessageBox(string title, string message, bool isConfirmation)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;

            if (!isConfirmation)
            {
                // Single OK button
                BtnCancel.Visibility = Visibility.Collapsed;
                Separator.Visibility = Visibility.Collapsed;
                Grid.SetColumn(BtnOk, 0);
                Grid.SetColumnSpan(BtnOk, 2);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        public static void Show(Window owner, string message, string title = "提示")
        {
            var dlg = new AppleMessageBox(title, message, false) { Owner = owner };
            dlg.ShowDialog();
        }

        public static bool ShowConfirm(Window owner, string message, string title = "确认")
        {
            var dlg = new AppleMessageBox(title, message, true) { Owner = owner };
            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}

using System.Windows;

namespace WebViewHub.Controls
{
    public partial class AppleRoleDialog : Window
    {
        public string RoleTag { get; private set; } = string.Empty;

        public AppleRoleDialog(string currentRoleTag)
        {
            InitializeComponent();
            RoleTagTextBox.Text = currentRoleTag;
            
            RoleTagTextBox.SelectAll();
            RoleTagTextBox.Focus();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            RoleTag = RoleTagTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

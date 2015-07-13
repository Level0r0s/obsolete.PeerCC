using Windows.UI.Xaml;

// The Settings Flyout item template is documented at http://go.microsoft.com/fwlink/?LinkId=273769

namespace PeerConnectionClient
{
    public sealed partial class ConnectionSettingsFlyout
    {
        public ConnectionSettingsFlyout()
        {
            InitializeComponent();
        }

        private void ConfirmAddButton_Click(object sender, RoutedEventArgs e)
        {
            AddButton.Flyout.Hide();
        }
    }
}

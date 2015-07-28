using Windows.UI.ApplicationSettings;
using Windows.UI.Xaml.Navigation;
using PeerConnectionClient.SettingsFlyouts;
using PeerConnectionClient.ViewModels;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace PeerConnectionClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private readonly DebugSettingsFlyout _debugSettingsFlyout;
        private readonly ConnectionSettingsFlyout _connectionSettingsFlyout;
        private readonly AudioVideoSettingsFlyout _audioVideoSettingsFlyout;
        private readonly AboutSettingsFlyout _aboutSettingsFlyout;

        private MainViewModel _mainViewModel;

        public MainPage()
        {
            InitializeComponent();
            _debugSettingsFlyout = new DebugSettingsFlyout();
            _connectionSettingsFlyout = new ConnectionSettingsFlyout();
            _audioVideoSettingsFlyout = new AudioVideoSettingsFlyout();
            _aboutSettingsFlyout = new AboutSettingsFlyout();
            SettingsPane.GetForCurrentView().CommandsRequested += OnCommandsRequested;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _mainViewModel = (MainViewModel)e.Parameter;
            DataContext = _debugSettingsFlyout.DataContext
              = _connectionSettingsFlyout.DataContext
              = _audioVideoSettingsFlyout.DataContext
              = _aboutSettingsFlyout.DataContext
              = _mainViewModel;
            _mainViewModel.PeerVideo = PeerVideo;
            _mainViewModel.SelfVideo = SelfVideo;
        }

        private void OnCommandsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
        {
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "ConnectionSettings", "Connection", handler => ShowConectionSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "AudioVideo", "Audio & Video", handler => ShowAudioVideoSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "DebugSettings", "Debug", handler => ShowDebugSettingFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "AboutSettings", "About", handler => ShowAboutSettingsFlyout()));
        }

        public void ShowDebugSettingFlyout()
        {
            _debugSettingsFlyout.Show();
        }

        public void ShowConectionSettingsFlyout()
        {
            _connectionSettingsFlyout.Show();
        }

        public void ShowAudioVideoSettingsFlyout()
        {
            _audioVideoSettingsFlyout.Show();
        }

        public void ShowAboutSettingsFlyout()
        {
            _aboutSettingsFlyout.Show();
        }

        private void PeerVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          if(_mainViewModel!=null)
          {
            _mainViewModel.PeerVideo_MediaFailed(sender, e);
          }
        }

        private void SelfVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          if (_mainViewModel != null)
          {
            _mainViewModel.SelfVideo_MediaFailed(sender, e);
          }
        }
        
    }
}

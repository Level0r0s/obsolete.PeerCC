using PeerConnectionClient.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ApplicationSettings;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace PeerConnectionClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            debugSettingsFlyout = new DebugSettingsFlyout();
            connectionSettingsFlyout = new ConnectionSettingsFlyout();
            audioVideoSettingsFlyout = new SettingsFlyouts.AudioVideoSettingsFlyout();
            this.DataContext = debugSettingsFlyout.DataContext
              = connectionSettingsFlyout.DataContext
              = audioVideoSettingsFlyout.DataContext
              = new MainViewModel(Dispatcher, SelfVideo, PeerVideo);
            SettingsPane.GetForCurrentView().CommandsRequested += OnCommandsRequested;
        }

        private void OnCommandsRequested(SettingsPane sender, SettingsPaneCommandsRequestedEventArgs args)
        {
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "ConnectionSettings", "Connection", (handler) => ShowConectionSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "AudioVideo", "Audio & Video", (handler) => ShowAudioVideoSettingsFlyout()));
            args.Request.ApplicationCommands.Add(new SettingsCommand(
                "DebugSettings", "Debug", (handler) => ShowDebugSettingFlyout()));
            
        }

        public void ShowDebugSettingFlyout()
        {
            debugSettingsFlyout.Show();
        }
        public void ShowConectionSettingsFlyout()
        {
            connectionSettingsFlyout.Show();
        }

        public void ShowAudioVideoSettingsFlyout()
        {
            audioVideoSettingsFlyout.Show();
        }
        DebugSettingsFlyout debugSettingsFlyout;
        ConnectionSettingsFlyout connectionSettingsFlyout;
        SettingsFlyouts.AudioVideoSettingsFlyout audioVideoSettingsFlyout;
    }
}

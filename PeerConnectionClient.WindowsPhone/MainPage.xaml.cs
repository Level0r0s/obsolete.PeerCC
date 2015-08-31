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

namespace PeerConnectionClient
{
    /// <summary>
    /// The application main page.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.

            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.

            _mainViewModel = (MainViewModel)e.Parameter;
            this.DataContext = _mainViewModel;
            _mainViewModel.PeerVideo = PeerVideo;
            _mainViewModel.SelfVideo = SelfVideo;
        }

        /// <summary>
        /// Handles the click on Application bar button
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about routes event.</param>
        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage), this.DataContext);
        }


        /// <summary>
        /// Media Failed event handler for remote/peer video.
        /// Invoked when an error occurs in peer media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void PeerVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.PeerVideo_MediaFailed(sender, e);
            }
        }

        /// <summary>
        /// Media Failed event handler for self video.
        /// Invoked when an error occurs in self media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        private void SelfVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.SelfVideo_MediaFailed(sender, e);
            }
        }

        private MainViewModel _mainViewModel;

    }
}

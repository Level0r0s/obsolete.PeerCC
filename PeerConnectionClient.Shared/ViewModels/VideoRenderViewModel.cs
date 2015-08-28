using System.Windows.Input;
using Windows.UI.Core;
using PeerConnectionClient.MVVM;

namespace PeerConnectionClient.ViewModels
{
    internal class VideoRenderViewModel : DispatcherBindableBase
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="uiDispatcher">Core event message dispatcher.</param>
        public VideoRenderViewModel(CoreDispatcher uiDispatcher)
            : base(uiDispatcher)
        {
        }

        private ICommand _disconnectCommand;
        public ICommand DisconnectCommand
        {
            get
            {
                return _disconnectCommand;
            }
            set { SetProperty(ref _disconnectCommand, value); }
        }
    }
}

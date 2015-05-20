using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using Windows.UI.Core;

namespace PeerConnectionClient.ViewModels
{
    internal class VideoRenderViewModel : BaseViewModel
    {
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
            set
            {
                _disconnectCommand = value;
                NotifyPropertyChanged();
            }
        }


    }
}

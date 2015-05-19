using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace PeerConnectionClient.ViewModels
{
    internal class VideoRenderViewModel : BaseViewModel
    {
        public VideoRenderViewModel()
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

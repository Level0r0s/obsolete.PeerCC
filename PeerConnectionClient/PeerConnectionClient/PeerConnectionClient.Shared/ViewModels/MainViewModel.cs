using PeerConnectionClient.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace PeerConnectionClient.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        public MainViewModel()
        {
            ConnectCommand = new ActionCommand(ConnectCommandExecute);
        }

        #region Bindings

        private string _ip = "localhost";
        public string Ip
        {
            get
            {
                return _ip;
            }
            set
            {
                _ip = value;
                NotifyPropertyChanged();
            }
        }

        private string _port = "8888";
        public string Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
                NotifyPropertyChanged();
            }
        }

        private ObservableCollection<String> _peers;
        public ObservableCollection<String> Peers
        {
            get
            {
                return _peers;
            }
            set
            {
                _peers = value;
                NotifyPropertyChanged();
            }
        }

        private ICommand _connectCommand;
        public ICommand ConnectCommand
        {
            get { return _connectCommand; }
            set 
            { 
                _connectCommand = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        private void ConnectCommandExecute(object obj)
        {

        }
    }
}

using PeerConnectionClient.Utilities;
using PeerConnectionClient.Signalling;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace PeerConnectionClient.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        // Take the UI dispatcher because INotifyPropertyChanged doesn't handle
        // that automatically.
        public MainViewModel(CoreDispatcher uiDispatcher)
            : base(uiDispatcher)
        {
            ConnectCommand = new ActionCommand(ConnectCommandExecute, ConnectCommandCanExecute);
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

        public class Peer
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Id + ": " + Name;
            }
        }

        private ObservableCollection<Peer> _peers;
        public ObservableCollection<Peer> Peers
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

        private ActionCommand _connectCommand;
        public ActionCommand ConnectCommand
        {
            get { return _connectCommand; }
            set 
            { 
                _connectCommand = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                _isConnected = value;
                NotifyPropertyChanged();
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        private Signaller _signaller;

        private bool ConnectCommandCanExecute(object obj)
        {
            return !IsConnected;
        }

        private void ConnectCommandExecute(object obj)
        {
            _signaller = new Signaller();

            _signaller.OnPeerConnected += (peerId, peerName) =>
                {
                    RunOnUiThread(() =>
                    {
                        if (Peers == null)
                            Peers = new ObservableCollection<Peer>();
                        Peers.Add(new Peer { Id = peerId, Name = peerName });
                    });
                };

            _signaller.OnPeerDisconnected += peerId =>
                {
                    RunOnUiThread(() =>
                    {
                        var peerToRemove = Peers.FirstOrDefault(p => p.Id == peerId);
                        if (peerToRemove != null)
                            Peers.Remove(peerToRemove);
                    });
                };

            _signaller.OnSignedIn += () =>
                {
                    RunOnUiThread(() =>
                    {
                        IsConnected = true;
                    });
                };

            _signaller.OnDisconnected += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = false;
                });
            };

            new Task(() =>
            {
                _signaller.Connect(_ip, _port, "Fred");
            }).Start();
        }
    }
}

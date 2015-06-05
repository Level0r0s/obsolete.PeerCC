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
using webrtc_winrt_api;
using Windows.UI.Xaml.Controls;

namespace PeerConnectionClient.ViewModels
{
    internal class MainViewModel : BaseViewModel
    {
        // Take the UI dispatcher because INotifyPropertyChanged doesn't handle
        // that automatically.
        // Also take the media elements as they don't databind easily to a media stream source.
        public MainViewModel(CoreDispatcher uiDispatcher, MediaElement selfVideo, MediaElement peerVideo)
            : base(uiDispatcher)
        {
            ConnectCommand = new ActionCommand(ConnectCommandExecute, ConnectCommandCanExecute);
            ConnectToPeerCommand = new ActionCommand(ConnectToPeerCommandExecute, ConnectToPeerCommandCanExecute);
            DisconnectFromServerCommand = new ActionCommand(DisconnectFromServerExecute, DisconnectFromServerCanExecute);

            SelfVideo = selfVideo;
            PeerVideo = peerVideo;

            webrtc_winrt_api.WebRTC.Initialize(uiDispatcher);

            Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
            {
                RunOnUiThread(() =>
                {
                    if (Peers == null)
                        Peers = new ObservableCollection<Peer>();
                    Peers.Add(new Peer { Id = peerId, Name = peerName });
                });
            };

            Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
            {
                RunOnUiThread(() =>
                {
                    var peerToRemove = Peers.FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                        Peers.Remove(peerToRemove);
                });
            };

            Conductor.Instance.Signaller.OnSignedIn += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = true;
                });
            };

            Conductor.Instance.Signaller.OnDisconnected += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = false;
                });
            };

            Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
            Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;
        }

        private void Conductor_OnAddRemoteStream(MediaStreamEvent evt)
        {
            RunOnUiThread(() =>
                {
                    var videoTrack = evt.Stream.GetVideoTracks().First();
                    var source = new Media().CreateMediaStreamSource(videoTrack, 640, 480, 30);
                    PeerVideo.SetMediaStreamSource(source);
                });
        }

        private void Conductor_OnRemoveRemoteStream(MediaStreamEvent evt)
        {
            RunOnUiThread(() =>
            {
                PeerVideo.SetMediaStreamSource(null);
            });
        }

        private void Conductor_OnAddLocalStream(MediaStreamEvent evt)
        {
            if (_cameraEnabled)
                Conductor.Instance.EnableLocalVideoStream();
            else
                Conductor.Instance.DisableLocalVideoStream();

            if (_microphoneIsOn)
                Conductor.Instance.UnmuteMicrophone();
            else
                Conductor.Instance.MuteMicrophone();
        }

        #region Bindings

        //private string _ip = "localhost";
        // Temporary: Our Azure server.
        private string _ip = "23.96.124.41";
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

        private Peer _selectedPeer;
        public Peer SelectedPeer
        {
            get { return _selectedPeer; }
            set
            {
                _selectedPeer = value;
                NotifyPropertyChanged();
                ConnectToPeerCommand.RaiseCanExecuteChanged();
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

        private ActionCommand _connectToPeerCommand;
        public ActionCommand ConnectToPeerCommand
        {
            get { return _connectToPeerCommand; }
            set
            {
                _connectToPeerCommand = value;
                NotifyPropertyChanged();
            }
        }

        private ActionCommand _disconnectFromServerCommand;
        public ActionCommand DisconnectFromServerCommand
        {
            get { return _disconnectFromServerCommand; }
            set
            {
                _disconnectFromServerCommand = value;
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
                DisconnectFromServerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isConnectedToPeer;
        public bool IsConnectedToPeer
        {
            get { return _isConnectedToPeer; }
            set
            {
                _isConnectedToPeer = value;
                NotifyPropertyChanged();
            }
        }

        private bool _cameraEnabled = true;
        public bool CameraEnabled
        {
            get { return _cameraEnabled; }
            set
            {
                if (_cameraEnabled != value)
                {
                    _cameraEnabled = value;
                    if (_cameraEnabled)
                        Conductor.Instance.EnableLocalVideoStream();
                    else
                        Conductor.Instance.DisableLocalVideoStream();
                }
            }
        }

        private bool _microphoneIsOn = true;
        public bool MicrophoneIsOn
        {
            get { return _microphoneIsOn; }
            set
            {
                if (_microphoneIsOn != value)
                {
                    _microphoneIsOn = value;
                    if (_microphoneIsOn)
                        Conductor.Instance.UnmuteMicrophone();
                    else
                        Conductor.Instance.MuteMicrophone();
                }
            }
        }

        private MediaElement SelfVideo;
        private MediaElement PeerVideo;
        #endregion

        private bool ConnectCommandCanExecute(object obj)
        {
            return !IsConnected;
        }

        private void ConnectCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.StartLogin(Ip, Port);
            }).Start();
        }

        private bool ConnectToPeerCommandCanExecute(object obj)
        {
            return SelectedPeer != null && Peers.Contains(SelectedPeer);
        }

        private void ConnectToPeerCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.ConnectToPeer(SelectedPeer.Id);
                IsConnectedToPeer = true;
            }).Start();
        }

        private bool DisconnectFromServerCanExecute(object obj)
        {
            return IsConnected;
        }

        private void DisconnectFromServerExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.DisconnectFromServer();
                IsConnectedToPeer = false;
                IsConnected = false;
            }).Start();

            if (Peers != null)
                Peers.Clear();
        }
    }
}

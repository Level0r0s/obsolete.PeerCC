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
            SendTracesCommand = new ActionCommand(SendTracesExecute, SendTracesCanExecute);

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

            Conductor.Instance.OnPeerConnectionCreated += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnectedToPeer = true;
                });
            };

            Conductor.Instance.OnPeerConnectionClosed += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnectedToPeer = false;
                });
            };
            LoadSettings();
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
            RunOnUiThread(() =>
            {
                if (_cameraEnabled)
                    Conductor.Instance.EnableLocalVideoStream();
                else
                    Conductor.Instance.DisableLocalVideoStream();

                if (_microphoneIsOn)
                    Conductor.Instance.UnmuteMicrophone();
                else
                    Conductor.Instance.MuteMicrophone();
                var videoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
                if (videoTrack != null)
                {
                    var source = new Media().CreateMediaStreamSource(videoTrack, 640, 480, 30);
                    SelfVideo.SetMediaStreamSource(source);
                }
            });
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

        private ActionCommand _sendTracesCommand;
        public ActionCommand SendTracesCommand
        {
            get { return _sendTracesCommand; }
            set
            {
                _sendTracesCommand = value;
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

        private bool _tracingEnabled = false;
        public bool TracingEnabled
        {
            get { return _tracingEnabled; }
            set
            {
                if (_tracingEnabled != value)
                {
                    _tracingEnabled = value;
                    if (_tracingEnabled)
                    {
                        webrtc_winrt_api.WebRTC.StartTracing();
                    }
                    else
                    {
                        webrtc_winrt_api.WebRTC.StopTracing();
                        if(_tracesAutoSendEnabled)
                        {
                            SendTracesExecute(null);
                        }
                    }
                    SendTracesCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _tracesAutoSendEnabled = true;
        public bool TracesAutoSendEnabled
        {
            get { return _tracesAutoSendEnabled; }
            set
            {
                if (_tracesAutoSendEnabled != value)
                {
                    _tracesAutoSendEnabled = value;
                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["TracesAutoSendEnabled"] = _tracesAutoSendEnabled;
                    NotifyPropertyChanged();
                }
            }
        }

        private string _traceServerIp = "";
        public string TraceServerIp
        {
            get { return _traceServerIp; }
            set
            {
                if (_traceServerIp != value)
                {
                    _traceServerIp = value;
                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["TraceServerIp"] = _traceServerIp;
                    NotifyPropertyChanged();
                }
            }
        }
        private string _traceServerPort = "";
        public string TraceServerPort
        {
            get { return _traceServerPort; }
            set
            {
                if (_traceServerPort != value)
                {
                    _traceServerPort = value;
                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["TraceServerPort"] = _traceServerPort;
                    NotifyPropertyChanged();
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
                IsConnected = false;
            }).Start();

            if (Peers != null)
                Peers.Clear();
        }

        private bool SendTracesCanExecute(object obj)
        {
            return !webrtc_winrt_api.WebRTC.IsTracing();
        }

        private void SendTracesExecute(object obj)
        {
            webrtc_winrt_api.WebRTC.SaveTrace(_traceServerIp, Int32.Parse(_traceServerPort));
        }
        void LoadSettings()
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if(settings.Values["TraceServerIp"] != null)
            {
                _traceServerIp = (string)settings.Values["TraceServerIp"];
            }
            else
            {
                _traceServerIp = "127.0.0.1";
            }

            if (settings.Values["TraceServerPort"] != null)
            {
                _traceServerPort = (string)settings.Values["TraceServerPort"];
            }
            else
            {
                _traceServerPort = "55000";
            }

            if (settings.Values["TracesAutoSendEnabled"] != null)
            {
                _tracesAutoSendEnabled = (bool)settings.Values["TracesAutoSendEnabled"];
            }
            else
            {
                _tracesAutoSendEnabled = true;
            }
        }
    }
}

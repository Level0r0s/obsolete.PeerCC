using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using PeerConnectionClient.Model;
using PeerConnectionClient.MVVM;
using PeerConnectionClient.Signalling;
using PeerConnectionClient.Utilities;
using webrtc_winrt_api;

namespace PeerConnectionClient.ViewModels
{
    public delegate void InitializedDelegate();
    internal class MainViewModel : DispatcherBindableBase
    {

        public event InitializedDelegate OnInitialized;
        public MainViewModel(CoreDispatcher uiDispatcher)
            : base(uiDispatcher)
        {
            ConnectCommand = new ActionCommand(ConnectCommandExecute, ConnectCommandCanExecute);
            ConnectToPeerCommand = new ActionCommand(ConnectToPeerCommandExecute, ConnectToPeerCommandCanExecute);
            DisconnectFromPeerCommand = new ActionCommand(DisconnectFromPeerCommandExecute, DisconnectFromPeerCommandCanExecute);
            DisconnectFromServerCommand = new ActionCommand(DisconnectFromServerExecute, DisconnectFromServerCanExecute);
            AddIceServerCommand = new ActionCommand(AddIceServerExecute, AddIceServerCanExecute);
            RemoveSelectedIceServerCommand = new ActionCommand(RemoveSelectedIceServerExecute, RemoveSelectedIceServerCanExecute);

            Ip = new ValidableNonEmptyString("23.96.124.41");//Temporary: Our Azure server.
            Port = new ValidableIntegerString(8888, 0, 65535);

            WebRTC.RequestAccessForMediaCapture().AsTask().ContinueWith(antecedent =>
            {
                if (antecedent.Result)
                {
                    Initialize(uiDispatcher);
                }
                else
                {
                    RunOnUiThread(async () =>
                    {
                        var msgDialog = new MessageDialog(
                            "Failed to obtain access to multimedia devices!");
                        await msgDialog.ShowAsync();
                    });
                }
            });
        }

        readonly DisplayRequest _keepScreenOnRequest = new DisplayRequest();

        public void Initialize(CoreDispatcher uiDispatcher)
        {
            WebRTC.Initialize(uiDispatcher);
            Cameras = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnVideoCaptureDeviceFound += deviceInfo =>
            {
                RunOnUiThread(() =>
                {
                    Cameras.Add(deviceInfo);
                });
            };
            Microphones = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnAudioCaptureDeviceFound += deviceInfo =>
            {
                RunOnUiThread(() =>
                {
                    Microphones.Add(deviceInfo);
                });
            };

            FrameCounterHelper.FramesPerSecondChanged += (frameRate) =>
            {
              RunOnUiThread(() =>
              {
                FrameRate = frameRate;
              });
            };

            Conductor.Instance.Media.EnumerateAudioVideoCaptureDevices();

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
                    IsMicrophoneEnabled = true;
                    IsCameraEnabled = true;
                    IsConnecting = false;
                });
            };
            Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
            {
                RunOnUiThread(async() =>
                {
                    IsConnecting = false;
                    MessageDialog msgDialog = new MessageDialog(
                            "Failed to connect to server!");
                    await msgDialog.ShowAsync();
                });
            };

            Conductor.Instance.Signaller.OnDisconnected += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnected = false;
                    IsMicrophoneEnabled = false;
                    IsCameraEnabled = false;
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
                    _keepScreenOnRequest.RequestActive();
                    IsMicrophoneEnabled = MicrophoneIsOn;
                    IsCameraEnabled = CameraEnabled;
                });
            };

            Conductor.Instance.OnPeerConnectionClosed += () =>
            {
                RunOnUiThread(() =>
                {
                    IsConnectedToPeer = false;
                    PeerVideo.Source = null;
                    SelfVideo.Source = null;
                    IsMicrophoneEnabled = true;
                    IsCameraEnabled = true;
                    _keepScreenOnRequest.RequestRelease();
                });
            };

            IceServers = new ObservableCollection<IceServer>();
            NewIceServer = new IceServer();

            AudioCodecs = new ObservableCollection<CodecInfo>();
            var audioCodecList = WebRTC.GetAudioCodecs();

            VideoCodecs = new ObservableCollection<CodecInfo>();
            var videoCodecList = WebRTC.GetVideoCodecs();

            RunOnUiThread(() =>
            {
                foreach (var audioCodec in audioCodecList)
                    AudioCodecs.Add(audioCodec);
                if (AudioCodecs.Count > 0)
                    SelectedAudioCodec = AudioCodecs.First();
                foreach (var videoCodec in videoCodecList)
                    VideoCodecs.Add(videoCodec);
                if (VideoCodecs.Count > 0)
                    SelectedVideoCodec = VideoCodecs.First();
            });
            LoadSettings();
            RunOnUiThread(() =>
            {
                if (OnInitialized != null)
                    OnInitialized();
            });
        }

        private void Conductor_OnAddRemoteStream(MediaStreamEvent evt)
        {
            RunOnUiThread(() =>
                {
                    var videoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
                    if (videoTrack != null)
                    {
                        var source = new Media().CreateMediaStreamSource(videoTrack, 30);
                        PeerVideo.SetMediaStreamSource(source);
                    }
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
                    var source = new Media().CreateMediaStreamSource(videoTrack, 30);
                    SelfVideo.SetMediaStreamSource(source);
                }
            });
        }

        #region Bindings

        private ValidableNonEmptyString _ip;

        public ValidableNonEmptyString Ip
        {
            get { return _ip; }
            set
            {
                SetProperty(ref _ip, value);
                _ip.PropertyChanged += Ip_PropertyChanged;
            }
        }

        private ValidableIntegerString _port;
        public ValidableIntegerString Port
        {
            get { return _port; }
            set
            {
                SetProperty(ref _port, value);
                _port.PropertyChanged += Port_PropertyChanged;
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
                SetProperty(ref _peers, value);

            }
        }

        private Peer _selectedPeer;
        public Peer SelectedPeer
        {
            get { return _selectedPeer; }
            set
            {
                SetProperty(ref _selectedPeer, value);
                ConnectToPeerCommand.RaiseCanExecuteChanged();
            }
        }

        private ActionCommand _connectCommand;
        public ActionCommand ConnectCommand
        {
            get { return _connectCommand; }
            set
            {
                SetProperty(ref _connectCommand, value);
            }
        }

        private ActionCommand _connectToPeerCommand;
        public ActionCommand ConnectToPeerCommand
        {
            get { return _connectToPeerCommand; }
            set
            {
                SetProperty(ref _connectToPeerCommand, value);
            }
        }

        private ActionCommand _disconnectFromPeerCommand;
        public ActionCommand DisconnectFromPeerCommand
        {
            get { return _disconnectFromPeerCommand; }
            set
            {
                SetProperty(ref _disconnectFromPeerCommand, value);
            }
        }

        private ActionCommand _disconnectFromServerCommand;
        public ActionCommand DisconnectFromServerCommand
        {
            get { return _disconnectFromServerCommand; }
            set
            {
                SetProperty(ref _disconnectFromServerCommand, value);
            }
        }

        private ActionCommand _addIceServerCommand;
        public ActionCommand AddIceServerCommand
        {
            get { return _addIceServerCommand; }
            set
            {
                SetProperty(ref _addIceServerCommand, value);
            }
        }

        private ActionCommand _removeSelectedIceServerCommand;
        public ActionCommand RemoveSelectedIceServerCommand
        {
            get { return _removeSelectedIceServerCommand; }
            set
            {
                SetProperty(ref _removeSelectedIceServerCommand, value);
            }
        }

        private String _frameRate;
        public String FrameRate
        {
          get { return _frameRate; }
          set
          {
            SetProperty(ref _frameRate, value);
          }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                SetProperty(ref _isConnected, value);
                ConnectCommand.RaiseCanExecuteChanged();
                DisconnectFromServerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get { return _isConnecting; }
            set
            {
                SetProperty(ref _isConnecting, value);
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isConnectedToPeer;
        public bool IsConnectedToPeer
        {
            get { return _isConnectedToPeer; }
            set
            {
                SetProperty(ref _isConnectedToPeer, value);
                ConnectToPeerCommand.RaiseCanExecuteChanged();
                DisconnectFromPeerCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _cameraEnabled = true;
        public bool CameraEnabled
        {
            get { return _cameraEnabled; }
            set
            {
                if (!SetProperty(ref _cameraEnabled, value)) return;
                if (_cameraEnabled)
                    Conductor.Instance.EnableLocalVideoStream();
                else
                    Conductor.Instance.DisableLocalVideoStream();
            }
        }

        private bool _microphoneIsOn = true;
        public bool MicrophoneIsOn
        {
            get { return _microphoneIsOn; }
            set
            {
                if (!SetProperty(ref _microphoneIsOn, value)) return;
                if (_microphoneIsOn)
                    Conductor.Instance.UnmuteMicrophone();
                else
                    Conductor.Instance.MuteMicrophone();
            }
        }

        private bool _isMicrophoneEnabled = true;
        public bool IsMicrophoneEnabled
        {
            get { return _isMicrophoneEnabled; }
            set { SetProperty(ref _isMicrophoneEnabled, value); }
        }

        private bool _isCameraEnabled = true;
        public bool IsCameraEnabled
        {
            get { return _isCameraEnabled; }
            set { SetProperty(ref _isCameraEnabled, value); }
        }



        private bool _tracingEnabled;
        public bool TracingEnabled
        {
            get { return _tracingEnabled; }
            set
            {
                if (!SetProperty(ref _tracingEnabled, value)) return;
                if (_tracingEnabled)
                {
                    WebRTC.StartTracing();
                }
                else
                {
                    WebRTC.StopTracing();
                    WebRTC.SaveTrace(_traceServerIp, Int32.Parse(_traceServerPort));
                }

            }
        }

        private string _traceServerIp = string.Empty;
        public string TraceServerIp
        {
            get { return _traceServerIp; }
            set
            {
                if (!SetProperty(ref _traceServerIp, value)) return;
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["TraceServerIp"] = _traceServerIp;
            }
        }
        private string _traceServerPort = string.Empty;
        public string TraceServerPort
        {
            get { return _traceServerPort; }
            set
            {
                if (!SetProperty(ref _traceServerPort, value)) return;
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["TraceServerPort"] = _traceServerPort;
            }
        }

        private ObservableCollection<MediaDevice> _cameras;
        public ObservableCollection<MediaDevice> Cameras
        {
            get { return _cameras; }
            set
            {
                SetProperty(ref _cameras, value);
            }
        }

        private MediaDevice _selectedCamera;
        public MediaDevice SelectedCamera
        {
            get { return _selectedCamera; }
            set
            {
                SetProperty(ref _selectedCamera, value);
                Conductor.Instance.Media.SelectVideoDevice(_selectedCamera);
            }
        }

        private ObservableCollection<MediaDevice> _microphones;
        public ObservableCollection<MediaDevice> Microphones
        {
            get
            {
                return _microphones;
            }
            set { SetProperty(ref _microphones, value); }
        }

        private MediaDevice _selectedMicrophone;
        public MediaDevice SelectedMicrophone
        {
            get { return _selectedMicrophone; }
            set
            {
                SetProperty(ref _selectedMicrophone, value);
                Conductor.Instance.Media.SelectAudioDevice(_selectedMicrophone);
            }
        }

        private bool _loggingEnabled;
        public bool LoggingEnabled
        {
            get { return _loggingEnabled; }
            set
            {
                if (!SetProperty(ref _loggingEnabled, value)) return;
                if (_loggingEnabled)
                    WebRTC.EnableLogging(LogLevel.LOGLVL_INFO);
                else
                    WebRTC.DisableLogging();
            }
        }

        private ObservableCollection<IceServer> _iceServers;
        public ObservableCollection<IceServer> IceServers
        {
            get { return _iceServers; }
            set { SetProperty(ref _iceServers, value); }
        }

        private IceServer _selectedIceServer;
        public IceServer SelectedIceServer
        {
            get { return _selectedIceServer; }
            set
            {
                SetProperty(ref _selectedIceServer, value);
                RemoveSelectedIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        private IceServer _newIceServer;
        public IceServer NewIceServer
        {
            get { return _newIceServer; }
            set
            {
                if (SetProperty(ref _newIceServer, value))
                {
                    _newIceServer.PropertyChanged += NewIceServer_PropertyChanged;
                }
            }
        }

        private ObservableCollection<CodecInfo> _audioCodecs;

        public ObservableCollection<CodecInfo> AudioCodecs
        {
            get { return _audioCodecs; }
            set { SetProperty(ref _audioCodecs, value); }
        }

        public CodecInfo SelectedAudioCodec
        {
            get { return Conductor.Instance.AudioCodec; }
            set
            {
                if (Conductor.Instance.AudioCodec == value) return;
                Conductor.Instance.AudioCodec = value;
                OnPropertyChanged(() => SelectedAudioCodec);
            }
        }

        private ObservableCollection<CodecInfo> _videoCodecs;

        public ObservableCollection<CodecInfo> VideoCodecs
        {
            get { return _videoCodecs; }
            set { SetProperty(ref _videoCodecs, value); }
        }

        public CodecInfo SelectedVideoCodec
        {
            get { return Conductor.Instance.VideoCodec; }
            set
            {
                if (Conductor.Instance.VideoCodec == value) return;
                Conductor.Instance.VideoCodec = value;
                OnPropertyChanged(() => SelectedVideoCodec);
            }
        }

        public MediaElement SelfVideo;
        public MediaElement PeerVideo;

        #endregion

        private bool ConnectCommandCanExecute(object obj)
        {
            return !IsConnected && !IsConnecting && Ip.Valid && Port.Valid;
        }

        private void ConnectCommandExecute(object obj)
        {
            new Task(() =>
            {
                IsConnecting = true;
                Conductor.Instance.StartLogin(Ip.Value, Port.Value);
            }).Start();
        }

        private bool ConnectToPeerCommandCanExecute(object obj)
        {
            return SelectedPeer != null && Peers.Contains(SelectedPeer) && !IsConnectedToPeer;
        }

        private void ConnectToPeerCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.ConnectToPeer(SelectedPeer.Id);
            }).Start();
        }

        private bool DisconnectFromPeerCommandCanExecute(object obj)
        {
            return IsConnectedToPeer;
        }

        private void DisconnectFromPeerCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.DisconnectFromPeer();
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

        private bool AddIceServerCanExecute(object obj)
        {
            return NewIceServer.Valid;
        }

        private void AddIceServerExecute(object obj)
        {
            IceServers.Add(_newIceServer);
            OnPropertyChanged(() => IceServers);
            Conductor.Instance.ConfigureIceServers(IceServers);
            SaveIceServerList();
            NewIceServer = new IceServer();
        }

        private bool RemoveSelectedIceServerCanExecute(object obj)
        {
            return SelectedIceServer != null;
        }

        private void RemoveSelectedIceServerExecute(object obj)
        {
            IceServers.Remove(_selectedIceServer);
            OnPropertyChanged(() => IceServers);
            SaveIceServerList();
            Conductor.Instance.ConfigureIceServers(IceServers);
        }


        void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            //Default values:
            var configTraceServerIp = "127.0.0.1";
            var configTraceServerPort = "55000";

            var configIceServers = new ObservableCollection<IceServer>();

            if (settings.Values["TraceServerIp"] != null)
            {
                configTraceServerIp = (string)settings.Values["TraceServerIp"];
            }

            if (settings.Values["TraceServerPort"] != null)
            {
                configTraceServerPort = (string)settings.Values["TraceServerPort"];
            }

            bool useDefaultIceServers = true;
            if (settings.Values["IceServerList"] != null)
            {
                try
                {
                    configIceServers = XmlSerializer<ObservableCollection<IceServer>>.FromXml((string)settings.Values["IceServerList"]);
                    useDefaultIceServers = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to load IceServer from config, using defaults (ex=" + ex.Message + ")");
                }
            }
            if (useDefaultIceServers)
            {
                //Default values:
                configIceServers.Clear();
                configIceServers.Add(new IceServer("stun.l.google.com", "19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun1.l.google.com", "19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun2.l.google.com", "19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun3.l.google.com", "19302", IceServer.ServerType.STUN));
                configIceServers.Add(new IceServer("stun4.l.google.com", "19302", IceServer.ServerType.STUN));
            }

            RunOnUiThread(() =>
            {
                IceServers = configIceServers;
                TraceServerIp = configTraceServerIp;
                TraceServerPort = configTraceServerPort;
            });

            Conductor.Instance.ConfigureIceServers(IceServers);
        }

        void SaveIceServerList()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string xmlIceServers = XmlSerializer<ObservableCollection<IceServer>>.ToXml(IceServers);
            localSettings.Values["IceServerList"] = xmlIceServers;
        }

        void NewIceServer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                AddIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        void Ip_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }

        void Port_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
        }
    }
}

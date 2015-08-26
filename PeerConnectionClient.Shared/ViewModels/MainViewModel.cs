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
#if !WINDOWS_UAP // Disable on Win10 for now.
using HockeyApp;
using Windows.Networking.Connectivity;
using Windows.Networking;
#endif

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
            SendFeedbackCommand = new ActionCommand(SendFeedbackExecute);

            Ip = new ValidableNonEmptyString("23.96.124.41");//Temporary: Our Azure server.
            Port = new ValidableIntegerString(8888, 0, 65535);

            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            AppVersion = String.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

            IsReadyToConnect = true;

            LoadHockeyAppSettings();

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

        public void PeerVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("PeerVideo_MediaFailed");
          if (_peerVideoTrack != null)
          {
            Debug.WriteLine("Re-establishing peer video");
            var source = new Media().CreateMediaStreamSource(_peerVideoTrack, 30, "PEER");
            PeerVideo.SetMediaStreamSource(source);
            PeerVideo.Play();
            Debug.WriteLine("Peer video re-established");
          }
        }

        public void SelfVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("SelfVideo_MediaFailed");
          if (_selfVideoTrack != null)
          {
            Debug.WriteLine("Re-establishing self video");
            var source = new Media().CreateMediaStreamSource(_selfVideoTrack, 30, "SELF");
            SelfVideo.SetMediaStreamSource(source);
            SelfVideo.Play();
            Debug.WriteLine("Self video re-established");
          }
        }

        readonly DisplayRequest _keepScreenOnRequest = new DisplayRequest();
        private bool _keepOnScreenRequested = false;
        private MediaVideoTrack _peerVideoTrack;
        private MediaVideoTrack _selfVideoTrack;

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

            FrameCounterHelper.FramesPerSecondChanged += (id, frameRate) =>
            {
                RunOnUiThread(() =>
                {
                    if (id == "SELF")
                    {
                        SelfVideoFps = frameRate;
                    }
                    else if (id == "PEER")
                    {
                        PeerVideoFps = frameRate;
                    }
                });
            };

            ResolutionHelper.ResolutionChanged += (id, width, height) => {
                RunOnUiThread(() => {
                    if (id == "SELF") {
                        SelfHeight = height.ToString();
                        SelfWidth = width.ToString();
                    }
                    else if (id == "PEER") {
                        PeerHeight = height.ToString();
                        PeerWidth = width.ToString();
                    }
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
                    IsDisconnecting = false;
                });
            };

            Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
            Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

            Conductor.Instance.OnPeerConnectionCreated += () =>
            {
                RunOnUiThread(() =>
                {
                    IsReadyToConnect = false;
                    IsConnectedToPeer = true;
                    if (!_keepOnScreenRequested) {
                         _keepScreenOnRequest.RequestActive();
                         _keepOnScreenRequested = true;
                    }
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
                    _peerVideoTrack = null;
                    _selfVideoTrack = null;
                    GC.Collect(); // Ensure all references are truly dropped.
                    IsMicrophoneEnabled = true;
                    IsCameraEnabled = true;
                    SelfVideoFps = PeerVideoFps = "";
                    if (_keepOnScreenRequested) {
                        _keepScreenOnRequest.RequestRelease();
                        _keepOnScreenRequested = false;
                    }
                });
            };

            Conductor.Instance.OnReadyToConnect += () =>
            {
                RunOnUiThread(() =>
                {
                    IsReadyToConnect = true;
                });
            };

            IceServers = new ObservableCollection<IceServer>();
            NewIceServer = new IceServer();

            AudioCodecs = new ObservableCollection<CodecInfo>();
            var audioCodecList = WebRTC.GetAudioCodecs();
            // these are features added to existing codecs, they can't decode/encode real audio data so ignore them
            string[] incompatibleAudioCodecs = new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            VideoCodecs = new ObservableCollection<CodecInfo>();
            //Right now, The WebRTC reports the trial codecs before the officially supported one.
            //reverse the list, so that the official ones will  be default.
            var videoCodecList = WebRTC.GetVideoCodecs().Reverse();


            RunOnUiThread(() =>
            {
                foreach (var audioCodec in audioCodecList) 
                {
                    if (!incompatibleAudioCodecs.Contains(audioCodec.Name + audioCodec.Clockrate))
                    {
                        AudioCodecs.Add(audioCodec);
                    }
                }
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
            _peerVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if (_peerVideoTrack != null)
            {
                var source = new Media().CreateMediaStreamSource(_peerVideoTrack, 30, "PEER");
                RunOnUiThread(() =>
                {
                    PeerVideo.SetMediaStreamSource(source);
                });
            }
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
                _selfVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
                if (_selfVideoTrack != null)
                {
                  var source = new Media().CreateMediaStreamSource(_selfVideoTrack, 30, "SELF");
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

        private String _peerWidth;
        public String PeerWidth
        {
            get { return _peerWidth; }
            set
            {
                SetProperty(ref _peerWidth, value);
            }
        }

        private String _peerHeight;
        public String PeerHeight {
            get { return _peerHeight; }
            set {
                SetProperty(ref _peerHeight, value);
            }
        }
        private String _selfWidth;
        public String SelfWidth {
            get { return _selfWidth; }
            set {
                SetProperty(ref _selfWidth, value);
            }
        }

        private String _selfHeight;
        public String SelfHeight {
            get { return _selfHeight; }
            set {
                SetProperty(ref _selfHeight, value);
            }
        }

        private String _peerVideoFps;
        public String PeerVideoFps {
          get { return _peerVideoFps; }
          set {
            SetProperty(ref _peerVideoFps, value);
          }
        }

        private String _selfVideoFps;
        public String SelfVideoFps
        {
            get { return _selfVideoFps; }
            set
            {
                SetProperty(ref _selfVideoFps, value);
            }
        }

        private ActionCommand _sendFeedbackCommand;
        public ActionCommand SendFeedbackCommand
        {
            get { return _sendFeedbackCommand;  }
            set
            {
                SetProperty(ref _sendFeedbackCommand, value);
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

        private bool _isDisconnecting;
        public bool IsDisconnecting
        {
          get { return _isDisconnecting; }
          set
          {
              SetProperty(ref _isDisconnecting, value);
              DisconnectFromServerCommand.RaiseCanExecuteChanged();
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

        private bool _isReadyToConnect;
        public bool IsReadyToConnect
        {
            get { return _isReadyToConnect; }
            set
            {
                SetProperty(ref _isReadyToConnect, value);
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

        private ObservableCollection<ComboBoxItemCapRes> _allCapRes;
        public ObservableCollection<ComboBoxItemCapRes> AllCapRes
        {
          get { return _allCapRes; }
          set { SetProperty(ref _allCapRes, value); }
        }


        //selected capture resolution
        private ComboBoxItemCapRes _selectedCapResItem;
        public ComboBoxItemCapRes SelectedCapResItem
        {
            get { return _selectedCapResItem; }
            set
            {
                if (SetProperty(ref _selectedCapResItem, value))
                {
                    Conductor.Instance.VideoCaptureRes = value.ValueCapResEnum;
                    Conductor.Instance.updatePreferredFrameFormat();
                }
            }
        }

        private ObservableCollection<ComboBoxItemCapFPS> _allCapFPS;
        public ObservableCollection<ComboBoxItemCapFPS> AllCapFPS
        {
          get { return _allCapFPS; }
          set { SetProperty(ref _allCapFPS, value); }
        }

        // selected capture frame rate
        private ComboBoxItemCapFPS _selectedCapFPSItem;
        public ComboBoxItemCapFPS SelectedCapFPSItem
        {
            get { return _selectedCapFPSItem; }
            set
            {
                if (SetProperty(ref _selectedCapFPSItem, value))
                {
                    Conductor.Instance.VideoCaptureFPS = value.ValueCapFPSEnum;
                    Conductor.Instance.updatePreferredFrameFormat();
                }
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

        private string _appVersion = "N/A";
        public string AppVersion
        {
            get { return _appVersion; }
            set { SetProperty(ref _appVersion, value); }
        }

        private string _crashReportUserInfo = "";
        public string CrashReportUserInfo
        {
            get { return _crashReportUserInfo; }
            set
            {
                if (SetProperty(ref _crashReportUserInfo, value))
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    localSettings.Values["CrashReportUserInfo"] = _crashReportUserInfo;
                    HockeyClient.Current.UpdateContactInfo(_crashReportUserInfo, "");
                }
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
            return SelectedPeer != null && Peers.Contains(SelectedPeer) && !IsConnectedToPeer && IsReadyToConnect;
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
          if (IsDisconnecting)
              return false;

          return IsConnected;
        }

        private void DisconnectFromServerExecute(object obj)
        {

            new Task(() =>
            {
                IsDisconnecting = true;
                Conductor.Instance.DisconnectFromServer();
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

        private void SendFeedbackExecute(object obj)
        {
#if !WINDOWS_UAP // Disable on Win10 for now.
            HockeyClient.Current.ShowFeedback();
#endif
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

            Conductor.Instance.ConfigureIceServers(configIceServers);


            RunOnUiThread(() =>
            {
            _allCapRes = new ObservableCollection<ComboBoxItemCapRes>() { 
                      new ComboBoxItemCapRes(){ ValueCapResEnum = CapRes.Default, ValueCapResString = "default" },
                      new ComboBoxItemCapRes(){ ValueCapResEnum = CapRes._640_480, ValueCapResString = "640 x 480" },
                      new ComboBoxItemCapRes(){ ValueCapResEnum = CapRes._320_240, ValueCapResString = "320 x 240" },
            };
            SelectedCapResItem = _allCapRes.First();

            _allCapFPS = new ObservableCollection<ComboBoxItemCapFPS>() { 
                      new ComboBoxItemCapFPS(){ ValueCapFPSEnum = CapFPS.Default, ValueCapFPSString = "default" },
                      new ComboBoxItemCapFPS(){ ValueCapFPSEnum = CapFPS._5, ValueCapFPSString = "5" },
                      new ComboBoxItemCapFPS(){ ValueCapFPSEnum = CapFPS._15, ValueCapFPSString = "15" },
                      new ComboBoxItemCapFPS(){ ValueCapFPSEnum = CapFPS._30, ValueCapFPSString = "30" },
            };
            SelectedCapFPSItem = _allCapFPS.First();

            });
        }

        void LoadHockeyAppSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            //Default values:
            var configCrashReportUserInfo = "";

            if (settings.Values["CrashReportUserInfo"] != null)
            {
                configCrashReportUserInfo = (string)settings.Values["CrashReportUserInfo"];
            }
            if (configCrashReportUserInfo == "")
            {
                var hostname = NetworkInformation.GetHostNames().FirstOrDefault(
                    h => h.Type == HostNameType.DomainName);
                configCrashReportUserInfo = hostname != null ? hostname.CanonicalName : "<unknown host>";
            }

            RunOnUiThread(() =>
            {
                CrashReportUserInfo = configCrashReportUserInfo;
            });
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

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
using PeerConnectionClient.Model;
using System.ComponentModel;

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
            AddIceServerCommand = new ActionCommand(AddIceServerExecute, AddIceServerCanExecute);
            RemoveSelectedIceServerCommand = new ActionCommand(RemoveSelectedIceServerExecute, RemoveSelectedIceServerCanExecute);

            SelfVideo = selfVideo;
            PeerVideo = peerVideo;
            webrtc_winrt_api.WebRTC.InitializeMediaEngine().AsTask().ContinueWith((x) =>
            {
              this.initialize(uiDispatcher);
            });

        }

        public void initialize(CoreDispatcher uiDispatcher)
        {
            webrtc_winrt_api.WebRTC.Initialize(uiDispatcher);

            Cameras = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnVideoCaptureDeviceFound += (deviceInfo) => {
               RunOnUiThread(() => {
                  Cameras.Add(deviceInfo);
              });
            };
            Microphones = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnAudioCaptureDeviceFound += (deviceInfo) =>{
                RunOnUiThread(() =>{
                    Microphones.Add(deviceInfo);
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
                    PeerVideo.Source = null;
                    SelfVideo.Source = null;
                });
            };

            IceServers = new ObservableCollection<IceServer>();
            NewIceServer = new IceServer();

            AudioCodecs = new ObservableCollection<CodecInfo>();
            IList<CodecInfo> audioCodecList = new List<webrtc_winrt_api.CodecInfo>();
            audioCodecList = webrtc_winrt_api.WebRTC.GetAudioCodecs();
            foreach (var audioCodec in audioCodecList)
                AudioCodecs.Add(audioCodec);
            if (AudioCodecs.Count > 0)
                SelectedAudioCodec = AudioCodecs.First();

            VideoCodecs = new ObservableCollection<CodecInfo>();
            IList<CodecInfo> videoCodecList = new List<CodecInfo>();
            videoCodecList = webrtc_winrt_api.WebRTC.GetVideoCodecs();
            foreach (var videoCodec in videoCodecList)
                VideoCodecs.Add(videoCodec);
            if (VideoCodecs.Count > 0)
                SelectedVideoCodec = VideoCodecs.First();
            
            LoadSettings();
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

        private ActionCommand _addNewIceServerCommand;
        public ActionCommand AddIceServerCommand
        {
            get { return _addNewIceServerCommand; }
            set
            {
                _addNewIceServerCommand = value;
                NotifyPropertyChanged();
            }
        }

        private ActionCommand _removeSelectedIceServerCommand;
        public ActionCommand RemoveSelectedIceServerCommand
        {
            get { return _removeSelectedIceServerCommand; }
            set
            {
                _removeSelectedIceServerCommand = value;
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
                        webrtc_winrt_api.WebRTC.SaveTrace(_traceServerIp, Int32.Parse(_traceServerPort));
                    }
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

        private ObservableCollection<MediaDevice> _cameras;
        public ObservableCollection<MediaDevice> Cameras {
          get {
            return _cameras;
          }
          set {
            _cameras = value;
            NotifyPropertyChanged();
          }
        }

        private MediaDevice _selectedCamera;
        public MediaDevice SelectedCamera {
          get { return _selectedCamera; }
          set {
            _selectedCamera = value;
            Conductor.Instance.Media.SelectVideoDevice(_selectedCamera);
            NotifyPropertyChanged();
          }
        }

        private ObservableCollection<MediaDevice> _microphones;
        public ObservableCollection<MediaDevice> Microphones {
          get {
            return _microphones;
          }
          set {
            _microphones = value;
            NotifyPropertyChanged();
          }
        }

        private MediaDevice _selectedMicrophone;
        public MediaDevice SelectedMicrophone {
          get { return _selectedMicrophone; }
          set {
            _selectedMicrophone = value;
            Conductor.Instance.Media.SelectAudioDevice(_selectedMicrophone);
            NotifyPropertyChanged();
          }
        }

        private bool _loggingEnabled = false;
        public bool LoggingEnabled
        {
            get { return _loggingEnabled; }
            set
            {
                if (_loggingEnabled != value)
                {
                    _loggingEnabled = value;
                    if (_loggingEnabled)
                        webrtc_winrt_api.WebRTC.EnableLogging(LogLevel.LOGLVL_INFO);
                    else
                        webrtc_winrt_api.WebRTC.DisableLogging();
                    NotifyPropertyChanged();
                }
            }
        }

        private ObservableCollection<IceServer> _IceServers;
        public ObservableCollection<IceServer> IceServers
        {
          get
          {
            return _IceServers;
          }
          set
          {
            _IceServers = value;
            NotifyPropertyChanged();
          }
        }

        private IceServer _SelectedIceServer;
        public IceServer SelectedIceServer
        {
            get { return _SelectedIceServer; }
            set
            { 
              _SelectedIceServer = value;
              NotifyPropertyChanged();
              RemoveSelectedIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        private IceServer _NewIceServer;
        public IceServer NewIceServer
        {
            get { return _NewIceServer; }
            set 
            {
                _NewIceServer = value;
                _NewIceServer.PropertyChanged += NewIceServer_PropertyChanged;
                NotifyPropertyChanged();
            }
        }

        private ObservableCollection<CodecInfo> _audioCodecs;

        public ObservableCollection<CodecInfo> AudioCodecs
        {
            get { return _audioCodecs; }
            set
            {
                _audioCodecs = value;
                NotifyPropertyChanged();
            }
        }

        public CodecInfo SelectedAudioCodec
        {
            get { return Conductor.Instance.AudioCodec; }
            set
            {
                if (Conductor.Instance.AudioCodec != value)
                {
                    Conductor.Instance.AudioCodec = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private ObservableCollection<CodecInfo> _videoCodecs;

        public ObservableCollection<CodecInfo> VideoCodecs
        {
            get { return _videoCodecs; }
            set
            {
                _videoCodecs = value;
                NotifyPropertyChanged();
            }
        }

        public CodecInfo SelectedVideoCodec
        {
            get { return Conductor.Instance.VideoCodec; }
            set
            {
                if (Conductor.Instance.VideoCodec != value)
                {
                    Conductor.Instance.VideoCodec = value;
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

        private bool AddIceServerCanExecute(object obj)
        {
            return NewIceServer.Valid;
        }

        private void AddIceServerExecute(object obj)
        {
            _IceServers.Add(_NewIceServer);
            NotifyPropertyChanged("IceServers");
            Conductor.Instance.ConfigureIceServers(IceServers);
            SaveIceServerList();
            NewIceServer = new IceServer();
        }

        private bool RemoveSelectedIceServerCanExecute(object obj)
        {
            return _SelectedIceServer != null;
        }

        private void RemoveSelectedIceServerExecute(object obj)
        {
            _IceServers.Remove(_SelectedIceServer);
            NotifyPropertyChanged("IceServers");
            SaveIceServerList();
            Conductor.Instance.ConfigureIceServers(IceServers);
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

            bool useDefaults = true;
            if(settings.Values["IceServerList"] != null)
            {
                try
                {
                    IceServers = XmlSerializer<ObservableCollection<IceServer>>.FromXml((string)settings.Values["IceServerList"]);
                    useDefaults = false;
                }
                catch (Exception ex)
                {

                }
            }
            if(useDefaults)
            {
                //Default values
                IceServers.Add(new IceServer("stun.l.google.com", "19302", IceServer.ServerType.STUN));
                IceServers.Add(new IceServer("stun1.l.google.com", "19302", IceServer.ServerType.STUN));
                IceServers.Add(new IceServer("stun2.l.google.com", "19302", IceServer.ServerType.STUN));
                IceServers.Add(new IceServer("stun3.l.google.com", "19302", IceServer.ServerType.STUN));
                IceServers.Add(new IceServer("stun4.l.google.com", "19302", IceServer.ServerType.STUN));
            }
            Conductor.Instance.ConfigureIceServers(IceServers);
        }

        void SaveIceServerList()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            string xmlIceServers = XmlSerializer<ObservableCollection<IceServer>>.ToXml(IceServers);
            localSettings.Values["IceServerList"] = xmlIceServers;
        }

        void NewIceServer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "Valid")
            {
                AddIceServerCommand.RaiseCanExecuteChanged();
            }
        }
    }
}

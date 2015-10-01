using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
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
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Activation;
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

        /// <summary>
        /// Constructor for MainViewModel.
        /// </summary>
        /// <param name="uiDispatcher">Core event message dispatcher.</param>
        public MainViewModel(CoreDispatcher uiDispatcher)
            : base(uiDispatcher)
        {
            // Initialize all the action commands
            ConnectCommand = new ActionCommand(ConnectCommandExecute, ConnectCommandCanExecute);
            ConnectToPeerCommand = new ActionCommand(ConnectToPeerCommandExecute, ConnectToPeerCommandCanExecute);
            DisconnectFromPeerCommand = new ActionCommand(DisconnectFromPeerCommandExecute, DisconnectFromPeerCommandCanExecute);
            DisconnectFromServerCommand = new ActionCommand(DisconnectFromServerExecute, DisconnectFromServerCanExecute);
            AddIceServerCommand = new ActionCommand(AddIceServerExecute, AddIceServerCanExecute);
            RemoveSelectedIceServerCommand = new ActionCommand(RemoveSelectedIceServerExecute, RemoveSelectedIceServerCanExecute);
            SendFeedbackCommand = new ActionCommand(SendFeedbackExecute);
            SettingsButtonCommand = new ActionCommand(SettingsButtonExecute);

            // Configure application version string format
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            AppVersion = String.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

            IsReadyToConnect = true;
            _settingsButtonChecked = false;
            ScrollBarVisibilityType = ScrollBarVisibility.Auto;

            // Prepare Hockey app to collect the crash logs and send to the server
            LoadHockeyAppSettings();

            // Display a permission dialog to request access to the microphone and camera
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

        /// <summary>
        /// Media Failed event handler for remote/peer video.
        /// Invoked when an error occurs in peer media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        public void PeerVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("PeerVideo_MediaFailed");
          if (_peerVideoTrack != null)
          {
            Debug.WriteLine("Re-establishing peer video");
            Media.CreateMediaAsync().AsTask().ContinueWith(media => 
            {
              var source = media.Result.CreateMediaStreamSource(_peerVideoTrack, 30, "PEER");
              RunOnUiThread(() =>
              {
                PeerVideo.SetMediaStreamSource(source);
                PeerVideo.Play();
                Debug.WriteLine("Peer video re-established");
              });
            });
          }
        }

        /// <summary>
        /// Media Failed event handler for the self video.
        /// Invoked when an error occurs in the self media source.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the exception routed event.</param>
        public void SelfVideo_MediaFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
          Debug.WriteLine("SelfVideo_MediaFailed");
          if (_selfVideoTrack != null)
          {
            Debug.WriteLine("Re-establishing self video");
            Media.CreateMediaAsync().AsTask().ContinueWith(media =>
            {
              var source = media.Result.CreateMediaStreamSource(_selfVideoTrack, 30, "SELF");
              RunOnUiThread(() =>
              {
                SelfVideo.SetMediaStreamSource(source);
                SelfVideo.Play();
                Debug.WriteLine("Self video re-established");
              });
            });
          }
        }

        // Help to make sure the screen is not locked while on call
        readonly DisplayRequest _keepScreenOnRequest = new DisplayRequest();
        private bool _keepOnScreenRequested = false;

        private MediaVideoTrack _peerVideoTrack;
        private MediaVideoTrack _selfVideoTrack;


        /// <summary>
        /// The initializer for MainViewModel.
        /// </summary>
        /// <param name="uiDispatcher">The UI dispatcher.</param>
        public void Initialize(CoreDispatcher uiDispatcher)
        {
            WebRTC.Initialize(uiDispatcher);

            // Get information of cameras attached to the device
            Cameras = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnVideoCaptureDeviceFound += deviceInfo =>
            {
                RunOnUiThread(() =>
                {
                    Cameras.Add(deviceInfo);
                    if (Cameras.Count == 1)
                    {
                      SelectedCamera = Cameras[0];
                    }
                });
            };

            // Get information of microphones attached to the device
            Microphones = new ObservableCollection<MediaDevice>();
            Conductor.Instance.Media.OnAudioCaptureDeviceFound += deviceInfo =>
            {
                RunOnUiThread(() =>
                {
                    Microphones.Add(deviceInfo);
                    if (Microphones.Count == 1)
                    {
                      SelectedMicrophone = Microphones[0];
                    }
                });
            };

            // Handler for Peer/Self video frame rate changed event
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

            // Handler for Peer/Self video resolution changed event 
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

            // Enumerate available audio and video devices
            Conductor.Instance.Media.EnumerateAudioVideoCaptureDevices();

            // A Peer is connected to the server event handler
            Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
            {
                RunOnUiThread(() =>
                {
                    if (Peers == null)
                        Peers = new ObservableCollection<Peer>();
                    Peers.Add(new Peer { Id = peerId, Name = peerName });
                });
            };

            // A Peer is disconnected from the server event handler
            Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
            {
                RunOnUiThread(() =>
                {
                    var peerToRemove = Peers.FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                        Peers.Remove(peerToRemove);
                });
            };

            // The user is Signed in to the server event handler
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

            // Failed to connect to the server event handler
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

            // The current user is disconnected from the server event handler
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

            // Event handlers for managing the media streams 
            Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
            Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
            Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

            // Connected to a peer event handler
            Conductor.Instance.OnPeerConnectionCreated += () =>
            {
                RunOnUiThread(() =>
                {
                    IsReadyToConnect = false;
                    IsConnectedToPeer = true;
<<<<<<< HEAD
                    IsReadyToDisconnect = false;
=======
                    if (SettingsButtonChecked) {
                        // close settings screen if open
                        SettingsButtonChecked = false;
                        ScrollBarVisibilityType = ScrollBarVisibility.Disabled;
                    }
>>>>>>> Fixed Bug 12948 If call is started when settings are open, not switching to the active call view

                    // Make sure the screen is always active while on call
                    if (!_keepOnScreenRequested) {
                         _keepScreenOnRequest.RequestActive();
                         _keepOnScreenRequested = true;
                    }
                    IsMicrophoneEnabled = MicrophoneIsOn;
                    IsCameraEnabled = CameraEnabled;
                    UpdateScrollBarVisibilityTypeHelper();
                });
            };

            // Connection between the current user and a peer is closed event handler
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

                    // Make sure to allow the screen to be locked after the call
                    if (_keepOnScreenRequested) {
                        _keepScreenOnRequest.RequestRelease();
                        _keepOnScreenRequested = false;
                    }
                    UpdateScrollBarVisibilityTypeHelper();
                });
            };

            // Ready to connect to the server event handler
            Conductor.Instance.OnReadyToConnect += () =>
            {
                RunOnUiThread(() =>
                {
                    IsReadyToConnect = true;
                });
            };

            // Initialize the Ice servers list
            IceServers = new ObservableCollection<IceServer>();
            NewIceServer = new IceServer();

            // Prepare to list supported audio codecs
            AudioCodecs = new ObservableCollection<CodecInfo>();
            var audioCodecList = WebRTC.GetAudioCodecs();

            // These are features added to existing codecs, they can't decode/encode real audio data so ignore them
            string[] incompatibleAudioCodecs = new string[] { "CN32000", "CN16000", "CN8000", "red8000", "telephone-event8000" };

            // Prepare to list supported video codecs
            VideoCodecs = new ObservableCollection<CodecInfo>();

            // Order the video codecs so that the stable VP8 is in front.
            var videoCodecList = WebRTC.GetVideoCodecs().OrderBy(codec =>
            {
                switch (codec.Name)
                {
                    case "VP8": return 1;
                    case "VP9": return 2;
                    case "H264": return 3;
                    default: return 99;
                }
            });

            // Load the supported audio/video information into the Settings controls
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
                {
                    SelectedAudioCodec = AudioCodecs.First();
                }
                foreach (var videoCodec in videoCodecList)
                {
                    VideoCodecs.Add(videoCodec);
                }
                if (VideoCodecs.Count > 0)
                {
                    SelectedVideoCodec = VideoCodecs.First();
                }
            });
            LoadSettings();
            RunOnUiThread(() =>
            {
              if (OnInitialized != null)
              {
                  OnInitialized();
              }
            });
        }

        /// <summary>
        /// Add remote stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnAddRemoteStream(MediaStreamEvent evt)
        {
            _peerVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
            if (_peerVideoTrack != null)
            {
                Media.CreateMediaAsync().AsTask().ContinueWith(media => 
                {
                  var source = media.Result.CreateMediaStreamSource(_peerVideoTrack, 30, "PEER");
                  RunOnUiThread(() =>
                  {
                    PeerVideo.SetMediaStreamSource(source);
                  });
                });
            }
            IsReadyToDisconnect = true;
        }

        /// <summary>
        /// Remove remote stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnRemoveRemoteStream(MediaStreamEvent evt)
        {
            RunOnUiThread(() =>
            {
                PeerVideo.SetMediaStreamSource(null);
            });
        }

        /// <summary>
        /// Add local stream event handler.
        /// </summary>
        /// <param name="evt">Details about Media stream event.</param>
        private void Conductor_OnAddLocalStream(MediaStreamEvent evt)
        {
          _selfVideoTrack = evt.Stream.GetVideoTracks().FirstOrDefault();
          if (_selfVideoTrack != null)
          {
            Media.CreateMediaAsync().AsTask().ContinueWith(media => 
            {
              var source = media.Result.CreateMediaStreamSource(_selfVideoTrack, 30, "SELF");
              RunOnUiThread(() =>
                {
                  if (_cameraEnabled)
                  {
                    Conductor.Instance.EnableLocalVideoStream();
                  }
                  else
                  {
                    Conductor.Instance.DisableLocalVideoStream();
                  }

                  if (_microphoneIsOn)
                  {
                    Conductor.Instance.UnmuteMicrophone();
                  }
                  else
                  {
                    Conductor.Instance.MuteMicrophone();
                  }
                  SelfVideo.SetMediaStreamSource(source);
                });
            });
          }
        }

        #region Bindings

        private ValidableNonEmptyString _ip;

        /// <summary>
        /// IP address of the server to connect.
        /// </summary>
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

        /// <summary>
        /// The port used to connect to the server.
        /// </summary>
        public ValidableIntegerString Port
        {
            get { return _port; }
            set
            {
                SetProperty(ref _port, value);
                _port.PropertyChanged += Port_PropertyChanged;
            }
        }

        /// <summary>
        /// A class represents a peer.
        /// </summary>
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
        
        /// <summary>
        /// The list of connected peers.
        /// </summary>
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

        /// <summary>
        /// The selected peer's info.
        /// </summary>
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

        /// <summary>
        /// Command to connect to the server.
        /// </summary>
        public ActionCommand ConnectCommand
        {
            get { return _connectCommand; }
            set
            {
                SetProperty(ref _connectCommand, value);
            }
        }

        private ActionCommand _connectToPeerCommand;

        /// <summary>
        /// Command to connect to a peer.
        /// </summary>
        public ActionCommand ConnectToPeerCommand
        {
            get { return _connectToPeerCommand; }
            set
            {
                SetProperty(ref _connectToPeerCommand, value);
            }
        }

        private ActionCommand _disconnectFromPeerCommand;

        /// <summary>
        /// Command to disconnect from a peer.
        /// </summary>
        public ActionCommand DisconnectFromPeerCommand
        {
            get { return _disconnectFromPeerCommand; }
            set
            {
                SetProperty(ref _disconnectFromPeerCommand, value);
            }
        }

        private ActionCommand _disconnectFromServerCommand;

        /// <summary>
        /// Command to disconnect from the server.
        /// </summary>
        public ActionCommand DisconnectFromServerCommand
        {
            get { return _disconnectFromServerCommand; }
            set
            {
                SetProperty(ref _disconnectFromServerCommand, value);
            }
        }

        private ActionCommand _addIceServerCommand;
        
        /// <summary>
        /// Command to add a new Ice server to the list.
        /// </summary>
        public ActionCommand AddIceServerCommand
        {
            get { return _addIceServerCommand; }
            set
            {
                SetProperty(ref _addIceServerCommand, value);
            }
        }

        private ActionCommand _removeSelectedIceServerCommand;

        /// <summary>
        /// Command to remove an Ice server from the list.
        /// </summary>
        public ActionCommand RemoveSelectedIceServerCommand
        {
            get { return _removeSelectedIceServerCommand; }
            set
            {
                SetProperty(ref _removeSelectedIceServerCommand, value);
            }
        }

        private ActionCommand _settingsButtonCommand;

        /// <summary>
        /// Command to open/hide the Settings controls.
        /// </summary>
        public ActionCommand SettingsButtonCommand
        {
            get { return _settingsButtonCommand; }
            set
            {
                SetProperty(ref _settingsButtonCommand, value);
            }
        }

        private String _peerWidth;

        /// <summary>
        /// Peer video width.
        /// </summary>
        public String PeerWidth
        {
            get { return _peerWidth; }
            set
            {
                SetProperty(ref _peerWidth, value);
            }
        }

        private String _peerHeight;

        /// <summary>
        /// Peer video height.
        /// </summary>
        public String PeerHeight
        {
            get { return _peerHeight; }
            set 
            {
                SetProperty(ref _peerHeight, value);
            }
        }

        private String _selfWidth;

        /// <summary>
        /// Self video width.
        /// </summary>
        public String SelfWidth 
        {
            get { return _selfWidth; }
            set 
            {
                SetProperty(ref _selfWidth, value);
            }
        }

        private String _selfHeight;

        /// <summary>
        /// Self video height.
        /// </summary>
        public String SelfHeight 
        {
            get { return _selfHeight; }
            set
            {
                SetProperty(ref _selfHeight, value);
            }
        }

        private String _peerVideoFps;

        /// <summary>
        /// Frame rate per second for the peer's video.
        /// </summary>
        public String PeerVideoFps
        {
          get { return _peerVideoFps; }
          set 
          {
            SetProperty(ref _peerVideoFps, value);
          }
        }

        private String _selfVideoFps;

        /// <summary>
        /// Frame rate per second for the self video.
        /// </summary>
        public String SelfVideoFps
        {
            get { return _selfVideoFps; }
            set
            {
                SetProperty(ref _selfVideoFps, value);
            }
        }

        private ActionCommand _sendFeedbackCommand;

        /// <summary>
        /// Command to send feedback to the specified email account.
        /// </summary>
        public ActionCommand SendFeedbackCommand
        {
            get { return _sendFeedbackCommand;  }
            set
            {
                SetProperty(ref _sendFeedbackCommand, value);
            }
        }

        private bool _hasServer;

        /// <summary>
        /// Indicator if a server IP address is specified in Settings.
        /// </summary>
        public bool HasServer
        {
            get { return _hasServer; }
            set
            {
                SetProperty(ref _hasServer, value);
            }
        }

        private bool _isConnected;

        /// <summary>
        /// Indicator if the user is connected to the server.
        /// </summary>
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

        /// <summary>
        /// Indicator if the application is in the process of connecting to the server.
        /// </summary>
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

        /// <summary>
        /// Indicator if the application is in the process of difconnecting from the server.
        /// </summary>
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

        /// <summary>
        /// Indicator if the user is connected to a peer.
        /// </summary>
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

        /// <summary>
        /// Indicator if the app is ready to connect to a peer.
        /// </summary>
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

        private bool _isReadyToDisconnect;
        /// <summary>
        /// Indicator if the app is ready to disconnect from a peer.
        /// </summary>
        public bool IsReadyToDisconnect
        {
            get { return _isReadyToDisconnect; }
            set
            {
                SetProperty(ref _isReadyToDisconnect, value);
                DisconnectFromPeerCommand.RaiseCanExecuteChanged();
            }
        }


        private ScrollBarVisibility _scrollBarVisibility;

        /// <summary>
        /// The scroll bar visibility type.
        /// This is used to have a scrollable UI if the application 
        /// main page is bigger in size than the device screen.
        /// </summary>
        public ScrollBarVisibility ScrollBarVisibilityType
        {
            get { return _scrollBarVisibility;  }
            set { SetProperty(ref _scrollBarVisibility, value); }
        }

        private bool _cameraEnabled = true;

        /// <summary>
        /// Camera on/off toggle button.
        /// Disabled/Enabled local stream if the camera is off/on.
        /// </summary>
        public bool CameraEnabled
        {
            get { return _cameraEnabled; }
            set
            {
                if (!SetProperty(ref _cameraEnabled, value))
                {
                    return;
                }

                if (_cameraEnabled)
                {
                    Conductor.Instance.EnableLocalVideoStream();
                }
                else
                {
                    Conductor.Instance.DisableLocalVideoStream();
                }
            }
        }

        private bool _microphoneIsOn = true;

        /// <summary>
        /// Microphone on/off toggle button.
        /// Unmute/Mute audio if the microphone is off/on.
        /// </summary>
        public bool MicrophoneIsOn
        {
            get { return _microphoneIsOn; }
            set
            {
                if (!SetProperty(ref _microphoneIsOn, value))
                {
                    return;
                }

                if (_microphoneIsOn)
                {
                    Conductor.Instance.UnmuteMicrophone();
                }
                else
                {
                    Conductor.Instance.MuteMicrophone();
                }
            }
        }

        private bool _isMicrophoneEnabled = true;

        /// <summary>
        /// Indicator if the microphone is enabled.
        /// </summary>
        public bool IsMicrophoneEnabled
        {
            get { return _isMicrophoneEnabled; }
            set { SetProperty(ref _isMicrophoneEnabled, value); }
        }

        private bool _isCameraEnabled = true;

        /// <summary>
        /// Indicator if the camera is enabled.
        /// </summary>
        public bool IsCameraEnabled
        {
            get { return _isCameraEnabled; }
            set { SetProperty(ref _isCameraEnabled, value); }
        }

        private bool _tracingEnabled;

        /// <summary>
        /// Enable tracing toggle button.
        /// Stop tracing and send logs/Start tracing if the tracing is disabled/enabled.
        /// </summary>
        public bool TracingEnabled
        {
            get { return _tracingEnabled; }
            set
            {
                if (!SetProperty(ref _tracingEnabled, value))
                {
                    return;
                }

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

        /// <summary>
        /// The trace server IP address.
        /// </summary>
        public string TraceServerIp
        {
            get { return _traceServerIp; }
            set
            {
                if (!SetProperty(ref _traceServerIp, value))
                {
                    return;
                }
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["TraceServerIp"] = _traceServerIp;
            }
        }

        private string _traceServerPort = string.Empty;

        /// <summary>
        /// The trace server port to connect.
        /// </summary>
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

        /// <summary>
        /// The list of available cameras.
        /// </summary>
        public ObservableCollection<MediaDevice> Cameras
        {
            get { return _cameras; }
            set
            {
                SetProperty(ref _cameras, value);
            }
        }

        private MediaDevice _selectedCamera;

        /// <summary>
        /// The selected camera.
        /// </summary>
        public MediaDevice SelectedCamera
        {
            get { return _selectedCamera; }
            set
            {
                SetProperty(ref _selectedCamera, value);
                Conductor.Instance.Media.SelectVideoDevice(_selectedCamera);
                if (_allCapRes == null)
                {
                    _allCapRes = new ObservableCollection<String>();
                }
                else
                {
                    _allCapRes.Clear();
                }
                if (value == null)
                {
                    String errorMsg = "SetSelectedCamera: Skip GetVideoCaptureCapabilities (Trying to set Null)";
                    Debug.WriteLine(errorMsg);
                    return;
                }
                var opRes = value.GetVideoCaptureCapabilities();
                opRes.AsTask().ContinueWith(resolutions =>
                {
                    RunOnUiThread(async () =>
                    {
                        if (resolutions.IsFaulted)
                        {
                            Exception ex = resolutions.Exception;
                            while (ex is AggregateException && ex.InnerException != null)
                                ex = ex.InnerException;
                            String errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Error: " + ex.Message + ")";
                            Debug.WriteLine(errorMsg);
                            var msgDialog = new MessageDialog(errorMsg);
                            await msgDialog.ShowAsync();
                            return;
                        }
                        if (resolutions.Result == null)
                        {
                            String errorMsg = "SetSelectedCamera: Failed to GetVideoCaptureCapabilities (Result is null)";
                            Debug.WriteLine(errorMsg);
                            var msgDialog = new MessageDialog(errorMsg);
                            await msgDialog.ShowAsync();
                            return;
                        }
                        var uniqueRes = resolutions.Result.GroupBy(test => test.ResolutionDescription).Select(grp => grp.First()).ToList();
                        CaptureCapability defaultResolution = null;
                        foreach (var resolution in uniqueRes)
                        {
                            if (defaultResolution == null)
                            {
                                defaultResolution = resolution;
                            }
                            _allCapRes.Add(resolution.ResolutionDescription);
                            if ((resolution.Width == 640) && (resolution.Height == 480))
                            {
                                defaultResolution = resolution;
                            }
                        }
                        SelectedCapResItem = defaultResolution.ResolutionDescription;
                    });
                });
            }
        }

        private ObservableCollection<MediaDevice> _microphones;

        /// <summary>
        /// The list of available microphones.
        /// </summary>
        public ObservableCollection<MediaDevice> Microphones
        {
            get
            {
                return _microphones;
            }
            set { SetProperty(ref _microphones, value); }
        }

        private MediaDevice _selectedMicrophone;

        /// <summary>
        /// The selected microphone.
        /// </summary>
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

        /// <summary>
        /// Indicator if logging is enabled.
        /// </summary>
        public bool LoggingEnabled
        {
            get { return _loggingEnabled; }
            set
            {
                if (!SetProperty(ref _loggingEnabled, value))
                {
                    return;
                }

                if (_loggingEnabled)
                {
                  WebRTC.EnableLogging(LogLevel.LOGLVL_INFO);
                }
                else
                {
                  WebRTC.DisableLogging();
                  SavingLogging();
                }
            }
        }
       
        /// <summary>
        /// Saves the logs to a file in a selected directory.
        /// </summary>
        private async Task SavingLogging()
        {
            StorageFolder logFolder = WebRTC.LogFolder();

            String logFileName = WebRTC.LogFileName();

            StorageFile logFile= await logFolder.GetFileAsync(logFileName);

            webrtcLoggingFile = null; // Reset

            if (logFile != null) 
            {
                Windows.Storage.Pickers.FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

                // Generate log file with timestamp
                DateTime now = DateTime.Now;
                object[] args = { now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second };
                String targetFileName = string.Format("webrt_logging_{0}{1}{2}{3}{4}{5}", args);
                savePicker.SuggestedFileName = targetFileName;

                savePicker.FileTypeChoices.Add("webrtc log", new System.Collections.Generic.List<string>() { ".log" });

#if WINDOWS_PHONE_APP
                CoreApplication.GetCurrentView().Activated += ViewActivated;
                webrtcLoggingFile = logFile;
                savePicker.PickSaveFileAndContinue();
#else
                // Prompt user to select destination to save
                StorageFile targetFile = await savePicker.PickSaveFileAsync();

                saveLogFileToUserSelectedFile(logFile, targetFile);
#endif
            }
        }

        /// <summary>
        /// Helper to save the log file .
        /// </summary>
        /// <param name="source">The log source file</param>
        /// <param name="targetFile">The target file</param>
        /// <returns></returns>
        async Task saveLogFileToUserSelectedFile(StorageFile source, StorageFile targetFile)
        {
            if (targetFile != null)
            {
                await source.CopyAndReplaceAsync(targetFile);
            }
        }

        // Reached after WP device selects file
        void ViewActivated(CoreApplicationView sender, IActivatedEventArgs args)
        {
#if WINDOWS_PHONE_APP
            if (args.Kind == ActivationKind.PickSaveFileContinuation)
            {
                FileSavePickerContinuationEventArgs fileArgs = args as FileSavePickerContinuationEventArgs;
                if (fileArgs != null && fileArgs.File != null)
                {
                    saveLogFileToUserSelectedFile(webrtcLoggingFile, fileArgs.File);
                }
            }
            CoreApplication.GetCurrentView().Activated -= ViewActivated;
#endif
        }

        private ObservableCollection<IceServer> _iceServers;

        /// <summary>
        /// The list of Ice servers.
        /// </summary>
        public ObservableCollection<IceServer> IceServers
        {
            get { return _iceServers; }
            set { SetProperty(ref _iceServers, value); }
        }

        private IceServer _selectedIceServer;

        /// <summary>
        /// The selected Ice server.
        /// </summary>
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

        /// <summary>
        /// New Ice server, invokes the NewIceServer event.
        /// </summary>
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

        /// <summary>
        /// The list of audio codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> AudioCodecs
        {
            get { return _audioCodecs; }
            set { SetProperty(ref _audioCodecs, value); }
        }

        /// <summary>
        /// The selected Audio codec.
        /// </summary>
        public CodecInfo SelectedAudioCodec
        {
            get { return Conductor.Instance.AudioCodec; }
            set
            {
                if (Conductor.Instance.AudioCodec == value)
                {
                    return;
                }
                Conductor.Instance.AudioCodec = value;
                OnPropertyChanged(() => SelectedAudioCodec);
            }
        }

        private ObservableCollection<String> _allCapRes;
        public ObservableCollection<String> AllCapRes
        /// <summary>
        /// The list of all capture resolutions.
        /// </summary>
        {
            get { return _allCapRes; }
            set { SetProperty(ref _allCapRes, value); }
        }

        private String _selectedCapResItem;
        public String SelectedCapResItem
        /// <summary>
        /// The selected capture resolution.
        /// </summary>
        {
            get { return _selectedCapResItem; }
            set
            {
                if (AllCapFPS == null)
                {
                  AllCapFPS = new ObservableCollection<CaptureCapability>();
                }
                else
                {
                  AllCapFPS.Clear();
                }
                var opCap = SelectedCamera.GetVideoCaptureCapabilities();
                opCap.AsTask().ContinueWith(caps =>
                {
                  var fpsList = from cap in caps.Result where cap.ResolutionDescription == value select cap;
                  var t = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                      CaptureCapability defaultFPS = null;
                      foreach (var fps in fpsList)
                      {
                        AllCapFPS.Add(fps);
                        if ((defaultFPS == null) || (fps.FrameRate == 30))
                        {
                          defaultFPS = fps;
                        }
                      }
                      SelectedCapFPSItem = defaultFPS;
                    });
                });
                SetProperty(ref _selectedCapResItem, value);
            }
        }

        private ObservableCollection<CaptureCapability> _allCapFPS;
        public ObservableCollection<CaptureCapability> AllCapFPS
        /// <summary>
        /// The list of all capture frame rates.
        /// </summary>
        {
            get { return _allCapFPS; }
            set { SetProperty(ref _allCapFPS, value); }
        }

        private CaptureCapability _selectedCapFPSItem;
        public CaptureCapability SelectedCapFPSItem
        /// <summary>
        /// The selected capture frame rate.
        /// </summary>
        {
            get { return _selectedCapFPSItem; }
            set
            {
                if (SetProperty(ref _selectedCapFPSItem, value))
                {
                  Conductor.Instance.VideoCaptureProfile = value;
                  Conductor.Instance.updatePreferredFrameFormat();
                }
            }
        }

        private ObservableCollection<CodecInfo> _videoCodecs;

        /// <summary>
        /// The list of video codecs.
        /// </summary>
        public ObservableCollection<CodecInfo> VideoCodecs
        {
            get { return _videoCodecs; }
            set { SetProperty(ref _videoCodecs, value); }
        }

        /// <summary>
        /// The selected video codec.
        /// </summary>
        public CodecInfo SelectedVideoCodec
        {
            get { return Conductor.Instance.VideoCodec; }
            set
            {
                if (Conductor.Instance.VideoCodec == value)
                {
                    return;
                }

                Conductor.Instance.VideoCodec = value;
                OnPropertyChanged(() => SelectedVideoCodec);
            }
        }

        private string _appVersion = "N/A";

        /// <summary>
        /// The application version.
        /// </summary>
        public string AppVersion
        {
            get { return _appVersion; }
            set { SetProperty(ref _appVersion, value); }
        }

        private string _crashReportUserInfo = "";

        /// <summary>
        /// The user info to provide when a crash happens.
        /// </summary>
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

        /// <summary>
        /// Logic to determine if the server is configured.
        /// </summary>
        private void ReevaluateHasServer()
        {
            HasServer = Ip != null && Ip.Valid && Port != null && Port.Valid;
        }

        /// <summary>
        /// Logic to determine if the application is ready to connect to a server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to connect to server.</returns>
        private bool ConnectCommandCanExecute(object obj)
        {
            return !IsConnected && !IsConnecting && Ip.Valid && Port.Valid;
        }

        /// <summary>
        /// Executer command for connecting to server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void ConnectCommandExecute(object obj)
        {
            new Task(() =>
            {
                IsConnecting = true;
                Conductor.Instance.StartLogin(Ip.Value, Port.Value);
            }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to connect to a peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to connect to a peer.</returns>
        private bool ConnectToPeerCommandCanExecute(object obj)
        {
            return SelectedPeer != null && Peers.Contains(SelectedPeer) && !IsConnectedToPeer && IsReadyToConnect;
        }

        /// <summary>
        /// Executer command to connect to a peer.
        /// </summary>
        /// <param name="obj"></param>
        private void ConnectToPeerCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.ConnectToPeer(SelectedPeer.Id);
            }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to disconnect from peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to disconnect from a peer.</returns>
        private bool DisconnectFromPeerCommandCanExecute(object obj)
        {
            return IsConnectedToPeer && IsReadyToDisconnect;
        }

        /// <summary>
        /// Executer command to disconnect from a peer.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void DisconnectFromPeerCommandExecute(object obj)
        {
            new Task(() =>
            {
                Conductor.Instance.DisconnectFromPeer();
            }).Start();
        }

        /// <summary>
        /// Logic to determine if the application is ready to disconnect from server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application is ready to disconnect from the server.</returns>
        private bool DisconnectFromServerCanExecute(object obj)
        {
            if (IsDisconnecting)
            {
                return false;
            }

            return IsConnected;
        }

        /// <summary>
        /// Executer command to disconnect from server.
        /// </summary>
        /// <param name="obj"></param>
        private void DisconnectFromServerExecute(object obj)
        {
            new Task(() =>
            {
                IsDisconnecting = true;
                Conductor.Instance.DisconnectFromServer();
            }).Start();

            if (Peers != null)
            {
                Peers.Clear();
            }
        }

        /// <summary>
        /// Logic to determine if the application is ready to add an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application can add an Ice server to the list.</returns>
        private bool AddIceServerCanExecute(object obj)
        {
            return NewIceServer.Valid;
        }

        /// <summary>
        /// Executer command to add an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void AddIceServerExecute(object obj)
        {
            IceServers.Add(_newIceServer);
            OnPropertyChanged(() => IceServers);
            Conductor.Instance.ConfigureIceServers(IceServers);
            SaveIceServerList();
            NewIceServer = new IceServer();
        }

        /// <summary>
        /// Logic to determine if the application is ready to remove an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        /// <returns>True if the application can remove an ice server from the list.</returns>
        private bool RemoveSelectedIceServerCanExecute(object obj)
        {
            return SelectedIceServer != null;
        }

        /// <summary>
        /// Executer command to remove an Ice server.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void RemoveSelectedIceServerExecute(object obj)
        {
            IceServers.Remove(_selectedIceServer);
            OnPropertyChanged(() => IceServers);
            SaveIceServerList();
            Conductor.Instance.ConfigureIceServers(IceServers);
        }

        /// <summary>
        /// Executer command to send feedback.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void SendFeedbackExecute(object obj)
        {
#if !WINDOWS_UAP // Disable on Win10 for now.
            HockeyClient.Current.ShowFeedback();
#endif
        }

        private bool _settingsButtonChecked;

        /// <summary>
        /// Indicator if Settings button is checked
        /// </summary>
        public bool SettingsButtonChecked
        {
            get { return _settingsButtonChecked; }
            set
            {
                SetProperty(ref _settingsButtonChecked, value);
            }
        }

        /// <summary>
        /// Execute for Settings button is hit event.
        /// Calls to update the ScrollBarVisibilityType property.
        /// </summary>
        /// <param name="obj">The sender object.</param>
        private void SettingsButtonExecute(object obj) 
        {
            UpdateScrollBarVisibilityTypeHelper();
        }

        /// <summary>
        /// Makes the UI scrollable if the controls do not fit the device
        /// screen size.
        /// The UI is not scrollable if connected to a peer.
        /// </summary>
        private void UpdateScrollBarVisibilityTypeHelper()
        {
            if (SettingsButtonChecked)
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Auto;
            }
            else if (IsConnectedToPeer)
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Disabled;
            }
            else
            {
                ScrollBarVisibilityType = ScrollBarVisibility.Auto;
            }
        }

        /// <summary>
        /// Loads the settings with predefined and default values.
        /// </summary>
        void LoadSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Default values:
            var configTraceServerIp = "127.0.0.1";
            var configTraceServerPort = "55000";
            var peerCcServerIp = new ValidableNonEmptyString("127.0.0.1");
            var peerCcPortInt = 8888;

            if (settings.Values["PeerCCServerIp"] != null)
            {
                peerCcServerIp = new ValidableNonEmptyString((string)settings.Values["PeerCCServerIp"]);
            }

            if (settings.Values["PeerCCServerPort"] != null)
            {
                peerCcPortInt = Convert.ToInt32(settings.Values["PeerCCServerPort"]);
            }
          
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
                // Default values:
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
                Ip = peerCcServerIp;
                Port = new ValidableIntegerString(peerCcPortInt, 0, 65535);
                ReevaluateHasServer();
            });

            Conductor.Instance.ConfigureIceServers(configIceServers);

        }

        /// <summary>
        /// Loads the Hockey app settings.
        /// </summary>
        void LoadHockeyAppSettings()
        {
            var settings = ApplicationData.Current.LocalSettings;

            // Default values:
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

        /// <summary>
        /// Saves the Ice servers list.
        /// </summary>
        void SaveIceServerList()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string xmlIceServers = XmlSerializer<ObservableCollection<IceServer>>.ToXml(IceServers);
            localSettings.Values["IceServerList"] = xmlIceServers;
        }

        /// <summary>
        /// NewIceServer event handler .
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void NewIceServer_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                AddIceServerCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// IP changed event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void Ip_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
            ReevaluateHasServer();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["PeerCCServerIp"] = _ip.Value;
        }


        /// <summary>
        /// Port changed event handler.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">Property Changed event information.</param>
        void Port_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                ConnectCommand.RaiseCanExecuteChanged();
            }
            ReevaluateHasServer();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["PeerCCServerPort"] = _port.Value;
        }
        private StorageFile webrtcLoggingFile = null;

        /// <summary>
        /// retrieve the current network time from ntp server  "time.windows.com"
        /// </summary>
        public static async Task GetNetworkTime()
        {
            //default Windows time server
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            byte[] ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)


            //NTP uses UDP
            var socket = new Windows.Networking.Sockets.DatagramSocket();
            socket.MessageReceived += OnNTPTimeReceived;

            try
            {
                //The UDP port number assigned to NTP is 123
                await socket.ConnectAsync(new Windows.Networking.HostName(ntpServer), "123");
                await socket.OutputStream.WriteAsync(ntpData.AsBuffer());

            }
            catch (Exception e)
            {
                MessageDialog dialog = new MessageDialog("Failed To sync with ntp server.");
                dialog.ShowAsync();
            }

        }

        /// <summary>
        /// event hander when receiving response from the ntp server
        /// </summary>
        /// <param name="socket">The udp socket object which triggered this event </param>
        /// <param name="eventArguments">event information</param>
        static async void OnNTPTimeReceived(Windows.Networking.Sockets.DatagramSocket socket, Windows.Networking.Sockets.DatagramSocketMessageReceivedEventArgs eventArguments)
        {
            byte[] ntpData = new byte[48];

            eventArguments.GetDataReader().ReadBytes(ntpData);

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            WebRTC.SynNTPTime((long)milliseconds);

            socket.Dispose();


        }


        static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}

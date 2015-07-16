using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Windows.Networking.Connectivity;
using Windows.Networking;
using Windows.Data.Json;
using webrtc_winrt_api;
using System.Diagnostics;
using System.Threading.Tasks;
using PeerConnectionClient.Model;
using System.Collections.ObjectModel;
using PeerConnectionClient.Utilities;

namespace PeerConnectionClient.Signalling
{
    internal class Conductor
    {
        private static Object _instanceLock = new Object();
        private static Conductor _instance;
        public static Conductor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Conductor();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly Signaller _signaller;
        public Signaller Signaller
        {
            get
            {
                return _signaller;
            }
        }

        public CodecInfo VideoCodec { get; set; }
        public CodecInfo AudioCodec { get; set; }

        private static readonly string kCandidateSdpMidName = "sdpMid";
        private static readonly string kCandidateSdpMlineIndexName = "sdpMLineIndex";
        private static readonly string kCandidateSdpName = "candidate";
        private static readonly string kSessionDescriptionTypeName = "type";
        private static readonly string kSessionDescriptionSdpName = "sdp";

        RTCPeerConnection _peerConnection;
        Media _media;
        public Media Media {
          get {
            return _media;
          }
        }

        MediaStream _mediaStream;
        List<RTCIceServer> _iceServers;

        private int _peerId = -1;
        bool _videoEnabled = true;
        bool _audioEnabled = true;

        public event Action<MediaStreamEvent> OnAddLocalStream;
        public event Action<MediaStreamEvent> OnRemoveLocalStream;

        public event Action OnPeerConnectionCreated;
        public event Action OnPeerConnectionClosed;

        private async Task<bool> CreatePeerConnection()
        {
            Debug.Assert(_peerConnection == null);

            var config = new RTCConfiguration()
            {
                BundlePolicy = RTCBundlePolicy.Balanced,
                IceTransportPolicy = RTCIceTransportPolicy.All,
                IceServers = _iceServers
                //IceServers = new List<RTCIceServer>() {
                //        new RTCIceServer { Url = "stun:stun.l.google.com:19302" },
                //        new RTCIceServer { Url = "stun:stun1.l.google.com:19302" },
                //        new RTCIceServer { Url = "stun:stun2.l.google.com:19302" },
                //        new RTCIceServer { Url = "stun:stun3.l.google.com:19302" },
                //        new RTCIceServer { Url = "stun:stun4.l.google.com:19302" },
                //    }
            };

            Debug.WriteLine("Conductor: Creating peer connection.");
            _peerConnection = new RTCPeerConnection(config);

            if (OnPeerConnectionCreated != null)
                OnPeerConnectionCreated();

            _peerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
            _peerConnection.OnAddStream += PeerConnection_OnAddStream;
            _peerConnection.OnRemoveStream += PeerConnection_OnRemoveStream;

            Debug.WriteLine("Conductor: Getting user media.");
            RTCMediaStreamConstraints mediaStreamConstraints = new RTCMediaStreamConstraints();
            mediaStreamConstraints.audioEnabled = _audioEnabled;
            mediaStreamConstraints.videoEnabled = _videoEnabled;
            _mediaStream = await _media.GetUserMedia(mediaStreamConstraints);

            Debug.WriteLine("Conductor: Adding local media stream.");
            _peerConnection.AddStream(_mediaStream);
            if (OnAddLocalStream != null)
                OnAddLocalStream(new MediaStreamEvent() { Stream = _mediaStream });

            return true;
        }

        private void ClosePeerConnection()
        {
            if (_peerConnection != null)
            {
                _peerConnection.Close();
                _peerConnection = null;
                _peerId = -1;
                foreach (var track in _mediaStream.GetTracks())
                {
                    track.Stop();
                    _mediaStream.RemoveTrack(track);
                }
                _mediaStream = null;
                if (OnPeerConnectionClosed != null)
                    OnPeerConnectionClosed();
            }
        }

        private void PeerConnection_OnIceCandidate(RTCPeerConnectionIceEvent evt)
        {
            var json = new JsonObject
            {
                {kCandidateSdpMidName, JsonValue.CreateStringValue(evt.Candidate.SdpMid)},
                {kCandidateSdpMlineIndexName, JsonValue.CreateNumberValue(evt.Candidate.SdpMLineIndex)},
                {kCandidateSdpName, JsonValue.CreateStringValue(evt.Candidate.Candidate)}
            };
            Debug.WriteLine("Conductor: Sending ice candidate.\n" + json.Stringify());
            SendMessage(json);
        }

        public event Action<MediaStreamEvent> OnAddRemoteStream;
        private void PeerConnection_OnAddStream(MediaStreamEvent evt)
        {
            if (OnAddRemoteStream != null)
                OnAddRemoteStream(evt);
        }

        public event Action<MediaStreamEvent> OnRemoveRemoteStream;
        private void PeerConnection_OnRemoveStream(MediaStreamEvent evt)
        {
            if (OnRemoveRemoteStream != null)
                OnRemoveRemoteStream(evt);
        }

        private Conductor()
        {
            _signaller = new Signaller();
            _media = new Media();

            Signaller.OnDisconnected += Signaller_OnDisconnected;
            Signaller.OnMessageFromPeer += Signaller_OnMessageFromPeer;
            Signaller.OnMessageSent += Signaller_OnMessageSent;
            Signaller.OnPeerConnected += Signaller_OnPeerConnected;
            Signaller.OnPeerHangup += Signaller_OnPeerHangup;
            Signaller.OnPeerDisconnected += Signaller_OnPeerDisconnected;
            Signaller.OnServerConnectionFailure += Signaller_OnServerConnectionFailure;
            Signaller.OnSignedIn += Signaller_OnSignedIn;

            _iceServers = new List<RTCIceServer>();
        }

        void Signaller_OnPeerHangup(int peer_id)
        {
            if (peer_id == _peerId) {
                Debug.WriteLine("Conductor: Our peer hung up.");
                ClosePeerConnection();
            }
        }

        private void Signaller_OnSignedIn()
        {
        }

        private void Signaller_OnServerConnectionFailure()
        {
            Debug.WriteLine("ERROR: Connection to server failed!");
        }

        private void Signaller_OnPeerDisconnected(int peer_id)
        {
            if (peer_id == _peerId)
            {
                Debug.WriteLine("Conductor: Our peer disconnected.");
                ClosePeerConnection();
            }
        }

        private void Signaller_OnPeerConnected(int id, string name)
        {
        }

        private void Signaller_OnMessageSent(int err)
        {
        }

        private void Signaller_OnMessageFromPeer(int peerId, string message)
        {
            Task.Run(async () =>
                {
                    Debug.Assert(_peerId == peerId || _peerId == -1);
                    Debug.Assert(message.Length > 0);

                    if (_peerConnection == null)
                    {
                        Debug.Assert(_peerId == -1);
                        _peerId = peerId;

                        if (!await CreatePeerConnection())
                        {
                            Debug.WriteLine("Conductor: Failed to initialize our PeerConnection instance");
                            await Signaller.SignOut();
                            return;
                        }
                        else if (_peerId != peerId)
                        {
                            Debug.WriteLine("Conductor: Received a message from unknown peer while already in a conversation with a different peer.");
                            return;
                        }
                    }

                    JsonObject jMessage;
                    if (!JsonObject.TryParse(message, out jMessage))
                    {
                        Debug.WriteLine("Conductor: Received unknown message." + message);
                        return;
                    }

                    string type = jMessage.ContainsKey(kSessionDescriptionTypeName) ? jMessage.GetNamedString(kSessionDescriptionTypeName) : null;
                    if (!String.IsNullOrEmpty(type))
                    {
                        if (type == "offer-loopback")
                        {
                            // TODO: Loopback support?
                            Debug.Assert(false);
                        }

                        string sdp = jMessage.ContainsKey(kSessionDescriptionSdpName) ? jMessage.GetNamedString(kSessionDescriptionSdpName) : null;
                        if (String.IsNullOrEmpty(sdp))
                        {
                            Debug.WriteLine("Conductor: Can't parse received session description message.");
                            return;
                        }

                        RTCSdpType sdpType = RTCSdpType.Offer;
                        switch (type)
                        {
                            case "offer": sdpType = RTCSdpType.Offer; break;
                            case "answer": sdpType = RTCSdpType.Answer; break;
                            case "pranswer": sdpType = RTCSdpType.Pranswer; break;
                            default: Debug.Assert(false, type); break;
                        }

                        Debug.WriteLine("Conductor: Received session description: " + message);
                        await _peerConnection.SetRemoteDescription(new RTCSessionDescription(sdpType, sdp));

                        if (sdpType == RTCSdpType.Offer)
                        {
                            var answer = await _peerConnection.CreateAnswer();
                            await _peerConnection.SetLocalDescription(answer);
                            // Send answer
                            SendSdp(answer);
                        }
                    }
                    else
                    {
                        var sdpMid = jMessage.ContainsKey(kCandidateSdpMidName) ? jMessage.GetNamedString(kCandidateSdpMidName) : null;
                        var sdpMlineIndex = jMessage.ContainsKey(kCandidateSdpMlineIndexName) ? jMessage.GetNamedNumber(kCandidateSdpMlineIndexName) : -1;
                        var sdp = jMessage.ContainsKey(kCandidateSdpName) ? jMessage.GetNamedString(kCandidateSdpName) : null;
                        if (String.IsNullOrEmpty(sdpMid) || sdpMlineIndex == -1 || String.IsNullOrEmpty(sdp))
                        {
                            Debug.WriteLine("Conductor: Can't parse received message.\n" + message);
                            return;
                        }

                        var candidate = new RTCIceCandidate(sdp, sdpMid, (ushort)sdpMlineIndex);
                        await _peerConnection.AddIceCandidate(candidate);
                        Debug.WriteLine("Conductor: Received candidate : " + message);
                    }
                }).Wait();
        }

        private void Signaller_OnDisconnected()
        {
            ClosePeerConnection();
        }

        public void StartLogin(string server, string port)
        {
            if (_signaller.IsConnected())
                return;
            _signaller.Connect(server, port, GetLocalPeerName());
        }

        public async void DisconnectFromServer()
        {
            if (_signaller.IsConnected())
                await _signaller.SignOut();
        }

        public async void ConnectToPeer(int peerId)
        {
            Debug.Assert(peerId != -1);
            Debug.Assert(_peerId == -1);


            if (_peerConnection != null)
            {
                Debug.WriteLine("Error: We only support connecting to one peer at a time");
                return;
            }

            if (await CreatePeerConnection())
            {
                _peerId = peerId;
                var offer = await _peerConnection.CreateOffer();

                //Alter sdp to force usage of selected codecs
                string newSdp = offer.Sdp;
                SdpUtils.SelectCodecs(ref newSdp, AudioCodec, VideoCodec);
                offer.Sdp = newSdp;

                await _peerConnection.SetLocalDescription(offer);
                Debug.WriteLine("Conductor: Sending offer.");
                SendSdp(offer);
            }
        }

        public void DisconnectFromPeer()
        {
            SendHangupMessage();
            ClosePeerConnection();
        }

        private string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            return hostname != null ? hostname.CanonicalName : "<unknown host>";
        }

        private void SendSdp(RTCSessionDescription description)
        {
            var json = new JsonObject();
            json.Add(kSessionDescriptionTypeName, JsonValue.CreateStringValue(description.Type.GetValueOrDefault().ToString().ToLower()));
            json.Add(kSessionDescriptionSdpName, JsonValue.CreateStringValue(description.Sdp));
            SendMessage(json);
        }

        private void SendMessage(IJsonValue json)
        {
            // Don't await, send it async.
            _signaller.SendToPeer(_peerId, json);
        }

        private void SendHangupMessage()
        {
            _signaller.SendToPeer(_peerId, "BYE");
        }

        public void EnableLocalVideoStream()
        {
            if (_mediaStream != null)
            {
                foreach (MediaVideoTrack videoTrack in _mediaStream.GetVideoTracks())
                {
                    videoTrack.Enabled = true;
                }
            }
            _videoEnabled = true;
        }

        public void DisableLocalVideoStream()
        {
            if (_mediaStream != null)
            {
                foreach (MediaVideoTrack videoTrack in _mediaStream.GetVideoTracks())
                {
                    videoTrack.Enabled = false;
                }
            }
            _videoEnabled = false;
        }

        public void MuteMicrophone()
        {
            if (_mediaStream != null)
            {
                var audioTracks = _mediaStream.GetAudioTracks();
                foreach (MediaAudioTrack audioTrack in _mediaStream.GetAudioTracks())
                {
                    audioTrack.Enabled = false;
                }
            }
            _audioEnabled = false;
        }
        public void UnmuteMicrophone()
        {
            if (_mediaStream != null)
            {
                var audioTracks = _mediaStream.GetAudioTracks();
                foreach (MediaAudioTrack audioTrack in _mediaStream.GetAudioTracks())
                {
                    audioTrack.Enabled = true;
                }
            }
            _audioEnabled = true;
        }

        public void ConfigureIceServers(Collection<IceServer> iceServers)
        {
            _iceServers.Clear();
            foreach(IceServer iceServer in iceServers)
            {
                //Url format: stun:stun.l.google.com:19302
                string url = "stun:";
                if (iceServer.Type == IceServer.ServerType.TURN)
                    url = "turn:";
                url += iceServer.Host.Value + ":" + iceServer.Port.Value;
                RTCIceServer server = new RTCIceServer { Url = url };
                if(iceServer.Credential != null)
                    server.Credential = iceServer.Credential;
                if(iceServer.Username != null)
                    server.Username = iceServer.Username;
                _iceServers.Add(server);
            }
        }
    }
}

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

namespace PeerConnectionClient.Signalling
{
    class Conductor
    {
        private static Conductor _instance;
        public static Conductor Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Conductor();
                return _instance;
            }
        }

        Signaller _signaller;
        public Signaller Signaller
        {
            get
            {
                return _signaller;
            }
        }

        private static readonly string kCandidateSdpMidName = "sdpMid";
        private static readonly string kCandidateSdpMlineIndexName = "sdpMLineIndex";
        private static readonly string kCandidateSdpName = "candidate";
        private static readonly string kSessionDescriptionTypeName = "type";
        private static readonly string kSessionDescriptionSdpName = "sdp";

        RTCPeerConnection _peerConnection;
        Media _media;
        private int _peerId = -1;

        public event Action<MediaStreamEvent> OnAddLocalStream;
        public event Action<MediaStreamEvent> OnRemoveLocalStream;

        private async Task<bool> CreatePeerConnection()
        {
            Debug.Assert(_peerConnection == null);

            var config = new RTCConfiguration()
            {
                BundlePolicy = RTCBundlePolicy.MaxCompat,
                IceTransportPolicy = RTCIceTransportPolicy.All,
                IceServers = new List<RTCIceServer>() {
                        new RTCIceServer { Url = "stun:stun.l.google.com:19302" },
                        new RTCIceServer { Url = "stun:stun1.l.google.com:19302" },
                        new RTCIceServer { Url = "stun:stun2.l.google.com:19302" },
                        new RTCIceServer { Url = "stun:stun3.l.google.com:19302" },
                        new RTCIceServer { Url = "stun:stun4.l.google.com:19302" },
                    }
            };

            Debug.WriteLine("Conductor: Creating peer connection.");
            _peerConnection = new RTCPeerConnection(config);

            _peerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
            _peerConnection.OnAddStream += PeerConnection_OnAddStream;
            _peerConnection.OnRemoveStream += PeerConnection_OnRemoveStream;

            if (_media == null)
                _media = new Media();

            Debug.WriteLine("Conductor: Getting user media.");
            var stream = await _media.GetUserMedia();
            Debug.WriteLine("Conductor: Adding local media stream.");
            _peerConnection.AddStream(stream);
            if (OnAddLocalStream != null)
                OnAddLocalStream(new MediaStreamEvent() { Stream = stream });

            return true;
        }

        private void PeerConnection_OnIceCandidate(RTCPeerConnectionIceEvent evt)
        {
            var json = new JsonObject();
            json.Add(kCandidateSdpMidName, JsonValue.CreateStringValue(evt.Candidate.SdpMid));
            json.Add(kCandidateSdpMlineIndexName, JsonValue.CreateNumberValue(evt.Candidate.SdpMLineIndex));
            json.Add(kCandidateSdpName, JsonValue.CreateStringValue(evt.Candidate.Candidate));
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

            Signaller.OnDisconnected += Signaller_OnDisconnected;
            Signaller.OnMessageFromPeer += Signaller_OnMessageFromPeer;
            Signaller.OnMessageSent += Signaller_OnMessageSent;
            Signaller.OnPeerConnected += Signaller_OnPeerConnected;
            Signaller.OnPeerDisconnected += Signaller_OnPeerDisconnected;
            Signaller.OnServerConnectionFailure += Signaller_OnServerConnectionFailure;
            Signaller.OnSignedIn += Signaller_OnSignedIn;
        }

        private void Signaller_OnSignedIn()
        {
        }

        private void Signaller_OnServerConnectionFailure()
        {
        }

        private void Signaller_OnPeerDisconnected(int peer_id)
        {
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
                await _peerConnection.SetLocalDescription(offer);
                Debug.WriteLine("Conductor: Sending offer.");
                SendSdp(offer);
            }
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
    }
}

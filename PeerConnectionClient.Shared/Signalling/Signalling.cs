using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace PeerConnectionClient.Signalling
{
    public delegate void SignedInDelegate();
    public delegate void DisconnectedDelegate();
    public delegate void PeerConnectedDelegate(int id, string name);
    public delegate void PeerDisonnectedDelegate(int peer_id);
    public delegate void PeerHangupDelegate(int peer_id);
    public delegate void MessageFromPeerDelegate(int peer_id, string message);
    public delegate void MessageSentDelegate(int err);
    public delegate void ServerConnectionFailureDelegate();

    class Signaller
    {
        public event SignedInDelegate OnSignedIn;
        public event DisconnectedDelegate OnDisconnected;
        public event PeerConnectedDelegate OnPeerConnected;
        public event PeerDisonnectedDelegate OnPeerDisconnected;
        public event PeerHangupDelegate OnPeerHangup;
        public event MessageFromPeerDelegate OnMessageFromPeer;
        public event MessageSentDelegate OnMessageSent;
        public event ServerConnectionFailureDelegate OnServerConnectionFailure;

        public Signaller()
        {
            _state = State.NOT_CONNECTED;
            _myId = -1;

            // Annoying but register empty handlers
            // so we don't have to check for null everywhere.
            OnSignedIn += () => { };
            OnDisconnected += () => { };
            OnPeerConnected += (a, b) => { };
            OnPeerDisconnected += (a) => { };
            OnMessageFromPeer += (a, b) => { };
            OnMessageSent += (a) => { };
            OnServerConnectionFailure += () => { };

        }

        public enum State
        {
            NOT_CONNECTED,
            RESOLVING, //Note: State not used
            SIGNING_IN,
            CONNECTED,
            SIGNING_OUT_WAITING, //Note: State not used
            SIGNING_OUT,
        };
        private State _state;

        private HostName _server;
        private string _port;
        private string _clientName;
        private int _myId;
        private Dictionary<int, string> _peers = new Dictionary<int,string>();

        public bool IsConnected()
        {
            return _myId != -1;
        }

        public async void Connect(string server, string port, string client_name)
        {
            try
            {
                if (_state != State.NOT_CONNECTED)
                {
                    OnServerConnectionFailure();
                    return;
                }

                _server = new HostName(server);
                _port = port;
                _clientName = client_name;

                _state = State.SIGNING_IN;
                await ControlSocketRequestAsync(string.Format("GET /sign_in?{0} HTTP/1.0\r\n\r\n", client_name));
                if (_state == State.CONNECTED)
                {
                    // Start the long polling loop without await.
                    HangingGetReadLoopAsync();
                }
                else
                {
                    _state = State.NOT_CONNECTED;
                    OnServerConnectionFailure();
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Failed to connect: " + ex.Message);
            }
        }

        private StreamSocket _hangingGetSocket;

        #region Parsing
        private static bool GetHeaderValue(string buffer, int eoh, string header, out int value)
        {
            try
            {
                int index = buffer.IndexOf(header) + header.Length;
                value = buffer.Substring(index).ParseLeadingInt();
                return true;
            }
            catch
            {
                Debug.WriteLine("[Error] Failed to find header <" + header + "> in buffer(" + buffer.Length + ")=<" + buffer + ">");
                value = -1;
                return false;
            }
        }

        private static bool GetHeaderValue(string buffer, int eoh, string header, out string value)
        {
            try
            {
                int startIndex = buffer.IndexOf(header) + header.Length;
                int endIndex = buffer.IndexOf("\r\n", startIndex);
                value = buffer.Substring(startIndex, endIndex - startIndex);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private bool ParseServerResponse(string buffer, out int peer_id, out int eoh)
        {
            peer_id = -1;
            eoh = -1;
            try
            {
                int index = buffer.IndexOf(' ') + 1;
                int status = int.Parse(buffer.Substring(index, 3));
                if (status != 200)
                {
                    Close();
                    OnDisconnected();
                    return false;
                }

                eoh = buffer.IndexOf("\r\n\r\n");
                if (eoh == -1)
                {
                    Debug.WriteLine("[Error] Failed to parse server response (end of header not found)! Buffer(" + buffer.Length + ")=<" + buffer + ">");
                    return false;
                }

                return GetHeaderValue(buffer, eoh, "\r\nPragma: ", out peer_id);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("[Error] Failed to parse server response (ex=" + ex.Message + ")! Buffer(" + buffer.Length + ")=<" + buffer + ">");
                return false;
            }
        }

        private static bool ParseEntry(string entry, ref string name, ref int id, ref bool connected)
        {
            connected = false;
            int separator = entry.IndexOf(',');
            if (separator != -1)
            {
                id = entry.Substring(separator + 1).ParseLeadingInt();
                name = entry.Substring(0, separator);
                separator = entry.IndexOf(',', separator + 1);
                if (separator != -1)
                {
                    connected = entry.Substring(separator + 1).ParseLeadingInt() > 0 ? true : false;
                }
            }
            return name.Length > 0;
        }
        #endregion

        private async Task<Tuple<string, int>> ReadIntoBufferAsync(StreamSocket socket)
        {
            var reader = new DataReader(socket.InputStream);
            // set the DataReader to only wait for available data
            reader.InputStreamOptions = InputStreamOptions.Partial;

            var loadTask = reader.LoadAsync(0xffff);
            bool succeeded = loadTask.AsTask().Wait(20000);

            if (!succeeded)
            {
                loadTask.Cancel();
                Debug.WriteLine("Timed out long polling, re-trying.");
                return null;
            }
            var count = loadTask.GetResults();
            if (count == 0)
                return null;

            string data = reader.ReadString(count);

            int content_length = 0;
            bool ret = false;
            int i = data.IndexOf("\r\n\r\n");
            if (i != -1)
            {
                Debug.WriteLine("Headers received [i=" + i + " data(" + data.Length + ")"/*=" + data*/ + "]");
                if (GetHeaderValue(data, i, "\r\nContent-Length: ", out content_length))
                {
                    int total_response_size = (i + 4) + content_length;
                    if (data.Length >= total_response_size)
                    {
                        ret = true;
                    }
                    else
                    {
                        //TODO: if content length recived, but content is smaller (packet is fragmented), then throw all received data?  
                        // We haven't received everything.  Just continue to accept data.
                        Debug.WriteLine("Error: incomplete response; expected to receive " + total_response_size + ", received" + data.Length);
                    }
                }
                else
                {
                    Debug.WriteLine("Error: No content length field specified by the server.");
                }
            }
            return ret ? Tuple.Create(data, content_length) : null;
        }

        private async Task<bool> ControlSocketRequestAsync(string sendBuffer)
        {
            using (var socket = new StreamSocket())
            {
                // Connect to the server
                try
                {
                    await socket.ConnectAsync(_server, _port);
                }
                catch (Exception e)
                {
                    // This could be a connection failure like a timeout.
                    Debug.WriteLine("[Error] Failed to connect to " + _server + ":" + _port + " : " + e.Message);
                    return false;
                }
                // Send the request
                socket.WriteStringAsync(sendBuffer);

                // Read the response.
                var readResult = await ReadIntoBufferAsync(socket);
                if (readResult == null)
                    return false;

                string buffer = readResult.Item1;
                int content_length = readResult.Item2;

                int peer_id, eoh;
                if (!ParseServerResponse(buffer, out peer_id, out eoh))
                    return false;

                if (_myId == -1)
                {
                    Debug.Assert(_state == State.SIGNING_IN);
                    _myId = peer_id;
                    Debug.Assert(_myId != -1);

                    // The body of the response will be a list of already connected peers.
                    if (content_length > 0)
                    {
                        int pos = eoh + 4; // Start after the header.
                        while (pos < buffer.Length)
                        {
                            int eol = buffer.IndexOf('\n', pos);
                            if (eol == -1)
                                break;
                            int id = 0;
                            string name = "";
                            bool connected = false;
                            if (ParseEntry(buffer.Substring(pos, eol - pos), ref name, ref id, ref connected) && id != _myId)
                            {
                                _peers[id] = name;
                                OnPeerConnected(id, name);
                            }
                            pos = eol + 1;
                        }
                        OnSignedIn();
                    }
                }
                else if (_state == State.SIGNING_OUT)
                {
                    Close();
                    OnDisconnected();
                }
                else if (_state == State.SIGNING_OUT_WAITING)
                {
                    await SignOut();
                }

                if (_state == State.SIGNING_IN)
                {
                    _state = State.CONNECTED;
                }
            }

            return true;
        }

        private async Task HangingGetReadLoopAsync()
        {
            while (_state != State.NOT_CONNECTED)
            {
                using (_hangingGetSocket = new StreamSocket())
                {
                    try
                    {

                        // Connect to the server
                        await _hangingGetSocket.ConnectAsync(_server, _port);
                        if (_hangingGetSocket == null)
                            return;

                        // Send the request
                        _hangingGetSocket.WriteStringAsync(String.Format("GET /wait?peer_id={0} HTTP/1.0\r\n\r\n", _myId));

                        // Read the response.
                        var readResult = await ReadIntoBufferAsync(_hangingGetSocket);
                        if (readResult == null)
                            continue;

                        string buffer = readResult.Item1;
                        int content_length = readResult.Item2;

                        int peer_id, eoh;
                        if (!ParseServerResponse(buffer, out peer_id, out eoh))
                            continue;

                        // Store the position where the body begins.
                        int pos = eoh + 4;

                        if (_myId == peer_id)
                        {
                            // A notification about a new member or a member that just
                            // disconnected.
                            int id = 0;
                            string name = "";
                            bool connected = false;
                            if (ParseEntry(buffer.Substring(pos), ref name, ref id, ref connected))
                            {
                                if (connected)
                                {
                                    _peers[id] = name;
                                    OnPeerConnected(id, name);
                                }
                                else
                                {
                                    _peers.Remove(id);
                                    OnPeerDisconnected(id);
                                }
                            }
                        }
                        else
                        {
                            string message = buffer.Substring(pos);
                            if (message == "BYE")
                            {
                                OnPeerHangup(peer_id);
                            }
                            else
                            {
                                OnMessageFromPeer(peer_id, message);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("SIGNALLING LONG-POLLING EXCEPTION: {0}", e.Message);
                    }
                }
            }
        }

        public async Task<bool> SignOut()
        {
            if (_state == State.NOT_CONNECTED || _state == State.SIGNING_OUT)
                return true;

            if (_hangingGetSocket != null)
            {
                _hangingGetSocket.Dispose();
                _hangingGetSocket = null;
            }

            _state = State.SIGNING_OUT;

            if (_myId != -1)
            {
                await ControlSocketRequestAsync(String.Format("GET /sign_out?peer_id={0} HTTP/1.0\r\n\r\n", _myId));
            }
            else
            {
                // Can occur if the app is closed before we finish connecting.
                return true;
            }

            _myId = -1;
            _state = State.NOT_CONNECTED;
            return true;
        }

        private void Close()
        {
            if (_hangingGetSocket != null)
            {
                _hangingGetSocket.Dispose();
                _hangingGetSocket = null;
            }

            _peers.Clear();
            _state = State.NOT_CONNECTED;
        }

        public async Task<bool> SendToPeer(int peerId, string message) {
            if (_state != State.CONNECTED)
               return false;

            Debug.Assert(IsConnected());

            if (!IsConnected() || peerId == -1)
                return false;

            string buffer = String.Format(
                "POST /message?peer_id={0}&to={1} HTTP/1.0\r\n" +
                "Content-Length: {2}\r\n" +
                "Content-Type: text/plain\r\n" +
                "\r\n" +
                "{3}",
                _myId, peerId, message.Length, message);
            return await ControlSocketRequestAsync(buffer);
        }

        public async Task<bool> SendToPeer(int peerId, IJsonValue json)
        {
            string message = json.Stringify();
            return await SendToPeer(peerId, message);
        }
    }

    public static class Extensions
    {
        public static async void WriteStringAsync(this StreamSocket socket, string str)
        {
            var writer = new DataWriter(socket.OutputStream);
            writer.WriteString(str);
            await writer.StoreAsync();
        }

        public static int ParseLeadingInt(this string str)
        {
            return int.Parse(Regex.Match(str, "\\d+").Value);
        }
    }
}

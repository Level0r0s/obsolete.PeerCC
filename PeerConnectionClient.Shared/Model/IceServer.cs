using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using PeerConnectionClient.MVVM;
using PeerConnectionClient.Utilities;

namespace PeerConnectionClient.Model
{
    public class IceServer : BindableBase
    {
        public IceServer() : this(string.Empty, string.Empty, ServerType.STUN)
        {
        }

        public IceServer(string host, string port, ServerType type)
        {
            Port.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.Value = host;
            Port.Value = port;
            Type = type;
        }

        public enum ServerType { STUN, TURN };

        [XmlIgnore]
        public IEnumerable<ServerType> Types
        {
            get
            {
                return Enum.GetValues(typeof(ServerType)).Cast<ServerType>();
            }
        }

        protected ServerType _type;
        public ServerType Type
        {
            get
            {
                return _type;
            }
            set
            {
                switch (value)
                {
                    case ServerType.STUN:
                        _typeStr = "stun";
                        break;
                    case ServerType.TURN:
                        _typeStr = "turn";
                        break;
                    default:
                        _typeStr = "unknown";
                        break;
                }
                _type = value;
            }
        }

        protected string _typeStr;
        public string TypeStr
        {
            get { return _typeStr; }
        }

        [XmlIgnore]
        public string HostAndPort
        {
            get { return string.Format("{0}:{1}", Host.Value, Port.Value); }
        }

        private ValidableIntegerString _port = new ValidableIntegerString(0, 65535);
        public ValidableIntegerString Port
        {
            get { return _port; }
            set { _port = value; }
        }

        public string Credential { get; set; }


        protected ValidableNonEmptyString _host = new ValidableNonEmptyString();
        public ValidableNonEmptyString Host
        {
            get { return _host; }
            set { _host = value; }
        }
        
        public string Username { get; set; }

        [XmlIgnore]
        protected bool _valid;
        [XmlIgnore]
        public bool Valid
        {
            get { return _valid; }
            set { SetProperty(ref _valid, value); }
        }


        void ValidableProperties_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
                Valid = Port.Valid && Host.Valid;
        }
    }
}

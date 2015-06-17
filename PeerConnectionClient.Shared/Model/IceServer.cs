using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using PeerConnectionClient.Utilities;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace PeerConnectionClient.Model
{
    public class IceServer : INotifyPropertyChanged
    {
        public IceServer()
        {
            Type = ServerType.STUN;
            Port.Value = "";
            Host.Value = "";
            Port.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.PropertyChanged += ValidableProperties_PropertyChanged;
        }

        public IceServer(string host_, string _port, ServerType type_)
        {
            Port.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.Value = host_;
            Port.Value = _port;
            Type = type_;
        }

        public enum ServerType { STUN, TURN };

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
                if (value == ServerType.STUN)
                    _typeStr = "stun";
                else if (value == ServerType.TURN)
                    _typeStr = "turn";
                else _typeStr = "unknown";
                _type = value;
            }
        }

        protected string _typeStr;
        public string TypeStr
        {
            get { return _typeStr; }
        }

        [XmlIgnoreAttribute]
        public string HostAndPort
        {
            get { return Host.Value + ":" + Port.Value; }
        }

        protected ValidableIntegerString _port = new ValidableIntegerString(0, 65535);
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

        [XmlIgnoreAttribute]
        protected bool _valid = false;
        [XmlIgnoreAttribute]
        public bool Valid
        {
            get { return _valid; }
            set
            {
                    _valid = value;
                    NotifyPropertyChanged();
            }
        }

        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void ValidableProperties_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
                Valid = Port.Valid && Host.Valid;
        }
    }
}

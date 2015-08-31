﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using PeerConnectionClient.MVVM;
using PeerConnectionClient.Utilities;

namespace PeerConnectionClient.Model
{
    /// <summary>
    /// Class represents an Ice server
    /// </summary>
    public class IceServer : BindableBase
    {
        /// <summary>
        /// Default constructor for Ice server.
        /// </summary>
        public IceServer() : this(string.Empty, string.Empty, ServerType.STUN)
        {
        }

        /// <summary>
        /// Creates an Ice server with specified host, port and server type.
        /// </summary>
        /// <param name="host">The host name of the Ice server.</param>
        /// <param name="port">The port to connect to the Ice server.</param>
        /// <param name="type">The type of the Ice server.</param>
        public IceServer(string host, string port, ServerType type)
        {
            Port.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.PropertyChanged += ValidableProperties_PropertyChanged;
            Host.Value = host;
            Port.Value = port;
            Type = type;
        }

        public enum ServerType { STUN, TURN };

        /// <summary>
        /// Make the enumerable available in XAML.
        /// </summary>
        [XmlIgnore]
        public IEnumerable<ServerType> Types
        {
            get
            {
                return Enum.GetValues(typeof(ServerType)).Cast<ServerType>();
            }
        }

        protected ServerType _type;

        /// <summary>
        /// Ice server type property.
        /// </summary>
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

        /// <summary>
        /// Ice server type string property.
        /// </summary>
        public string TypeStr
        {
            get { return _typeStr; }
        }

        /// <summary>
        /// Ice server's host and port.
        /// </summary>
        [XmlIgnore]
        public string HostAndPort
        {
            get { return string.Format("{0}:{1}", Host.Value, Port.Value); }
        }

        private ValidableIntegerString _port = new ValidableIntegerString(0, 65535);

        /// <summary>
        /// Ice server's port.
        /// </summary>
        public ValidableIntegerString Port
        {
            get { return _port; }
            set { _port = value; }
        }

        /// <summary>
        /// The Ice server's password.
        /// Used with the Username below to connect to the Ice server.
        /// </summary>
        public string Credential { get; set; }

        protected ValidableNonEmptyString _host = new ValidableNonEmptyString();

        /// <summary>
        /// Ice server's host (IP).
        /// </summary>
        public ValidableNonEmptyString Host
        {
            get { return _host; }
            set { _host = value; }
        }
        
        /// <summary>
        /// Username for the Ice server.
        /// </summary>
        public string Username { get; set; }

        [XmlIgnore]
        protected bool _valid;

        /// <summary>
        /// Property to check the validity of Ice server information.
        /// </summary>
        [XmlIgnore]
        public bool Valid
        {
            get { return _valid; }
            set { SetProperty(ref _valid, value); }
        }


        /// <summary>
        /// Invokes when a property of an Ice server is changed and 
        /// the new information needs validation.
        /// </summary>
        /// <param name="sender">Information about event sender.</param>
        /// <param name="e">Details about Property changed event.</param>
        void ValidableProperties_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Valid")
            {
                Valid = Port.Valid && Host.Valid;
            }
        }
    }
}

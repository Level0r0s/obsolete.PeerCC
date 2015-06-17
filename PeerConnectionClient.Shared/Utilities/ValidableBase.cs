using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PeerConnectionClient.Utilities
{
    public abstract class ValidableBase<T> : INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] String propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        T _Value = default(T);
        public T Value
        {
            get { return _Value; }
            set
            {
                _Value = value;
                RaisePropertyChanged();
                Validate();
                RaisePropertyChanged("Valid");
            }
        }

        [XmlIgnoreAttribute]
        bool _valid = true;
        [XmlIgnoreAttribute]
        public bool Valid
        {
            get { return _valid; }
            protected set
            {
                if (_valid != value)
                {
                    _valid = value;
                    RaisePropertyChanged();
                }
            }
        }

        abstract protected void Validate();
    }
}

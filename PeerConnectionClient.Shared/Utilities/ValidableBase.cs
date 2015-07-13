using System.Xml.Serialization;
using PeerConnectionClient.MVVM;

namespace PeerConnectionClient.Utilities
{
    public abstract class ValidableBase<T> : BindableBase
    {

        private T _value;
        public T Value
        {
            get { return _value; }
            set
            {
                if (SetProperty(ref _value, value))
                {
                    Validate();
                }
            }
        }

        [XmlIgnore]
        bool _valid = true;
        [XmlIgnore]
        public bool Valid
        {
            get { return _valid; }
            protected set { SetProperty(ref _valid, value); }
        }

        abstract protected void Validate();
    }
}

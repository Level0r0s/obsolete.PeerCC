using System.Xml.Serialization;
using PeerConnectionClient.MVVM;

namespace PeerConnectionClient.Utilities
{
    /// <summary>
    /// A base class for validable values.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public abstract class ValidableBase<T> : BindableBase
    {
        private T _value;

        /// <summary>
        /// The value to validate.
        /// </summary>
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
       
        /// <summary>
        /// Property to indicate if the value is valid.
        /// </summary>
        [XmlIgnore]
        public bool Valid
        {
            get { return _valid; }
            protected set { SetProperty(ref _valid, value); }
        }

        /// <summary>
        /// Validate that the value meets the requirements for the
        /// specific validable classes.
        /// </summary>
        abstract protected void Validate();
    }
}

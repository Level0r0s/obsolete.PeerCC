using System;
using System.Text;

namespace PeerConnectionClient.Utilities
{
    public class ValidableNonEmptyString : ValidableBase<string>
    {
        override protected void Validate()
        {
            if (Value == null || Value.Length == 0)
                Valid = false;
            else
                Valid = true;
        }

        protected int _minValue;
        protected int _maxValue;
    }
}

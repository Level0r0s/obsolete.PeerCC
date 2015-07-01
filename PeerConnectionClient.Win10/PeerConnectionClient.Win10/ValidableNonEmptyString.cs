using System;
using System.ComponentModel;
using System.Text;

namespace PeerConnectionClient.Utilities
{
    public class ValidableNonEmptyString : ValidableBase<string>
    {
        public ValidableNonEmptyString(string value = "")
        {
            Value = value;
        }
        override protected void Validate()
        {
            if (Value == null || Value.Length == 0)
                Valid = false;
            else
                Valid = true;
        }
    }
}

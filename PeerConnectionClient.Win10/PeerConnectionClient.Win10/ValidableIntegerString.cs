using System;
using System.Text;

namespace PeerConnectionClient.Utilities
{
    public class ValidableIntegerString : ValidableBase<string>
    {
        public ValidableIntegerString()
        {
            _minValue = 0;
            _maxValue = 100;
        }
        public ValidableIntegerString(int minValue = 0, int maxValue = 100)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public ValidableIntegerString(int defaultValue, int minValue = 0, int maxValue = 100)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            Value = defaultValue.ToString();
        }

        public ValidableIntegerString(string defaultValue)
        {
            Value = defaultValue;
        }

        override protected void Validate()
        {
            try
            {
                int intVal = Convert.ToInt32(Value);
                if (intVal >= _minValue && intVal <= _maxValue)
                    Valid = true;
                else
                    Valid = false;
            }
            catch (Exception)
            {
                Valid = false;
            }
        }

        protected int _minValue;
        protected int _maxValue;
    }
}

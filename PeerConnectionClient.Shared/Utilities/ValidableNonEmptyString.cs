namespace PeerConnectionClient.Utilities
{
    public class ValidableNonEmptyString : ValidableBase<string>
    {
        public ValidableNonEmptyString()
        {
            Value = "";
        }
        public ValidableNonEmptyString(string value = "")
        {
            Value = value;
        }
        override protected void Validate()
        {
            Valid = !string.IsNullOrEmpty(Value);
        }
    }
}

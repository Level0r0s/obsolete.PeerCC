
namespace PeerConnectionClient.Utilities
{
  public enum CapRes
  {
    Default = 0,
    _640_480 = 1,
    _320_240 = 2,
  };
  public enum CapFPS
  {
    Default = 0,
    _16 = 1,
    _18 = 2,
    _24 = 3
  };

  public class ComboBoxItemCapRes
  {
    public CapRes ValueCapResEnum { get; set; }
    public string ValueCapResString { get; set; }
  }

  public class ComboBoxItemCapFPS
  {
    public CapFPS ValueCapFPSEnum { get; set; }
    public string ValueCapFPSString { get; set; }
  }
}
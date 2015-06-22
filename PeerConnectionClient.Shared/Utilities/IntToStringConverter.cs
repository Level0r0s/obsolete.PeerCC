using System;
using System.Text;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value.ToString();

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Int32.Parse((string)value);
        }
    }
}

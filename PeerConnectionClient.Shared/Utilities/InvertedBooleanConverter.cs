using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    public class InvertedBoolenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }
    }
}

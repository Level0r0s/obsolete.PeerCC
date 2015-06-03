using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    class BoolToVisConverter : IValueConverter
    {
        public bool Negated { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool result = (bool)value;
            result = Negated ? !result : result;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (Negated)
            {
                return (bool)value ? Visibility.Collapsed : Visibility.Visible;
            }
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

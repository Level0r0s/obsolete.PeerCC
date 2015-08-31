using System;
using Windows.UI.Xaml.Data;

namespace PeerConnectionClient.Utilities
{
    /// <summary>
    /// Class to invert the boolean value.
    /// </summary>
    public class InvertedBoolenConverter : IValueConverter
    {
        /// <summary>
        /// See IValueConverter.Convert().
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }

        /// <summary>
        /// See IValueConverter.ConvertBack().
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }
    }
}
